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

	/// <summary>
	/// Directory for per-assembly acw-map.{AssemblyName}.txt files.
	/// </summary>
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
		var systemRuntimeVersion = ParseTargetFrameworkVersion (TargetFrameworkVersion);
		// Don't filter by HasMonoAndroidReference — ReferencePath items from the compiler
		// don't carry this metadata. The scanner handles non-Java assemblies gracefully.
		var assemblyPaths = ResolvedAssemblies.Select (i => i.ItemSpec).Distinct ().ToList ();

		// Currently we generate JCWs for ALL assemblies including framework bindings.
		// Pre-generating SDK-compatible JCWs (mono.android-trimmable.jar) is tracked by #10792.
		// Once that's done, we can skip framework assemblies here.
		var frameworkAssemblyNames = new HashSet<string> (StringComparer.OrdinalIgnoreCase);

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
}
