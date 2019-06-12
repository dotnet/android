// Copyright (C) 2011 Xamarin, Inc. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	public class GeneratePackageManagerJava : Task
	{
		Guid buildId = Guid.NewGuid ();

		[Required]
		public ITaskItem[] ResolvedAssemblies { get; set; }

		[Required]
		public ITaskItem[] ResolvedUserAssemblies { get; set; }

		[Required]
		public string OutputDirectory { get; set; }

		[Required]
		public string EnvironmentOutputDirectory { get; set; }

		[Required]
		public string UseSharedRuntime { get; set; }

		[Required]
		public string MainAssembly { get; set; }

		[Required]
		public string TargetFrameworkVersion { get; set; }

		[Required]
		public string Manifest { get; set; }

		[Required]
		public bool IsBundledApplication { get; set; }

		[Required]
		public string [] SupportedAbis { get; set; }

		[Required]
		public string AndroidPackageName { get; set; }

		[Required]
		public bool EnablePreloadAssembliesDefault { get; set; }

		public string Debug { get; set; }
		public ITaskItem[] Environments { get; set; }
		public string AndroidAotMode { get; set; }
		public bool EnableLLVM { get; set; }
		public string HttpClientHandlerType { get; set; }
		public string TlsProvider { get; set; }
		public string AndroidSequencePointsMode { get; set; }
		public bool EnableSGenConcurrent { get; set; }

		[Output]
		public string BuildId { get; set; }

		bool _Debug {
			get {
				return string.Equals (Debug, "true", StringComparison.OrdinalIgnoreCase);
			}
		}

		public override bool Execute ()
		{
			BuildId = buildId.ToString ();
			Log.LogDebugMessage ("  [Output] BuildId: {0}", BuildId);

			var shared_runtime = string.Compare (UseSharedRuntime, "true", true) == 0;
			var doc = AndroidAppManifest.Load (Manifest, MonoAndroidHelper.SupportedVersions);
			int minApiVersion = doc.MinSdkVersion == null ? 4 : (int) doc.MinSdkVersion;
			// We need to include any special assemblies in the Assemblies list
			var assemblies = ResolvedUserAssemblies
				.Concat (MonoAndroidHelper.GetFrameworkAssembliesToTreatAsUserAssemblies (ResolvedAssemblies))						
				.ToList ();
			var mainFileName = Path.GetFileName (MainAssembly);
			Func<string,string,bool> fileNameEq = (a,b) => a.Equals (b, StringComparison.OrdinalIgnoreCase);
			assemblies = assemblies.Where (a => fileNameEq (a.ItemSpec, mainFileName)).Concat (assemblies.Where (a => !fileNameEq (a.ItemSpec, mainFileName))).ToList ();

			using (var stream = new MemoryStream ())
			using (var pkgmgr = new StreamWriter (stream)) {
				pkgmgr.WriteLine ("package mono;");

				// Write all the user assemblies
				pkgmgr.WriteLine ("public class MonoPackageManager_Resources {");
				pkgmgr.WriteLine ("\tpublic static String[] Assemblies = new String[]{");

				pkgmgr.WriteLine ("\t\t/* We need to ensure that \"{0}\" comes first in this list. */", mainFileName);
				foreach (var assembly in assemblies) {
					pkgmgr.WriteLine ("\t\t\"" + Path.GetFileName (assembly.ItemSpec) + "\",");
				}

				// Write the assembly dependencies
				pkgmgr.WriteLine ("\t};");
				pkgmgr.WriteLine ("\tpublic static String[] Dependencies = new String[]{");

				//foreach (var assembly in assemblies.Except (args.Assemblies)) {
				//        if (args.SharedRuntime && !Toolbox.IsInSharedRuntime (assembly))
				//                pkgmgr.WriteLine ("\t\t\"" + Path.GetFileName (assembly) + "\",");
				//}

				pkgmgr.WriteLine ("\t};");

				// Write the platform api apk we need
				pkgmgr.WriteLine ("\tpublic static String ApiPackageName = {0};", shared_runtime
						? string.Format ("\"Mono.Android.Platform.ApiLevel_{0}\"",
							MonoAndroidHelper.SupportedVersions.GetApiLevelFromFrameworkVersion (TargetFrameworkVersion))
						: "null");
				pkgmgr.WriteLine ("}");
				pkgmgr.Flush ();

				// Only copy to the real location if the contents actually changed
				var dest = Path.GetFullPath (Path.Combine (OutputDirectory, "MonoPackageManager_Resources.java"));

				MonoAndroidHelper.CopyIfStreamChanged (stream, dest);
			}

			AddEnvironment ();

			return !Log.HasLoggedErrors;
		}

		static readonly string[] defaultLogLevel = {"MONO_LOG_LEVEL", "info"};
		static readonly string[] defaultMonoDebug = {"MONO_DEBUG", "gen-compact-seq-points"};
		static readonly string[] defaultHttpMessageHandler = {"XA_HTTP_CLIENT_HANDLER_TYPE", "System.Net.Http.HttpClientHandler, System.Net.Http"};
		static readonly string[] defaultTlsProvider = {"XA_TLS_PROVIDER", "btls"};

		void AddEnvironment ()
		{
			bool usesEmbeddedDSOs = false;
			bool usesMonoAOT = false;
			bool usesAssemblyPreload = EnablePreloadAssembliesDefault;
			uint monoAOTMode = 0;
			string androidPackageName = null;
			var environmentVariables = new Dictionary<string, string> (StringComparer.Ordinal);
			var systemProperties = new Dictionary<string, string> (StringComparer.Ordinal);

			AotMode aotMode;
			if (AndroidAotMode != null && Aot.GetAndroidAotMode (AndroidAotMode, out aotMode)) {
				usesMonoAOT = true;
				monoAOTMode = (uint)aotMode;
			}

			bool haveLogLevel = false;
			bool haveMonoDebug = false;
			bool havebuildId = false;
			bool haveHttpMessageHandler = false;
			bool haveTlsProvider = false;
			bool haveMonoGCParams = false;

			SequencePointsMode sequencePointsMode;
			if (!Aot.TryGetSequencePointsMode (AndroidSequencePointsMode, out sequencePointsMode))
				sequencePointsMode = SequencePointsMode.None;

			foreach (ITaskItem env in Environments ?? new TaskItem[0]) {
				foreach (string line in File.ReadLines (env.ItemSpec)) {
					var lineToWrite = line;
					if (lineToWrite.StartsWith ("MONO_LOG_LEVEL=", StringComparison.Ordinal))
						haveLogLevel = true;
					if (lineToWrite.StartsWith ("MONO_GC_PARAMS=", StringComparison.Ordinal))
						haveMonoGCParams = true;
					if (lineToWrite.StartsWith ("XAMARIN_BUILD_ID=", StringComparison.Ordinal))
						havebuildId = true;
					if (lineToWrite.StartsWith ("MONO_DEBUG=", StringComparison.Ordinal)) {
						haveMonoDebug = true;
						if (sequencePointsMode != SequencePointsMode.None && !lineToWrite.Contains ("gen-compact-seq-points"))
							lineToWrite = line  + ",gen-compact-seq-points";
					}
					if (lineToWrite.StartsWith ("XA_HTTP_CLIENT_HANDLER_TYPE=", StringComparison.Ordinal))
						haveHttpMessageHandler = true;
					if (lineToWrite.StartsWith ("XA_TLS_PROVIDER=", StringComparison.Ordinal))
						haveTlsProvider = true;
					if (lineToWrite.StartsWith ("__XA_DSO_IN_APK", StringComparison.Ordinal)) {
						usesEmbeddedDSOs = true;
						continue;
					}
					if (lineToWrite.StartsWith ("mono.enable_assembly_preload=", StringComparison.Ordinal)) {
						int idx = lineToWrite.IndexOf ('=');
						uint val;
						if (idx < lineToWrite.Length - 1 && UInt32.TryParse (lineToWrite.Substring (idx + 1), out val)) {
							usesAssemblyPreload = idx == 1;
						}
						continue;
					}

					AddEnvironmentVariableLine (lineToWrite);
				}
			}

			if (_Debug && !haveLogLevel) {
				AddEnvironmentVariable (defaultLogLevel[0], defaultLogLevel[1]);
			}

			if (sequencePointsMode != SequencePointsMode.None && !haveMonoDebug) {
				AddEnvironmentVariable (defaultMonoDebug[0], defaultMonoDebug[1]);
			}

			if (!havebuildId)
				AddEnvironmentVariable ("XAMARIN_BUILD_ID", BuildId);

			if (!haveHttpMessageHandler) {
				if (HttpClientHandlerType == null)
					AddEnvironmentVariable (defaultHttpMessageHandler[0], defaultHttpMessageHandler[1]);
				else
					AddEnvironmentVariable ("XA_HTTP_CLIENT_HANDLER_TYPE", HttpClientHandlerType.Trim ());
			}

			if (!haveTlsProvider) {
				if (TlsProvider == null)
					AddEnvironmentVariable (defaultTlsProvider[0], defaultTlsProvider[1]);
				else
					AddEnvironmentVariable ("XA_TLS_PROVIDER", TlsProvider.Trim ());
			}

			if (!haveMonoGCParams) {
				if (EnableSGenConcurrent)
					AddEnvironmentVariable ("MONO_GC_PARAMS", "major=marksweep-conc");
				else
					AddEnvironmentVariable ("MONO_GC_PARAMS", "major=marksweep");
			}

			using (var ms = new MemoryStream ()) {
				var utf8Encoding = new UTF8Encoding (false);
				foreach (string abi in SupportedAbis) {
					ms.SetLength (0);
					NativeAssemblerTargetProvider asmTargetProvider;
					string asmFileName = Path.Combine (EnvironmentOutputDirectory, $"environment.{abi.ToLowerInvariant ()}.s");
					switch (abi.Trim ()) {
						case "armeabi-v7a":
							asmTargetProvider = new ARMNativeAssemblerTargetProvider (false);
							break;

						case "arm64-v8a":
							asmTargetProvider = new ARMNativeAssemblerTargetProvider (true);
							break;

						case "x86":
							asmTargetProvider = new X86NativeAssemblerTargetProvider (false);
							break;

						case "x86_64":
							asmTargetProvider = new X86NativeAssemblerTargetProvider (true);
							break;

						default:
							throw new InvalidOperationException ($"Unknown ABI {abi}");
					}

					var asmgen = new ApplicationConfigNativeAssemblyGenerator (asmTargetProvider, environmentVariables, systemProperties) {
						IsBundledApp = IsBundledApplication,
						UsesEmbeddedDSOs = usesEmbeddedDSOs,
						UsesMonoAOT = usesMonoAOT,
						UsesMonoLLVM = EnableLLVM,
						UsesAssemblyPreload = usesAssemblyPreload,
						MonoAOTMode = monoAOTMode.ToString ().ToLowerInvariant (),
						AndroidPackageName = AndroidPackageName,
					};

					using (var sw = new StreamWriter (ms, utf8Encoding, bufferSize: 8192, leaveOpen: true)) {
						asmgen.Write (sw, asmFileName);
						MonoAndroidHelper.CopyIfStreamChanged (ms, asmFileName);
					}

				}
			}

			void AddEnvironmentVariable (string name, string value)
			{
				if (Char.IsUpper(name [0]) || !Char.IsLetter(name [0]))
					environmentVariables [ValidAssemblerString (name)] = ValidAssemblerString (value);
				else
					systemProperties [ValidAssemblerString (name)] = ValidAssemblerString (value);
			}

			void AddEnvironmentVariableLine (string l)
			{
				string line = l?.Trim ();
				if (String.IsNullOrEmpty (line) || line [0] == '#')
					return;

				string[] nv = line.Split (new char[]{'='}, 2);
				AddEnvironmentVariable (nv[0].Trim (), nv.Length < 2 ? String.Empty : nv[1].Trim ());
			}

			string ValidAssemblerString (string s)
			{
				return s.Replace ("\"", "\\\"");
			}
		}
	}
}
