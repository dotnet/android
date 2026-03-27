#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Android.Build.Tasks;
using Microsoft.Android.Sdk.TrimmableTypeMap;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks;

/// <summary>
/// MSBuild task adapter for <see cref="TrimmableTypeMapGenerator"/>.
/// Opens files and maps ITaskItem to/from strings, then delegates to the core class.
/// </summary>
public class GenerateTrimmableTypeMap : AndroidTask
{
	public override string TaskPrefix => "GTT";

	[Required]
	public ITaskItem [] ResolvedAssemblies { get; set; } = [];

	[Required]
	public string OutputDirectory { get; set; } = "";

	[Required]
	public string JavaSourceOutputDirectory { get; set; } = "";

	[Required]
	public string AcwMapDirectory { get; set; } = "";

	/// <summary>
	/// The .NET target framework version (e.g., "v11.0"). Used to set the System.Runtime
	/// assembly reference version in generated typemap assemblies.
	/// </summary>
	[Required]
	public string TargetFrameworkVersion { get; set; } = "";

	[Output]
	public ITaskItem [] GeneratedAssemblies { get; set; } = [];

	[Output]
	public ITaskItem [] GeneratedJavaFiles { get; set; } = [];

	/// <summary>
	/// Per-assembly acw-map files produced during scanning. Each file contains
	/// three lines per type: PartialAssemblyQualifiedName;JavaKey,
	/// ManagedKey;JavaKey, and CompatJniName;JavaKey.
	/// </summary>
	[Output]
	public ITaskItem []? PerAssemblyAcwMapFiles { get; set; }

	public override bool RunTask ()
	{
		var systemRuntimeVersion = TrimmableTypeMapGenerator.ParseTargetFrameworkVersion (TargetFrameworkVersion);
		var assemblyPaths = GetJavaInteropAssemblyPaths (ResolvedAssemblies);

		// Framework binding types (Activity, View, etc.) already exist in java_runtime.dex and don't
		// need JCW .java files. Framework Implementor types (mono/ prefix, e.g. OnClickListenerImplementor)
		// DO need JCWs — they're included via the mono/ filter below.
		// User NuGet libraries also need JCWs, so we only filter by FrameworkReferenceName.
		// Note: Pre-generating SDK-compatible JCWs (mono.android-trimmable.jar) is tracked by #10792.
		var frameworkAssemblyNames = new HashSet<string> (StringComparer.OrdinalIgnoreCase);
		foreach (var item in ResolvedAssemblies) {
			if (!item.GetMetadata ("FrameworkReferenceName").IsNullOrEmpty ()) {
				frameworkAssemblyNames.Add (Path.GetFileNameWithoutExtension (item.ItemSpec));
			}
		}

		Directory.CreateDirectory (AcwMapDirectory);

		var generator = new TrimmableTypeMapGenerator (msg => Log.LogMessage (MessageImportance.Low, msg));
		var result = generator.Execute (
			assemblyPaths,
			OutputDirectory,
			JavaSourceOutputDirectory,
			systemRuntimeVersion,
			frameworkAssemblyNames);

		GeneratedAssemblies = result.GeneratedAssemblies.Select (p => (ITaskItem) new TaskItem (p)).ToArray ();
		GeneratedJavaFiles = result.GeneratedJavaFiles.Select (p => (ITaskItem) new TaskItem (p)).ToArray ();
		PerAssemblyAcwMapFiles = GeneratePerAssemblyAcwMaps (result.AllPeers);

		return !Log.HasLoggedErrors;
	}

	static IReadOnlyList<string> GetJavaInteropAssemblyPaths (ITaskItem [] items)
	{
		var paths = new List<string> (items.Length);
		foreach (var item in items) {
			if (MonoAndroidHelper.IsMonoAndroidAssembly (item)) {
				paths.Add (item.ItemSpec);
			}
		}
		return paths;
	}

	ITaskItem [] GeneratePerAssemblyAcwMaps (IReadOnlyList<JavaPeerInfo> allPeers)
	{
		var peersByAssembly = allPeers
			.GroupBy (p => p.AssemblyName, StringComparer.Ordinal)
			.OrderBy (g => g.Key, StringComparer.Ordinal);

		var outputFiles = new List<ITaskItem> ();

		foreach (var group in peersByAssembly) {
			var peers = group.ToList ();
			string outputFile = Path.Combine (AcwMapDirectory, $"acw-map.{group.Key}.txt");

			bool written;
			using (var sw = MemoryStreamPool.Shared.CreateStreamWriter ()) {
				AcwMapWriter.Write (sw, peers);
				sw.Flush ();
				written = Files.CopyIfStreamChanged (sw.BaseStream, outputFile);
			}

			Log.LogDebugMessage (written
				? $"  acw-map.{group.Key}.txt: {peers.Count} types"
				: $"  acw-map.{group.Key}.txt: unchanged");

			var item = new TaskItem (outputFile);
			item.SetMetadata ("AssemblyName", group.Key);
			outputFiles.Add (item);
		}

		Log.LogDebugMessage ($"Generated {outputFiles.Count} per-assembly ACW map files.");
		return outputFiles.ToArray ();
	}
}
