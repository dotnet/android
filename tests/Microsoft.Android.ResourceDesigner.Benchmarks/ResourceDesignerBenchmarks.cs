extern alias CecilTasks;

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Collections;
using System.Reflection;
using System.Runtime.Loader;
using BenchmarkDotNet.Attributes;
using Microsoft.Build.Utilities;
using Mono.Cecil;
using Mono.Cecil.Cil;
using CecilTask = CecilTasks::Xamarin.Android.Tasks.GenerateResourceDesignerAssembly;
using MetadataTask = Microsoft.Android.Tasks.GenerateResourceDesignerAssembly;

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
	string cecilOutput = "";
	string metadataOutput = "";
	readonly BenchmarkBuildEngine engine = new ();
	readonly TaskItem [] frameworkDirectories = [
		new TaskItem (Path.GetDirectoryName (typeof (object).Assembly.Location) ?? throw new InvalidOperationException ()),
	];

	[Params (true, false)]
	public bool IsApplication { get; set; }

	[Params (GenerationScenario.Generate, GenerationScenario.CompareUnchanged, GenerationScenario.UpToDate)]
	public GenerationScenario Scenario { get; set; }

	[GlobalSetup]
	public void Setup ()
	{
		directory = Path.Combine (Path.GetTempPath (), "ResourceDesignerBenchmarks", Guid.NewGuid ().ToString ("N"));
		Directory.CreateDirectory (Path.Combine (directory, "cecil"));
		Directory.CreateDirectory (Path.Combine (directory, "metadata"));
		rtxt = Path.Combine (directory, "R.txt");
		caseMap = Path.Combine (directory, "case_map.txt");
		// Optional caller-supplied real build input; the default is the captured MAUI resource set.
		var input = Environment.GetEnvironmentVariable ("RESOURCE_DESIGNER_RTXT")
			?? Path.Combine (AppContext.BaseDirectory, "Resources", "R.txt");
		File.Copy (input, rtxt);
		var inputMap = Environment.GetEnvironmentVariable ("RESOURCE_DESIGNER_CASE_MAP");
		if (inputMap != null)
			File.Copy (inputMap, caseMap);
		cecilOutput = Path.Combine (directory, "cecil", "_Microsoft.Android.Resource.Designer.dll");
		metadataOutput = Path.Combine (directory, "metadata", "_Microsoft.Android.Resource.Designer.dll");
		GenerateCecil ();
		GenerateMetadata ();
		VerifyEquivalentAssemblies (cecilOutput, metadataOutput);
		if (IsApplication)
			VerifyRuntimeValues (cecilOutput, metadataOutput);
		Console.WriteLine ($"Verified {File.ReadLines (rtxt).Count ()} resource lines, IsApplication={IsApplication}: " +
			$"Cecil={new FileInfo (cecilOutput).Length} bytes, metadata={new FileInfo (metadataOutput).Length} bytes.");
		var newestOutput = new [] { File.GetLastWriteTimeUtc (cecilOutput), File.GetLastWriteTimeUtc (metadataOutput) }.Max ();
		File.SetLastWriteTimeUtc (rtxt, Scenario == GenerationScenario.UpToDate ? newestOutput.AddSeconds (-1) : newestOutput.AddSeconds (1));
	}

	[GlobalCleanup]
	public void Cleanup ()
	{
		if (directory.Length > 0 && Directory.Exists (directory))
			Directory.Delete (directory, recursive: true);
	}

	[Benchmark (Baseline = true)]
	public void Cecil ()
	{
		if (Scenario == GenerationScenario.Generate)
			File.Delete (cecilOutput);
		GenerateCecil ();
	}

	[Benchmark]
	public void Metadata ()
	{
		if (Scenario == GenerationScenario.Generate)
			File.Delete (metadataOutput);
		GenerateMetadata ();
	}

	void GenerateCecil ()
	{
		var task = new CecilTask {
			BuildEngine = engine,
			RTxtFile = new TaskItem (rtxt),
			OutputFile = new TaskItem (cecilOutput),
			CaseMapFile = caseMap,
			ProjectDir = directory,
			ResourceDirectory = directory,
			FrameworkDirectories = frameworkDirectories,
			Deterministic = true,
			IsApplication = IsApplication,
			AssemblyName = "TestApp",
		};
		if (!task.Execute ())
			throw new InvalidOperationException ("Cecil resource designer generation failed.");
	}

	void GenerateMetadata ()
	{
		var task = new MetadataTask {
			BuildEngine = engine,
			RTxtFile = new TaskItem (rtxt),
			OutputFile = new TaskItem (metadataOutput),
			CaseMapFile = caseMap,
			ProjectDir = directory,
			ResourceDirectory = directory,
			FrameworkDirectories = frameworkDirectories,
			Deterministic = true,
			IsApplication = IsApplication,
			AssemblyName = "TestApp",
		};
		if (!task.Execute ())
			throw new InvalidOperationException ("Metadata resource designer generation failed.");
	}

	static void VerifyEquivalentAssemblies (string cecilPath, string metadataPath)
	{
		using var cecil = AssemblyDefinition.ReadAssembly (cecilPath);
		using var metadata = AssemblyDefinition.ReadAssembly (metadataPath);
		VerifyGeneratorVersion (cecil, typeof (CecilTask).Assembly.GetName ().Version);
		VerifyGeneratorVersion (metadata, typeof (MetadataTask).Assembly.GetName ().Version);
		if (!cecil.Name.PublicKey.SequenceEqual (metadata.Name.PublicKey))
			throw new InvalidOperationException ("The strong-name public keys differ.");
		var expected = DescribeAssembly (cecil).OrderBy (line => line, StringComparer.Ordinal).ToArray ();
		var actual = DescribeAssembly (metadata).OrderBy (line => line, StringComparer.Ordinal).ToArray ();
		if (!expected.SequenceEqual (actual)) {
			throw new InvalidOperationException (
				$"The emitted contracts differ.\nCecil only:\n{string.Join ("\n", expected.Except (actual).Take (10))}" +
				$"\nMetadata only:\n{string.Join ("\n", actual.Except (expected).Take (10))}");
		}
	}

	static IEnumerable<string> DescribeAssembly (AssemblyDefinition assembly)
	{
		yield return $"assembly:{assembly.Name.Name}:{assembly.Name.Version}:{assembly.Name.Attributes}:{assembly.Name.HashAlgorithm}";
		foreach (var attribute in assembly.CustomAttributes)
			yield return DescribeAttribute ("assembly", attribute);
		foreach (var type in assembly.MainModule.Types.SelectMany (Flatten)) {
			yield return $"type:{type.FullName}:{type.Attributes}:{type.BaseType?.FullName}";
			foreach (var attribute in type.CustomAttributes)
				yield return DescribeAttribute (type.FullName, attribute);
			foreach (var field in type.Fields)
				yield return $"field:{type.FullName}:{field.Name}:{field.Attributes}:{field.FieldType.FullName}:{field.Constant}";
			foreach (var property in type.Properties)
				yield return $"property:{type.FullName}:{property.Name}:{property.Attributes}:{property.PropertyType.FullName}:{property.GetMethod?.Name}";
			foreach (var method in type.Methods) {
				yield return $"method:{type.FullName}:{method.Name}:{method.Attributes}:{method.ImplAttributes}:{method.ReturnType.FullName}";
				if (method.Name != ".cctor")
					yield return $"il:{type.FullName}:{method.Name}:{string.Join (";", method.Body.Instructions.Select (DescribeInstruction))}";
			}
		}
	}

	static void VerifyRuntimeValues (string cecilPath, string metadataPath)
	{
		var cecilContext = new AssemblyLoadContext ("CecilDesigner", isCollectible: true);
		var metadataContext = new AssemblyLoadContext ("MetadataDesigner", isCollectible: true);
		try {
			var cecil = cecilContext.LoadFromAssemblyPath (cecilPath);
			var metadata = metadataContext.LoadFromAssemblyPath (metadataPath);
			foreach (var type in cecil.GetTypes ()) {
				var emittedType = metadata.GetType (type.FullName ?? throw new InvalidOperationException ())
					?? throw new InvalidOperationException ($"Missing emitted type {type}.");
				foreach (var property in type.GetProperties (BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
					VerifyValue (property.Name, property.GetValue (null), emittedType.GetProperty (property.Name)?.GetValue (null));
				foreach (var field in type.GetFields (BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
					VerifyValue (field.Name, field.GetValue (null), emittedType.GetField (field.Name)?.GetValue (null));
			}
		} finally {
			cecilContext.Unload ();
			metadataContext.Unload ();
		}
	}

	static void VerifyValue (string name, object? expected, object? actual)
	{
		if (!StructuralComparisons.StructuralEqualityComparer.Equals (expected, actual))
			throw new InvalidOperationException ($"Emitted resource {name} has different runtime values.");
	}

	static IEnumerable<TypeDefinition> Flatten (TypeDefinition type)
	{
		yield return type;
		foreach (var nested in type.NestedTypes.SelectMany (Flatten))
			yield return nested;
	}

	static void VerifyGeneratorVersion (AssemblyDefinition assembly, Version? version)
	{
		foreach (var type in assembly.MainModule.Types.Where (type => type.Name is "Resource" or "ResourceConstant")) {
			var generated = type.CustomAttributes.Single (attribute => attribute.AttributeType.FullName == "System.CodeDom.Compiler.GeneratedCodeAttribute");
			if ((string) generated.ConstructorArguments [1].Value != version?.ToString ())
				throw new InvalidOperationException ("GeneratedCodeAttribute does not identify the emitting task assembly's version.");
		}
	}

	static string DescribeAttribute (string parent, CustomAttribute attribute)
	{
		// Moving the emitter changes its assembly version, which is recorded in GeneratedCodeAttribute.
		var arguments = attribute.AttributeType.FullName == "System.CodeDom.Compiler.GeneratedCodeAttribute"
			? attribute.ConstructorArguments.Take (1) : attribute.ConstructorArguments;
		return $"attribute:{parent}:{attribute.AttributeType.FullName}:" +
			string.Join (";", arguments.Select (argument => $"{argument.Type.FullName}={argument.Value}")) +
			":" + string.Join (";", attribute.Properties.Select (property => $"{property.Name}={property.Argument.Value}"));
	}

	static string DescribeInstruction (Instruction instruction) => instruction.OpCode.Code switch {
		Code.Ldc_I4_M1 => "ldc.i4:-1",
		Code.Ldc_I4_0 => "ldc.i4:0",
		Code.Ldc_I4_1 => "ldc.i4:1",
		Code.Ldc_I4_2 => "ldc.i4:2",
		Code.Ldc_I4_3 => "ldc.i4:3",
		Code.Ldc_I4_4 => "ldc.i4:4",
		Code.Ldc_I4_5 => "ldc.i4:5",
		Code.Ldc_I4_6 => "ldc.i4:6",
		Code.Ldc_I4_7 => "ldc.i4:7",
		Code.Ldc_I4_8 => "ldc.i4:8",
		Code.Ldc_I4_S or Code.Ldc_I4 => $"ldc.i4:{instruction.Operand}",
		_ => $"{instruction.OpCode}:{instruction.Operand}",
	};
}
