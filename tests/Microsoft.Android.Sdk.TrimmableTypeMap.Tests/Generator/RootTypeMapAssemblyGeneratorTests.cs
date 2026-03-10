using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Xunit;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.Tests;

public class RootTypeMapAssemblyGeneratorTests : FixtureTestBase
{
	static MemoryStream GenerateRootAssembly (IReadOnlyList<string> perAssemblyNames, string? assemblyName = null)
	{
		var stream = new MemoryStream ();
		var generator = new RootTypeMapAssemblyGenerator (new Version (11, 0, 0, 0));
		generator.Generate (perAssemblyNames, stream, assemblyName);
		stream.Position = 0;
		return stream;
	}

	[Fact]
	public void Generate_ProducesValidPEAssembly ()
	{
		using var stream = GenerateRootAssembly (new [] { "_App.TypeMap", "_Mono.Android.TypeMap" });
		using var pe = new PEReader (stream);
		Assert.True (pe.HasMetadata);
	}

	[Theory]
	[InlineData (null, "_Microsoft.Android.TypeMaps")]
	[InlineData ("MyRoot", "MyRoot")]
	public void Generate_AssemblyName_MatchesExpected (string? assemblyName, string expectedName)
	{
		using var stream = GenerateRootAssembly (Array.Empty<string> (), assemblyName);
		using var pe = new PEReader (stream);
		var reader = pe.GetMetadataReader ();
		var asmDef = reader.GetAssemblyDefinition ();
		Assert.Equal (expectedName, reader.GetString (asmDef.Name));
	}

	[Fact]
	public void Generate_ReferencesGenericTypeMapAssemblyTargetAttribute ()
	{
		using var stream = GenerateRootAssembly (new [] { "_App.TypeMap" });
		using var pe = new PEReader (stream);
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

	[Theory]
	[InlineData (0, 0)]
	[InlineData (3, 3)]
	public void Generate_AttributeCount_MatchesTargetCount (int targetCount, int expectedCount)
	{
		var targets = Enumerable.Range (0, targetCount).Select (i => $"_Target{i}.TypeMap").ToArray ();
		using var stream = GenerateRootAssembly (targets);
		using var pe = new PEReader (stream);
		var reader = pe.GetMetadataReader ();
		var asmAttrs = reader.GetCustomAttributes (EntityHandle.AssemblyDefinition);
		Assert.Equal (expectedCount, asmAttrs.Count ());
	}

	[Fact]
	public void Generate_AttributeBlobValues_MatchTargetNames ()
	{
		var targets = new [] { "_App.TypeMap", "_Mono.Android.TypeMap" };
		using var stream = GenerateRootAssembly (targets);
		using var pe = new PEReader (stream);
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
