// Copyright (C) 2011 Xamarin, Inc. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using Java.Interop.Tools.TypeNameMappings;
using Xamarin.Android.Tools;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	using PackageNamingPolicyEnum   = PackageNamingPolicy;

	public class GeneratePackageManagerJava : AndroidTask
	{
		public override string TaskPrefix => "GPM";

		Guid buildId = Guid.NewGuid ();

		[Required]
		public ITaskItem[] ResolvedAssemblies { get; set; }

		[Required]
		public ITaskItem[] ResolvedUserAssemblies { get; set; }

		public ITaskItem[] NativeLibraries { get; set; }

		public ITaskItem[] MonoComponents { get; set; }

		public ITaskItem[] SatelliteAssemblies { get; set; }

		public bool UseAssemblyStore { get; set; }

		[Required]
		public string OutputDirectory { get; set; }

		[Required]
		public string EnvironmentOutputDirectory { get; set; }

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

		[Required]
		public bool InstantRunEnabled { get; set; }

		public string RuntimeConfigBinFilePath { get; set; }
		public string BoundExceptionType { get; set; }

		public string PackageNamingPolicy { get; set; }
		public string Debug { get; set; }
		public ITaskItem[] Environments { get; set; }
		public string AndroidAotMode { get; set; }
		public bool EnableLLVM { get; set; }
		public string HttpClientHandlerType { get; set; }
		public string TlsProvider { get; set; }
		public string AndroidSequencePointsMode { get; set; }
		public bool EnableSGenConcurrent { get; set; }
		public bool UsingAndroidNETSdk { get; set; }

		[Output]
		public string BuildId { get; set; }

		bool _Debug {
			get {
				return string.Equals (Debug, "true", StringComparison.OrdinalIgnoreCase);
			}
		}

		public override bool RunTask ()
		{
			BuildId = buildId.ToString ();
			Log.LogDebugMessage ("  [Output] BuildId: {0}", BuildId);

			var doc = AndroidAppManifest.Load (Manifest, MonoAndroidHelper.SupportedVersions);
			int minApiVersion = doc.MinSdkVersion == null ? 4 : (int) doc.MinSdkVersion;
			// We need to include any special assemblies in the Assemblies list
			var mainFileName = Path.GetFileName (MainAssembly);

			using (var pkgmgr = MemoryStreamPool.Shared.CreateStreamWriter ()) {
				pkgmgr.WriteLine ("package mono;");

				// Write all the user assemblies
				pkgmgr.WriteLine ("public class MonoPackageManager_Resources {");
				pkgmgr.WriteLine ("\tpublic static String[] Assemblies = new String[]{");

				pkgmgr.WriteLine ("\t\t/* We need to ensure that \"{0}\" comes first in this list. */", mainFileName);
				pkgmgr.WriteLine ("\t\t\"" + mainFileName + "\",");
				foreach (var assembly in ResolvedUserAssemblies) {
					if (string.Compare (Path.GetFileName (assembly.ItemSpec), mainFileName, StringComparison.OrdinalIgnoreCase) == 0)
						continue;
					pkgmgr.WriteLine ("\t\t\"" + Path.GetFileName (assembly.ItemSpec) + "\",");
				}
				foreach (var assembly in MonoAndroidHelper.GetFrameworkAssembliesToTreatAsUserAssemblies (ResolvedAssemblies)) {
					if (string.Compare (Path.GetFileName (assembly.ItemSpec), mainFileName, StringComparison.OrdinalIgnoreCase) == 0)
						continue;
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

				pkgmgr.WriteLine ("}");
				pkgmgr.Flush ();

				// Only copy to the real location if the contents actually changed
				var dest = Path.GetFullPath (Path.Combine (OutputDirectory, "MonoPackageManager_Resources.java"));

				Files.CopyIfStreamChanged (pkgmgr.BaseStream, dest);
			}

			AddEnvironment ();

			return !Log.HasLoggedErrors;
		}

		static internal AndroidTargetArch GetAndroidTargetArchForAbi (string abi)
		{
			switch (abi.Trim ()) {
				case "armeabi-v7a":
					return AndroidTargetArch.Arm;

				case "arm64-v8a":
					return AndroidTargetArch.Arm64;

				case "x86":
					return AndroidTargetArch.X86;

				case "x86_64":
					return AndroidTargetArch.X86_64;

				default:
					throw new InvalidOperationException ($"Unknown ABI {abi}");
			}
		}

		static readonly string[] defaultLogLevel = {"MONO_LOG_LEVEL", "info"};
		static readonly string[] defaultMonoDebug = {"MONO_DEBUG", "gen-compact-seq-points"};
		static readonly string[] defaultHttpMessageHandler = {"XA_HTTP_CLIENT_HANDLER_TYPE", "System.Net.Http.HttpClientHandler, System.Net.Http"};
		static readonly string[] defaultTlsProvider = {"XA_TLS_PROVIDER", "btls"};

		void AddEnvironment ()
		{
			bool usesMonoAOT = false;
			bool usesAssemblyPreload = EnablePreloadAssembliesDefault;
			bool brokenExceptionTransitions = false;
			var environmentVariables = new Dictionary<string, string> (StringComparer.Ordinal);
			var systemProperties = new Dictionary<string, string> (StringComparer.Ordinal);

			if (!Enum.TryParse (PackageNamingPolicy, out PackageNamingPolicy pnp)) {
				pnp = PackageNamingPolicyEnum.LowercaseCrc64;
			}

			AotMode aotMode = AotMode.None;
			if (!string.IsNullOrEmpty (AndroidAotMode) && Aot.GetAndroidAotMode (AndroidAotMode, out aotMode) && aotMode != AotMode.None) {
				usesMonoAOT = true;
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

			foreach (ITaskItem env in Environments ?? Array.Empty<ITaskItem> ()) {
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

					if (!UsingAndroidNETSdk && lineToWrite.StartsWith ("XA_TLS_PROVIDER=", StringComparison.Ordinal))
						haveTlsProvider = true;

					if (lineToWrite.StartsWith ("mono.enable_assembly_preload=", StringComparison.Ordinal)) {
						int idx = lineToWrite.IndexOf ('=');
						uint val;
						if (idx < lineToWrite.Length - 1 && UInt32.TryParse (lineToWrite.Substring (idx + 1), out val)) {
							usesAssemblyPreload = idx == 1;
						}
						continue;
					}
					if (lineToWrite.StartsWith ("XA_BROKEN_EXCEPTION_TRANSITIONS=", StringComparison.Ordinal)) {
						brokenExceptionTransitions = true;
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

			if (!UsingAndroidNETSdk && !haveTlsProvider) {
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

			global::Android.Runtime.BoundExceptionType boundExceptionType;
			if (String.IsNullOrEmpty (BoundExceptionType) || String.Compare (BoundExceptionType, "System", StringComparison.OrdinalIgnoreCase) == 0) {
				boundExceptionType = global::Android.Runtime.BoundExceptionType.System;
			} else if (String.Compare (BoundExceptionType, "Java", StringComparison.OrdinalIgnoreCase) == 0) {
				boundExceptionType = global::Android.Runtime.BoundExceptionType.Java;
			} else {
				throw new InvalidOperationException ($"Unsupported BoundExceptionType value '{BoundExceptionType}'");
			}

			int assemblyNameWidth = 0;
			Encoding assemblyNameEncoding = Encoding.UTF8;

			Action<ITaskItem> updateNameWidth = (ITaskItem assembly) => {
				if (UseAssemblyStore) {
					return;
				}

				string assemblyName = Path.GetFileName (assembly.ItemSpec);
				int nameBytes = assemblyNameEncoding.GetBytes (assemblyName).Length;
				if (nameBytes > assemblyNameWidth) {
					assemblyNameWidth = nameBytes;
				}
			};

			int assemblyCount = 0;
			HashSet<string> archAssemblyNames = null;

			Action<ITaskItem> updateAssemblyCount = (ITaskItem assembly) => {
				if (!UseAssemblyStore) {
					assemblyCount++;
					return;
				}

				if (Boolean.TryParse (assembly.GetMetadata ("AndroidSkipAddToPackage"), out bool value) && value) {
					return;
				}

				string abi = assembly.GetMetadata ("Abi");
				if (String.IsNullOrEmpty (abi)) {
					assemblyCount++;
				} else {
					archAssemblyNames ??= new HashSet<string> (StringComparer.OrdinalIgnoreCase);

					string assemblyName = Path.GetFileName (assembly.ItemSpec);
					if (!archAssemblyNames.Contains (assemblyName)) {
						assemblyCount++;
						archAssemblyNames.Add (assemblyName);
					}
				}
			};

			if (SatelliteAssemblies != null) {
				foreach (ITaskItem assembly in SatelliteAssemblies) {
					updateNameWidth (assembly);
					updateAssemblyCount (assembly);
				}
			}

			foreach (var assembly in ResolvedAssemblies) {
				updateNameWidth (assembly);
				updateAssemblyCount (assembly);
			}

			if (!UseAssemblyStore) {
				int abiNameLength = 0;
				foreach (string abi in SupportedAbis) {
					if (abi.Length <= abiNameLength) {
						continue;
					}
					abiNameLength = abi.Length;
				}
				assemblyNameWidth += abiNameLength + 2; // room for '/' and the terminating NUL
			}

			MonoComponent monoComponents = MonoComponent.None;
			if (MonoComponents != null && MonoComponents.Length > 0) {
				foreach (ITaskItem item in MonoComponents) {
					if (String.Compare ("diagnostics_tracing", item.ItemSpec, StringComparison.OrdinalIgnoreCase) == 0) {
						monoComponents |= MonoComponent.Tracing;
					} else if (String.Compare ("hot_reload", item.ItemSpec, StringComparison.OrdinalIgnoreCase) == 0) {
						monoComponents |= MonoComponent.HotReload;
					} else if (String.Compare ("debugger", item.ItemSpec, StringComparison.OrdinalIgnoreCase) == 0) {
						monoComponents |= MonoComponent.Debugger;
					}
				}
			}

			var uniqueNativeLibraries = new List<ITaskItem> ();
			if (NativeLibraries != null) {
				var seenNativeLibraryNames = new HashSet<string> (StringComparer.OrdinalIgnoreCase);
				foreach (ITaskItem item in NativeLibraries) {
					// We don't care about different ABIs here, just the file name
					string name = Path.GetFileName (item.ItemSpec);
					if (seenNativeLibraryNames.Contains (name)) {
						continue;
					}

					seenNativeLibraryNames.Add (name);
					uniqueNativeLibraries.Add (item);
				}
			}

			bool haveRuntimeConfigBlob = !String.IsNullOrEmpty (RuntimeConfigBinFilePath) && File.Exists (RuntimeConfigBinFilePath);
			var appConfState = BuildEngine4.GetRegisteredTaskObjectAssemblyLocal<ApplicationConfigTaskState> (ApplicationConfigTaskState.RegisterTaskObjectKey, RegisteredTaskObjectLifetime.Build);

			foreach (string abi in SupportedAbis) {
				string baseAsmFilePath = Path.Combine (EnvironmentOutputDirectory, $"environment.{abi.ToLowerInvariant ()}");
				string asmFilePath = $"{baseAsmFilePath}.s";

				var asmgen = new ApplicationConfigNativeAssemblyGenerator (GetAndroidTargetArchForAbi (abi), environmentVariables, systemProperties, Log) {
					IsBundledApp = IsBundledApplication,
					UsesMonoAOT = usesMonoAOT,
					UsesMonoLLVM = EnableLLVM,
					UsesAssemblyPreload = usesAssemblyPreload,
					MonoAOTMode = aotMode.ToString ().ToLowerInvariant (),
					AndroidPackageName = AndroidPackageName,
					BrokenExceptionTransitions = brokenExceptionTransitions,
					PackageNamingPolicy = pnp,
					BoundExceptionType = boundExceptionType,
					InstantRunEnabled = InstantRunEnabled,
					JniAddNativeMethodRegistrationAttributePresent = appConfState != null ? appConfState.JniAddNativeMethodRegistrationAttributePresent : false,
					HaveRuntimeConfigBlob = haveRuntimeConfigBlob,
					NumberOfAssembliesInApk = assemblyCount,
					BundledAssemblyNameWidth = assemblyNameWidth,
					NumberOfAssemblyStoresInApks = 2, // Until feature APKs are a thing, we're going to have just two stores in each app - one for arch-agnostic
									  // and up to 4 other for arch-specific assemblies. Only **one** arch-specific store is ever loaded on the app
									  // runtime, thus the number 2 here. All architecture specific stores contain assemblies with the same names
									  // and in the same order.
					MonoComponents = monoComponents,
					NativeLibraries = uniqueNativeLibraries,
					HaveAssemblyStore = UseAssemblyStore,
				};

				using (var sw = MemoryStreamPool.Shared.CreateStreamWriter ()) {
					asmgen.Write (sw, asmFilePath);
					sw.Flush ();
					Files.CopyIfStreamChanged (sw.BaseStream, asmFilePath);
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
