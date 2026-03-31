#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
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
// TODO(#10792): populate with framework assembly names to skip JCW generation for pre-compiled framework types
var frameworkAssemblyNames = new HashSet<string> (StringComparer.OrdinalIgnoreCase);

Directory.CreateDirectory (OutputDirectory);
Directory.CreateDirectory (JavaSourceOutputDirectory);
Directory.CreateDirectory (AcwMapDirectory);

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

var generator = new TrimmableTypeMapGenerator (msg => Log.LogMessage (MessageImportance.Low, msg));
result = generator.Execute (assemblies, systemRuntimeVersion, frameworkAssemblyNames);

GeneratedAssemblies = WriteAssembliesToDisk (result.GeneratedAssemblies, assemblyPaths);
GeneratedJavaFiles = WriteJavaSourcesToDisk (result.GeneratedJavaSources);
PerAssemblyAcwMapFiles = GeneratePerAssemblyAcwMaps (result.AllPeers);
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

ITaskItem [] GeneratePerAssemblyAcwMaps (IReadOnlyList<JavaPeerInfo> allPeers)
{
var peersByAssembly = allPeers
.GroupBy (p => p.AssemblyName, StringComparer.Ordinal)
.OrderBy (g => g.Key, StringComparer.Ordinal);
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
if (tfv.Length > 0 && (tfv [0] == 'v' || tfv [0] == 'V')) {
tfv = tfv.Substring (1);
}
if (Version.TryParse (tfv, out var version)) {
return version;
}
throw new ArgumentException ($"Cannot parse TargetFrameworkVersion '{tfv}' as a Version.");
}
}
