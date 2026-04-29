using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using Xunit;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.Tests;

public class RootTypeMapAssemblyGeneratorTests : FixtureTestBase
{

	static MemoryStream GenerateRootAssembly (IReadOnlyList<string> perAssemblyNames, bool useSharedTypemapUniverse = false, string? assemblyName = null, int maxArrayRank = 0)
	{
		var stream = new MemoryStream ();
		var generator = new RootTypeMapAssemblyGenerator (new Version (11, 0, 0, 0));
		generator.Generate (perAssemblyNames, useSharedTypemapUniverse, stream, assemblyName, maxArrayRank: maxArrayRank);
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
		using var stream = GenerateRootAssembly ([], assemblyName: assemblyName);
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

		var attrValues = GetTypeMapAssemblyTargetAttributeTargets (reader)
			.Select (target => target.TargetName)
			.ToList ();

		Assert.Equal (2, attrValues.Count);
		Assert.Contains ("_App.TypeMap", attrValues);
		Assert.Contains ("_Mono.Android.TypeMap", attrValues);
	}

	[Fact]
	public void Generate_AggregateMode_TargetAttributesUsePerAssemblyAnchors ()
	{
		var targets = new [] { "_App.TypeMap", "_Mono.Android.TypeMap" };
		using var stream = GenerateRootAssembly (targets, useSharedTypemapUniverse: false);
		using var pe = new PEReader (stream);
		var reader = pe.GetMetadataReader ();

		var targetAttributes = GetTypeMapAssemblyTargetAttributeTargets (reader);

		Assert.Equal (new [] {
			("_App.TypeMap", "_App.TypeMap"),
			("_Mono.Android.TypeMap", "_Mono.Android.TypeMap"),
		}, targetAttributes);
	}

	[Fact]
	public void Generate_MergedMode_TargetAttributesUseSharedAnchor ()
	{
		var targets = new [] { "_App.TypeMap", "_Mono.Android.TypeMap" };
		using var stream = GenerateRootAssembly (targets, useSharedTypemapUniverse: true);
		using var pe = new PEReader (stream);
		var reader = pe.GetMetadataReader ();

		var targetAttributes = GetTypeMapAssemblyTargetAttributeTargets (reader);

		Assert.Equal (new [] {
			("_App.TypeMap", "Mono.Android"),
			("_Mono.Android.TypeMap", "Mono.Android"),
		}, targetAttributes);
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

	static List<(string TargetName, string GenericArgumentScope)> GetTypeMapAssemblyTargetAttributeTargets (MetadataReader reader)
	{
		var result = new List<(string TargetName, string GenericArgumentScope)> ();
		foreach (var attr in GetTypeMapAssemblyTargetAttributes (reader)) {
			var targetName = GetTypeMapAssemblyTargetName (reader, attr);
			var memberRef = reader.GetMemberReference ((MemberReferenceHandle)attr.Constructor);
			var typeSpec = reader.GetTypeSpecification ((TypeSpecificationHandle)memberRef.Parent);
			var blob = reader.GetBlobReader (typeSpec.Signature);
			Assert.Equal (0x15, blob.ReadByte ()); // ELEMENT_TYPE_GENERICINST
			Assert.Equal (0x12, blob.ReadByte ()); // ELEMENT_TYPE_CLASS
			blob.ReadCompressedInteger (); // TypeMapAssemblyTargetAttribute`1 type
			Assert.Equal (1, blob.ReadCompressedInteger ());
			Assert.Equal (0x12, blob.ReadByte ()); // ELEMENT_TYPE_CLASS
			var targetType = DecodeTypeDefOrRefOrSpec (blob.ReadCompressedInteger ());
			result.Add ((targetName, GetResolutionScopeName (reader, targetType)));
		}
		return result;
	}

	static string GetTypeMapAssemblyTargetName (MetadataReader reader, CustomAttribute attr)
	{
		var blob = reader.GetBlobReader (attr.Value);
		var prolog = blob.ReadUInt16 ();
		Assert.Equal (1, prolog); // ECMA-335 custom attribute prolog
		var value = blob.ReadSerializedString ();
		if (value is null) {
			throw new InvalidOperationException ("TypeMapAssemblyTargetAttribute value must not be null.");
		}
		return value;
	}

	static EntityHandle DecodeTypeDefOrRefOrSpec (int codedIndex)
	{
		var row = codedIndex >> 2;
		return (codedIndex & 0x3) switch {
			0 => MetadataTokens.TypeDefinitionHandle (row),
			1 => MetadataTokens.TypeReferenceHandle (row),
			2 => MetadataTokens.TypeSpecificationHandle (row),
			_ => throw new InvalidOperationException ($"Invalid TypeDefOrRefOrSpec coded index: {codedIndex}"),
		};
	}

	static string GetResolutionScopeName (MetadataReader reader, EntityHandle handle)
	{
		if (handle.Kind == HandleKind.TypeDefinition) {
			return reader.GetString (reader.GetAssemblyDefinition ().Name);
		}
		if (handle.Kind != HandleKind.TypeReference) {
			throw new InvalidOperationException ($"Unexpected type handle kind: {handle.Kind}");
		}
		var typeReference = reader.GetTypeReference ((TypeReferenceHandle)handle);
		var scope = typeReference.ResolutionScope;
		return scope.Kind switch {
			HandleKind.AssemblyReference => reader.GetString (reader.GetAssemblyReference ((AssemblyReferenceHandle)scope).Name),
			HandleKind.ModuleDefinition => reader.GetString (reader.GetAssemblyDefinition ().Name),
			HandleKind.TypeReference => GetResolutionScopeName (reader, (TypeReferenceHandle)scope),
			_ => throw new InvalidOperationException ($"Unexpected resolution scope kind: {scope.Kind}"),
		};
	}

	[Theory]
	[InlineData (true)]
	[InlineData (false)]
	public void Generate_BothMergeModes_ProduceValidPEAssembly (bool useSharedTypemapUniverse)
	{
		using var stream = GenerateRootAssembly (["_App.TypeMap", "_Mono.Android.TypeMap"], useSharedTypemapUniverse);
		using var pe = new PEReader (stream);
		Assert.True (pe.HasMetadata);

		var reader = pe.GetMetadataReader ();

		// Both modes should have TypeMapLoader type in the correct namespace, with public visibility and Initialize method
		var typeDefs = reader.TypeDefinitions
			.Select (h => reader.GetTypeDefinition (h))
			.ToList ();
		var typeMapLoader = typeDefs.Single (t => reader.GetString (t.Name) == "TypeMapLoader");
		Assert.Equal ("Microsoft.Android.Runtime", reader.GetString (typeMapLoader.Namespace));
		Assert.True (typeMapLoader.Attributes.HasFlag (System.Reflection.TypeAttributes.Public));

		// Both modes should have assembly target attributes
		var targetAttrs = GetTypeMapAssemblyTargetAttributes (reader);
		Assert.Equal (2, targetAttrs.Count);
	}

	[Fact]
	public void Generate_MergedMode_ReferencesRootAnchorOnly ()
	{
		using var stream = GenerateRootAssembly (["_App.TypeMap", "_Mono.Android.TypeMap"], useSharedTypemapUniverse: true);
		using var pe = new PEReader (stream);
		var reader = pe.GetMetadataReader ();

		// In merged mode, the root assembly's __TypeMapAnchor is used.
		// The per-assembly anchors should NOT be referenced directly (no assembly refs for per-assembly typemaps).
		var asmRefs = reader.AssemblyReferences
			.Select (h => reader.GetString (reader.GetAssemblyReference (h).Name))
			.ToList ();
		Assert.DoesNotContain ("_App.TypeMap", asmRefs);
		Assert.DoesNotContain ("_Mono.Android.TypeMap", asmRefs);
	}

	[Fact]
	public void Generate_AggregateMode_ReferencesPerAssemblyAnchors ()
	{
		using var stream = GenerateRootAssembly (["_App.TypeMap", "_Mono.Android.TypeMap"], useSharedTypemapUniverse: false);
		using var pe = new PEReader (stream);
		var reader = pe.GetMetadataReader ();

		// In aggregate mode, the root assembly should reference each per-assembly typemap.
		var asmRefs = reader.AssemblyReferences
			.Select (h => reader.GetString (reader.GetAssemblyReference (h).Name))
			.ToList ();
		Assert.Contains ("_App.TypeMap", asmRefs);
		Assert.Contains ("_Mono.Android.TypeMap", asmRefs);
	}

	[Fact]
	public void Generate_MergedMode_HasIgnoresAccessChecksToMonoAndroidOnly ()
	{
		using var stream = GenerateRootAssembly (["_App.TypeMap"], useSharedTypemapUniverse: true);
		using var pe = new PEReader (stream);
		var reader = pe.GetMetadataReader ();

		var accessAttrs = GetIgnoresAccessChecksToValues (reader);
		Assert.Contains ("Mono.Android", accessAttrs);
		Assert.DoesNotContain ("_App.TypeMap", accessAttrs);
	}

	[Fact]
	public void Generate_AggregateMode_HasIgnoresAccessChecksToAllAssemblies ()
	{
		using var stream = GenerateRootAssembly (["_App.TypeMap", "_Mono.Android.TypeMap"], useSharedTypemapUniverse: false);
		using var pe = new PEReader (stream);
		var reader = pe.GetMetadataReader ();

		var accessAttrs = GetIgnoresAccessChecksToValues (reader);
		Assert.Contains ("Mono.Android", accessAttrs);
		Assert.Contains ("_App.TypeMap", accessAttrs);
		Assert.Contains ("_Mono.Android.TypeMap", accessAttrs);
	}

	static List<string> GetIgnoresAccessChecksToValues (MetadataReader reader)
	{
		var result = new List<string> ();
		foreach (var attrHandle in reader.GetCustomAttributes (EntityHandle.AssemblyDefinition)) {
			var attr = reader.GetCustomAttribute (attrHandle);

			string? typeName = null;
			if (attr.Constructor.Kind == HandleKind.MemberReference) {
				var memberRef = reader.GetMemberReference ((MemberReferenceHandle) attr.Constructor);
				if (memberRef.Parent.Kind == HandleKind.TypeReference) {
					typeName = reader.GetString (reader.GetTypeReference ((TypeReferenceHandle) memberRef.Parent).Name);
				} else if (memberRef.Parent.Kind == HandleKind.TypeDefinition) {
					typeName = reader.GetString (reader.GetTypeDefinition ((TypeDefinitionHandle) memberRef.Parent).Name);
				}
			} else if (attr.Constructor.Kind == HandleKind.MethodDefinition) {
				var methodDef = reader.GetMethodDefinition ((MethodDefinitionHandle) attr.Constructor);
				typeName = reader.GetString (reader.GetTypeDefinition (methodDef.GetDeclaringType ()).Name);
			}

			if (typeName != "IgnoresAccessChecksToAttribute") {
				continue;
			}
			var blob = reader.GetBlobReader (attr.Value);
			blob.ReadUInt16 (); // prolog
			var value = blob.ReadSerializedString ();
			if (value is not null) {
				result.Add (value);
			}
		}
		return result;
	}

	[Fact]
	public void Generate_MergedMode_WithArrays_ProducesValidPEAssembly ()
	{
		using var stream = GenerateRootAssembly (["_App.TypeMap", "_Mono.Android.TypeMap"],
			useSharedTypemapUniverse: true, maxArrayRank: 3);
		using var pe = new PEReader (stream);
		Assert.True (pe.HasMetadata);
	}

	[Fact]
	public void Generate_MergedMode_WithArrays_ReferencesPerAsmRankSentinels ()
	{
		using var stream = GenerateRootAssembly (["_App.TypeMap", "_Mono.Android.TypeMap"],
			useSharedTypemapUniverse: true, maxArrayRank: 2);
		using var pe = new PEReader (stream);
		var reader = pe.GetMetadataReader ();

		var typeRefNames = reader.TypeReferences
			.Select (h => reader.GetString (reader.GetTypeReference (h).Name))
			.ToList ();
		Assert.Contains ("__ArrayMapRank1", typeRefNames);
		Assert.Contains ("__ArrayMapRank2", typeRefNames);
		Assert.DoesNotContain ("__ArrayMapRank3", typeRefNames);
	}

	[Fact]
	public void Generate_MergedMode_WithArrays_NoPerAsmAccessNeeded ()
	{
		using var stream = GenerateRootAssembly (["_App.TypeMap", "_Mono.Android.TypeMap"],
			useSharedTypemapUniverse: true, maxArrayRank: 3);
		using var pe = new PEReader (stream);
		var reader = pe.GetMetadataReader ();

		var accessAttrs = GetIgnoresAccessChecksToValues (reader);
		Assert.Contains ("Mono.Android", accessAttrs);
		// Shared-mode root never needs per-asm internal access — rank anchors live in Mono.Android.
		Assert.DoesNotContain ("_App.TypeMap", accessAttrs);
	}
}
