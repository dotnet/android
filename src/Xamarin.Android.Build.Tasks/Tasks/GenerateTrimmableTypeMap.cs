using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Android.Build.Tasks;
using Microsoft.Android.Sdk.TrimmableTypeMap;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks;

/// <summary>
/// Generates trimmable TypeMap assemblies and JCW Java source files from resolved assemblies.
/// Runs before the trimmer to produce per-assembly typemap .dll files and a root
/// _Microsoft.Android.TypeMaps.dll, plus .java files for ACW types with registerNatives.
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
	/// The .NET target framework version (e.g., "v11.0"). Used to set the System.Runtime
	/// assembly reference version in generated typemap assemblies.
	/// </summary>
	[Required]
	public string TargetFrameworkVersion { get; set; } = "";

	[Output]
	public ITaskItem []? GeneratedAssemblies { get; set; }

	[Output]
	public ITaskItem []? GeneratedJavaFiles { get; set; }

	public override bool RunTask ()
	{
		var systemRuntimeVersion = ParseTargetFrameworkVersion (TargetFrameworkVersion);
		var assemblyPaths = GetAssemblyPaths (ResolvedAssemblies);

		Directory.CreateDirectory (OutputDirectory);
		Directory.CreateDirectory (JavaSourceOutputDirectory);

		// Phase 1: Scan assemblies
		List<JavaPeerInfo> allPeers;
		using (var scanner = new JavaPeerScanner ()) {
			allPeers = scanner.Scan (assemblyPaths);
		}

		if (allPeers.Count == 0) {
			Log.LogDebugMessage ("No Java peer types found, skipping typemap generation.");
			GeneratedAssemblies = [];
			GeneratedJavaFiles = [];
			return !Log.HasLoggedErrors;
		}

		// Phase 2: Group peers by source assembly
		var peersByAssembly = new Dictionary<string, List<JavaPeerInfo>> (StringComparer.Ordinal);
		foreach (var peer in allPeers) {
			if (!peersByAssembly.TryGetValue (peer.AssemblyName, out var list)) {
				list = new List<JavaPeerInfo> ();
				peersByAssembly [peer.AssemblyName] = list;
			}
			list.Add (peer);
		}

		// Phase 3: Generate per-assembly typemap assemblies
		var generatedAssemblies = new List<ITaskItem> ();
		var perAssemblyNames = new List<string> ();

		var generator = new TypeMapAssemblyGenerator (systemRuntimeVersion);
		foreach (var kvp in peersByAssembly) {
			string assemblyName = $"_{kvp.Key}.TypeMap";
			string outputPath = Path.Combine (OutputDirectory, assemblyName + ".dll");

			generator.Generate (kvp.Value, outputPath, assemblyName);
			generatedAssemblies.Add (new TaskItem (outputPath));
			perAssemblyNames.Add (assemblyName);

			Log.LogDebugMessage ($"Generated typemap assembly: {outputPath} ({kvp.Value.Count} types)");
		}

		// Phase 4: Generate root _Microsoft.Android.TypeMaps.dll
		var rootGenerator = new RootTypeMapAssemblyGenerator (systemRuntimeVersion);
		string rootOutputPath = Path.Combine (OutputDirectory, "_Microsoft.Android.TypeMaps.dll");
		rootGenerator.Generate (perAssemblyNames, rootOutputPath);
		generatedAssemblies.Add (new TaskItem (rootOutputPath));

		Log.LogDebugMessage ($"Generated root typemap assembly: {rootOutputPath} ({perAssemblyNames.Count} per-assembly refs)");

		// Phase 5: Generate JCW Java source files
		var jcwGenerator = new JcwJavaSourceGenerator ();
		var generatedJavaFiles = jcwGenerator.Generate (allPeers, JavaSourceOutputDirectory);

		Log.LogDebugMessage ($"Generated {generatedJavaFiles.Count} JCW Java source files.");

		GeneratedAssemblies = generatedAssemblies.ToArray ();
		GeneratedJavaFiles = generatedJavaFiles
			.ConvertAll (path => (ITaskItem) new TaskItem (path))
			.ToArray ();

		return !Log.HasLoggedErrors;
	}

	static Version ParseTargetFrameworkVersion (string tfv)
	{
		// Strip leading 'v' if present (e.g., "v11.0" → "11.0")
		if (tfv.Length > 0 && (tfv [0] == 'v' || tfv [0] == 'V')) {
			tfv = tfv.Substring (1);
		}
		if (Version.TryParse (tfv, out var version)) {
			return version;
		}
		return new Version (11, 0, 0, 0);
	}

	static IReadOnlyList<string> GetAssemblyPaths (ITaskItem [] items)
	{
		var paths = new List<string> (items.Length);
		foreach (var item in items) {
			paths.Add (item.ItemSpec);
		}
		return paths;
	}
}
