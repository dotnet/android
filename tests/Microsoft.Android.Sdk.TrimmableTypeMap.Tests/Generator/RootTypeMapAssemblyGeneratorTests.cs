using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Xunit;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.Tests;

public class RootTypeMapAssemblyGeneratorTests
{
	string GenerateRootAssembly (IReadOnlyList<string> perAssemblyNames, string? assemblyName = null)
	{
		var outputPath = Path.Combine (Path.GetTempPath (), $"root-typemap-{Guid.NewGuid ():N}",
			(assemblyName ?? "_Microsoft.Android.TypeMaps") + ".dll");
		var generator = new RootTypeMapAssemblyGenerator (new Version (11, 0, 0, 0));
		generator.Generate (perAssemblyNames, outputPath, assemblyName);
		return outputPath;
	}

	static void CleanUp (string path)
	{
		var dir = Path.GetDirectoryName (path);
		if (dir != null && Directory.Exists (dir))
			try { Directory.Delete (dir, true); } catch { }
	}

	[Fact]
	public void Generate_ProducesValidPEAssembly ()
	{
		var path = GenerateRootAssembly (new [] { "_App.TypeMap", "_Mono.Android.TypeMap" });
		try {
			Assert.True (File.Exists (path));
			using var pe = new PEReader (File.OpenRead (path));
			Assert.True (pe.HasMetadata);
		} finally {
			CleanUp (path);
		}
	}

	[Fact]
	public void Generate_DefaultAssemblyName ()
	{
		var path = GenerateRootAssembly (Array.Empty<string> ());
		try {
			using var pe = new PEReader (File.OpenRead (path));
			var reader = pe.GetMetadataReader ();
			var asmDef = reader.GetAssemblyDefinition ();
			Assert.Equal ("_Microsoft.Android.TypeMaps", reader.GetString (asmDef.Name));
		} finally {
			CleanUp (path);
		}
	}

	[Fact]
	public void Generate_CustomAssemblyName ()
	{
		var path = GenerateRootAssembly (Array.Empty<string> (), "MyRoot");
		try {
			using var pe = new PEReader (File.OpenRead (path));
			var reader = pe.GetMetadataReader ();
			var asmDef = reader.GetAssemblyDefinition ();
			Assert.Equal ("MyRoot", reader.GetString (asmDef.Name));
		} finally {
			CleanUp (path);
		}
	}

	[Fact]
	public void Generate_ReferencesGenericTypeMapAssemblyTargetAttribute ()
	{
		var path = GenerateRootAssembly (new [] { "_App.TypeMap" });
		try {
			using var pe = new PEReader (File.OpenRead (path));
			var reader = pe.GetMetadataReader ();

			// The attribute type is referenced (not defined) — look for TypeRef
			var typeRefs = reader.TypeReferences
				.Select (h => reader.GetTypeReference (h))
				.ToList ();
			Assert.Contains (typeRefs, t =>
				reader.GetString (t.Name) == "TypeMapAssemblyTargetAttribute`1" &&
				reader.GetString (t.Namespace) == "System.Runtime.InteropServices");

			// Java.Lang.Object must also be referenced (generic type argument)
			Assert.Contains (typeRefs, t =>
				reader.GetString (t.Name) == "Object" &&
				reader.GetString (t.Namespace) == "Java.Lang");

			// No TypeDefinition for the attribute (it's external)
			var typeDefs = reader.TypeDefinitions
				.Select (h => reader.GetTypeDefinition (h))
				.ToList ();
			Assert.DoesNotContain (typeDefs, t =>
				reader.GetString (t.Name).Contains ("TypeMapAssemblyTarget"));
		} finally {
			CleanUp (path);
		}
	}

	[Fact]
	public void Generate_AttributeCtorIsOnGenericTypeSpec ()
	{
		var path = GenerateRootAssembly (new [] { "_App.TypeMap" });
		try {
			using var pe = new PEReader (File.OpenRead (path));
			var reader = pe.GetMetadataReader ();

			var attr = reader.GetCustomAttribute (
				reader.GetCustomAttributes (EntityHandle.AssemblyDefinition).First ());

			// The ctor should be a MemberReference (on a TypeSpec), not a MethodDefinition
			Assert.Equal (HandleKind.MemberReference, attr.Constructor.Kind);

			var memberRef = reader.GetMemberReference ((MemberReferenceHandle) attr.Constructor);
			// Parent should be a TypeSpec (closed generic)
			Assert.Equal (HandleKind.TypeSpecification, memberRef.Parent.Kind);
		} finally {
			CleanUp (path);
		}
	}

	[Fact]
	public void Generate_EmptyList_ProducesValidAssemblyWithNoTargetAttributes ()
	{
		var path = GenerateRootAssembly (Array.Empty<string> ());
		try {
			using var pe = new PEReader (File.OpenRead (path));
			var reader = pe.GetMetadataReader ();
			var asmAttrs = reader.GetCustomAttributes (EntityHandle.AssemblyDefinition);
			Assert.Empty (asmAttrs);
		} finally {
			CleanUp (path);
		}
	}

	[Fact]
	public void Generate_MultipleTargets_HasCorrectAttributeCount ()
	{
		var targets = new [] { "_App.TypeMap", "_Mono.Android.TypeMap", "_Java.Interop.TypeMap" };
		var path = GenerateRootAssembly (targets);
		try {
			using var pe = new PEReader (File.OpenRead (path));
			var reader = pe.GetMetadataReader ();
			var asmAttrs = reader.GetCustomAttributes (EntityHandle.AssemblyDefinition);
			Assert.Equal (3, asmAttrs.Count ());
		} finally {
			CleanUp (path);
		}
	}

	[Fact]
	public void Generate_HasModuleType ()
	{
		var path = GenerateRootAssembly (Array.Empty<string> ());
		try {
			using var pe = new PEReader (File.OpenRead (path));
			var reader = pe.GetMetadataReader ();
			var types = reader.TypeDefinitions
				.Select (h => reader.GetTypeDefinition (h))
				.ToList ();
			Assert.Contains (types, t => reader.GetString (t.Name) == "<Module>");
		} finally {
			CleanUp (path);
		}
	}

	[Fact]
	public void Generate_AttributeBlobValues_MatchTargetNames ()
	{
		var targets = new [] { "_App.TypeMap", "_Mono.Android.TypeMap" };
		var path = GenerateRootAssembly (targets);
		try {
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
		} finally {
			CleanUp (path);
		}
	}
}
