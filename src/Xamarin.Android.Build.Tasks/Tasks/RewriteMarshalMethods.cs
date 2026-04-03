#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

/// <summary>
/// MSBuild task that classifies, rewrites, and generates LLVM IR for marshal methods in
/// the inner (per-RID) build.  Runs after ILLink and _PostTrimmingPipeline on the trimmed
/// assemblies, and before ReadyToRun/crossgen2 so that R2R images are built from the
/// rewritten assemblies.
///
/// The task performs the following steps:
///
/// 1. Opens the trimmed assemblies with Cecil and classifies marshal methods via
///    <see cref="MarshalMethodsCollection.FromAssemblies"/>
/// 2. Rewrites assemblies in-place: adds [UnmanagedCallersOnly] wrappers, removes
///    connector methods and callback delegate backing fields
/// 3. Generates the <c>marshal_methods.{abi}.ll</c> LLVM IR file into
///    <see cref="MarshalMethodsOutputDirectory"/> (the outer build's intermediate dir)
///
/// Because this runs in the inner build, the outer build sees already-rewritten assemblies
/// in <c>@(ResolvedFileToPublish)</c>.  Downstream consumers
/// (<c>_AfterILLinkAdditionalSteps</c>, <c>GenerateTypeMappings</c>) therefore work on
/// post-rewrite tokens, eliminating the token staleness problem.
/// </summary>
public class RewriteMarshalMethods : AndroidTask
{
	public override string TaskPrefix => "RMM";

	/// <summary>
	/// The trimmed assemblies to process (from <c>@(ResolvedFileToPublish)</c> filtered to .dll).
	/// </summary>
	[Required]
	public ITaskItem [] Assemblies { get; set; } = [];

	/// <summary>
	/// The Android runtime type (MonoVM or CoreCLR).  Determines which LLVM IR generator
	/// to use for the <c>marshal_methods.{abi}.ll</c> file.
	/// </summary>
	[Required]
	public string AndroidRuntime { get; set; } = "";

	/// <summary>
	/// Whether to enable managed marshal methods lookup tables.
	/// </summary>
	public bool EnableManagedMarshalMethodsLookup { get; set; }

	/// <summary>
	/// Whether marshal methods are enabled.  Should always be <c>true</c> when this task
	/// is invoked, but is kept as a property for clarity and consistency with the target
	/// condition.
	/// </summary>
	public bool EnableMarshalMethods { get; set; }

	/// <summary>
	/// Environment files to parse for configuration (e.g. XA_BROKEN_EXCEPTION_TRANSITIONS).
	/// </summary>
	public ITaskItem [] Environments { get; set; } = [];

	/// <summary>
	/// Directory where the <c>marshal_methods.{abi}.ll</c> file is written.
	/// Typically <c>$(_OuterIntermediateOutputPath)android</c> so the outer build can
	/// find it via <c>@(_MarshalMethodsAssemblySource)</c>.
	/// </summary>
	[Required]
	public string MarshalMethodsOutputDirectory { get; set; } = "";

	/// <summary>
	/// The RuntimeIdentifier for this inner build (e.g. <c>android-arm64</c>).
	/// Converted to an ABI and target architecture internally.
	/// </summary>
	[Required]
	public string RuntimeIdentifier { get; set; } = "";

	public override bool RunTask ()
	{
		if (!EnableMarshalMethods) {
			Log.LogDebugMessage ("Marshal methods are not enabled, skipping.");
			return true;
		}

		var androidRuntime = MonoAndroidHelper.ParseAndroidRuntime (AndroidRuntime);

		// Parse environment files for configuration (e.g. broken exception transitions)
		var environmentParser = new EnvironmentFilesParser ();
		bool brokenExceptionTransitionsEnabled = environmentParser.AreBrokenExceptionTransitionsEnabled (Environments);

		string abi = MonoAndroidHelper.RidToAbi (RuntimeIdentifier);
		var targetArch = MonoAndroidHelper.AbiToTargetArch (abi);
		ProcessArchitecture (targetArch, abi, androidRuntime, brokenExceptionTransitionsEnabled);

		return !Log.HasLoggedErrors;
	}

	void ProcessArchitecture (AndroidTargetArch targetArch, string abi, AndroidRuntime androidRuntime, bool brokenExceptionTransitionsEnabled)
	{
		// Step 1: Open assemblies with Cecil and classify marshal methods
		// Build the dictionary keyed by assembly name that MakeResolver and FromAssemblies expect
		var assemblyDict = new Dictionary<string, ITaskItem> (StringComparer.OrdinalIgnoreCase);
		foreach (var item in Assemblies) {
			var name = Path.GetFileNameWithoutExtension (item.ItemSpec);
			assemblyDict [name] = item;
		}
		var assemblyItems = assemblyDict.Values.ToList ();

		XAAssemblyResolver resolver = MonoAndroidHelper.MakeResolver (Log, useMarshalMethods: true, targetArch, assemblyDict);

		MarshalMethodsCollection classifier;
		try {
			classifier = MarshalMethodsCollection.FromAssemblies (targetArch, assemblyItems, resolver, Log);
		} catch (Exception ex) {
			Log.LogError ($"[{targetArch}] Failed to classify marshal methods: {ex.Message}");
			Log.LogDebugMessage (ex.ToString ());
			return;
		}

		// Step 2: Rewrite assemblies
		if (!EnableManagedMarshalMethodsLookup) {
			RewriteAssemblies (targetArch, classifier, resolver, brokenExceptionTransitionsEnabled);
			classifier.AddSpecialCaseMethods ();
		} else {
			// When managed lookup is enabled, add special cases first so they
			// appear in the lookup tables
			classifier.AddSpecialCaseMethods ();
			var lookupInfo = new ManagedMarshalMethodsLookupInfo (Log);
			RewriteAssemblies (targetArch, classifier, resolver, brokenExceptionTransitionsEnabled, lookupInfo);
		}

		ReportStatistics (targetArch, classifier);

		// Step 3: Build NativeCodeGenStateObject from Cecil state and generate .ll
		var codeGenState = MarshalMethodCecilAdapter.CreateNativeCodeGenStateObjectFromClassifier (targetArch, classifier);
		GenerateLlvmIr (targetArch, abi, androidRuntime, codeGenState);

		// Step 4: Dispose Cecil resolvers
		resolver.Dispose ();
	}

	void RewriteAssemblies (AndroidTargetArch targetArch, MarshalMethodsCollection classifier, XAAssemblyResolver resolver, bool brokenExceptionTransitionsEnabled, ManagedMarshalMethodsLookupInfo? lookupInfo = null)
	{
		var rewriter = new MarshalMethodsAssemblyRewriter (Log, targetArch, classifier, resolver, lookupInfo);
		rewriter.Rewrite (brokenExceptionTransitionsEnabled);
	}

	void ReportStatistics (AndroidTargetArch targetArch, MarshalMethodsCollection classifier)
	{
		Log.LogDebugMessage ($"[{targetArch}] Number of generated marshal methods: {classifier.MarshalMethods.Count}");

		if (classifier.DynamicallyRegisteredMarshalMethods.Count > 0) {
			Log.LogWarning ($"[{targetArch}] Number of methods in the project that will be registered dynamically: {classifier.DynamicallyRegisteredMarshalMethods.Count}");
		}

		var wrappedCount = classifier.MarshalMethods.Sum (m => m.Value.Count (m2 => m2.NeedsBlittableWorkaround));
		if (wrappedCount > 0) {
			// TODO: change to LogWarning once the generator can output code which requires no non-blittable wrappers
			Log.LogDebugMessage ($"[{targetArch}] Number of methods in the project that need marshal method wrappers: {wrappedCount}");
		}
	}

	void GenerateLlvmIr (AndroidTargetArch targetArch, string abi, AndroidRuntime androidRuntime, NativeCodeGenStateObject codeGenState)
	{
		var targetAbi = abi.ToLowerInvariant ();
		var llFilePath = Path.Combine (MarshalMethodsOutputDirectory, $"marshal_methods.{targetAbi}.ll");
		var (assemblyCount, uniqueAssemblyNames) = GetAssemblyCountAndUniqueNames ();

		MarshalMethodsNativeAssemblyGenerator generator = androidRuntime switch {
			Tasks.AndroidRuntime.MonoVM => new MarshalMethodsNativeAssemblyGeneratorMonoVM (
				Log,
				assemblyCount,
				uniqueAssemblyNames,
				codeGenState,
				EnableManagedMarshalMethodsLookup
			),
			Tasks.AndroidRuntime.CoreCLR => new MarshalMethodsNativeAssemblyGeneratorCoreCLR (
				Log,
				uniqueAssemblyNames,
				codeGenState,
				EnableManagedMarshalMethodsLookup
			),
			_ => throw new NotSupportedException ($"Internal error: unsupported runtime type '{androidRuntime}'")
		};

		Directory.CreateDirectory (MarshalMethodsOutputDirectory);

		var module = generator.Construct ();
		using var writer = MemoryStreamPool.Shared.CreateStreamWriter ();
		bool fileFullyWritten = false;

		try {
			generator.Generate (module, targetArch, writer, llFilePath);
			writer.Flush ();
			Files.CopyIfStreamChanged (writer.BaseStream, llFilePath);
			fileFullyWritten = true;
			Log.LogDebugMessage ($"[{targetArch}] Generated marshal methods LLVM IR: {llFilePath}");
		} finally {
			if (!fileFullyWritten) {
				MonoAndroidHelper.LogTextStreamContents (Log, $"Partial contents of file '{llFilePath}'", writer.BaseStream);
			}
		}
	}

	(int assemblyCount, HashSet<string> uniqueAssemblyNames) GetAssemblyCountAndUniqueNames ()
	{
		var assemblyCount = 0;
		var uniqueAssemblyNames = new HashSet<string> (StringComparer.OrdinalIgnoreCase);

		foreach (var assembly in Assemblies) {
			var culture = MonoAndroidHelper.GetAssemblyCulture (assembly);
			var fileName = Path.GetFileName (assembly.ItemSpec);
			string assemblyName;

			if (culture.IsNullOrEmpty ()) {
				assemblyName = fileName;
			} else {
				assemblyName = $"{culture}/{fileName}";
			}

			if (uniqueAssemblyNames.Add (assemblyName)) {
				assemblyCount++;
			}
		}

		return (assemblyCount, uniqueAssemblyNames);
	}
}
