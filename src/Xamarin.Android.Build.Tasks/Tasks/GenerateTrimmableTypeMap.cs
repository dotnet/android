#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using Microsoft.Android.Build.Tasks;
using Microsoft.Android.Sdk.TrimmableTypeMap;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks;

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
	[Required]
	public string TargetFrameworkVersion { get; set; } = "";
	[Output]
	public ITaskItem [] GeneratedAssemblies { get; set; } = [];
	[Output]
	public ITaskItem [] GeneratedJavaFiles { get; set; } = [];
	[Output]
	public ITaskItem []? PerAssemblyAcwMapFiles { get; set; }

	public override bool RunTask ()
	{
		var systemRuntimeVersion = ParseTargetFrameworkVersion (TargetFrameworkVersion);
		var assemblyPaths = ResolvedAssemblies.Select (i => i.ItemSpec).Distinct ().ToList ();
		var frameworkAssemblyNames = new HashSet<string> (StringComparer.OrdinalIgnoreCase);
		Directory.CreateDirectory (OutputDirectory);
		Directory.CreateDirectory (JavaSourceOutputDirectory);
		Directory.CreateDirectory (AcwMapDirectory);
		var peReaders = new List<PEReader> ();
		var assemblies = new List<(string Name, PEReader Reader)> ();
		try {
			foreach (var path in assemblyPaths) {
				var peReader = new PEReader (File.OpenRead (path));
				peReaders.Add (peReader);
				assemblies.Add ((Path.GetFileNameWithoutExtension (path), peReader));
			}
			var generator = new TrimmableTypeMapGenerator (msg => Log.LogMessage (MessageImportance.Low, msg));
			var result = generator.Execute (assemblies, systemRuntimeVersion, frameworkAssemblyNames);
			GeneratedAssemblies = WriteAssembliesToDisk (result.GeneratedAssemblies);
			GeneratedJavaFiles = WriteJavaSourcesToDisk (result.GeneratedJavaSources);
			PerAssemblyAcwMapFiles = GeneratePerAssemblyAcwMaps (result.AllPeers);
		} finally {
			foreach (var peReader in peReaders) peReader.Dispose ();
		}
		return !Log.HasLoggedErrors;
	}

	ITaskItem [] WriteAssembliesToDisk (IReadOnlyList<GeneratedAssembly> assemblies)
	{
		var items = new List<ITaskItem> ();
		foreach (var assembly in assemblies) {
			string outputPath = Path.Combine (OutputDirectory, assembly.Name + ".dll");
			Files.CopyIfStreamChanged (assembly.Content, outputPath);
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
			if (!string.IsNullOrEmpty (dir)) Directory.CreateDirectory (dir);
			using (var sw = MemoryStreamPool.Shared.CreateStreamWriter ()) {
				sw.Write (source.Content);
				sw.Flush ();
				Files.CopyIfStreamChanged (sw.BaseStream, outputPath);
			}
			items.Add (new TaskItem (outputPath));
		}
		return items.ToArray ();
	}

	ITaskItem [] GeneratePerAssemblyAcwMaps (IReadOnlyList<JavaPeerInfo> allPeers)
	{
		var peersByAssembly = allPeers.GroupBy (p => p.AssemblyName, StringComparer.Ordinal).OrderBy (g => g.Key, StringComparer.Ordinal);
		var outputFiles = new List<ITaskItem> ();
		foreach (var group in peersByAssembly) {
			var peers = group.ToList ();
			string outputFile = Path.Combine (AcwMapDirectory, $"acw-map.{group.Key}.txt");
			using (var sw = MemoryStreamPool.Shared.CreateStreamWriter ()) {
				AcwMapWriter.Write (sw, peers);
				sw.Flush ();
				Files.CopyIfStreamChanged (sw.BaseStream, outputFile);
			}
			var item = new TaskItem (outputFile);
			item.SetMetadata ("AssemblyName", group.Key);
			outputFiles.Add (item);
		}
		return outputFiles.ToArray ();
	}

	static Version ParseTargetFrameworkVersion (string tfv)
	{
		if (tfv.Length > 0 && (tfv [0] == 'v' || tfv [0] == 'V')) tfv = tfv.Substring (1);
		if (Version.TryParse (tfv, out var version)) return version;
		throw new ArgumentException ($"Cannot parse TargetFrameworkVersion '{tfv}' as a Version.");
	}
}
