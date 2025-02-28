// Copyright (C) 2011 Xamarin, Inc. All rights reserved.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Java.Interop.Tools.Cecil;
using Java.Interop.Tools.Diagnostics;
using Java.Interop.Tools.JavaCallableWrappers;
using Java.Interop.Tools.JavaCallableWrappers.Adapters;
using Java.Interop.Tools.JavaCallableWrappers.CallableWrapperMembers;
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

		public string AndroidLinkMode { get; set; }

		public bool PublishTrimmed { get; set; }

		public bool ShouldUseNewJCWGenerator { get; set; }

		JavaPeerStyle codeGenerationTarget;

		[Output]
		public ITaskItem [] GeneratedJavaFilesOutput { get; set; }

		public List<ITaskItem> ScannedJLOAssemblies { get; set; } = [];

		internal const string AndroidSkipJavaStubGeneration = "AndroidSkipJavaStubGeneration";

		public bool ShouldGenerateNewJavaCallableWrappers => false;// !PublishTrimmed && string.Compare (AndroidLinkMode, "None", true) == 0 && !(!Debug && EnableMarshalMethods);

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

			var firstArch = allAssembliesPerArch.First ().Key;
			var generateSucceeded = true;

			//if (ShouldGenerateNewJavaCallableWrappers) {
				var sw = Stopwatch.StartNew ();
				//GenerateJavaCallableWrappers (allAssembliesPerArch.First ().Value.Values.ToList ());
				GenerateJavaCallableWrappers (ResolvedAssemblies.ToList ());
				Log.LogDebugMessage ($"Generated NEW Java callable wrappers in: '{sw.ElapsedMilliseconds}ms'");
			//}

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

			CompareScannedAssemblies ();

			// If we hit an error generating the Java code, we should bail out now
			if (!generateSucceeded)
				return;

			if (templateCodeGenState == null) {
				throw new InvalidOperationException ($"Internal error: no native code generator state defined");
			}
			JCWGenerator.EnsureAllArchitecturesAreIdentical (Log, nativeCodeGenStates);

			// Save NativeCodeGenState for later tasks
			Log.LogDebugMessage ($"Saving {nameof (NativeCodeGenState)} to {nameof (NativeCodeGenStateRegisterTaskKey)}");
			BuildEngine4.RegisterTaskObjectAssemblyLocal (MonoAndroidHelper.GetProjectBuildSpecificTaskObjectKey (NativeCodeGenStateRegisterTaskKey, WorkingDirectory, IntermediateOutputDirectory), nativeCodeGenStates, RegisteredTaskObjectLifetime.Build);
		}

		public List<string> GeneratedJavaFiles { get; } = new List<string> ();

		bool GenerateJavaCallableWrappers (List<ITaskItem> assemblies)
		{
			Directory.CreateDirectory (Path.Combine (OutputDirectory, "src"));

			var sw = Stopwatch.StartNew ();
			// Deserialize JavaCallableWrappers
			var wrappers = new List<CallableWrapperType> ();

			foreach (var assembly in assemblies) {
				var assemblyPath = assembly.ItemSpec;
				var assemblyName = Path.GetFileNameWithoutExtension (assemblyPath);
				var wrappersPath = Path.Combine (Path.GetDirectoryName (assemblyPath), $"{assemblyName}.jlo.xml");

				if (!File.Exists (wrappersPath)) {
					Log.LogError ($"'{wrappersPath}' not found.");
					return false;
				}

				wrappers.AddRange (XmlImporter.Import (wrappersPath, out var wasScanned));

				if (wasScanned) {
					Log.LogDebugMessage ($"Adding scanned assembly '{assemblyPath}' for Java callable wrappers");
					ScannedJLOAssemblies.Add (assembly);
				}
			}
			Log.LogDebugMessage ($"Deserialized Java callable wrappers in: '{sw.ElapsedMilliseconds}ms'");

			return true;

			sw.Restart ();
			foreach (var generator in wrappers) {
				using var writer = MemoryStreamPool.Shared.CreateStreamWriter ();

				var writer_options = new CallableWrapperWriterOptions {
					CodeGenerationTarget = codeGenerationTarget
				};

				generator.Generate (writer, writer_options);
				writer.Flush ();


				var path = generator.GetDestinationPath (Path.Combine (OutputDirectory, "src"));
				var changed = Files.CopyIfStreamChanged (writer.BaseStream, path);
				Log.LogDebugMessage ($"*NEW* Generated Java callable wrapper code: '{path}' (changed: {changed})");

				//if (changed)
				//	Log.LogError ($"Java callable wrapper code changed: '{path}'");

				GeneratedJavaFiles.Add (path);
			}
			Log.LogDebugMessage ($"Wrote Java callable wrappers in: '{sw.ElapsedMilliseconds}ms'");

			return true;
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
			var jcwContext = new JCWGeneratorContext (arch, resolver, assemblies.Values, javaTypesForJCW, tdCache, useMarshalMethods);
			var jcwGenerator = new JCWGenerator (Log, jcwContext) {
				CodeGenerationTarget = codeGenerationTarget,
			};
			bool success;

			if (generateJavaCode) {
				success = jcwGenerator.GenerateAndClassify (AndroidSdkPlatform, outputPath: Path.Combine (OutputDirectory, "src"), ApplicationJavaClass);

				GeneratedJavaFilesOutput = jcwGenerator.GeneratedJavaFiles.Select (f => new TaskItem (f)).ToArray ();
				//if (ShouldGenerateNewJavaCallableWrappers) {
				//	var new_generated_java_files = GeneratedJavaFiles.Select (f => f.Replace ("src2", "src")).ToList ();
				//	var old_generated_java_files = jcwGenerator.GeneratedJavaFiles;

				//	var extra_new_files = new_generated_java_files.Except (old_generated_java_files).ToList ();

				//	if (extra_new_files.Count > 0)
				//		Log.LogWarning ($"The following Java files were generated but not previously generated: {string.Join (", ", extra_new_files)}");

				//	var missing_old_files = old_generated_java_files.Except (new_generated_java_files).ToList ();

				//	if (missing_old_files.Count > 0)
				//		Log.LogWarning ($"The following Java files were previously generated but not generated this time: {string.Join (", ", missing_old_files)}");

				//	if (extra_new_files.Count > 0 || missing_old_files.Count > 0) {
				//		Log.LogError ($"New JCW gen ({new_generated_java_files.Count}) mismatch with old JCW gen ({old_generated_java_files.Count})");
				//		return (false, null);
				//	}
				//}
			} else {
				success = jcwGenerator.Classify (AndroidSdkPlatform);
			}

			if (!success) {
				return (false, null);
			}

			return (true, new NativeCodeGenState (arch, tdCache, resolver, allJavaTypes, javaTypesForJCW, jcwGenerator.Classifier));
		}

		ConcurrentDictionary<string, ITaskItem> scannedAssemblies = new ();

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

		void CompareScannedAssemblies ()
		{
			var old_scanned_assemblies = scannedAssemblies.Values.Select (a => a.ItemSpec).ToList ();
			var new_scanned_assemblies = ScannedJLOAssemblies.Select (a => a.ItemSpec).ToList ();

			if (old_scanned_assemblies.Count != new_scanned_assemblies.Count)
				Log.LogError ($"Number of assemblies scanned for Java types changed from {old_scanned_assemblies.Count} to {new_scanned_assemblies.Count}");

			// Log warnings for assemblies that were scanned but not found in the new set
			var missingAssemblies = old_scanned_assemblies.Except (new_scanned_assemblies).ToList ();
			if (missingAssemblies.Count > 0) {
				Log.LogDebugMessage ($"The following assemblies are missing from the new set:");

				foreach (var assembly in missingAssemblies) {
					Log.LogDebugMessage ($"  {assembly}");
				}
			}

			// Log warnings for assemblies that were found in the new set but not in the old set
			var extraAssemblies = new_scanned_assemblies.Except (old_scanned_assemblies).ToList ();

			if (extraAssemblies.Count > 0) {
				Log.LogDebugMessage ($"The following assemblies were found in the new set but not in the old set:");
				foreach (var assembly in extraAssemblies) {
					Log.LogDebugMessage ($"  {assembly}");
				}
			}
		}
	}
}
