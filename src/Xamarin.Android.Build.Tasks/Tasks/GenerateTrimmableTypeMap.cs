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
		var assemblyPaths = GetJavaInteropAssemblyPaths (ResolvedAssemblies);

		Directory.CreateDirectory (OutputDirectory);
		Directory.CreateDirectory (JavaSourceOutputDirectory);

		var allPeers = ScanAssemblies (assemblyPaths);
		if (allPeers.Count == 0) {
			Log.LogDebugMessage ("No Java peer types found, skipping typemap generation.");
			GeneratedAssemblies = [];
			GeneratedJavaFiles = [];
			return !Log.HasLoggedErrors;
		}

		GeneratedAssemblies = GenerateTypeMapAssemblies (allPeers, systemRuntimeVersion);
		GeneratedJavaFiles = GenerateJcwJavaSources (allPeers);

		return !Log.HasLoggedErrors;
	}

	List<JavaPeerInfo> ScanAssemblies (IReadOnlyList<string> assemblyPaths)
	{
		using var scanner = new JavaPeerScanner ();
		var peers = scanner.Scan (assemblyPaths);
		Log.LogDebugMessage ($"Scanned {assemblyPaths.Count} assemblies, found {peers.Count} Java peer types.");
		return peers;
	}

	ITaskItem [] GenerateTypeMapAssemblies (List<JavaPeerInfo> allPeers, Version systemRuntimeVersion)
	{
		var peersByAssembly = allPeers
			.GroupBy (p => p.AssemblyName, StringComparer.Ordinal)
			.OrderBy (g => g.Key, StringComparer.Ordinal);

		var generatedAssemblies = new List<ITaskItem> ();
		var perAssemblyNames = new List<string> ();
		var generator = new TypeMapAssemblyGenerator (systemRuntimeVersion);

		foreach (var group in peersByAssembly) {
			string assemblyName = $"_{group.Key}.TypeMap";
			string outputPath = Path.Combine (OutputDirectory, assemblyName + ".dll");

			generator.Generate (group.ToList (), outputPath, assemblyName);
			generatedAssemblies.Add (new TaskItem (outputPath));
			perAssemblyNames.Add (assemblyName);

			Log.LogDebugMessage ($"  {assemblyName}: {group.Count ()} types");
		}

		var rootGenerator = new RootTypeMapAssemblyGenerator (systemRuntimeVersion);
		string rootOutputPath = Path.Combine (OutputDirectory, "_Microsoft.Android.TypeMaps.dll");
		rootGenerator.Generate (perAssemblyNames, rootOutputPath);
		generatedAssemblies.Add (new TaskItem (rootOutputPath));

		Log.LogDebugMessage ($"Generated {generatedAssemblies.Count} typemap assemblies ({perAssemblyNames.Count} per-assembly + root).");
		return generatedAssemblies.ToArray ();
	}

	ITaskItem [] GenerateJcwJavaSources (List<JavaPeerInfo> allPeers)
	{
		var jcwGenerator = new JcwJavaSourceGenerator ();
		var files = jcwGenerator.Generate (allPeers, JavaSourceOutputDirectory);
		Log.LogDebugMessage ($"Generated {files.Count} JCW Java source files.");
		return files.Select (p => (ITaskItem) new TaskItem (p)).ToArray ();
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

	/// <summary>
	/// Filters resolved assemblies to only those that reference Mono.Android or Java.Interop
	/// (i.e., assemblies that could contain [Register] types). Skips BCL assemblies.
	/// </summary>
	static IReadOnlyList<string> GetJavaInteropAssemblyPaths (ITaskItem [] items)
	{
		return items
			.Where (item => {
				var frameworkAssembly = item.GetMetadata ("FrameworkAssembly");
				if (string.Equals (frameworkAssembly, "true", StringComparison.OrdinalIgnoreCase)) {
					// Framework assemblies that reference Mono.Android (like Mono.Android itself) are included
					var hasRef = item.GetMetadata ("HasMonoAndroidReference");
					return string.Equals (hasRef, "True", StringComparison.OrdinalIgnoreCase);
				}
				return true; // Non-framework assemblies are always included
			})
			.Select (item => item.ItemSpec)
			.ToList ();
	}
}
