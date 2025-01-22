// Copyright (C) 2011 Xamarin, Inc. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Mono.Cecil;
using Microsoft.Build.Utilities;

using Java.Interop.Tools.Cecil;
using Java.Interop.Tools.Diagnostics;
using Java.Interop.Tools.JavaCallableWrappers;
using Java.Interop.Tools.TypeNameMappings;

using Xamarin.Android.Tools;
using Microsoft.Android.Build.Tasks;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace Xamarin.Android.Tasks
{
	using PackageNamingPolicyEnum   = PackageNamingPolicy;

	public class GenerateJavaStubs : AndroidTask
	{
		public const string NativeCodeGenStateRegisterTaskKey = ".:!MarshalMethods!:.";

		public override string TaskPrefix => "GJS";

		[Required]
		public ITaskItem[] ResolvedAssemblies { get; set; }

		[Required]
		public ITaskItem[] ResolvedUserAssemblies { get; set; }

		[Required]
		public ITaskItem [] FrameworkDirectories { get; set; }

		[Required]
		public string [] SupportedAbis { get; set; }

		[Required]
		public string TypemapOutputDirectory { get; set; }

		[Required]
		public bool GenerateNativeAssembly { get; set; }

		public string IntermediateOutputDirectory { get; set; }
		public bool LinkingEnabled { get; set; }
		public bool HaveMultipleRIDs { get; set; }
		public bool EnableMarshalMethods { get; set; }

		public bool Debug { get; set; }

		public string AndroidSdkPlatform { get; set; }
		public string OutputDirectory { get; set; }

		public bool ErrorOnCustomJavaObject { get; set; }

		public string PackageNamingPolicy { get; set; }

		public string ApplicationJavaClass { get; set; }

		public bool SkipJniAddNativeMethodRegistrationAttributeScan { get; set; }

		public ITaskItem[] Environments { get; set; }

		[Output]
		public ITaskItem[] GeneratedBinaryTypeMaps { get; set; }

		[Required]
		public string AndroidRuntime { get; set; } = "";

		internal const string AndroidSkipJavaStubGeneration = "AndroidSkipJavaStubGeneration";

		public override bool RunTask ()
		{
			try {
				androidRuntime = MonoAndroidHelper.ParseAndroidRuntime (AndroidRuntime);
				codeGenerationTarget = MonoAndroidHelper.ParseCodeGenerationTarget (CodeGenerationTarget);
				bool useMarshalMethods = !Debug && EnableMarshalMethods;
				Run (useMarshalMethods);
			} catch (XamarinAndroidException e) {
				Log.LogCodedError (string.Format ("XA{0:0000}", e.Code), e.MessageWithoutCode);
				if (MonoAndroidHelper.LogInternalExceptions)
					Log.LogMessage (e.ToString ());
			}

			return !Log.HasLoggedErrors;
		}

		XAAssemblyResolver MakeResolver (bool useMarshalMethods, AndroidTargetArch targetArch, Dictionary<string, ITaskItem> assemblies)
		{
			var readerParams = new ReaderParameters ();
			if (useMarshalMethods) {
				readerParams.ReadWrite = true;
				readerParams.InMemory = true;
			}

			var res = new XAAssemblyResolver (targetArch, Log, loadDebugSymbols: true, loadReaderParameters: readerParams);
			var uniqueDirs = new HashSet<string> (StringComparer.OrdinalIgnoreCase);

			Log.LogDebugMessage ($"Adding search directories to new architecture {targetArch} resolver:");
			foreach (var kvp in assemblies) {
				string assemblyDir = Path.GetDirectoryName (kvp.Value.ItemSpec);
				if (uniqueDirs.Contains (assemblyDir)) {
					continue;
				}

				uniqueDirs.Add (assemblyDir);
				res.SearchDirectories.Add (assemblyDir);
				Log.LogDebugMessage ($"  {assemblyDir}");
			}

			return res;
		}

		void Run (bool useMarshalMethods)
		{
			PackageNamingPolicy pnp;
			JavaNativeTypeManager.PackageNamingPolicy = Enum.TryParse (PackageNamingPolicy, out pnp) ? pnp : PackageNamingPolicyEnum.LowercaseCrc64;

			// We will process each architecture completely separately as both type maps and marshal methods are strictly per-architecture and
			// the assemblies should be processed strictly per architecture.  Generation of JCWs, and the manifest are ABI-agnostic.
			// We will generate them only for the first architecture, whichever it is.
			Dictionary<AndroidTargetArch, Dictionary<string, ITaskItem>> allAssembliesPerArch = MonoAndroidHelper.GetPerArchAssemblies (ResolvedAssemblies, SupportedAbis, validate: true);

			// Should "never" happen...
			if (allAssembliesPerArch.Count != SupportedAbis.Length) {
				// ...but it happens at least in our `BuildAMassiveApp` test, where `SupportedAbis` mentions only the `x86` and `armeabi-v7a` ABIs, but `ResolvedAssemblies` contains
				// entries for all the ABIs we support, so let's be flexible and ignore the extra architectures but still error out if there are less architectures than supported ABIs.
				if (allAssembliesPerArch.Count < SupportedAbis.Length) {
					throw new InvalidOperationException ($"Internal error: number of architectures ({allAssembliesPerArch.Count}) must equal the number of target ABIs ({SupportedAbis.Length})");
				}
			}

			// ...or this...
			foreach (string abi in SupportedAbis) {
				AndroidTargetArch arch = MonoAndroidHelper.AbiToTargetArch (abi);
				if (!allAssembliesPerArch.ContainsKey (arch)) {
					throw new InvalidOperationException ($"Internal error: no assemblies for architecture '{arch}', which corresponds to target abi '{abi}'");
				}
			}

			// ...as well as this
			Dictionary<AndroidTargetArch, Dictionary<string, ITaskItem>> userAssembliesPerArch = MonoAndroidHelper.GetPerArchAssemblies (ResolvedUserAssemblies, SupportedAbis, validate: true);
			foreach (var kvp in userAssembliesPerArch) {
				if (!allAssembliesPerArch.TryGetValue (kvp.Key, out Dictionary<string, ITaskItem> allAssemblies)) {
					throw new InvalidOperationException ($"Internal error: found user assemblies for architecture '{kvp.Key}' which isn't found in ResolvedAssemblies");
				}

				foreach (var asmKvp in kvp.Value) {
					if (!allAssemblies.ContainsKey (asmKvp.Key)) {
						throw new InvalidOperationException ($"Internal error: user assembly '{asmKvp.Value}' not found in ResolvedAssemblies");
					}
				}
			}

			// Now that "never" never happened, we can proceed knowing that at least the assembly sets are the same for each architecture
			var nativeCodeGenStates = new ConcurrentDictionary<AndroidTargetArch, NativeCodeGenState> ();
			NativeCodeGenState? templateCodeGenState = null;

			var firstArch = allAssembliesPerArch.First ().Key;
			var generateSucceeded = true;

			// Generate Java sources in parallel
			Parallel.ForEach (allAssembliesPerArch, (kvp) => {
				AndroidTargetArch arch = kvp.Key;
				Dictionary<string, ITaskItem> archAssemblies = kvp.Value;

				// We only need to generate Java code for one ABI, as the Java code is ABI-agnostic
				// Pick the "first" one as the one to generate Java code for
				var generateJavaCode = arch == firstArch;

				(bool success, NativeCodeGenState? state) = GenerateJavaSourcesAndMaybeClassifyMarshalMethods (arch, archAssemblies, MaybeGetArchAssemblies (userAssembliesPerArch, arch), useMarshalMethods, generateJavaCode);

				if (!success) {
					generateSucceeded = false;
				}

				// If this is the first architecture, we need to store the state for later use
				if (generateJavaCode) {
					templateCodeGenState = state;
				}

				nativeCodeGenStates.TryAdd (arch, state);
			});

			// If we hit an error generating the Java code, we should bail out now
			if (!generateSucceeded)
				return;

			if (templateCodeGenState == null) {
				throw new InvalidOperationException ($"Internal error: no native code generator state defined");
			}
			JCWGenerator.EnsureAllArchitecturesAreIdentical (Log, nativeCodeGenStates);

			if (useMarshalMethods) {
				// We need to parse the environment files supplied by the user to see if they want to use broken exception transitions. This information is needed
				// in order to properly generate wrapper methods in the marshal methods assembly rewriter.
				// We don't care about those generated by us, since they won't contain the `XA_BROKEN_EXCEPTION_TRANSITIONS` variable we look for.
				var environmentParser = new EnvironmentFilesParser ();
				bool brokenExceptionTransitionsEnabled = environmentParser.AreBrokenExceptionTransitionsEnabled (Environments);

				foreach (var kvp in nativeCodeGenStates) {
					NativeCodeGenState state = kvp.Value;
					RewriteMarshalMethods (state, brokenExceptionTransitionsEnabled);
					state.Classifier.AddSpecialCaseMethods ();

					Log.LogDebugMessage ($"[{state.TargetArch}] Number of generated marshal methods: {state.Classifier.MarshalMethods.Count}");
					if (state.Classifier.RejectedMethodCount > 0) {
						Log.LogWarning ($"[{state.TargetArch}] Number of methods in the project that will be registered dynamically: {state.Classifier.RejectedMethodCount}");
					}

					if (state.Classifier.WrappedMethodCount > 0) {
						// TODO: change to LogWarning once the generator can output code which requires no non-blittable wrappers
						Log.LogDebugMessage ($"[{state.TargetArch}] Number of methods in the project that need marshal method wrappers: {state.Classifier.WrappedMethodCount}");
					}
				}
			}

			bool typemapsAreAbiAgnostic = Debug && !GenerateNativeAssembly;
			bool first = true;
			foreach (var kvp in nativeCodeGenStates) {
				if (!first && typemapsAreAbiAgnostic) {
					Log.LogDebugMessage ("Typemaps: it's a debug build and type maps are ABI-agnostic, not processing more ABIs");
					break;
				}

				NativeCodeGenState state = kvp.Value;
				first = false;
				WriteTypeMappings (state);
			}

			// Set for use by <GeneratePackageManagerJava/> task later
			NativeCodeGenState.TemplateJniAddNativeMethodRegistrationAttributePresent = templateCodeGenState.JniAddNativeMethodRegistrationAttributePresent;

			// Save NativeCodeGenState for later tasks
			Log.LogDebugMessage ($"Saving {nameof (NativeCodeGenState)} to {nameof (NativeCodeGenStateRegisterTaskKey)}");
			BuildEngine4.RegisterTaskObjectAssemblyLocal (MonoAndroidHelper.GetProjectBuildSpecificTaskObjectKey (NativeCodeGenStateRegisterTaskKey, WorkingDirectory, IntermediateOutputDirectory), nativeCodeGenStates, RegisteredTaskObjectLifetime.Build);
		}

		internal static Dictionary<string, ITaskItem> MaybeGetArchAssemblies (Dictionary<AndroidTargetArch, Dictionary<string, ITaskItem>> dict, AndroidTargetArch arch)
		{
			if (androidRuntime != Xamarin.Android.Tasks.AndroidRuntime.MonoVM &&
			    androidRuntime != Xamarin.Android.Tasks.AndroidRuntime.CoreCLR) {
				Log.LogDebugMessage ($"Skipping MonoRuntimeProvider generation for: {androidRuntime}");
				return;
			}

			// For NativeAOT, generate JavaInteropRuntime.java
			if (androidRuntime == Xamarin.Android.Tasks.AndroidRuntime.NativeAOT) {
				const string fileName = "JavaInteropRuntime.java";
				string template = GetResource (fileName);
				var contents = template.Replace ("@MAIN_ASSEMBLY_NAME@", TargetName);
				var path = Path.Combine (OutputDirectory, "src", "net", "dot", "jni", "nativeaot", fileName);
				Log.LogDebugMessage ($"Writing: {path}");
				Files.CopyIfStringChanged (contents, path);
			}

			// Create additional application java sources.
			StringWriter regCallsWriter = new StringWriter ();
			regCallsWriter.WriteLine ("// Application and Instrumentation ACWs must be registered first.");
			foreach (TypeDefinition type in codeGenState.JavaTypesForJCW) {
				if (JavaNativeTypeManager.IsApplication (type, codeGenState.TypeCache) || JavaNativeTypeManager.IsInstrumentation (type, codeGenState.TypeCache)) {
					if (codeGenState.Classifier != null && !codeGenState.Classifier.FoundDynamicallyRegisteredMethods (type)) {
						continue;
					}

					string javaKey = JavaNativeTypeManager.ToJniName (type, codeGenState.TypeCache).Replace ('/', '.');
					regCallsWriter.WriteLine (
						codeGenerationTarget == JavaPeerStyle.XAJavaInterop1 ?
							"\t\tmono.android.Runtime.register (\"{0}\", {1}.class, {1}.__md_methods);" :
							"\t\tnet.dot.jni.ManagedPeer.registerNativeMembers ({1}.class, {1}.__md_methods);",
						type.GetAssemblyQualifiedName (codeGenState.TypeCache),
						javaKey
					);
				}
			}
			regCallsWriter.Close ();

			var real_app_dir = Path.Combine (OutputDirectory, "src", "net", "dot", "android");
			string applicationTemplateFile = "ApplicationRegistration.java";
			SaveResource (
				applicationTemplateFile,
				applicationTemplateFile,
				real_app_dir,
				template => template.Replace ("// REGISTER_APPLICATION_AND_INSTRUMENTATION_CLASSES_HERE", regCallsWriter.ToString ())
			);
		}

		IList<string> MergeManifest (NativeCodeGenState codeGenState, Dictionary<string, ITaskItem> userAssemblies)
		{
			var manifest = new ManifestDocument (ManifestTemplate) {
				PackageName = PackageName,
				VersionName = VersionName,
				ApplicationLabel = ApplicationLabel ?? PackageName,
				Placeholders = ManifestPlaceholders,
				Resolver = codeGenState.Resolver,
				SdkDir = AndroidSdkDir,
				TargetSdkVersion = AndroidSdkPlatform,
				MinSdkVersion = MonoAndroidHelper.ConvertSupportedOSPlatformVersionToApiLevel (SupportedOSPlatformVersion).ToString (),
				Debug = Debug,
				MultiDex = MultiDex,
				NeedsInternet = NeedsInternet,
				AndroidRuntime = AndroidRuntime,
			};
			// Only set manifest.VersionCode if there is no existing value in AndroidManifest.xml.
			if (manifest.HasVersionCode) {
				Log.LogDebugMessage ($"Using existing versionCode in: {ManifestTemplate}");
			} else if (!string.IsNullOrEmpty (VersionCode)) {
				manifest.VersionCode = VersionCode;
			}
			manifest.Assemblies.AddRange (userAssemblies.Values.Select (item => item.ItemSpec));

			if (!String.IsNullOrWhiteSpace (CheckedBuild)) {
				// We don't validate CheckedBuild value here, this will be done in BuildApk. We just know that if it's
				// on then we need android:debuggable=true and android:extractNativeLibs=true
				manifest.ForceDebuggable = true;
				manifest.ForceExtractNativeLibs = true;
			}

			IList<string> additionalProviders = manifest.Merge (Log, codeGenState.TypeCache, codeGenState.AllJavaTypes, ApplicationJavaClass, EmbedAssemblies, BundledWearApplicationName, MergedManifestDocuments);

			// Only write the new manifest if it actually changed
			if (manifest.SaveIfChanged (Log, MergedAndroidManifestOutput)) {
				Log.LogDebugMessage ($"Saving: {MergedAndroidManifestOutput}");
			}

			return additionalProviders;
		}

		(bool success, NativeCodeGenState? stubsState) GenerateJavaSourcesAndMaybeClassifyMarshalMethods (AndroidTargetArch arch, Dictionary<string, ITaskItem> assemblies, Dictionary<string, ITaskItem> userAssemblies, bool useMarshalMethods, bool generateJavaCode)
		{
			XAAssemblyResolver resolver = MakeResolver (useMarshalMethods, arch, assemblies);
			var tdCache = new TypeDefinitionCache ();
			(List<TypeDefinition> allJavaTypes, List<TypeDefinition> javaTypesForJCW) = ScanForJavaTypes (resolver, tdCache, assemblies, userAssemblies, useMarshalMethods);
			var jcwContext = new JCWGeneratorContext (arch, resolver, assemblies.Values, javaTypesForJCW, tdCache, useMarshalMethods);
			var jcwGenerator = new JCWGenerator (Log, jcwContext) {
				CodeGenerationTarget = string.Equals (AndroidRuntime, "MonoVM", StringComparison.OrdinalIgnoreCase) ? JavaPeerStyle.XAJavaInterop1 : JavaPeerStyle.JavaInterop1
			};
			bool success;

			if (generateJavaCode) {
				success = jcwGenerator.GenerateAndClassify (AndroidSdkPlatform, outputPath: Path.Combine (OutputDirectory, "src"), ApplicationJavaClass);
			} else {
				success = jcwGenerator.Classify (AndroidSdkPlatform);
			}

			if (!success) {
				return (false, null);
			}

			return (true, new NativeCodeGenState (arch, tdCache, resolver, allJavaTypes, javaTypesForJCW, jcwGenerator.Classifier));
		}

		(List<TypeDefinition> allJavaTypes, List<TypeDefinition> javaTypesForJCW) ScanForJavaTypes (XAAssemblyResolver res, TypeDefinitionCache cache, Dictionary<string, ITaskItem> assemblies, Dictionary<string, ITaskItem> userAssemblies, bool useMarshalMethods)
		{
			var scanner = new XAJavaTypeScanner (res.TargetArch, Log, cache) {
				ErrorOnCustomJavaObject     = ErrorOnCustomJavaObject,
			};
			List<TypeDefinition> allJavaTypes = scanner.GetJavaTypes (assemblies.Values, res);
			var javaTypesForJCW = new List<TypeDefinition> ();

			// When marshal methods or non-JavaPeerStyle.XAJavaInterop1 are in use we do not want to skip non-user assemblies (such as Mono.Android) - we need to generate JCWs for them during
			// application build, unlike in Debug configuration or when marshal methods are disabled, in which case we use JCWs generated during Xamarin.Android
			// build and stored in a jar file.
			bool shouldSkipNonUserAssemblies = !useMarshalMethods && codeGenerationTarget == JavaPeerStyle.XAJavaInterop1;
			foreach (TypeDefinition type in allJavaTypes) {
				if ((shouldSkipNonUserAssemblies && !userAssemblies.ContainsKey (type.Module.Assembly.Name.Name)) || JavaTypeScanner.ShouldSkipJavaCallableWrapperGeneration (type, cache)) {
					continue;
				}
				javaTypesForJCW.Add (type);
			}

			return (allJavaTypes, javaTypesForJCW);
		}

		void RewriteMarshalMethods (NativeCodeGenState state, bool brokenExceptionTransitionsEnabled)
		{
			if (state.Classifier == null) {
				return;
			}

			var rewriter = new MarshalMethodsAssemblyRewriter (Log, state.TargetArch, state.Classifier, state.Resolver);
			rewriter.Rewrite (brokenExceptionTransitionsEnabled);
		}

		void WriteTypeMappings (NativeCodeGenState state)
		{
			if (androidRuntime == Xamarin.Android.Tasks.AndroidRuntime.NativeAOT) {
				// NativeAOT typemaps are generated in `Microsoft.Android.Sdk.ILLink.TypeMappingStep`
				return;
			}
			if (androidRuntime == Xamarin.Android.Tasks.AndroidRuntime.CoreCLR) {
				// TODO: CoreCLR typemaps will be emitted later
				return;
			}
			Log.LogDebugMessage ($"Generating type maps for architecture '{state.TargetArch}'");
			var tmg = new TypeMapGenerator (Log, state, androidRuntime);
			if (!tmg.Generate (Debug, SkipJniAddNativeMethodRegistrationAttributeScan, TypemapOutputDirectory, GenerateNativeAssembly)) {
				throw new XamarinAndroidException (4308, Properties.Resources.XA4308);
			}

			string abi = MonoAndroidHelper.ArchToAbi (state.TargetArch);
			var items = new List<ITaskItem> ();
			foreach (string file in tmg.GeneratedBinaryTypeMaps) {
				var item = new TaskItem (file);
				string fileName = Path.GetFileName (file);
				item.SetMetadata ("DestinationSubPath", $"{abi}/{fileName}");
				item.SetMetadata ("DestinationSubDirectory", $"{abi}/");
				item.SetMetadata ("Abi", abi);
				items.Add (item);
			}

			GeneratedBinaryTypeMaps = items.ToArray ();
		}
	}
}
