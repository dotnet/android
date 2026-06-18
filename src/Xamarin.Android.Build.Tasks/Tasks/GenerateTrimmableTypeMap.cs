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
		public void LogJniAddNativeMethodRegistrationAttributeError (string managedTypeName) =>
			log.LogCodedError ("XA4251", Properties.Resources.XA4251, managedTypeName);
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
	[Required]
	public string TargetFrameworkVersion { get; set; } = "";

	public string? AcwMapOutputFile { get; set; }

	public string? ApplicationRegistrationOutputFile { get; set; }

	public string? GeneratedAssembliesListFile { get; set; }

	public string? ManifestTemplate { get; set; }

	public string? MergedAndroidManifestOutput { get; set; }

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

	/// <summary>
	/// Maximum array rank for which the generator emits per-rank <c>__ArrayMapRank{N}</c>
	/// sentinels and <c>TypeMap</c> entries. 0 disables. Set via
	/// <c>$(_AndroidTrimmableTypeMapMaxArrayRank)</c>.
	/// </summary>
	public int MaxArrayRank { get; set; }

	public string? ManifestPlaceholders { get; set; }
	public string? CheckedBuild { get; set; }
	public string? ApplicationJavaClass { get; set; }

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
		var assemblyInputs = ResolvedAssemblies
			.GroupBy (i => Path.GetFullPath (i.ItemSpec), StringComparer.OrdinalIgnoreCase)
			.Select (g => (
				Path: g.Key,
				IsFrameworkAssembly: frameworkAssemblyPaths.Contains (g.Key) || g.Any (IsFrameworkAssemblyItem)))
			.ToList ();
		var frameworkAssemblyNames = new HashSet<string> (DefaultFrameworkAssemblyNames, StringComparer.OrdinalIgnoreCase);
		foreach (var assemblyName in FrameworkAssemblyNames) {
			frameworkAssemblyNames.Add (assemblyName);
		}

		Directory.CreateDirectory (OutputDirectory);
		Directory.CreateDirectory (JavaSourceOutputDirectory);

		var peReaders = new List<PEReader> ();
		var assemblies = new List<(string Name, PEReader Reader)> ();
		TrimmableTypeMapResult? result = null;
		try {
			foreach (var (path, isFrameworkAssembly) in assemblyInputs) {
				var peReader = new PEReader (File.OpenRead (path));
				peReaders.Add (peReader);
				var mdReader = peReader.GetMetadataReader ();
				var assemblyName = mdReader.GetString (mdReader.GetAssemblyDefinition ().Name);
				assemblies.Add ((assemblyName, peReader));
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
					ApplicationJavaClass: ApplicationJavaClass);
			}

			var generator = new TrimmableTypeMapGenerator (new MSBuildTrimmableTypeMapLogger (Log));

			XDocument? manifestTemplate = null;
			if (!ManifestTemplate.IsNullOrEmpty () && File.Exists (ManifestTemplate)) {
				manifestTemplate = XDocument.Load (ManifestTemplate);
			}

			result = generator.Execute (
				assemblies,
				systemRuntimeVersion,
				frameworkAssemblyNames,
				useSharedTypemapUniverse: !Debug,
				manifestConfig: manifestConfig,
				manifestTemplate: manifestTemplate,
				packageNamingPolicy: PackageNamingPolicy,
				maxArrayRank: MaxArrayRank);

			GeneratedAssemblies = WriteAssembliesToDisk (result.GeneratedAssemblies);
			WriteGeneratedAssembliesListFile (GeneratedAssemblies);
			GeneratedJavaFiles = WriteJavaSourcesToDisk (result.GeneratedJavaSources);
			DeletedJavaFiles = DeleteStaleJavaSources (GeneratedJavaFiles);

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

	ITaskItem [] WriteAssembliesToDisk (IReadOnlyList<GeneratedAssembly> assemblies)
	{
		var items = new List<ITaskItem> ();

		foreach (var assembly in assemblies) {
			if (assembly.Name == "_Microsoft.Android.TypeMaps") {
				continue; // Handle root assembly separately below
			}

			string outputPath = Path.Combine (OutputDirectory, assembly.Name + ".dll");
			var changed = Files.CopyIfStreamChanged (assembly.Content, outputPath);
			Log.LogDebugMessage ($"  {assembly.Name}: {(changed ? "written" : "unchanged")}");

			items.Add (new TaskItem (outputPath));
		}

		var rootAssembly = assemblies.FirstOrDefault (a => a.Name == "_Microsoft.Android.TypeMaps");
		if (rootAssembly is not null) {
			string rootOutputPath = Path.Combine (OutputDirectory, rootAssembly.Name + ".dll");
			var changed = Files.CopyIfStreamChanged (rootAssembly.Content, rootOutputPath);
			Log.LogDebugMessage ($"  Root: {(changed ? "written" : "unchanged")}");
			items.Add (new TaskItem (rootOutputPath));
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

	ITaskItem [] DeleteStaleJavaSources (IReadOnlyCollection<ITaskItem> generatedJavaFiles)
	{
		var expectedFiles = new HashSet<string> (
			generatedJavaFiles.Select (i => Path.GetFullPath (i.ItemSpec)),
			Path.DirectorySeparatorChar == '\\' ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
		var deleted = new List<ITaskItem> ();

		foreach (var path in Directory.EnumerateFiles (JavaSourceOutputDirectory, "*.java", SearchOption.AllDirectories)) {
			var fullPath = Path.GetFullPath (path);
			if (expectedFiles.Contains (fullPath)) {
				continue;
			}

			File.Delete (fullPath);
			Log.LogDebugMessage ($"Deleted stale generated Java source '{fullPath}'.");

			var item = new TaskItem (fullPath);
			item.SetMetadata ("RelativePath", PathUtil.GetRelativePath (JavaSourceOutputDirectory, fullPath));
			deleted.Add (item);
		}

		return deleted.ToArray ();
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
}
