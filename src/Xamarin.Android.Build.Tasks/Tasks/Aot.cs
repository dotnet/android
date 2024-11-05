using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using Xamarin.Android.Tools;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	public enum AotMode : uint
	{
		None      = 0x0000,
		Normal    = 0x0001,
		Hybrid    = 0x0002,
		Full      = 0x0003,
		Interp    = 0x0004,
		FullInterp = 0x0005,
	}

	public enum SequencePointsMode {
		None,
		Normal,
		Offline,
	}

	// can't be a single ToolTask, because it has to run mkbundle many times for each arch.
	public class Aot : GetAotArguments
	{
		public override string TaskPrefix => "AOT";

		// Which ABIs to include native libs for
		[Required]
		public string [] SupportedAbis { get; set; }

		[Required]
		public string IntermediateAssemblyDir { get; set; }

		public string LinkMode { get; set; }

		public ITaskItem[] AdditionalNativeLibraryReferences { get; set; }

		public string ExtraAotOptions { get; set; }

		public string AotAdditionalArguments { get; set; }

		[Output]
		public string[] NativeLibrariesReferences { get; set; }

		static string QuoteFileName(string fileName)
		{
			var builder = new CommandLineBuilder();
			builder.AppendFileNameIfNotNull(fileName);
			return builder.ToString();
		}

		public async override System.Threading.Tasks.Task RunTaskAsync ()
		{
			NdkTools ndk = NdkTools.Create (AndroidNdkDirectory, logErrors: UseAndroidNdk, log: Log);
			if (Log.HasLoggedErrors) {
				return; // NdkTools.Create will log appropriate error
			}

			bool hasValidAotMode = GetAndroidAotMode (AndroidAotMode, out AotMode);
			if (!hasValidAotMode) {
				LogCodedError ("XA3002", Properties.Resources.XA3002, AndroidAotMode);
				return;
			}

			if (AotMode == AotMode.Interp) {
				LogDebugMessage ("Interpreter AOT mode enabled");
				return;
			}

			TryGetSequencePointsMode (AndroidSequencePointsMode, out SequencePointsMode);

			var nativeLibs = new List<string> ();

			await this.WhenAllWithLock (GetAotConfigs (ndk),
				(config, lockObject) => {
					if (!config.Valid) {
						Cancel ();
						return;
					}

					if (!RunAotCompiler (config.AssembliesPath, config.AotCompiler, config.AotOptions, config.AssemblyPath, config.ResponseFile)) {
						LogCodedError ("XA3001", Properties.Resources.XA3001, Path.GetFileName (config.AssemblyPath));
						Cancel ();
						return;
					}

					File.Delete (config.ResponseFile);

					lock (lockObject)
						nativeLibs.Add (config.OutputFile);
				}
			);

			NativeLibrariesReferences = nativeLibs.ToArray ();

			LogDebugMessage ("Aot Outputs:");
			LogDebugTaskItems ("  NativeLibrariesReferences: ", NativeLibrariesReferences);
		}

		IEnumerable<Config> GetAotConfigs (NdkTools ndk)
		{
			if (!Directory.Exists (AotOutputDirectory))
				Directory.CreateDirectory (AotOutputDirectory);

			SdkBinDirectory = MonoAndroidHelper.GetOSBinPath ();
			foreach (var abi in SupportedAbis) {
				(string aotCompiler, string outdir, string mtriple, AndroidTargetArch arch) = GetAbiSettings (abi);

				if (UseAndroidNdk && !ndk.ValidateNdkPlatform (LogMessage, LogCodedError, arch, enableLLVM:EnableLLVM)) {
					yield return Config.Invalid;
					yield break;
				}

				outdir = Path.GetFullPath (outdir);
				if (!Directory.Exists (outdir))
					Directory.CreateDirectory (outdir);

				// dont use a full path if the outdir is withing the WorkingDirectory.
				if (outdir.StartsWith (WorkingDirectory, StringComparison.InvariantCultureIgnoreCase)) {
					outdir = outdir.Replace (WorkingDirectory + Path.DirectorySeparatorChar, string.Empty);
				}

				string toolPrefix = GetToolPrefix (ndk, arch, out int level);
				foreach (var assembly in ResolvedAssemblies) {
					string outputFile = Path.Combine(outdir, string.Format ("libaot-{0}.so",
						Path.GetFileName (assembly.ItemSpec)));

					string seqpointsFile = Path.Combine(outdir, string.Format ("{0}.msym",
						Path.GetFileName (assembly.ItemSpec)));

					string tempDir = Path.Combine (outdir, Path.GetFileName (assembly.ItemSpec));
					Directory.CreateDirectory (tempDir);

					GetAotOptions (ndk, arch, level, outdir, toolPrefix);
					// NOTE: ordering seems to matter on Windows
					var aotOptions = new List<string> ();
					aotOptions.Add ("asmwriter");
					aotOptions.Add ($"mtriple={mtriple}");
					aotOptions.Add ($"tool-prefix={toolPrefix}");
					aotOptions.Add ($"outfile={outputFile}");
					aotOptions.Add ($"llvm-path={SdkBinDirectory}");
					aotOptions.Add ($"temp-path={tempDir}");
					if (!string.IsNullOrEmpty (AotAdditionalArguments)) {
						aotOptions.Add (AotAdditionalArguments);
					}
					if (!string.IsNullOrEmpty (MsymPath)) {
						aotOptions.Add ($"msym-dir={MsymPath}");
					}
					if (Profiles != null && Profiles.Length > 0) {
						if (Path.GetFileNameWithoutExtension (assembly.ItemSpec) == TargetName) {
							LogDebugMessage ($"Not using profile(s) for main assembly: {assembly.ItemSpec}");
						} else {
							aotOptions.Add ("profile-only");
							foreach (var p in Profiles) {
								var fp = Path.GetFullPath (p.ItemSpec);
								aotOptions.Add ($"profile={fp}");
							}
						}
					}
					// NOTE: ld-name and ld-flags MUST be last, otherwise Mono fails to parse it on Windows
					if (!string.IsNullOrEmpty (LdName)) {
						aotOptions.Add ($"ld-name={LdName}");
					}

					// We don't check whether any mode option was added via `AotAdditionalArguments`, the `AndroidAotMode` property should always win here.
					// Modes not supported by us directly can be set by setting `AndroidAotMode` to "normal" and adding the desired mode name to the
					// `AotAdditionalArguments` property.
					switch (AotMode) {
						case AotMode.Full:
							aotOptions.Add ("full");
							break;

						case AotMode.Hybrid:
							aotOptions.Add ("hybrid");
							break;

						case AotMode.FullInterp:
							aotOptions.Add ("fullinterp");
							break;
					}

					if (!string.IsNullOrEmpty (LdFlags)) {
						aotOptions.Add ($"ld-flags={LdFlags}");
					}

					// we need to quote the entire --aot arguments here to make sure it is parsed
					// on windows as one argument. Otherwise it will be split up into multiple
					// values, which wont work.
					string aotOptionsStr = (EnableLLVM ? "--llvm " : "") + $"\"--aot={string.Join (",", aotOptions)}\"";

					if (!string.IsNullOrEmpty (ExtraAotOptions)) {
						aotOptionsStr += (aotOptions.Count > 0 ? " " : "") + ExtraAotOptions;
					}

					// Due to a Monodroid MSBuild bug we can end up with paths to assemblies that are not in the intermediate
					// assembly directory (typically obj/assemblies). This can lead to problems with the Mono loader not being
					// able to find their dependency laters, since framework assemblies are stored in different directories.
					// This can happen when linking is disabled (AndroidLinkMode=None). Workaround this problem by resolving
					// the paths to the right assemblies manually.
					var resolvedPath = Path.GetFullPath (assembly.ItemSpec);
					var intermediateAssemblyPath = Path.Combine (IntermediateAssemblyDir, Path.GetFileName (assembly.ItemSpec));

					if (LinkMode.ToLowerInvariant () == "none") {
						if (!resolvedPath.Contains (IntermediateAssemblyDir) && File.Exists (intermediateAssemblyPath))
							resolvedPath = intermediateAssemblyPath;
					}

					var assembliesPath = Path.GetFullPath (Path.GetDirectoryName (resolvedPath));
					var assemblyPath = Path.GetFullPath (resolvedPath);

					yield return new Config (assembliesPath, aotCompiler, aotOptionsStr, assemblyPath, outputFile, Path.Combine (tempDir, "response.txt"));
				}
			}
		}

		bool RunAotCompiler (string assembliesPath, string aotCompiler, string aotOptions, string assembly, string responseFile)
		{
			var stdout_completed = new ManualResetEvent (false);
			var stderr_completed = new ManualResetEvent (false);

			using (var sw = new StreamWriter (responseFile, append: false, encoding: Files.UTF8withoutBOM)) {
				sw.WriteLine (aotOptions + " " + QuoteFileName (assembly));
			}

			var psi = new ProcessStartInfo () {
				FileName = QuoteFileName (aotCompiler),
				Arguments = $"--response={QuoteFileName (responseFile)}",
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				StandardOutputEncoding = Encoding.UTF8,
				CreateNoWindow=true,
				WindowStyle=ProcessWindowStyle.Hidden,
				WorkingDirectory = WorkingDirectory,
			};

			// we do not want options to be provided out of band to the cross compilers
			psi.EnvironmentVariables ["MONO_ENV_OPTIONS"] = String.Empty;
			// the C code cannot parse all the license details, including the activation code that tell us which license level is allowed
			// so we provide this out-of-band to the cross-compilers - this can be extended to communicate a few others bits as well
			psi.EnvironmentVariables ["MONO_PATH"] = assembliesPath;

			LogDebugMessage ("[AOT] MONO_PATH=\"{0}\" MONO_ENV_OPTIONS=\"{1}\" {2} {3}",
				psi.EnvironmentVariables ["MONO_PATH"], psi.EnvironmentVariables ["MONO_ENV_OPTIONS"], psi.FileName, psi.Arguments);

			if (!string.IsNullOrEmpty (responseFile))
				LogDebugMessage ("[AOT] response file {0}: {1}", responseFile, File.ReadAllText (responseFile));

			using (var proc = new Process ()) {
				proc.OutputDataReceived += (s, e) => {
					if (e.Data != null)
						OnAotOutputData (s, e);
					else
						stdout_completed.Set ();
				};
				proc.ErrorDataReceived += (s, e) => {
					if (e.Data != null)
						OnAotErrorData (s, e);
					else
						stderr_completed.Set ();
				};
				proc.StartInfo = psi;
				proc.Start ();
				proc.BeginOutputReadLine ();
				proc.BeginErrorReadLine ();
				CancellationToken.Register (() => { try { proc.Kill (); } catch (Exception) { } });
				proc.WaitForExit ();
				if (psi.RedirectStandardError)
					stderr_completed.WaitOne (TimeSpan.FromSeconds (30));
				if (psi.RedirectStandardOutput)
					stdout_completed.WaitOne (TimeSpan.FromSeconds (30));
				return proc.ExitCode == 0;
			}
		}

		void OnAotOutputData (object sender, DataReceivedEventArgs e)
		{
			if (e.Data != null)
				LogMessage ("[aot-compiler stdout] {0}", e.Data);
		}

		void OnAotErrorData (object sender, DataReceivedEventArgs e)
		{
			if (e.Data != null)
				LogMessage ("[aot-compiler stderr] {0}", e.Data);
		}

		struct Config {
			public string AssembliesPath { get; }
			public string AotCompiler { get; }
			public string AotOptions { get; }
			public string AssemblyPath { get; }
			public string OutputFile { get; }
			public string ResponseFile { get; }

			public bool Valid { get; private set; }

			public Config (string assembliesPath, string aotCompiler, string aotOptions, string assemblyPath, string outputFile, string responseFile)
			{
				AssembliesPath = assembliesPath;
				AotCompiler = aotCompiler;
				AotOptions = aotOptions;
				AssemblyPath = assemblyPath;
				OutputFile = outputFile;
				ResponseFile = responseFile;
				Valid = true;
			}

			public static Config Invalid {
				get { return new Config { Valid = false }; }
			}
		}
	}
}
