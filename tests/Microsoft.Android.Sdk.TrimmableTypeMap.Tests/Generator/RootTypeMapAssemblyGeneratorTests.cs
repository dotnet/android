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
		generator.Generate (perAssemblyNames, isRelease: false, stream, assemblyName);
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
		using var stream = GenerateRootAssembly ([], assemblyName);
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

		var typeDefs = reader.TypeDefinitions
			.Select (h => reader.GetTypeDefinition (h))
			.ToList ();
		Assert.Contains (typeDefs, t =>
			reader.GetString (t.Name) == "__TypeMapAnchor");

		Assert.DoesNotContain (typeDefs, t =>
			reader.GetString (t.Name).Contains ("TypeMapAssemblyTarget"));
	}

	[Fact]
	public void Generate_EmptyList_ProducesValidAssemblyWithNoTargetAttributes ()
	{
		using var stream = GenerateRootAssembly ([]);
		using var pe = new PEReader (stream);
		var reader = pe.GetMetadataReader ();
		var targetAttrs = GetTypeMapAssemblyTargetAttributes (reader);
		Assert.Empty (targetAttrs);
	}

	[Fact]
	public void Generate_MultipleTargets_HasCorrectAttributeCount ()
	{
		string[] targets = ["_App.TypeMap", "_Mono.Android.TypeMap", "_Java.Interop.TypeMap"];
		using var stream = GenerateRootAssembly (targets);
		using var pe = new PEReader (stream);
		var reader = pe.GetMetadataReader ();
		var targetAttrs = GetTypeMapAssemblyTargetAttributes (reader);
		Assert.Equal (3, targetAttrs.Count);
	}

	[Fact]
	public void Generate_AttributeBlobValues_MatchTargetNames ()
	{
		var targets = new [] { "_App.TypeMap", "_Mono.Android.TypeMap" };
		using var stream = GenerateRootAssembly (targets);
		using var pe = new PEReader (stream);
		var reader = pe.GetMetadataReader ();

		var targetAttrs = GetTypeMapAssemblyTargetAttributes (reader);

		var attrValues = new List<string> ();
		foreach (var attr in targetAttrs) {
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

	static List<CustomAttribute> GetTypeMapAssemblyTargetAttributes (MetadataReader reader)
	{
		var result = new List<CustomAttribute> ();
		foreach (var attrHandle in reader.GetCustomAttributes (EntityHandle.AssemblyDefinition)) {
			var attr = reader.GetCustomAttribute (attrHandle);
			if (attr.Constructor.Kind == HandleKind.MemberReference) {
				var memberRef = reader.GetMemberReference ((MemberReferenceHandle)attr.Constructor);
				if (memberRef.Parent.Kind == HandleKind.TypeSpecification) {
					// TypeMapAssemblyTargetAttribute<T> is a generic type spec
					result.Add (attr);
				}
			}
		}
		return result;
	}
}
