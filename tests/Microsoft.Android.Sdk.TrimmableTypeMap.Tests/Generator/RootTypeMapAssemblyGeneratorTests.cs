using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Xunit;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.Tests;

public class RootTypeMapAssemblyGeneratorTests : FixtureTestBase, IDisposable
{
	readonly string _outputDir = CreateTempDir ();
	public void Dispose () => DeleteTempDir (_outputDir);

	string GenerateRootAssembly (IReadOnlyList<string> perAssemblyNames, string? assemblyName = null)
	{
		var outputPath = Path.Combine (_outputDir,
			(assemblyName ?? "_Microsoft.Android.TypeMaps") + ".dll");
		var generator = new RootTypeMapAssemblyGenerator (new Version (11, 0, 0, 0));
		generator.Generate (perAssemblyNames, outputPath, assemblyName);
		return outputPath;
	}

	[Fact]
	public void Generate_ProducesValidPEAssembly ()
	{
		var path = GenerateRootAssembly (new [] { "_App.TypeMap", "_Mono.Android.TypeMap" });
		Assert.True (File.Exists (path));
		using var pe = new PEReader (File.OpenRead (path));
		Assert.True (pe.HasMetadata);
	}

	[Theory]
	[InlineData (null, "_Microsoft.Android.TypeMaps")]
	[InlineData ("MyRoot", "MyRoot")]
	public void Generate_AssemblyName_MatchesExpected (string? assemblyName, string expectedName)
	{
		var path = GenerateRootAssembly (Array.Empty<string> (), assemblyName);
		using var pe = new PEReader (File.OpenRead (path));
		var reader = pe.GetMetadataReader ();
		var asmDef = reader.GetAssemblyDefinition ();
		Assert.Equal (expectedName, reader.GetString (asmDef.Name));
	}

	[Fact]
	public void Generate_ReferencesGenericTypeMapAssemblyTargetAttribute ()
	{
		var path = GenerateRootAssembly (new [] { "_App.TypeMap" });
		using var pe = new PEReader (File.OpenRead (path));
		var reader = pe.GetMetadataReader ();

		var typeRefs = reader.TypeReferences
			.Select (h => reader.GetTypeReference (h))
			.ToList ();
		Assert.Contains (typeRefs, t =>
			reader.GetString (t.Name) == "TypeMapAssemblyTargetAttribute`1" &&
			reader.GetString (t.Namespace) == "System.Runtime.InteropServices");

		Assert.Contains (typeRefs, t =>
			reader.GetString (t.Name) == "Object" &&
			reader.GetString (t.Namespace) == "Java.Lang");

		var typeDefs = reader.TypeDefinitions
			.Select (h => reader.GetTypeDefinition (h))
			.ToList ();
		Assert.DoesNotContain (typeDefs, t =>
			reader.GetString (t.Name).Contains ("TypeMapAssemblyTarget"));
	}

	[Fact]
	public void Generate_EmptyList_ProducesValidAssemblyWithNoTargetAttributes ()
	{
		var path = GenerateRootAssembly (Array.Empty<string> ());
		using var pe = new PEReader (File.OpenRead (path));
		var reader = pe.GetMetadataReader ();
		var asmAttrs = reader.GetCustomAttributes (EntityHandle.AssemblyDefinition);
		Assert.Empty (asmAttrs);
	}

	[Fact]
	public void Generate_MultipleTargets_HasCorrectAttributeCount ()
	{
		var targets = new [] { "_App.TypeMap", "_Mono.Android.TypeMap", "_Java.Interop.TypeMap" };
		var path = GenerateRootAssembly (targets);
		using var pe = new PEReader (File.OpenRead (path));
		var reader = pe.GetMetadataReader ();
		var asmAttrs = reader.GetCustomAttributes (EntityHandle.AssemblyDefinition);
		Assert.Equal (3, asmAttrs.Count ());
	}

	[Fact]
	public void Generate_AttributeBlobValues_MatchTargetNames ()
	{
		var targets = new [] { "_App.TypeMap", "_Mono.Android.TypeMap" };
		var path = GenerateRootAssembly (targets);
		using var pe = new PEReader (File.OpenRead (path));
		var reader = pe.GetMetadataReader ();

		var attrValues = new List<string> ();
		foreach (var attrHandle in reader.GetCustomAttributes (EntityHandle.AssemblyDefinition)) {
			var attr = reader.GetCustomAttribute (attrHandle);
			var blob = reader.GetBlobReader (attr.Value);

			// Custom attribute blob: prolog (2 bytes) + SerString value
			var prolog = blob.ReadUInt16 ();
			Assert.Equal (1, prolog); // ECMA-335 prolog
			var value = blob.ReadSerializedString ();
			Assert.NotNull (value);
			attrValues.Add (value!);
		}

		Assert.Equal (2, attrValues.Count);
		Assert.Contains ("_App.TypeMap", attrValues);
		Assert.Contains ("_Mono.Android.TypeMap", attrValues);
	}
}
