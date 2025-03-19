// Copyright (C) 2011 Xamarin, Inc. All rights reserved.

#nullable disable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Java.Interop.Tools.Cecil;
using Java.Interop.Tools.Diagnostics;
using Java.Interop.Tools.JavaCallableWrappers;
using Java.Interop.Tools.JavaCallableWrappers.Adapters;
using Java.Interop.Tools.TypeNameMappings;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Mono.Cecil;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	using PackageNamingPolicyEnum   = PackageNamingPolicy;

	public class GenerateJavaStubs : AndroidTask
	{
		public const string NativeCodeGenStateRegisterTaskKey = ".:!MarshalMethods!:.";
		public const string NativeCodeGenStateObjectRegisterTaskKey = ".:!MarshalMethodsObject!:.";

		public override string TaskPrefix => "GJS";

		[Required]
		public ITaskItem[] ResolvedAssemblies { get; set; }

		[Required]
		public ITaskItem[] ResolvedUserAssemblies { get; set; }

		[Required]
		public ITaskItem [] FrameworkDirectories { get; set; }

		[Required]
		public string [] SupportedAbis { get; set; }

		public string IntermediateOutputDirectory { get; set; }
		public bool EnableMarshalMethods { get; set; }

		public bool Debug { get; set; }

		public string AndroidSdkPlatform { get; set; }
		public string OutputDirectory { get; set; }

		public bool ErrorOnCustomJavaObject { get; set; }

		public string PackageNamingPolicy { get; set; }

		public string ApplicationJavaClass { get; set; }

		public string CodeGenerationTarget { get; set; } = "";

		public bool EnableNativeRuntimeLinking { get; set; }

		// These two properties are temporary and are used to ensure we still generate the
		// same files as before using the new _LinkAssembliesNoShrink JLO scanning. They will be removed in the future.
		public bool RunCheckedBuild { get; set; }

		public ITaskItem [] GeneratedJavaFiles { get; set; } = [];

		JavaPeerStyle codeGenerationTarget;

		//[Output]
		//public ITaskItem [] GeneratedJavaFilesOutput { get; set; }

		internal const string AndroidSkipJavaStubGeneration = "AndroidSkipJavaStubGeneration";

		public override bool RunTask ()
		{
			try {
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
			PinvokeScanner? pinvokeScanner = EnableNativeRuntimeLinking ? new PinvokeScanner (Log) : null;

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

				if (pinvokeScanner != null && state != null) {
					(success, List<PinvokeScanner.PinvokeEntryInfo> pinfos) = ScanForUsedPinvokes (pinvokeScanner, arch, state.Resolver);
					if (!success) {
						return;
					}
					state.PinvokeInfos = pinfos;
					Log.LogDebugMessage ($"Number of unique p/invokes for architecture '{arch}': {pinfos.Count}");
				}
			});

			// If we hit an error generating the Java code, we should bail out now
			if (!generateSucceeded)
				return;

			if (templateCodeGenState == null) {
				throw new InvalidOperationException ($"Internal error: no native code generator state defined");
			}
			JCWGenerator.EnsureAllArchitecturesAreIdentical (Log, nativeCodeGenStates);

			if (RunCheckedBuild)
				CompareScannedAssemblies ();

			// Save NativeCodeGenState for later tasks
			Log.LogDebugMessage ($"Saving {nameof (NativeCodeGenState)} to {nameof (NativeCodeGenStateRegisterTaskKey)}");
			BuildEngine4.RegisterTaskObjectAssemblyLocal (MonoAndroidHelper.GetProjectBuildSpecificTaskObjectKey (NativeCodeGenStateRegisterTaskKey, WorkingDirectory, IntermediateOutputDirectory), nativeCodeGenStates, RegisteredTaskObjectLifetime.Build);
		}

		(bool success, List<PinvokeScanner.PinvokeEntryInfo>? pinfos) ScanForUsedPinvokes (PinvokeScanner scanner, AndroidTargetArch arch, XAAssemblyResolver resolver)
		{
			if (!EnableNativeRuntimeLinking) {
				return (true, null);
			}

			var frameworkAssemblies = new List<ITaskItem> ();

			foreach (ITaskItem asm in ResolvedAssemblies) {
				string? metadata = asm.GetMetadata ("FrameworkAssembly");
				if (String.IsNullOrEmpty (metadata)) {
					continue;
				}

				if (!Boolean.TryParse (metadata, out bool isFrameworkAssembly) || !isFrameworkAssembly) {
					continue;
				}

				frameworkAssemblies.Add (asm);
			}

			var pinfos = scanner.Scan (arch, resolver, frameworkAssemblies);
			return (true, pinfos);
		}

		internal static Dictionary<string, ITaskItem> MaybeGetArchAssemblies (Dictionary<AndroidTargetArch, Dictionary<string, ITaskItem>> dict, AndroidTargetArch arch)
		{
			if (!dict.TryGetValue (arch, out Dictionary<string, ITaskItem> archDict)) {
				return new Dictionary<string, ITaskItem> (StringComparer.OrdinalIgnoreCase);
			}

			return archDict;
		}

		(bool success, NativeCodeGenState? stubsState) GenerateJavaSourcesAndMaybeClassifyMarshalMethods (AndroidTargetArch arch, Dictionary<string, ITaskItem> assemblies, Dictionary<string, ITaskItem> userAssemblies, bool useMarshalMethods, bool generateJavaCode)
		{
			XAAssemblyResolver resolver = MakeResolver (useMarshalMethods, arch, assemblies);
			var tdCache = new TypeDefinitionCache ();
			(List<TypeDefinition> allJavaTypes, List<TypeDefinition> javaTypesForJCW) = ScanForJavaTypes (resolver, tdCache, assemblies, userAssemblies, useMarshalMethods);
			var jcwContext = new JCWGeneratorContext (arch, resolver, assemblies.Values, javaTypesForJCW, tdCache);
			var jcwGenerator = new JCWGenerator (Log, jcwContext) {
				CodeGenerationTarget = codeGenerationTarget,
			};
			bool success = true;

			if (generateJavaCode && RunCheckedBuild) {
				success = jcwGenerator.Generate (AndroidSdkPlatform, outputPath: Path.Combine (OutputDirectory, "src"), ApplicationJavaClass);

				generatedJavaFiles = jcwGenerator.GeneratedJavaFiles;
			}

			if (!success) {
				return (false, null);
			}

			MarshalMethodsCollection? marshalMethodsCollection = null;

			if (useMarshalMethods)
				marshalMethodsCollection = MarshalMethodsCollection.FromAssemblies (arch, assemblies.Values.ToList (), resolver, Log);

			return (true, new NativeCodeGenState (arch, tdCache, resolver, allJavaTypes, javaTypesForJCW, marshalMethodsCollection));
		}

		(List<TypeDefinition> allJavaTypes, List<TypeDefinition> javaTypesForJCW) ScanForJavaTypes (XAAssemblyResolver res, TypeDefinitionCache cache, Dictionary<string, ITaskItem> assemblies, Dictionary<string, ITaskItem> userAssemblies, bool useMarshalMethods)
		{
			var scanner = new XAJavaTypeScanner (res.TargetArch, Log, cache) {
				ErrorOnCustomJavaObject     = ErrorOnCustomJavaObject,
			};
			List<TypeDefinition> allJavaTypes = scanner.GetJavaTypes (assemblies.Values, res, scannedAssemblies);

			var javaTypesForJCW = new List<TypeDefinition> ();

			// When marshal methods or non-JavaPeerStyle.XAJavaInterop1 are in use we do not want to skip non-user assemblies (such as Mono.Android) - we need to generate JCWs for them during
			// application build, unlike in Debug configuration or when marshal methods are disabled, in which case we use JCWs generated during Xamarin.Android
			// build and stored in a jar file.
			bool shouldSkipNonUserAssemblies = !useMarshalMethods && codeGenerationTarget == JavaPeerStyle.XAJavaInterop1;
			Log.LogDebugMessage ($"Should skip non-user assemblies: {shouldSkipNonUserAssemblies} (useMarshalMethods: {useMarshalMethods})");

			if (shouldSkipNonUserAssemblies) {
				foreach (var item in scannedAssemblies.Values.ToArray ())
					if (!ResolvedUserAssemblies.Any (a => a.ItemSpec == item.ItemSpec)) {
						var rm = scannedAssemblies.TryRemove (item.ItemSpec, out var _);
						Log.LogDebugMessage ($"Removing assembly '{item.ItemSpec}' from scanned assemblies ({rm})");
					}
			}

			foreach (TypeDefinition type in allJavaTypes) {
				if ((shouldSkipNonUserAssemblies && !userAssemblies.ContainsKey (type.Module.Assembly.Name.Name)) || JavaTypeScanner.ShouldSkipJavaCallableWrapperGeneration (type, cache)) {
					continue;
				}
				javaTypesForJCW.Add (type);
			}

			return (allJavaTypes, javaTypesForJCW);
		}

		ConcurrentDictionary<string, ITaskItem> scannedAssemblies = new ();
		List<string> generatedJavaFiles = new ();

		void CompareScannedAssemblies ()
		{
			// First we want to ensure that we scanned the same set of assemblies, as it can
			// be tricky to ensure we found all the assemblies we need to scan
			var linker_scanned_assemblies = new List<string> ();

			// Find every assembly that was scanned by the linker by looking at the .jlo.xml files
			foreach (var assembly in ResolvedAssemblies) {
				var assemblyPath = assembly.ItemSpec;
				var wrappersPath = JavaObjectsXmlFile.GetJavaObjectsXmlFilePath (assembly.ItemSpec);

				if (!File.Exists (wrappersPath))
					Log.LogError ($"'{wrappersPath}' not found.");

				var xml = JavaObjectsXmlFile.Import (wrappersPath, JavaObjectsXmlFileReadType.None);

				if (xml.WasScanned) {
					Log.LogDebugMessage ($"CompareScannedAssemblies: Found scanned assembly .jlo.xml '{assemblyPath}'");
					linker_scanned_assemblies.Add (assembly.ItemSpec);
				}
			}

			// These are the assemblies that were scanned by this task (GenerateJavaStubs)
			var legacy_scanned_assemblies = scannedAssemblies.Values.Select (a => a.ItemSpec).ToList ();

			CompareLists ("Scanned assemblies", legacy_scanned_assemblies, linker_scanned_assemblies);

			// Next we want to ensure that we generated the same .java files
			var linker_generated_files = GeneratedJavaFiles.Select (a => a.ItemSpec).ToList ();
			var legacy_generated_files = generatedJavaFiles;

			CompareLists ("Generated Java files", legacy_generated_files, linker_generated_files);
		}

		void CompareLists (string name, List<string> list1, List<string> list2)
		{
			var had_differences = false;

			Log.LogDebugMessage ($"Comparing {name} lists");

			if (list1.Count != list2.Count) {
				had_differences = true;
				Log.LogError ($"Number of assemblies scanned for Java types changed from {list1.Count} to {list2.Count}");
			}

			var missingAssemblies = list1.Except (list2).ToList ();

			if (missingAssemblies.Count > 0) {
				had_differences = true;
				Log.LogDebugMessage ($"The following assemblies are missing from the new set:");

				foreach (var assembly in missingAssemblies)
					Log.LogDebugMessage ($"  {assembly}");
			}

			var extraAssemblies = list2.Except (list1).ToList ();

			if (extraAssemblies.Count > 0) {
				had_differences = true;
				Log.LogDebugMessage ($"The following assemblies were found in the new set but not in the old set:");

				foreach (var assembly in extraAssemblies)
					Log.LogDebugMessage ($"  {assembly}");
			}

			if (!had_differences)
				Log.LogDebugMessage ($"No differences");
		}
	}
}
