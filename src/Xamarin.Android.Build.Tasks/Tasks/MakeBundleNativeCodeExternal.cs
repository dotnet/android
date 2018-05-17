using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using Java.Interop.Tools.Diagnostics;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	// can't be a single ToolTask, because it has to run mkbundle many times for each arch.
	public class MakeBundleNativeCodeExternal : Task
	{
		[Required]
		public string AndroidNdkDirectory { get; set; }

		[Required]
		public ITaskItem[] Assemblies { get; set; }
		
		// Which ABIs to include native libs for
		public string SupportedAbis { get; set; }
		
		[Required]
		public string TempOutputPath { get; set; }

		public string IncludePath { get; set; }

		[Required]
		public string ToolPath { get; set; }

		public bool AutoDeps { get; set; }
		public bool EmbedDebugSymbols { get; set; }
		public bool KeepTemp { get; set; }

		[Output]
		public ITaskItem [] OutputNativeLibraries { get; set; }

		public MakeBundleNativeCodeExternal ()
		{
		}

		public override bool Execute ()
		{
			Log.LogDebugMessage ("Assemblies: {0}", Assemblies.Length);
			Log.LogDebugMessage ("SupportedAbis: {0}", SupportedAbis);
			Log.LogDebugMessage ("AutoDeps: {0}", AutoDeps);
			try {
				if (String.IsNullOrEmpty (AndroidNdkDirectory)) {
					Log.LogCodedError ("XA5101", "Could not locate Android NDK. Please make sure to configure path to NDK in SDK Locations or set via /p:AndroidNdkDirectory in the MSBuild/xbuild argument.");
					return false;
				}
				return DoExecute ();
			} catch (XamarinAndroidException e) {
				Log.LogCodedError (string.Format ("XA{0:0000}", e.Code), e.MessageWithoutCode);
				if (MonoAndroidHelper.LogInternalExceptions)
					Log.LogMessage (e.ToString ());
			} catch (Exception ex) {
				Log.LogErrorFromException (ex);
			}
			return !Log.HasLoggedErrors;
		}

		bool DoExecute ()
		{
			var abis = SupportedAbis.Split (new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
			var results = new List<ITaskItem> ();
			string bundlepath = Path.Combine (TempOutputPath, "bundles");
			if (!Directory.Exists (bundlepath))
				Directory.CreateDirectory (bundlepath);
			else
				Directory.Delete (bundlepath, true);
			foreach (var abi in abis) {
				AndroidTargetArch arch = AndroidTargetArch.Other;
				switch (abi) {
				case "arm64":
				case "arm64-v8a":
				case "aarch64":
					arch = AndroidTargetArch.Arm64;
					break;
				case "armeabi":
				case "armeabi-v7a":
					arch = AndroidTargetArch.Arm;
					break;
				case "x86":
					arch = AndroidTargetArch.X86;
					break;
				case "x86_64":
					arch = AndroidTargetArch.X86_64;
					break;
				case "mips":
					arch = AndroidTargetArch.Mips;
					break;
				}

				if (!NdkUtil.ValidateNdkPlatform (Log, AndroidNdkDirectory, arch, enableLLVM: false)) {
					return false;
				}

				int level = NdkUtil.GetMinimumApiLevelFor (arch, AndroidNdkDirectory);
				var outpath = Path.Combine (bundlepath, abi);
				if (!Directory.Exists (outpath))
					Directory.CreateDirectory (outpath);

				var clb = new CommandLineBuilder ();
				clb.AppendSwitch ("--dos2unix=false");
				clb.AppendSwitch ("--nomain");
				clb.AppendSwitch ("--i18n none");
				clb.AppendSwitch ("--bundled-header");
				clb.AppendSwitch ("--style");
				clb.AppendSwitch ("linux");
				clb.AppendSwitch ("-c");
				clb.AppendSwitch ("-o");
				clb.AppendFileNameIfNotNull (Path.Combine (outpath, "temp.c"));
				clb.AppendSwitch ("-oo");
				clb.AppendFileNameIfNotNull (Path.Combine (outpath, "assemblies.o"));
				if (AutoDeps)
					clb.AppendSwitch ("--autodeps");
				if (KeepTemp)
					clb.AppendSwitch ("--keeptemp");
				clb.AppendSwitch ("-z"); // Compress
				clb.AppendFileNamesIfNotNull (Assemblies, " ");
				var psi = new ProcessStartInfo () {
					FileName = MkbundlePath,
					Arguments = clb.ToString (),
					UseShellExecute = false,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					CreateNoWindow = true,
					WindowStyle = ProcessWindowStyle.Hidden,
				};
				var gccNoQuotes = NdkUtil.GetNdkTool (AndroidNdkDirectory, arch, "gcc");
				var gcc = '"' + gccNoQuotes + '"';
				var gas = '"' + NdkUtil.GetNdkTool (AndroidNdkDirectory, arch, "as") + '"';
				psi.EnvironmentVariables ["CC"] = gcc;
				psi.EnvironmentVariables ["AS"] = gas;
				Log.LogDebugMessage ("CC=" + gcc);
				Log.LogDebugMessage ("AS=" + gas);
				//psi.EnvironmentVariables ["PKG_CONFIG_PATH"] = Path.Combine (Path.GetDirectoryName (MonoDroidSdk.MandroidTool), "lib", abi);
				Log.LogDebugMessage ("[mkbundle] " + psi.FileName + " " + clb);
				var proc = new Process ();
				proc.OutputDataReceived += OnMkbundleOutputData;
				proc.ErrorDataReceived += OnMkbundleErrorData;
				proc.StartInfo = psi;
				proc.Start ();
				proc.BeginOutputReadLine ();
				proc.BeginErrorReadLine ();
				proc.WaitForExit ();
				if (proc.ExitCode != 0) {
					Log.LogCodedError ("XA5102", "Conversion from assembly to native code failed. Exit code {0}", proc.ExitCode);
					return false;
				}

				Log.LogDebugMessage ("[mkbundle] modifying mono_mkbundle_init");
				// make some changes in the mkbundle output so that it does not require libmonodroid.so
				var mkbundleOutput = new StringBuilder (File.ReadAllText (Path.Combine (outpath, "temp.c")));

				mkbundleOutput.Replace ("mono_jit_set_aot_mode", "mono_jit_set_aot_mode_ptr")
					.Replace ("void mono_mkbundle_init ()", "void mono_mkbundle_init (void (register_bundled_assemblies_func)(const MonoBundledAssembly **), void (register_config_for_assembly_func)(const char *, const char *), void (mono_jit_set_aot_mode_func) (int mode))")
					.Replace ("mono_register_config_for_assembly (\"", "register_config_for_assembly_func (\"")
					.Replace ("install_dll_config_files (void)", "install_dll_config_files (void (register_config_for_assembly_func)(const char *, const char *))")
					.Replace ("install_dll_config_files ()", "install_dll_config_files (register_config_for_assembly_func)")
					.Replace ("mono_register_bundled_assemblies(", "register_bundled_assemblies_func(")
					.Replace ("int nbundles;", "int nbundles;\n\n\tmono_jit_set_aot_mode_ptr = mono_jit_set_aot_mode_func;");

				mkbundleOutput.Insert (0, "void (*mono_jit_set_aot_mode_ptr) (int mode);\n");

				File.WriteAllText (Path.Combine (outpath, "temp.c"), mkbundleOutput.ToString ());

				// then compile temp.c into temp.o and ...

				clb = new CommandLineBuilder ();
				clb.AppendSwitch ("-c");

				// This is necessary only when unified headers are in use but it won't hurt to have it
				// defined even if we don't use them
				clb.AppendSwitch ($"-D__ANDROID_API__={level}");

				clb.AppendSwitch ("-o");
				clb.AppendFileNameIfNotNull (Path.Combine (outpath, "temp.o"));
				if (!string.IsNullOrWhiteSpace (IncludePath)) {
					clb.AppendSwitch ("-I");
					clb.AppendFileNameIfNotNull (IncludePath);
				}

				string asmIncludePath = NdkUtil.GetNdkAsmIncludePath (AndroidNdkDirectory, arch, level);
				if (!String.IsNullOrEmpty (asmIncludePath)) {
					clb.AppendSwitch ("-I");
					clb.AppendFileNameIfNotNull (asmIncludePath);
				}

				clb.AppendSwitch ("-I");
				clb.AppendFileNameIfNotNull (NdkUtil.GetNdkPlatformIncludePath (AndroidNdkDirectory, arch, level));
				clb.AppendFileNameIfNotNull (Path.Combine (outpath, "temp.c"));
				Log.LogDebugMessage ("[CC] " + gcc + " " + clb);
				if (MonoAndroidHelper.RunProcess (gccNoQuotes, clb.ToString (), OnCcOutputData,  OnCcErrorData) != 0) {
					Log.LogCodedError ("XA5103", "NDK C compiler resulted in an error. Exit code {0}", proc.ExitCode);
					return false;
				}

				// ... link temp.o and assemblies.o into app.so

				clb = new CommandLineBuilder ();
				clb.AppendSwitch ("--shared");
				clb.AppendFileNameIfNotNull (Path.Combine (outpath, "temp.o"));
				clb.AppendFileNameIfNotNull (Path.Combine (outpath, "assemblies.o"));
				clb.AppendSwitch ("-o");
				clb.AppendFileNameIfNotNull (Path.Combine (outpath, "libmonodroid_bundle_app.so"));
				clb.AppendSwitch ("-L");
				clb.AppendFileNameIfNotNull (NdkUtil.GetNdkPlatformLibPath (AndroidNdkDirectory, arch, level));
				clb.AppendSwitch ("-lc");
				clb.AppendSwitch ("-lm");
				clb.AppendSwitch ("-ldl");
				clb.AppendSwitch ("-llog");
				clb.AppendSwitch ("-lz"); // Compress
				string ld = NdkUtil.GetNdkTool (AndroidNdkDirectory, arch, "ld");
				Log.LogMessage (MessageImportance.Normal, "[LD] " + ld + " " + clb);
				if (MonoAndroidHelper.RunProcess (ld, clb.ToString (), OnLdOutputData,  OnLdErrorData) != 0) {
					Log.LogCodedError ("XA5201", "NDK Linker resulted in an error. Exit code {0}", proc.ExitCode);
					return false;
				}
				results.Add (new TaskItem (Path.Combine (outpath, "libmonodroid_bundle_app.so")));
			}
			OutputNativeLibraries = results.ToArray ();
			return true;
		}

		void OnCcOutputData (object sender, DataReceivedEventArgs e)
		{
			Log.LogDebugMessage ("[cc stdout] {0}", e.Data ?? "");
		}

		void OnCcErrorData (object sender, DataReceivedEventArgs e)
		{
			Log.LogMessage ("[cc stderr] {0}", e.Data ?? "");
		}

		void OnLdOutputData (object sender, DataReceivedEventArgs e)
		{
			Log.LogDebugMessage ("[ld stdout] {0}", e.Data ?? "");
		}

		void OnLdErrorData (object sender, DataReceivedEventArgs e)
		{
			Log.LogMessage ("[ld stderr] {0}", e.Data ?? "");
		}

		void OnMkbundleOutputData (object sender, DataReceivedEventArgs e)
		{
			Log.LogDebugMessage ("[mkbundle stdout] {0}", e.Data ?? "");
		}

		void OnMkbundleErrorData (object sender, DataReceivedEventArgs e)
		{
			Log.LogMessage ("[mkbundle stderr] {0}", e.Data ?? "");
		}

		string MkbundlePath {
			get {
				return Path.Combine (ToolPath, "mkbundle.exe");
			}
		}
	}
}
