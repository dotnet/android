using System;
using System.IO;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;

namespace Xamarin.Android.Tasks;

public class GeneratePackageManagerJava : AndroidTask
{
	public override string TaskPrefix => "GPM";

	[Required]
	public string MainAssembly { get; set; } = "";

	[Required]
	public string OutputDirectory { get; set; } = "";

	[Required]
	public ITaskItem [] ResolvedUserAssemblies { get; set; } = [];

	public override bool RunTask ()
	{
		// We need to include any special assemblies in the Assemblies list
		var mainFileName = Path.GetFileName (MainAssembly);

		using (var pkgmgr = MemoryStreamPool.Shared.CreateStreamWriter ()) {
			pkgmgr.WriteLine ("package mono;");

			// Write all the user assemblies
			pkgmgr.WriteLine ("public class MonoPackageManager_Resources {");
			pkgmgr.WriteLine ("\tpublic static String[] Assemblies = new String[]{");

		public ITaskItem[] NativeLibraries { get; set; }

		public ITaskItem[] MonoComponents { get; set; }

		public ITaskItem[] SatelliteAssemblies { get; set; }

		public bool UseAssemblyStore { get; set; }

		[Required]
		public string OutputDirectory { get; set; }

		[Required]
		public string EnvironmentOutputDirectory { get; set; }

		[Required]
		public string IntermediateOutputDirectory { get; set; } = "";

		[Required]
		public string MainAssembly { get; set; }

		[Required]
		public string TargetFrameworkVersion { get; set; }

		[Required]
		public string Manifest { get; set; }

		[Required]
		public string [] SupportedAbis { get; set; }

		[Required]
		public string AndroidPackageName { get; set; }

		[Required]
		public bool EnablePreloadAssembliesDefault { get; set; }

		[Required]
		public bool TargetsCLR { get; set; }

		public bool EnableMarshalMethods { get; set; }
		public bool EnableManagedMarshalMethodsLookup { get; set; }
		public string RuntimeConfigBinFilePath { get; set; }
		public string ProjectRuntimeConfigFilePath { get; set; } = String.Empty;
		public string BoundExceptionType { get; set; }

		public string PackageNamingPolicy { get; set; }
		public string Debug { get; set; }
		public ITaskItem[] Environments { get; set; }
		public string AndroidAotMode { get; set; }
		public bool AndroidAotEnableLazyLoad { get; set; }
		public bool EnableLLVM { get; set; }
		public string HttpClientHandlerType { get; set; }
		public string TlsProvider { get; set; }
		public string AndroidSequencePointsMode { get; set; }
		public bool EnableSGenConcurrent { get; set; }
		public string? CustomBundleConfigFile { get; set; }
		public bool EnableNativeRuntimeLinking { get; set; }

		bool _Debug {
			get {
				return string.Equals (Debug, "true", StringComparison.OrdinalIgnoreCase);
			}
		}

		public override bool RunTask ()
		{
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

		static internal AndroidTargetArch GetAndroidTargetArchForAbi (string abi) => MonoAndroidHelper.AbiToTargetArch (abi);

		static readonly string[] defaultLogLevel = {"MONO_LOG_LEVEL", "info"};
		static readonly string[] defaultMonoDebug = {"MONO_DEBUG", "gen-compact-seq-points"};
		static readonly string[] defaultHttpMessageHandler = {"XA_HTTP_CLIENT_HANDLER_TYPE", "System.Net.Http.HttpClientHandler, System.Net.Http"};
		static readonly string[] defaultTlsProvider = {"XA_TLS_PROVIDER", "btls"};

		void AddEnvironment ()
		{
			bool usesMonoAOT = false;
			var environmentVariables = new Dictionary<string, string> (StringComparer.Ordinal);
			var systemProperties = new Dictionary<string, string> (StringComparer.Ordinal);

			if (!Enum.TryParse (PackageNamingPolicy, out PackageNamingPolicy pnp)) {
				pnp = PackageNamingPolicyEnum.LowercaseCrc64;
			}

			AotMode aotMode = AotMode.None;
			if (!string.IsNullOrEmpty (AndroidAotMode) && Aot.GetAndroidAotMode (AndroidAotMode, out aotMode) && aotMode != AotMode.None) {
				usesMonoAOT = true;
			}

			SequencePointsMode sequencePointsMode;
			if (!Aot.TryGetSequencePointsMode (AndroidSequencePointsMode, out sequencePointsMode))
				sequencePointsMode = SequencePointsMode.None;

			// Even though environment files were potentially parsed in GenerateJavaStubs, we need to do it here again because we might have additional environment
			// files (generated by us) which weren't present by the time GeneratJavaStubs ran.
			var environmentParser = new EnvironmentFilesParser {
				BrokenExceptionTransitions = false,
				UsesAssemblyPreload = EnablePreloadAssembliesDefault,
			};
			environmentParser.Parse (Environments, sequencePointsMode, Log);

			foreach (string line in environmentParser.EnvironmentVariableLines) {
				AddEnvironmentVariableLine (line);
			}

			if (_Debug && !environmentParser.HaveLogLevel) {
				AddEnvironmentVariable (defaultLogLevel[0], defaultLogLevel[1]);
			}

			if (sequencePointsMode != SequencePointsMode.None && !environmentParser.HaveMonoDebug) {
				AddEnvironmentVariable (defaultMonoDebug[0], defaultMonoDebug[1]);
			}

			if (!environmentParser.HaveHttpMessageHandler) {
				if (HttpClientHandlerType == null)
					AddEnvironmentVariable (defaultHttpMessageHandler[0], defaultHttpMessageHandler[1]);
				else
					AddEnvironmentVariable ("XA_HTTP_CLIENT_HANDLER_TYPE", HttpClientHandlerType.Trim ());
			}

			if (!environmentParser.HaveMonoGCParams) {
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
			bool enableMarshalMethods = EnableMarshalMethods;
			HashSet<string> archAssemblyNames = null;
			HashSet<string> uniqueAssemblyNames = new HashSet<string> (StringComparer.OrdinalIgnoreCase);
			Action<ITaskItem> updateAssemblyCount = (ITaskItem assembly) => {
				string? culture = MonoAndroidHelper.GetAssemblyCulture (assembly);
				string fileName = Path.GetFileName (assembly.ItemSpec);
				string assemblyName;

				if (String.IsNullOrEmpty (culture)) {
					assemblyName = fileName;
				} else {
					assemblyName = $"{culture}/{fileName}";
				}

				if (!uniqueAssemblyNames.Contains (assemblyName)) {
					uniqueAssemblyNames.Add (assemblyName);
				}

				string abi = MonoAndroidHelper.GetAssemblyAbi (assembly);
				archAssemblyNames ??= new HashSet<string> (StringComparer.OrdinalIgnoreCase);

				if (!archAssemblyNames.Contains (assemblyName)) {
					assemblyCount++;
					archAssemblyNames.Add (assemblyName);
				}
			};

			if (SatelliteAssemblies != null) {
				foreach (ITaskItem assembly in SatelliteAssemblies) {
					updateNameWidth (assembly);
					updateAssemblyCount (assembly);
				}
			}

			int android_runtime_jnienv_class_token = -1;
			int jnienv_initialize_method_token = -1;
			int jnienv_registerjninatives_method_token = -1;
			foreach (var assembly in ResolvedAssemblies) {
				updateNameWidth (assembly);
				updateAssemblyCount (assembly);

				if (android_runtime_jnienv_class_token != -1) {
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

			NativeCodeGenStateCollection? nativeCodeGenStates = null;

			if (enableMarshalMethods || EnableNativeRuntimeLinking) {
				nativeCodeGenStates = BuildEngine4.GetRegisteredTaskObjectAssemblyLocal<NativeCodeGenStateCollection> (
					MonoAndroidHelper.GetProjectBuildSpecificTaskObjectKey (GenerateJavaStubs.NativeCodeGenStateObjectRegisterTaskKey, WorkingDirectory, IntermediateOutputDirectory),
					RegisteredTaskObjectLifetime.Build
				);
			}

			bool haveRuntimeConfigBlob = !String.IsNullOrEmpty (RuntimeConfigBinFilePath) && File.Exists (RuntimeConfigBinFilePath);
			var jniRemappingNativeCodeInfo = BuildEngine4.GetRegisteredTaskObjectAssemblyLocal<GenerateJniRemappingNativeCode.JniRemappingNativeCodeInfo> (ProjectSpecificTaskObjectKey (GenerateJniRemappingNativeCode.JniRemappingNativeCodeInfoKey), RegisteredTaskObjectLifetime.Build);
			LLVMIR.LlvmIrComposer appConfigAsmGen;

			if (TargetsCLR) {
				Dictionary<string, string>? runtimeProperties = RuntimePropertiesParser.ParseConfig (ProjectRuntimeConfigFilePath);
				appConfigAsmGen = new ApplicationConfigNativeAssemblyGeneratorCLR (environmentVariables, systemProperties, runtimeProperties, Log) {
					UsesAssemblyPreload = environmentParser.UsesAssemblyPreload,
					AndroidPackageName = AndroidPackageName,
					PackageNamingPolicy = pnp,
					JniAddNativeMethodRegistrationAttributePresent = NativeCodeGenState.TemplateJniAddNativeMethodRegistrationAttributePresent,
					NumberOfAssembliesInApk = assemblyCount,
					BundledAssemblyNameWidth = assemblyNameWidth,
					NativeLibraries = uniqueNativeLibraries,
					AndroidRuntimeJNIEnvToken = android_runtime_jnienv_class_token,
					JNIEnvInitializeToken = jnienv_initialize_method_token,
					JNIEnvRegisterJniNativesToken = jnienv_registerjninatives_method_token,
					JniRemappingReplacementTypeCount = jniRemappingNativeCodeInfo == null ? 0 : jniRemappingNativeCodeInfo.ReplacementTypeCount,
					JniRemappingReplacementMethodIndexEntryCount = jniRemappingNativeCodeInfo == null ? 0 : jniRemappingNativeCodeInfo.ReplacementMethodIndexEntryCount,
					MarshalMethodsEnabled = EnableMarshalMethods,
					ManagedMarshalMethodsLookupEnabled = EnableManagedMarshalMethodsLookup,
					IgnoreSplitConfigs = ShouldIgnoreSplitConfigs (),
				};
			} else {
				appConfigAsmGen = new ApplicationConfigNativeAssemblyGenerator (environmentVariables, systemProperties, Log) {
					UsesMonoAOT = usesMonoAOT,
					UsesMonoLLVM = EnableLLVM,
					UsesAssemblyPreload = environmentParser.UsesAssemblyPreload,
					MonoAOTMode = aotMode.ToString ().ToLowerInvariant (),
					AotEnableLazyLoad = AndroidAotEnableLazyLoad,
					AndroidPackageName = AndroidPackageName,
					BrokenExceptionTransitions = environmentParser.BrokenExceptionTransitions,
					PackageNamingPolicy = pnp,
					BoundExceptionType = boundExceptionType,
					JniAddNativeMethodRegistrationAttributePresent = NativeCodeGenState.TemplateJniAddNativeMethodRegistrationAttributePresent,
					HaveRuntimeConfigBlob = haveRuntimeConfigBlob,
					NumberOfAssembliesInApk = assemblyCount,
					BundledAssemblyNameWidth = assemblyNameWidth,
					MonoComponents = (MonoComponent)monoComponents,
					NativeLibraries = uniqueNativeLibraries,
					HaveAssemblyStore = UseAssemblyStore,
					AndroidRuntimeJNIEnvToken = android_runtime_jnienv_class_token,
					JNIEnvInitializeToken = jnienv_initialize_method_token,
					JNIEnvRegisterJniNativesToken = jnienv_registerjninatives_method_token,
					JniRemappingReplacementTypeCount = jniRemappingNativeCodeInfo == null ? 0 : jniRemappingNativeCodeInfo.ReplacementTypeCount,
					JniRemappingReplacementMethodIndexEntryCount = jniRemappingNativeCodeInfo == null ? 0 : jniRemappingNativeCodeInfo.ReplacementMethodIndexEntryCount,
					MarshalMethodsEnabled = EnableMarshalMethods,
					ManagedMarshalMethodsLookupEnabled = EnableManagedMarshalMethodsLookup,
					IgnoreSplitConfigs = ShouldIgnoreSplitConfigs (),
				};
			}
			LLVMIR.LlvmIrModule appConfigModule = appConfigAsmGen.Construct ();

			foreach (string abi in SupportedAbis) {
				string targetAbi = abi.ToLowerInvariant ();
				string environmentBaseAsmFilePath = Path.Combine (EnvironmentOutputDirectory, $"environment.{targetAbi}");
				string marshalMethodsBaseAsmFilePath = Path.Combine (EnvironmentOutputDirectory, $"marshal_methods.{targetAbi}");
				string? pinvokePreserveBaseAsmFilePath = EnableNativeRuntimeLinking ? Path.Combine (EnvironmentOutputDirectory, $"pinvoke_preserve.{targetAbi}") : null;
				string environmentLlFilePath  = $"{environmentBaseAsmFilePath}.ll";
				string marshalMethodsLlFilePath = $"{marshalMethodsBaseAsmFilePath}.ll";
				string? pinvokePreserveLlFilePath = pinvokePreserveBaseAsmFilePath != null ? $"{pinvokePreserveBaseAsmFilePath}.ll" : null;
				AndroidTargetArch targetArch = GetAndroidTargetArchForAbi (abi);

				using var appConfigWriter = MemoryStreamPool.Shared.CreateStreamWriter ();
				try {
					appConfigAsmGen.Generate (appConfigModule, targetArch, appConfigWriter, environmentLlFilePath);
				} catch {
					throw;
				} finally {
					appConfigWriter.Flush ();
					Files.CopyIfStreamChanged (appConfigWriter.BaseStream, environmentLlFilePath);
				}

				MarshalMethodsNativeAssemblyGenerator marshalMethodsAsmGen;
				if (enableMarshalMethods) {
					marshalMethodsAsmGen = new MarshalMethodsNativeAssemblyGenerator (
						Log,
						assemblyCount,
						uniqueAssemblyNames,
						EnsureCodeGenState (targetArch),
						EnableManagedMarshalMethodsLookup
					);
				} else {
					marshalMethodsAsmGen = new MarshalMethodsNativeAssemblyGenerator (
						Log,
						targetArch,
						assemblyCount,
						uniqueAssemblyNames
					);
				}

				if (EnableNativeRuntimeLinking) {
					var pinvokePreserveGen = new PreservePinvokesNativeAssemblyGenerator (Log, EnsureCodeGenState (targetArch), MonoComponents);
					LLVMIR.LlvmIrModule pinvokePreserveModule = pinvokePreserveGen.Construct ();
					using var pinvokePreserveWriter = MemoryStreamPool.Shared.CreateStreamWriter ();
					try {
						pinvokePreserveGen.Generate (pinvokePreserveModule, targetArch, pinvokePreserveWriter, pinvokePreserveLlFilePath);
					} catch {
						throw;
					} finally {
						pinvokePreserveWriter.Flush ();
						Files.CopyIfStreamChanged (pinvokePreserveWriter.BaseStream, pinvokePreserveLlFilePath);
					}
				}

				LLVMIR.LlvmIrModule marshalMethodsModule = marshalMethodsAsmGen.Construct ();
				using var marshalMethodsWriter = MemoryStreamPool.Shared.CreateStreamWriter ();
				try {
					marshalMethodsAsmGen.Generate (marshalMethodsModule, targetArch, marshalMethodsWriter, marshalMethodsLlFilePath);
				} catch {
					throw;
				} finally {
					marshalMethodsWriter.Flush ();
					Files.CopyIfStreamChanged (marshalMethodsWriter.BaseStream, marshalMethodsLlFilePath);
				}
			}

			NativeCodeGenStateObject EnsureCodeGenState (AndroidTargetArch targetArch)
			{
				if (nativeCodeGenStates == null || !nativeCodeGenStates.States.TryGetValue (targetArch, out NativeCodeGenStateObject? state)) {
					throw new InvalidOperationException ($"Internal error: missing native code generation state for architecture '{targetArch}'");
				}

				return state;
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

		return !Log.HasLoggedErrors;
	}
}
