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

namespace Xamarin.Android.Tasks;

public class GenerateTrimmableTypeMap : AndroidTask
{
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
	}

	public override string TaskPrefix => "GTT";

	[Required]
	public ITaskItem [] ResolvedAssemblies { get; set; } = [];
	[Required]
	public string OutputDirectory { get; set; } = "";
	[Required]
	public string JavaSourceOutputDirectory { get; set; } = "";
	[Required]
	public string TargetFrameworkVersion { get; set; } = "";

	public string? AcwMapOutputFile { get; set; }

	public string? ApplicationRegistrationOutputFile { get; set; }

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
	public string[]? AdditionalProviderSources { get; set; }

	public override bool RunTask ()
	{
		var systemRuntimeVersion = ParseTargetFrameworkVersion (TargetFrameworkVersion);
		var assemblyPaths = ResolvedAssemblies.Select (i => i.ItemSpec).Distinct ().ToList ();
		// TODO(#10792): populate with framework assembly names to skip JCW generation for pre-compiled framework types
		var frameworkAssemblyNames = new HashSet<string> (StringComparer.OrdinalIgnoreCase);

		Directory.CreateDirectory (OutputDirectory);
		Directory.CreateDirectory (JavaSourceOutputDirectory);

		var peReaders = new List<PEReader> ();
		var assemblies = new List<(string Name, PEReader Reader)> ();
		TrimmableTypeMapResult? result = null;
		try {
			foreach (var path in assemblyPaths) {
				var peReader = new PEReader (File.OpenRead (path));
				peReaders.Add (peReader);
				var mdReader = peReader.GetMetadataReader ();
				assemblies.Add ((mdReader.GetString (mdReader.GetAssemblyDefinition ().Name), peReader));
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
				manifestConfig,
				manifestTemplate,
				maxArrayRank: MaxArrayRank);

			GeneratedAssemblies = WriteAssembliesToDisk (result.GeneratedAssemblies, assemblyPaths);
			GeneratedJavaFiles = WriteJavaSourcesToDisk (result.GeneratedJavaSources);

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

	ITaskItem [] WriteAssembliesToDisk (IReadOnlyList<GeneratedAssembly> assemblies, IReadOnlyList<string> assemblyPaths)
	{
		// Build a map from assembly name -> source path for timestamp comparison
		var sourcePathByName = new Dictionary<string, string> (StringComparer.Ordinal);
		foreach (var path in assemblyPaths) {
			var name = Path.GetFileNameWithoutExtension (path);
			sourcePathByName [name] = path;
		}

		var items = new List<ITaskItem> ();
		bool anyRegenerated = false;

		foreach (var assembly in assemblies) {
			if (assembly.Name == "_Microsoft.Android.TypeMaps") {
				continue; // Handle root assembly separately below
			}

			string outputPath = Path.Combine (OutputDirectory, assembly.Name + ".dll");
			// Extract the original assembly name from the typemap name (e.g., "_Foo.TypeMap" -> "Foo")
			string originalName = assembly.Name;
			if (originalName.StartsWith ("_", StringComparison.Ordinal) && originalName.EndsWith (".TypeMap", StringComparison.Ordinal)) {
				originalName = originalName.Substring (1, originalName.Length - ".TypeMap".Length - 1);
			}

			if (IsUpToDate (outputPath, originalName, sourcePathByName)) {
				Log.LogDebugMessage ($"  {assembly.Name}: up to date, skipping");
			} else {
				Files.CopyIfStreamChanged (assembly.Content, outputPath);
				anyRegenerated = true;
				Log.LogDebugMessage ($"  {assembly.Name}: written");
			}

			items.Add (new TaskItem (outputPath));
		}

		// Root assembly — regenerate if any per-assembly typemap changed
		var rootAssembly = assemblies.FirstOrDefault (a => a.Name == "_Microsoft.Android.TypeMaps");
		if (rootAssembly is not null) {
			string rootOutputPath = Path.Combine (OutputDirectory, rootAssembly.Name + ".dll");
			if (anyRegenerated || !File.Exists (rootOutputPath)) {
				Files.CopyIfStreamChanged (rootAssembly.Content, rootOutputPath);
				Log.LogDebugMessage ($"  Root: written");
			} else {
				Log.LogDebugMessage ($"  Root: up to date, skipping");
			}
			items.Add (new TaskItem (rootOutputPath));
		}

		return items.ToArray ();
	}

	static bool IsUpToDate (string outputPath, string assemblyName, Dictionary<string, string> sourcePathByName)
	{
		if (!File.Exists (outputPath)) {
			return false;
		}
		if (!sourcePathByName.TryGetValue (assemblyName, out var sourcePath)) {
			return false;
		}
		return File.GetLastWriteTimeUtc (outputPath) >= File.GetLastWriteTimeUtc (sourcePath);
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
