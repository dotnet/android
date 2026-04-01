#nullable enable
using System.Collections.Generic;
using System.Linq;
using Java.Interop.Tools.Cecil;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

/// <summary>
/// MSBuild task that rewrites .NET assemblies to use marshal methods instead of dynamic JNI registration.
/// This task modifies method implementations to use efficient native callbacks with [UnmanagedCallersOnly]
/// attributes, significantly improving startup performance and reducing runtime overhead for Android applications.
/// </summary>
/// <remarks>
/// This task runs in the inner (per-RID) build after ILLink and <c>_PostTrimmingPipeline</c> but before
/// <c>CreateReadyToRunImages</c> or <c>IlcCompile</c>. It is fully self-contained: it creates its own
/// assembly resolver, scans assemblies for marshal method candidates, classifies them, and rewrites them
/// in a single pass.
///
/// The rewriting process:
///
/// 1. Derives the target architecture from the <see cref="RuntimeIdentifier"/>
/// 2. Creates an assembly resolver with ReadWrite+InMemory mode for Cecil
/// 3. Scans assemblies to discover marshal method candidates via <see cref="MarshalMethodsCollection.FromAssemblies"/>
/// 4. Parses environment files to determine exception transition behavior
/// 5. Rewrites assemblies to replace dynamic registration with static marshal methods
/// 6. Optionally builds managed lookup tables for runtime marshal method resolution
/// 7. Reports statistics on marshal method generation and any fallback to dynamic registration
///
/// The rewriting creates native callback wrappers for methods that have non-blittable
/// parameters or return types, ensuring compatibility with the [UnmanagedCallersOnly] attribute
/// while maintaining proper marshaling semantics.
/// </remarks>
public class RewriteMarshalMethods : AndroidTask
{
	/// <summary>
	/// Gets the task prefix used for logging and error messages.
	/// </summary>
	public override string TaskPrefix => "RMM";

	/// <summary>
	/// Gets or sets the resolved assemblies to scan and rewrite.
	/// These are the post-ILLink assemblies from <c>ResolvedFileToPublish</c> in the inner build.
	/// </summary>
	[Required]
	public ITaskItem [] Assemblies { get; set; } = [];

	/// <summary>
	/// Gets or sets whether to enable managed marshal methods lookup tables.
	/// When enabled, generates runtime lookup structures that allow dynamic resolution
	/// of marshal methods without string comparisons, improving runtime performance.
	/// </summary>
	public bool EnableManagedMarshalMethodsLookup { get; set; }

	/// <summary>
	/// Gets or sets the environment files to parse for configuration settings.
	/// These files may contain settings like XA_BROKEN_EXCEPTION_TRANSITIONS that
	/// affect how marshal method wrappers are generated.
	/// </summary>
	public ITaskItem [] Environments { get; set; } = [];

	/// <summary>
	/// Gets or sets the runtime identifier for this inner build (e.g., "android-arm64").
	/// Used to derive the target architecture and ABI for assembly scanning and rewriting.
	/// </summary>
	[Required]
	public string RuntimeIdentifier { get; set; } = "";

	/// <summary>
	/// Executes the marshal method rewriting task. This is the main entry point that
	/// coordinates the entire assembly rewriting process for the current target architecture.
	/// </summary>
	/// <returns>
	/// true if the task completed successfully; false if errors occurred during processing.
	/// </returns>
	/// <remarks>
	/// The execution flow is:
	///
	/// 1. Derive the target architecture and ABI from the RuntimeIdentifier
	/// 2. Build a dictionary of assemblies and set Abi metadata on each item
	/// 3. Create an assembly resolver with ReadWrite+InMemory mode
	/// 4. Scan assemblies and classify marshal methods via MarshalMethodsCollection.FromAssemblies
	/// 5. Parse environment files for configuration (e.g., broken exception transitions)
	/// 6. Rewrite assemblies to use marshal methods
	/// 7. Add special case methods (e.g., TypeManager methods)
	/// 8. Optionally build managed lookup tables
	/// 9. Report statistics on marshal method generation
	/// 10. Log warnings for methods that must fall back to dynamic registration
	///
	/// The task handles the ordering dependency between special case methods and managed
	/// lookup tables - special cases must be added first so they appear in the lookup tables.
	/// </remarks>
	public override bool RunTask ()
	{
		AndroidTargetArch arch = MonoAndroidHelper.RidToArch (RuntimeIdentifier);
		string abi = MonoAndroidHelper.RidToAbi (RuntimeIdentifier);

		// Build the assemblies dictionary and set Abi metadata required by XAJavaTypeScanner
		var assemblies = new Dictionary<string, ITaskItem> (Assemblies.Length);
		foreach (var asm in Assemblies) {
			asm.SetMetadata ("Abi", abi);
			assemblies [asm.ItemSpec] = asm;
		}

		// Create a resolver with ReadWrite+InMemory mode so Cecil can modify assemblies in place
		using XAAssemblyResolver resolver = MonoAndroidHelper.MakeResolver (Log, useMarshalMethods: true, arch, assemblies);

		// Scan and classify marshal methods
		MarshalMethodsCollection classifier = MarshalMethodsCollection.FromAssemblies (arch, [.. assemblies.Values], resolver, Log);

		// Parse environment files to determine configuration settings
		// We need to parse the environment files supplied by the user to see if they want to use broken exception transitions. This information is needed
		// in order to properly generate wrapper methods in the marshal methods assembly rewriter.
		// We don't care about those generated by us, since they won't contain the `XA_BROKEN_EXCEPTION_TRANSITIONS` variable we look for.
		var environmentParser = new EnvironmentFilesParser ();
		bool brokenExceptionTransitionsEnabled = environmentParser.AreBrokenExceptionTransitionsEnabled (Environments);

		// Handle the ordering dependency between special case methods and managed lookup tables
		ManagedMarshalMethodsLookupInfo? managedLookupInfo = null;
		if (!EnableManagedMarshalMethodsLookup) {
			// Standard path: rewrite first, then add special cases
			RewriteAssemblies (arch, classifier, resolver, managedLookupInfo, brokenExceptionTransitionsEnabled);
			classifier.AddSpecialCaseMethods ();
		} else {
			// Managed lookup path: add special cases first so they appear in lookup tables
			// We need to run `AddSpecialCaseMethods` before `RewriteMarshalMethods` so that we can see the special case
			// methods (such as TypeManager.n_Activate_mm) when generating the managed lookup tables.
			classifier.AddSpecialCaseMethods ();
			managedLookupInfo = new ManagedMarshalMethodsLookupInfo (Log);
			RewriteAssemblies (arch, classifier, resolver, managedLookupInfo, brokenExceptionTransitionsEnabled);
		}

		// Report statistics on marshal method generation
		Log.LogDebugMessage ($"[{arch}] Number of generated marshal methods: {classifier.MarshalMethods.Count}");
		if (classifier.DynamicallyRegisteredMarshalMethods.Count > 0) {
			Log.LogWarning ($"[{arch}] Number of methods in the project that will be registered dynamically: {classifier.DynamicallyRegisteredMarshalMethods.Count}");
		}

		// Count and report methods that need blittable workaround wrappers
		var wrappedCount = classifier.MarshalMethods.Sum (m => m.Value.Count (m2 => m2.NeedsBlittableWorkaround));

		if (wrappedCount > 0) {
			// TODO: change to LogWarning once the generator can output code which requires no non-blittable wrappers
			Log.LogDebugMessage ($"[{arch}] Number of methods in the project that need marshal method wrappers: {wrappedCount}");
		}

		return !Log.HasLoggedErrors;
	}

	/// <summary>
	/// Performs the actual assembly rewriting for a specific target architecture.
	/// Creates and executes the <see cref="MarshalMethodsAssemblyRewriter"/> that handles
	/// the low-level assembly modification operations.
	/// </summary>
	/// <param name="arch">The target Android architecture.</param>
	/// <param name="classifier">The marshal methods classifier containing method classifications.</param>
	/// <param name="resolver">The assembly resolver used to load and resolve assemblies.</param>
	/// <param name="managedLookupInfo">Optional managed marshal methods lookup info for building lookup tables.</param>
	/// <param name="brokenExceptionTransitionsEnabled">
	/// Whether to generate code compatible with broken exception transitions.
	/// This affects how wrapper methods handle exceptions during JNI calls.
	/// </param>
	/// <remarks>
	/// This method delegates the complex assembly rewriting logic to the specialized
	/// <see cref="MarshalMethodsAssemblyRewriter"/> class, which handles:
	/// - Adding [UnmanagedCallersOnly] attributes to native callbacks
	/// - Generating wrapper methods for non-blittable types
	/// - Modifying assembly references and imports
	/// - Building managed lookup table entries
	/// </remarks>
	void RewriteAssemblies (AndroidTargetArch arch, MarshalMethodsCollection classifier, XAAssemblyResolver resolver, ManagedMarshalMethodsLookupInfo? managedLookupInfo, bool brokenExceptionTransitionsEnabled)
	{
		var rewriter = new MarshalMethodsAssemblyRewriter (Log, arch, classifier, resolver, managedLookupInfo);
		rewriter.Rewrite (brokenExceptionTransitionsEnabled);
	}
}
