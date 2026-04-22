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

	static MemoryStream GenerateRootAssembly (IReadOnlyList<string> perAssemblyNames, bool mergeAssemblyTypeMaps = false, string? assemblyName = null)
	{
		var stream = new MemoryStream ();
		var generator = new RootTypeMapAssemblyGenerator (new Version (11, 0, 0, 0));
		generator.Generate (perAssemblyNames, mergeAssemblyTypeMaps, stream, assemblyName);
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

	[Theory]
	[InlineData (true)]
	[InlineData (false)]
	public void Generate_BothMergeModes_ProduceValidPEAssembly (bool mergeAssemblyTypeMaps)
	{
		using var stream = GenerateRootAssembly (["_App.TypeMap", "_Mono.Android.TypeMap"], mergeAssemblyTypeMaps);
		using var pe = new PEReader (stream);
		Assert.True (pe.HasMetadata);

		var reader = pe.GetMetadataReader ();

		// Both modes should have StartupHook type with Initialize method
		var typeDefs = reader.TypeDefinitions
			.Select (h => reader.GetTypeDefinition (h))
			.ToList ();
		Assert.Contains (typeDefs, t => reader.GetString (t.Name) == "StartupHook");

		// Both modes should have assembly target attributes
		var targetAttrs = GetTypeMapAssemblyTargetAttributes (reader);
		Assert.Equal (2, targetAttrs.Count);
	}

	[Fact]
	public void Generate_MergedMode_ReferencesRootAnchorOnly ()
	{
		using var stream = GenerateRootAssembly (["_App.TypeMap", "_Mono.Android.TypeMap"], mergeAssemblyTypeMaps: true);
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
		using var stream = GenerateRootAssembly (["_App.TypeMap", "_Mono.Android.TypeMap"], mergeAssemblyTypeMaps: false);
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
		using var stream = GenerateRootAssembly (["_App.TypeMap"], mergeAssemblyTypeMaps: true);
		using var pe = new PEReader (stream);
		var reader = pe.GetMetadataReader ();

		var accessAttrs = GetIgnoresAccessChecksToValues (reader);
		Assert.Contains ("Mono.Android", accessAttrs);
		Assert.DoesNotContain ("_App.TypeMap", accessAttrs);
	}

	[Fact]
	public void Generate_AggregateMode_HasIgnoresAccessChecksToAllAssemblies ()
	{
		using var stream = GenerateRootAssembly (["_App.TypeMap", "_Mono.Android.TypeMap"], mergeAssemblyTypeMaps: false);
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
}
