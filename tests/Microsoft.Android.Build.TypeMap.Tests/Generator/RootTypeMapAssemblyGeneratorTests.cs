using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Xunit;

namespace Microsoft.Android.Build.TypeMap.Tests;

public class RootTypeMapAssemblyGeneratorTests
{
	string GenerateRootAssembly (IReadOnlyList<string> perAssemblyNames, string? assemblyName = null)
	{
		var outputPath = Path.Combine (Path.GetTempPath (), $"root-typemap-{Guid.NewGuid ():N}",
			(assemblyName ?? "_Microsoft.Android.TypeMaps") + ".dll");
		var generator = new RootTypeMapAssemblyGenerator ();
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
	public void Generate_HasTypeMapAssemblyTargetAttributeType ()
	{
		var path = GenerateRootAssembly (new [] { "_App.TypeMap" });
		try {
			using var pe = new PEReader (File.OpenRead (path));
			var reader = pe.GetMetadataReader ();

			var types = reader.TypeDefinitions
				.Select (h => reader.GetTypeDefinition (h))
				.ToList ();
			Assert.Contains (types, t =>
				reader.GetString (t.Name) == "TypeMapAssemblyTargetAttribute" &&
				reader.GetString (t.Namespace) == "System.Runtime.InteropServices");
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
