using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Xml.Linq;
using Microsoft.Android.Build.Tasks;
using Microsoft.Android.Sdk.TrimmableTypeMap;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xamarin.Android.Tools;
using Xamarin.Tools.Zip;

namespace Xamarin.Android.Tasks;

public class GenerateTrimmableTypeMap : AndroidTask
{
	static readonly string [] DefaultFrameworkAssemblyNames = [
		"Java.Interop",
		"Mono.Android",
		"Mono.Android.Runtime",
	];

	sealed class MSBuildTrimmableTypeMapLogger (TaskLoggingHelper log) : ITrimmableTypeMapLogger
	{
		public void LogNoJavaPeerTypesFound () =>
			log.LogMessage (MessageImportance.Low, "No Java peer types found, skipping typemap generation.");
		public void LogJavaPeerScanInfo (int assemblyCount, int peerCount) =>
			log.LogMessage (MessageImportance.Low, $"Scanned {assemblyCount} assemblies, found {peerCount} Java peer types.");
		public void LogGeneratingJcwFilesInfo (int jcwPeerCount, int totalPeerCount) =>
			log.LogMessage (MessageImportance.Low, $"Generating JCW files for {jcwPeerCount} types (filtered from {totalPeerCount} total).");
		public void LogDeferredRegistrationTypesInfo (int typeCount) =>
			log.LogMessage (MessageImportance.Low, $"Found {typeCount} Application/Instrumentation types for deferred registration.");
		public void LogGeneratedTypeMapAssemblyInfo (string assemblyName, int typeCount) =>
			log.LogMessage (MessageImportance.Low, $"  {assemblyName}: {typeCount} types");
		public void LogGeneratedRootTypeMapInfo (int assemblyReferenceCount) =>
			log.LogMessage (MessageImportance.Low, $"  Root: {assemblyReferenceCount} per-assembly refs");
		public void LogGeneratedTypeMapAssembliesInfo (int assemblyCount) =>
			log.LogMessage (MessageImportance.Low, $"Generated {assemblyCount} typemap assemblies.");
		public void LogGeneratedJcwFilesInfo (int sourceCount) =>
			log.LogMessage (MessageImportance.Low, $"Generated {sourceCount} JCW Java source files.");
		public void LogRootingManifestReferencedTypeInfo (string javaTypeName, string managedTypeName) =>
			log.LogMessage (MessageImportance.Low, $"Rooting manifest-referenced type '{javaTypeName}' ({managedTypeName}) as unconditional.");
		public void LogManifestReferencedTypeNotFoundWarning (string javaTypeName) =>
			log.LogCodedWarning ("XA4250", Properties.Resources.XA4250, javaTypeName);
		public void LogLibraryManifestMergeWarning (string message) =>
			log.LogCodedWarning ("XA4302", Properties.Resources.XA4302, message);
		public void LogInvalidManifestPlaceholderWarning (string placeholders) =>
			log.LogCodedWarning ("XA1010", Properties.Resources.XA1010, placeholders);
		public void LogUnresolvableJavaPeerSkippedWarning (
			string managedTypeName,
			string assemblyName,
			string unresolvedTypeName,
			string unresolvedAssemblyName,
			string unresolvedAssemblyPath) =>
			log.LogCodedWarning ("XA4257", Properties.Resources.XA4257, managedTypeName, assemblyName, unresolvedTypeName, unresolvedAssemblyName, unresolvedAssemblyPath);
		public void LogJniAddNativeMethodRegistrationAttributeError (string managedTypeName) =>
			log.LogCodedError ("XA4251", Properties.Resources.XA4251, managedTypeName);
		public void LogInvalidJavaNameError (string javaName, string invalidIdentifier) =>
			log.LogCodedError ("XA4258", Properties.Resources.XA4258, javaName, invalidIdentifier);
		public void LogDuplicateJavaTypeError (string javaName) =>
			log.LogCodedError ("XA4215", Properties.Resources.XA4215, javaName);
		public void LogDuplicateJavaTypeDetailsError (string javaName, string managedTypeName) =>
			log.LogCodedError ("XA4215", Properties.Resources.XA4215_Details, javaName, managedTypeName);
		public void LogExportFieldWithParametersError () =>
			log.LogCodedError ("XA4205", Java.Interop.Localization.Resources.JavaCallableWrappers_XA4205);
		public void LogExportOnGenericTypeError () =>
			log.LogCodedError ("XA4206", Java.Interop.Localization.Resources.JavaCallableWrappers_XA4206);
		public void LogExportFieldOnGenericTypeError () =>
			log.LogCodedError ("XA4207", Java.Interop.Localization.Resources.JavaCallableWrappers_XA4207);
		public void LogExportFieldReturnsVoidError () =>
			log.LogCodedError ("XA4208", Java.Interop.Localization.Resources.JavaCallableWrappers_XA4208);
		public void LogUnsupportedExportSignatureError (string memberName, string managedTypeName) =>
			log.LogCodedError ("XA4263", Properties.Resources.XA4263, memberName, managedTypeName);
		public void LogCustomJavaObjectError (string managedTypeName) =>
			log.LogError ("{0}", $"XA4212: {string.Format (Properties.Resources.XA4212, managedTypeName)}");
		public void LogCustomJavaObjectWarning (string managedTypeName) =>
			log.LogWarning ("{0}", $"XA4212: {string.Format (Properties.Resources.XA4212, managedTypeName)}");
	}

	public override string TaskPrefix => "GTT";

	[Required]
	public ITaskItem [] ResolvedAssemblies { get; set; } = [];
	public ITaskItem [] ResolvedFrameworkAssemblies { get; set; } = [];
	public string [] FrameworkAssemblyNames { get; set; } = [];
	[Required]
	public string OutputDirectory { get; set; } = "";
	[Required]
	public string JavaSourceOutputDirectory { get; set; } = "";
	public string? JavaSourceInputDirectory { get; set; }
	[Required]
	public string TargetFrameworkVersion { get; set; } = "";

	public string? AcwMapOutputFile { get; set; }

	public string? ApplicationRegistrationOutputFile { get; set; }

	public string? GeneratedAssembliesListFile { get; set; }
	public string? TypeMapFingerprintsFile { get; set; }

	public string? ManifestTemplate { get; set; }

	public string? CustomViewMapFile { get; set; }

	public string? MergedAndroidManifestOutput { get; set; }

	/// <summary>
	/// Absolute paths to extracted library (.aar) <c>AndroidManifest.xml</c> documents that must be
	/// merged into the application manifest. Only populated on the legacy manifest-merger path;
	/// <c>manifestmerger.jar</c> handles this downstream in the <c>_ManifestMerger</c> target.
	/// </summary>
	public string []? MergedManifestDocuments { get; set; }

	public string? PackageName { get; set; }
	public string? ApplicationLabel { get; set; }
	public string? VersionCode { get; set; }
	public string? VersionName { get; set; }
	public string? AndroidApiLevel { get; set; }
	public string? SupportedOSPlatformVersion { get; set; }
	public string? RuntimeProviderJavaName { get; set; }
	public bool Debug { get; set; }
	public bool NeedsInternet { get; set; }
	public bool EmbedAssemblies { get; set; }
	public string? PackageNamingPolicy { get; set; }

	public string? ManifestPlaceholders { get; set; }
	public string? CheckedBuild { get; set; }
	public string? ApplicationJavaClass { get; set; }
	public bool GenerateTypeMapAssemblies { get; set; } = true;

	// When false, the per-assembly typemap DLLs (and JCWs) are emitted but the root
	// _Microsoft.Android.TypeMaps assembly is not. Used for SDK-build-time pre-generation of
	// framework typemaps (e.g. Mono.Android, issue #10792); the app build emits the root, which
	// references the pre-generated per-assembly typemaps alongside the app's own.
	public bool GenerateRootAssembly { get; set; } = true;

	// When true, forces the shared (Java.Lang.Object) typemap universe regardless of Debug.
	// Set for SDK-build-time pre-generation of framework typemaps (issue #10792) so aliases
	// across the pre-generated framework assemblies are coordinated. App builds can consume
	// the result only in Debug's per-assembly universe mode.
	public bool ForceSharedTypemapUniverse { get; set; }

	// Framework assemblies whose typemap is pre-generated at SDK build time (issue #10792), e.g.
	// Mono.Android. On the app build these are indexed for base-type resolution but NOT scanned for
	// peers; instead their pre-generated per-assembly typemap (_<Name>.TypeMap) is referenced by the
	// generated root assembly under the Java.Lang.Object universe.
	public ITaskItem [] PreGeneratedTypeMapAssemblies { get; set; } = [];

	// Assemblies that are indexed for type resolution but are not scanned for Java peers.
	public ITaskItem [] ReferenceOnlyAssemblies { get; set; } = [];

	// Pre-compiled framework JCWs whose Java names must not collide with app-generated JCWs.
	public string? PreGeneratedJcwJar { get; set; }

	// SDK-time framework typemaps cannot be rooted from an individual app's manifest or resources.
	public bool ForceFrameworkPeersUnconditional { get; set; }

	public bool CleanJavaSourceOutputDirectory { get; set; }

	/// <summary>
	/// When true (the default, from <c>$(AndroidErrorOnCustomJavaObject)</c>), a managed class
	/// that implements <c>Android.Runtime.IJavaObject</c> without deriving from a Java peer is
	/// reported as the XA4212 error; otherwise it is reported as a warning.
	/// </summary>
	public bool ErrorOnCustomJavaObject { get; set; } = true;

	[Output]
	public ITaskItem [] GeneratedAssemblies { get; set; } = [];
	[Output]
	public ITaskItem [] GeneratedJavaFiles { get; set; } = [];
	[Output]
	public ITaskItem [] DeletedJavaFiles { get; set; } = [];
	[Output]
	public string[]? AdditionalProviderSources { get; set; }

	public override bool RunTask ()
	{
		var systemRuntimeVersion = ParseTargetFrameworkVersion (TargetFrameworkVersion);
		var frameworkAssemblyPaths = new HashSet<string> (
			ResolvedFrameworkAssemblies.Select (i => Path.GetFullPath (i.ItemSpec)),
			StringComparer.OrdinalIgnoreCase);
		// Assemblies whose typemap is pre-generated: index them for resolution but don't scan for
		// peers, and reference their pre-generated _<Name>.TypeMap from the root under Java.Lang.Object.
		var preGeneratedAssemblyPaths = new HashSet<string> (
			PreGeneratedTypeMapAssemblies.Select (i => Path.GetFullPath (i.ItemSpec)),
			StringComparer.OrdinalIgnoreCase);
		var referenceOnlyAssemblyPaths = new HashSet<string> (
			ReferenceOnlyAssemblies.Select (i => Path.GetFullPath (i.ItemSpec)),
			StringComparer.OrdinalIgnoreCase);
		referenceOnlyAssemblyPaths.UnionWith (preGeneratedAssemblyPaths);
		var sharedFrameworkTypeMapNames = PreGeneratedTypeMapAssemblies
			.Select (i => $"_{Path.GetFileNameWithoutExtension (i.ItemSpec)}.TypeMap")
			.Distinct (StringComparer.Ordinal)
			.ToList ();
		var assemblyInputs = ResolvedAssemblies
			.GroupBy (i => Path.GetFullPath (i.ItemSpec), StringComparer.OrdinalIgnoreCase)
			.Select (g => (
				Path: g.Key,
				IsFrameworkAssembly: frameworkAssemblyPaths.Contains (g.Key) || g.Any (IsFrameworkAssemblyItem),
				ScanForPeers: !referenceOnlyAssemblyPaths.Contains (g.Key)))
			.ToList ();
		var frameworkAssemblyNames = new HashSet<string> (DefaultFrameworkAssemblyNames, StringComparer.OrdinalIgnoreCase);
		foreach (var assemblyName in FrameworkAssemblyNames) {
			frameworkAssemblyNames.Add (assemblyName);
		}
		if (CleanJavaSourceOutputDirectory && !JavaSourceInputDirectory.IsNullOrEmpty ()) {
			var inputDirectory = Path.GetFullPath (JavaSourceInputDirectory);
			var outputDirectory = Path.GetFullPath (JavaSourceOutputDirectory);
			if (string.Equals (inputDirectory, outputDirectory, StringComparison.OrdinalIgnoreCase)) {
				Log.LogCodedError ("XA4254", Properties.Resources.XA4254, inputDirectory, outputDirectory);
				return false;
			}
		}

		Directory.CreateDirectory (OutputDirectory);
		string[]? priorJavaSnapshot = null;
		if (CleanJavaSourceOutputDirectory) {
			// Capture the previously generated set before wiping it, so DeleteStaleJavaSources can
			// report which Java sources are no longer produced (e.g. a type that was trimmed away).
			// An empty snapshot (first run, nothing to wipe) still routes through the snapshot-diff
			// path so clean mode is handled consistently.
			if (Directory.Exists (JavaSourceOutputDirectory)) {
				priorJavaSnapshot = Directory.GetFiles (JavaSourceOutputDirectory, "*.java", SearchOption.AllDirectories);
				Directory.Delete (JavaSourceOutputDirectory, recursive: true);
			} else {
				priorJavaSnapshot = [];
			}
		}
		Directory.CreateDirectory (JavaSourceOutputDirectory);

		var peReaders = new List<PEReader> ();
		var assemblies = new List<AssemblyInput> ();
		var typeMapAssemblyNames = new List<string> ();
		var typeMapFingerprints = new SortedDictionary<string, string> (StringComparer.Ordinal);
		var priorTypeMapFingerprints = ReadTypeMapFingerprints ();
		TrimmableTypeMapResult? result = null;
		try {
			foreach (var (path, isFrameworkAssembly, scanForPeers) in assemblyInputs) {
				var peReader = new PEReader (File.OpenRead (path));
				peReaders.Add (peReader);
				var mdReader = peReader.GetMetadataReader ();
				var assemblyName = mdReader.GetString (mdReader.GetAssemblyDefinition ().Name);
				assemblies.Add (new AssemblyInput (assemblyName, path, peReader, scanForPeers));
				if (isFrameworkAssembly) {
					frameworkAssemblyNames.Add (assemblyName);
				}
			}

			ManifestConfig? manifestConfig = null;
			if (!MergedAndroidManifestOutput.IsNullOrEmpty () && !PackageName.IsNullOrEmpty ()) {
				manifestConfig = new ManifestConfig (
					PackageName: PackageName,
					ApplicationLabel: ApplicationLabel,
					VersionCode: VersionCode,
					VersionName: VersionName,
					AndroidApiLevel: AndroidApiLevel,
					SupportedOSPlatformVersion: SupportedOSPlatformVersion,
					RuntimeProviderJavaName: RuntimeProviderJavaName,
					Debug: Debug,
					NeedsInternet: NeedsInternet,
					EmbedAssemblies: EmbedAssemblies,
					ManifestPlaceholders: ManifestPlaceholders,
					CheckedBuild: CheckedBuild,
					ApplicationJavaClass: ApplicationJavaClass,
					LibraryManifests: MergedManifestDocuments);
			}

			var generator = new TrimmableTypeMapGenerator (new MSBuildTrimmableTypeMapLogger (Log));

			XDocument? manifestTemplate = null;
			if (!ManifestTemplate.IsNullOrEmpty () && File.Exists (ManifestTemplate)) {
				manifestTemplate = XDocument.Load (ManifestTemplate);
			}
			IReadOnlyCollection<string>? customViewTypeNames = CustomViewMapFile.IsNullOrEmpty ()
				? null
				: MonoAndroidHelper.LoadCustomViewMapFile (BuildEngine4, CustomViewMapFile).Keys;
			IReadOnlyCollection<string>? preGeneratedJcwNames = LoadPreGeneratedJcwNames ();

			result = generator.Execute (
				assemblies,
				systemRuntimeVersion,
				frameworkAssemblyNames,
				useSharedTypemapUniverse: ForceSharedTypemapUniverse || !Debug,
				manifestConfig: manifestConfig,
				manifestTemplate: manifestTemplate,
				packageNamingPolicy: PackageNamingPolicy,
				generateTypeMapAssemblies: GenerateTypeMapAssemblies,
				generateRootAssembly: GenerateRootAssembly,
				sharedFrameworkTypeMapNames: sharedFrameworkTypeMapNames,
				errorOnCustomJavaObject: ErrorOnCustomJavaObject,
				customViewTypeNames: customViewTypeNames,
				preGeneratedJcwNames: preGeneratedJcwNames,
				preGeneratedJcwSource: PreGeneratedJcwJar,
				forceFrameworkPeersUnconditional: ForceFrameworkPeersUnconditional,
				collectMarshalMethodsForNonAcw: false,
				shouldGenerateTypeMapAssembly: TypeMapFingerprintsFile.IsNullOrEmpty () ? null : ShouldGenerateTypeMapAssembly);
			if (Log.HasLoggedErrors) {
				return false;
			}

			if (GenerateTypeMapAssemblies) {
				if (TypeMapFingerprintsFile.IsNullOrEmpty ()) {
					typeMapAssemblyNames.AddRange (result.GeneratedAssemblies.Select (assembly => assembly.Name));
				}
				GeneratedAssemblies = WriteAssembliesToDisk (result.GeneratedAssemblies, typeMapAssemblyNames);
				WriteGeneratedAssembliesListFile (GeneratedAssemblies);
				WriteTypeMapFingerprints (typeMapFingerprints);
			}
			GeneratedJavaFiles = JavaSourceInputDirectory.IsNullOrEmpty ()
				? WriteJavaSourcesToDisk (result.GeneratedJavaSources)
				: CopyJavaSourcesFromInputDirectory (result.GeneratedJavaSources);
			DeletedJavaFiles = DeleteStaleJavaSources (GeneratedJavaFiles, priorJavaSnapshot);

			// Write manifest to disk if generated
			if (result.Manifest is not null && !MergedAndroidManifestOutput.IsNullOrEmpty ()) {
				var manifestDir = Path.GetDirectoryName (MergedAndroidManifestOutput);
				if (!manifestDir.IsNullOrEmpty ()) {
					Directory.CreateDirectory (manifestDir);
				}
				using (var ms = new MemoryStream ()) {
					result.Manifest.Document.Save (ms);
					ms.Position = 0;
					Files.CopyIfStreamChanged (ms, MergedAndroidManifestOutput);
				}
				AdditionalProviderSources = result.Manifest.AdditionalProviderSources;
			}

			// Write merged acw-map.txt if requested
			if (!AcwMapOutputFile.IsNullOrEmpty ()) {
				var acwDirectory = Path.GetDirectoryName (AcwMapOutputFile);
				if (!acwDirectory.IsNullOrEmpty ()) {
					Directory.CreateDirectory (acwDirectory);
				}
				using (var sw = MemoryStreamPool.Shared.CreateStreamWriter ()) {
					AcwMapWriter.Write (sw, result.AllPeers);
					sw.Flush ();
					Files.CopyIfStreamChanged (sw.BaseStream, AcwMapOutputFile);
				}
				Log.LogDebugMessage ($"Wrote merged acw-map.txt with {result.AllPeers.Count} types to {AcwMapOutputFile}.");
			}

			// Generate ApplicationRegistration.java with registerNatives calls for
			// Application/Instrumentation types whose static initializers were skipped.
			if (!ApplicationRegistrationOutputFile.IsNullOrEmpty ()) {
				var appRegDir = Path.GetDirectoryName (ApplicationRegistrationOutputFile);
				if (!appRegDir.IsNullOrEmpty ()) {
					Directory.CreateDirectory (appRegDir);
				}
				Files.CopyIfStringChanged (GenerateApplicationRegistrationJava (result.ApplicationRegistrationTypes), ApplicationRegistrationOutputFile);
				Log.LogDebugMessage ($"Generated ApplicationRegistration.java with {result.ApplicationRegistrationTypes.Count} deferred registration(s).");
			}
		} finally {
			if (result is not null) {
				foreach (var assembly in result.GeneratedAssemblies) {
					assembly.Content.Dispose ();
				}
			}
			foreach (var peReader in peReaders) {
				peReader.Dispose ();
			}
		}

		return !Log.HasLoggedErrors;

		bool ShouldGenerateTypeMapAssembly (string assemblyName, byte [] fingerprint)
		{
			typeMapAssemblyNames.Add (assemblyName);
			string fingerprintText = Files.ToHexString (fingerprint);
			typeMapFingerprints.Add (assemblyName, fingerprintText);
			string outputPath = Path.Combine (OutputDirectory, assemblyName + ".dll");
			bool generate = !File.Exists (outputPath) ||
				!priorTypeMapFingerprints.TryGetValue (assemblyName, out var priorFingerprint) ||
				!string.Equals (fingerprintText, priorFingerprint, StringComparison.Ordinal);
			Log.LogDebugMessage ($"  {assemblyName}: {(generate ? "changed, generating" : "unchanged, skipping emission")}");
			return generate;
		}
	}

	internal Dictionary<string, string> ReadTypeMapFingerprints ()
	{
		var fingerprints = new Dictionary<string, string> (StringComparer.Ordinal);
		if (TypeMapFingerprintsFile.IsNullOrEmpty () || !File.Exists (TypeMapFingerprintsFile)) {
			return fingerprints;
		}
		try {
			foreach (var line in File.ReadLines (TypeMapFingerprintsFile)) {
				int separator = line.IndexOf ('\t');
				if (separator <= 0 || separator == line.Length - 1) {
					Log.LogDebugMessage ($"Ignoring invalid trimmable typemap fingerprint cache '{TypeMapFingerprintsFile}'.");
					return new Dictionary<string, string> (StringComparer.Ordinal);
				}
				fingerprints [line.Substring (0, separator)] = line.Substring (separator + 1);
			}
		} catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) {
			Log.LogDebugMessage ($"Could not read trimmable typemap fingerprint cache '{TypeMapFingerprintsFile}': {ex.Message}");
			fingerprints.Clear ();
		}
		return fingerprints;
	}

	void WriteTypeMapFingerprints (IReadOnlyDictionary<string, string> fingerprints)
	{
		if (TypeMapFingerprintsFile.IsNullOrEmpty ()) {
			return;
		}
		var directory = Path.GetDirectoryName (TypeMapFingerprintsFile);
		if (!directory.IsNullOrEmpty ()) {
			Directory.CreateDirectory (directory);
		}
		var text = fingerprints.Count == 0
			? ""
			: string.Join (Environment.NewLine, fingerprints.Select (entry => $"{entry.Key}\t{entry.Value}")) + Environment.NewLine;
		Files.CopyIfStringChanged (text, TypeMapFingerprintsFile);
	}

	static bool IsFrameworkAssemblyItem (ITaskItem item) =>
		string.Equals (item.GetMetadata ("FrameworkAssembly"), bool.TrueString, StringComparison.OrdinalIgnoreCase) ||
		MonoAndroidHelper.IsFrameworkAssembly (item);

	void WriteGeneratedAssembliesListFile (IReadOnlyList<ITaskItem> assemblies)
	{
		if (GeneratedAssembliesListFile.IsNullOrEmpty ()) {
			return;
		}

		var directory = Path.GetDirectoryName (GeneratedAssembliesListFile);
		if (!directory.IsNullOrEmpty ()) {
			Directory.CreateDirectory (directory);
		}

		var text = assemblies.Count == 0
			? ""
			: string.Join (Environment.NewLine, assemblies.Select (a => a.ItemSpec)) + Environment.NewLine;
		Files.CopyIfStringChanged (text, GeneratedAssembliesListFile);
	}

	ITaskItem [] CopyJavaSourcesFromInputDirectory (IReadOnlyList<GeneratedJavaSource> javaSources)
	{
		var items = new List<ITaskItem> ();
		foreach (var source in javaSources) {
			string inputPath = Path.Combine (JavaSourceInputDirectory ?? "", source.RelativePath);
			if (!File.Exists (inputPath)) {
				Log.LogCodedError ("XA4255", Properties.Resources.XA4255, inputPath);
				continue;
			}

			string outputPath = Path.Combine (JavaSourceOutputDirectory, source.RelativePath);
			string? dir = Path.GetDirectoryName (outputPath);
			if (!string.IsNullOrEmpty (dir)) {
				Directory.CreateDirectory (dir);
			}
			using (var stream = File.OpenRead (inputPath)) {
				Files.CopyIfStreamChanged (stream, outputPath);
			}
			items.Add (new TaskItem (outputPath));
		}
		return items.ToArray ();
	}

	ITaskItem [] WriteAssembliesToDisk (IReadOnlyList<GeneratedAssembly> assemblies, IReadOnlyList<string> assemblyNames)
	{
		var generatedByName = assemblies.ToDictionary (assembly => assembly.Name, StringComparer.Ordinal);
		var items = new List<ITaskItem> ();
		foreach (var assemblyName in assemblyNames) {
			string outputPath = Path.Combine (OutputDirectory, assemblyName + ".dll");
			if (generatedByName.TryGetValue (assemblyName, out var assembly)) {
				Files.CopyIfStreamChanged (assembly.Content, outputPath);
				Log.LogDebugMessage ($"  {assemblyName}: written");
			}
			items.Add (new TaskItem (outputPath));
		}
		return items.ToArray ();
	}

	ITaskItem [] WriteJavaSourcesToDisk (IReadOnlyList<GeneratedJavaSource> javaSources)
	{
		var items = new List<ITaskItem> ();
		foreach (var source in javaSources) {
			string outputPath = Path.Combine (JavaSourceOutputDirectory, source.RelativePath);
			string? dir = Path.GetDirectoryName (outputPath);
			if (!string.IsNullOrEmpty (dir)) {
				Directory.CreateDirectory (dir);
			}
			using (var sw = MemoryStreamPool.Shared.CreateStreamWriter ()) {
				sw.Write (source.Content);
				sw.Flush ();
				Files.CopyIfStreamChanged (sw.BaseStream, outputPath);
			}
			items.Add (new TaskItem (outputPath));
		}
		return items.ToArray ();
	}

	// Removes generated Java sources from a previous build that the current generation pass
	// no longer produces (for example when a managed type is removed or trimmed away). Returns
	// the deleted files (with RelativePath metadata) so the targets can force Java recompilation.
	//
	// When the output directory was wiped before generation (CleanJavaSourceOutputDirectory), the
	// stale files are already gone from disk; the previous contents are supplied via
	// priorJavaSnapshot and the difference against the freshly generated set is reported.
	// Otherwise the directory is scanned and any file the current pass did not produce is deleted.
	ITaskItem [] DeleteStaleJavaSources (IReadOnlyCollection<ITaskItem> generatedJavaFiles, string[]? priorJavaSnapshot)
	{
		// GeneratedJavaFiles can be incomplete after an error (for example XA4255 when a
		// pre-trim source is missing). The build will fail, but keep the last known-good output
		// set intact rather than pruning files based on a partial result.
		if (Log.HasLoggedErrors) {
			return [];
		}

		var expectedFiles = new HashSet<string> (
			generatedJavaFiles.Select (i => Path.GetFullPath (i.ItemSpec)),
			Path.DirectorySeparatorChar == '\\' ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
		var deleted = new List<ITaskItem> ();

		if (priorJavaSnapshot is not null) {
			foreach (var path in priorJavaSnapshot) {
				var fullPath = Path.GetFullPath (path);
				if (expectedFiles.Contains (fullPath)) {
					continue;
				}

				Log.LogDebugMessage ($"Post-trim regeneration no longer produces generated Java source '{fullPath}'.");
				deleted.Add (CreateDeletedJavaItem (fullPath));
			}

			return deleted.ToArray ();
		}

		foreach (var path in Directory.EnumerateFiles (JavaSourceOutputDirectory, "*.java", SearchOption.AllDirectories)) {
			var fullPath = Path.GetFullPath (path);
			if (expectedFiles.Contains (fullPath)) {
				continue;
			}

			File.Delete (fullPath);
			Log.LogDebugMessage ($"Deleted stale generated Java source '{fullPath}'.");
			deleted.Add (CreateDeletedJavaItem (fullPath));
		}

		return deleted.ToArray ();
	}

	TaskItem CreateDeletedJavaItem (string fullPath)
	{
		var item = new TaskItem (fullPath);
		item.SetMetadata ("RelativePath", PathUtil.GetRelativePath (JavaSourceOutputDirectory, fullPath));
		return item;
	}

	static Version ParseTargetFrameworkVersion (string tfv)
	{
		if (tfv.Length > 0 && (tfv [0] == 'v' || tfv [0] == 'V')) {
			tfv = tfv.Substring (1);
		}
		if (Version.TryParse (tfv, out var version)) {
			return version;
		}
		throw new ArgumentException ($"Cannot parse TargetFrameworkVersion '{tfv}' as a Version.");
	}

	static string GenerateApplicationRegistrationJava (IReadOnlyList<string> registrationTypes)
	{
		var sb = new StringBuilder ();
		sb.AppendLine ("package net.dot.android;");
		sb.AppendLine ();
		sb.AppendLine ("public class ApplicationRegistration {");
		sb.AppendLine ();
		sb.AppendLine ("\tpublic static android.content.Context Context;");
		sb.AppendLine ();
		sb.AppendLine ("\tpublic static void registerApplications ()");
		sb.AppendLine ("\t{");
		foreach (var javaClassName in registrationTypes) {
			sb.AppendLine ($"\t\tmono.android.Runtime.registerNatives ({javaClassName}.class);");
		}
		sb.AppendLine ("\t}");
		sb.AppendLine ("}");
		return sb.ToString ();
	}

	IReadOnlyCollection<string>? LoadPreGeneratedJcwNames ()
	{
		if (PreGeneratedTypeMapAssemblies.Length == 0 || PreGeneratedJcwJar.IsNullOrEmpty ()) {
			return null;
		}

		var names = new HashSet<string> (StringComparer.Ordinal);
		using var stream = File.OpenRead (PreGeneratedJcwJar);
		using var jar = ZipArchive.Open (stream);
		foreach (var entry in jar) {
			if (!entry.IsDirectory && entry.FullName.EndsWith (".class", StringComparison.Ordinal)) {
				names.Add (entry.FullName.Substring (0, entry.FullName.Length - ".class".Length));
			}
		}
		return names;
	}
}
