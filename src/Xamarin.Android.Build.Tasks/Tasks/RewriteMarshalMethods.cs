#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

/// <summary>
/// MSBuild task that runs in the inner (per-RID) build to generate the
/// <c>marshal_methods.{abi}.ll</c> LLVM IR file.
///
/// When marshal methods are enabled (Release + MonoVM), the task classifies
/// marshal methods, rewrites assemblies (adds [UnmanagedCallersOnly] wrappers,
/// removes connectors), and generates a full .ll with native marshal method
/// functions.  Rewritten assemblies are written to a separate per-RID output
/// directory (<see cref="RewrittenAssembliesOutputDirectory"/>), never in-place,
/// so that parallel inner builds don't conflict even when the input assemblies
/// point to a shared location (e.g. the NuGet runtime pack when PublishTrimmed
/// is false).  The output items (<see cref="RewrittenAssemblies"/>) allow the
/// target to update <c>@(ResolvedFileToPublish)</c> so downstream processing
/// uses the rewritten copies.
///
/// When marshal methods are disabled (Debug, or Release without marshal
/// methods), the task generates an empty/minimal .ll containing only the
/// structural scaffolding the native runtime always links against.
///
/// Runs AfterTargets="_PostTrimmingPipeline" (which is AfterTargets="ILLink")
/// so that trimmed assemblies are available for rewriting when trimming is
/// active.  MSBuild fires AfterTargets hooks even when the referenced target
/// is condition-skipped, so this target also runs in untrimmed builds.
/// </summary>
public class RewriteMarshalMethods : AndroidTask
{
	public override string TaskPrefix => "RMM";

	/// <summary>
	/// The assemblies to process (from <c>@(ResolvedFileToPublish)</c> filtered to .dll).
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
	/// Whether marshal methods are enabled.  When false, the task skips classification
	/// and rewriting and generates an empty/minimal .ll file.
	/// </summary>
	public bool EnableMarshalMethods { get; set; }

	/// <summary>
	/// Environment files to parse for configuration (e.g. XA_BROKEN_EXCEPTION_TRANSITIONS).
	/// Only used when marshal methods are enabled.
	/// </summary>
	public ITaskItem [] Environments { get; set; } = [];

	/// <summary>
	/// Directory where the <c>marshal_methods.{abi}.ll</c> file is written.
	/// Typically <c>$(_OuterIntermediateOutputPath)android</c> so the outer build's
	/// <c>_CompileNativeAssemblySources</c> can compile it.
	/// </summary>
	[Required]
	public string MarshalMethodsOutputDirectory { get; set; } = "";

	/// <summary>
	/// Per-RID directory where rewritten assemblies are written.  Rewritten
	/// assemblies are always written to this directory (never in-place) so that
	/// parallel inner builds for different RIDs don't conflict, even when the
	/// input assemblies reside in a shared location such as the NuGet runtime pack.
	/// Only used when <see cref="EnableMarshalMethods"/> is true.
	/// </summary>
	[Required]
	public string RewrittenAssembliesOutputDirectory { get; set; } = "";

	/// <summary>
	/// The RuntimeIdentifier for this inner build (e.g. <c>android-arm64</c>).
	/// Converted to an ABI and target architecture internally.
	/// </summary>
	[Required]
	public string RuntimeIdentifier { get; set; } = "";

	/// <summary>
	/// Output: assemblies (and PDBs) that were rewritten to
	/// <see cref="RewrittenAssembliesOutputDirectory"/>.  Each item carries all
	/// metadata from the original input assembly plus <c>OriginalItemSpec</c>
	/// pointing to the original path.  The calling target uses these to replace
	/// the original items in <c>@(ResolvedFileToPublish)</c> so downstream
	/// processing picks up the rewritten copies.
	/// </summary>
	[Output]
	public ITaskItem []? RewrittenAssemblies { get; set; }

	public override bool RunTask ()
	{
		var androidRuntime = MonoAndroidHelper.ParseAndroidRuntime (AndroidRuntime);

		string abi = MonoAndroidHelper.RidToAbi (RuntimeIdentifier);
		var targetArch = MonoAndroidHelper.AbiToTargetArch (abi);

		// The inner build's @(ResolvedFileToPublish) items don't have %(Abi) metadata yet —
		// that's normally stamped later by ProcessAssemblies in the outer build.  Downstream
		// code (XAJavaTypeScanner) reads %(Abi) from each ITaskItem, so we need to set it here.
		EnsureAbiMetadata (abi);

		if (EnableMarshalMethods) {
			ProcessMarshalMethods (targetArch, abi, androidRuntime);
		} else {
			GenerateEmptyLlvmIr (targetArch, abi, androidRuntime);
		}

		return !Log.HasLoggedErrors;
	}

	/// <summary>
	/// Marshal methods enabled path: classify, rewrite assemblies to output directory, generate full .ll.
	/// </summary>
	void ProcessMarshalMethods (AndroidTargetArch targetArch, string abi, AndroidRuntime androidRuntime)
	{
		// Parse environment files for configuration (e.g. broken exception transitions)
		var environmentParser = new EnvironmentFilesParser ();
		bool brokenExceptionTransitionsEnabled = environmentParser.AreBrokenExceptionTransitionsEnabled (Environments);

		// Step 1: Open assemblies with Cecil and classify marshal methods
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

		// Step 2: Rewrite assemblies to the per-RID output directory
		HashSet<string> rewrittenOriginalPaths;
		if (!EnableManagedMarshalMethodsLookup) {
			rewrittenOriginalPaths = RewriteAssemblies (targetArch, classifier, resolver, brokenExceptionTransitionsEnabled);
			classifier.AddSpecialCaseMethods ();
		} else {
			classifier.AddSpecialCaseMethods ();
			var lookupInfo = new ManagedMarshalMethodsLookupInfo (Log);
			rewrittenOriginalPaths = RewriteAssemblies (targetArch, classifier, resolver, brokenExceptionTransitionsEnabled, lookupInfo);
		}

		ReportStatistics (targetArch, classifier);

		// Step 3: Build output items for rewritten assemblies (DLLs and PDBs)
		BuildRewrittenAssembliesOutput (rewrittenOriginalPaths);

		// Step 4: Build NativeCodeGenStateObject and generate .ll
		var codeGenState = MarshalMethodCecilAdapter.CreateNativeCodeGenStateObjectFromClassifier (targetArch, classifier);
		GenerateLlvmIr (targetArch, abi, androidRuntime, codeGenState);

		// Step 5: Dispose Cecil resolvers
		resolver.Dispose ();
	}

	/// <summary>
	/// Marshal methods disabled path: generate empty/minimal .ll with structural scaffolding only.
	/// </summary>
	void GenerateEmptyLlvmIr (AndroidTargetArch targetArch, string abi, AndroidRuntime androidRuntime)
	{
		var emptyCodeGenState = new NativeCodeGenStateObject {
			TargetArch = targetArch,
		};
		GenerateLlvmIr (targetArch, abi, androidRuntime, emptyCodeGenState);
	}

	HashSet<string> RewriteAssemblies (AndroidTargetArch targetArch, MarshalMethodsCollection classifier, XAAssemblyResolver resolver, bool brokenExceptionTransitionsEnabled, ManagedMarshalMethodsLookupInfo? lookupInfo = null)
	{
		var rewriter = new MarshalMethodsAssemblyRewriter (Log, targetArch, classifier, resolver, lookupInfo);
		return rewriter.Rewrite (brokenExceptionTransitionsEnabled, RewrittenAssembliesOutputDirectory);
	}

	/// <summary>
	/// Build output items for assemblies (and PDBs) that were rewritten to the output directory.
	/// Each output item has the rewritten path as its ItemSpec, all metadata copied from the
	/// corresponding input assembly, and <c>OriginalItemSpec</c> set to the original path.
	/// </summary>
	void BuildRewrittenAssembliesOutput (HashSet<string> rewrittenOriginalPaths)
	{
		if (rewrittenOriginalPaths.Count == 0) {
			return;
		}

		var rewrittenItems = new List<ITaskItem> ();

		foreach (var item in Assemblies) {
			if (!rewrittenOriginalPaths.Contains (item.ItemSpec)) {
				continue;
			}

			string rewrittenPath = Path.Combine (RewrittenAssembliesOutputDirectory, Path.GetFileName (item.ItemSpec));

			// Output item for the rewritten DLL
			var dllItem = new TaskItem (rewrittenPath);
			item.CopyMetadataTo (dllItem);
			dllItem.SetMetadata ("OriginalItemSpec", item.ItemSpec);
			rewrittenItems.Add (dllItem);

			// Output item for the rewritten PDB, if one was produced
			string rewrittenPdb = Path.ChangeExtension (rewrittenPath, ".pdb");
			if (File.Exists (rewrittenPdb)) {
				string originalPdb = Path.ChangeExtension (item.ItemSpec, ".pdb");
				var pdbItem = new TaskItem (rewrittenPdb);
				item.CopyMetadataTo (pdbItem);
				pdbItem.SetMetadata ("OriginalItemSpec", originalPdb);
				rewrittenItems.Add (pdbItem);
			}
		}

		RewrittenAssemblies = rewrittenItems.ToArray ();
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

	/// <summary>
	/// Stamp <c>%(Abi)</c> metadata on every assembly item that doesn't already have it.
	/// The inner build's <c>@(ResolvedFileToPublish)</c> items carry <c>%(RuntimeIdentifier)</c>
	/// but not <c>%(Abi)</c> — that is normally set later by <c>ProcessAssemblies</c> in the
	/// outer build.  Downstream code (<c>XAJavaTypeScanner</c>) reads <c>%(Abi)</c> from each
	/// <c>ITaskItem</c>, so we set it here from the task's <c>RuntimeIdentifier</c> parameter.
	/// </summary>
	void EnsureAbiMetadata (string abi)
	{
		foreach (var item in Assemblies) {
			string? existingAbi = item.GetMetadata ("Abi");
			if (existingAbi.IsNullOrEmpty ()) {
				item.SetMetadata ("Abi", abi);
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
