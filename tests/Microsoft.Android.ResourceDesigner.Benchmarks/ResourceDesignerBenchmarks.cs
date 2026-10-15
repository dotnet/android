using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.Loader;
using BenchmarkDotNet.Attributes;
using Microsoft.Android.Tasks;
using Microsoft.Build.Utilities;

namespace Microsoft.Android.ResourceDesigner.Benchmarks;

public enum GenerationScenario
{
	Generate,
	CompareUnchanged,
	UpToDate,
}

[MemoryDiagnoser]
public class ResourceDesignerBenchmarks
{
	string directory = "";
	string rtxt = "";
	string caseMap = "";
	string output = "";
	readonly BenchmarkBuildEngine engine = new ();

	[Params (true, false)]
	public bool IsApplication { get; set; }

	[Params (GenerationScenario.Generate, GenerationScenario.CompareUnchanged, GenerationScenario.UpToDate)]
	public GenerationScenario Scenario { get; set; }

	[GlobalSetup]
	public void Setup ()
	{
		directory = Path.Combine (Path.GetTempPath (), "ResourceDesignerBenchmarks", Guid.NewGuid ().ToString ("N"));
		Directory.CreateDirectory (directory);
		rtxt = Path.Combine (directory, "R.txt");
		caseMap = Path.Combine (directory, "case_map.txt");
		// Optional caller-supplied real build input; the default is the captured MAUI resource set.
		var input = Environment.GetEnvironmentVariable ("RESOURCE_DESIGNER_RTXT")
			?? Path.Combine (AppContext.BaseDirectory, "Resources", "R.txt");
		File.Copy (input, rtxt);
		var inputMap = Environment.GetEnvironmentVariable ("RESOURCE_DESIGNER_CASE_MAP");
		if (inputMap != null)
			File.Copy (inputMap, caseMap);
		output = Path.Combine (directory, "_Microsoft.Android.Resource.Designer.dll");
		GenerateMetadata ();
		VerifyAssembly ();
		Console.WriteLine ($"Verified {File.ReadLines (rtxt).Count ()} resource lines, IsApplication={IsApplication}: " +
			$"metadata={new FileInfo (output).Length} bytes.");
		var outputTime = File.GetLastWriteTimeUtc (output);
		File.SetLastWriteTimeUtc (rtxt, Scenario == GenerationScenario.UpToDate ? outputTime.AddSeconds (-1) : outputTime.AddSeconds (1));
	}

	[GlobalCleanup]
	public void Cleanup ()
	{
		if (directory.Length > 0 && Directory.Exists (directory))
			Directory.Delete (directory, recursive: true);
	}

	[Benchmark]
	public void Metadata ()
	{
		if (Scenario == GenerationScenario.Generate)
			File.Delete (output);
		GenerateMetadata ();
	}

	void GenerateMetadata ()
	{
		var task = new GenerateResourceDesignerAssembly {
			BuildEngine = engine,
			RTxtFile = new TaskItem (rtxt),
			OutputFile = new TaskItem (output),
			CaseMapFile = caseMap,
			ProjectDir = directory,
			ResourceDirectory = directory,
			Deterministic = true,
			IsApplication = IsApplication,
			AssemblyName = "TestApp",
		};
		if (!task.Execute ())
			throw new InvalidOperationException ("Metadata resource designer generation failed.");
	}

	void VerifyAssembly ()
	{
		using var stream = File.OpenRead (output);
		using var pe = new PEReader (stream);
		var reader = pe.GetMetadataReader ();
		var assembly = reader.GetAssemblyDefinition ();
		if (reader.GetString (assembly.Name) != Path.GetFileNameWithoutExtension (output) ||
			assembly.Version != new Version (1, 0, 0, 0) || reader.GetBlobBytes (assembly.PublicKey).Length != 160)
			throw new InvalidOperationException ("Unexpected resource designer assembly identity.");
		if (pe.PEHeaders.CoffHeader.TimeDateStamp != 0 || reader.GetGuid (reader.GetModuleDefinition ().Mvid) == Guid.Empty)
			throw new InvalidOperationException ("Missing deterministic resource designer metadata.");
		var types = reader.TypeDefinitions.Select (handle => reader.GetString (reader.GetTypeDefinition (handle).Name)).ToArray ();
		if (!types.Contains ("Resource") || types.Contains ("ResourceConstant") != IsApplication)
			throw new InvalidOperationException ("Unexpected resource designer types.");
		if (!IsApplication)
			return;

		var context = new AssemblyLoadContext ("MetadataDesigner", isCollectible: true);
		try {
			var loaded = context.LoadFromAssemblyPath (output);
			foreach (var type in loaded.GetTypes ()) {
				foreach (var property in type.GetProperties (BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
					VerifyValue (property.GetValue (null));
				foreach (var field in type.GetFields (BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
					VerifyValue (field.GetValue (null));
			}
		} finally {
			context.Unload ();
		}
	}

	static void VerifyValue (object? value)
	{
		if (value is not int && value is not int [])
			throw new InvalidOperationException ("Unexpected resource designer value.");
	}
}
