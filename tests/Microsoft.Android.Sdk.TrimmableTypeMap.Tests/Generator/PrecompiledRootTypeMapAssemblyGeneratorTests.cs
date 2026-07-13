using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

using Microsoft.Android.Runtime;

using Xunit;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.Tests;

public class PrecompiledRootTypeMapAssemblyGeneratorTests
{
	static PrecompiledUniverse Universe (
		IEnumerable<(string JniName, PrecompiledProxyRef[] Proxies)> external,
		IEnumerable<(string Key, PrecompiledProxyRef Proxy)> proxy)
	{
		var universe = new PrecompiledUniverse ();
		foreach (var (jniName, proxies) in external) {
			universe.External.Add (new KeyValuePair<string, List<PrecompiledProxyRef>> (jniName, proxies.ToList ()));
		}
		foreach (var (key, p) in proxy) {
			universe.Proxy.Add (new KeyValuePair<string, PrecompiledProxyRef> (key, p));
		}
		return universe;
	}

	static MemoryStream Generate (params PrecompiledUniverse[] universes)
	{
		var stream = new MemoryStream ();
		new PrecompiledRootTypeMapAssemblyGenerator (new Version (11, 0, 0, 0)).Generate (universes, stream);
		stream.Position = 0;
		return stream;
	}

	// Reads the bytes of every HasFieldRVA field, in field-definition order.
	static List<byte[]> ReadRvaBlobs (PEReader pe, MetadataReader reader)
	{
		var blobs = new List<byte[]> ();
		foreach (var handle in reader.FieldDefinitions) {
			var field = reader.GetFieldDefinition (handle);
			if ((field.Attributes & System.Reflection.FieldAttributes.HasFieldRVA) == 0) {
				continue;
			}
			int rva = field.GetRelativeVirtualAddress ();
			var section = pe.GetSectionData (rva);
			blobs.Add (section.GetContent ().ToArray ());
		}
		return blobs;
	}

	static (string Namespace, string Name, string Assembly) ResolveTypeRef (MetadataReader reader, int token)
	{
		var handle = (TypeReferenceHandle) MetadataTokens.Handle (token);
		var typeRef = reader.GetTypeReference (handle);
		var asmRef = reader.GetAssemblyReference ((AssemblyReferenceHandle) typeRef.ResolutionScope);
		return (reader.GetString (typeRef.Namespace), reader.GetString (typeRef.Name), reader.GetString (asmRef.Name));
	}

	static readonly PrecompiledProxyRef ActivityProxy = new ("_Mono.Android.TypeMap", "_TypeMap.Proxies.Android_App_Activity_Proxy");
	static readonly PrecompiledProxyRef ButtonProxy = new ("_App.TypeMap", "_TypeMap.Proxies.Android_Widget_Button_Proxy");

	[Fact]
	public void Generate_ProducesValidPEAssembly_WithTypeMapLoaderInitialize ()
	{
		using var stream = Generate (Universe (
			external: new [] { ("android/app/Activity", new [] { ActivityProxy }) },
			proxy: new [] { ("Android.App.Activity, Mono.Android", ActivityProxy) }));
		using var pe = new PEReader (stream);
		var reader = pe.GetMetadataReader ();

		Assert.True (pe.HasMetadata);

		var loader = reader.TypeDefinitions
			.Select (reader.GetTypeDefinition)
			.Single (t => reader.GetString (t.Name) == "TypeMapLoader");
		Assert.Equal ("Microsoft.Android.Runtime", reader.GetString (loader.Namespace));

		var initialize = loader.GetMethods ()
			.Select (reader.GetMethodDefinition)
			.Single (m => reader.GetString (m.Name) == "Initialize");
		Assert.True (initialize.Attributes.HasFlag (System.Reflection.MethodAttributes.Static));
	}

	[Fact]
	public void Generate_SharedUniverse_EmbedsResolvableProxyTokens ()
	{
		using var stream = Generate (Universe (
			external: new [] {
				("android/app/Activity", new [] { ActivityProxy }),
				("android/widget/Button", new [] { ButtonProxy }),
			},
			proxy: new [] {
				("Android.App.Activity, Mono.Android", ActivityProxy),
				("Android.Widget.Button, Mono.Android", ButtonProxy),
			}));
		using var pe = new PEReader (stream);
		var reader = pe.GetMetadataReader ();

		var blobs = ReadRvaBlobs (pe, reader);
		Assert.Single (blobs);
		byte[] blob = blobs [0];
		Assert.True (PrecompiledTypeMapBlobFormat.IsValid (blob));

		AssertExternalResolvesTo (reader, blob, "android/app/Activity", ActivityProxy);
		AssertExternalResolvesTo (reader, blob, "android/widget/Button", ButtonProxy);

		Assert.True (PrecompiledTypeMapBlobFormat.TryGetProxyToken (blob, "Android.App.Activity, Mono.Android", out int activityToken));
		Assert.Equal (("_TypeMap.Proxies", "Android_App_Activity_Proxy", "_Mono.Android.TypeMap"), ResolveTypeRef (reader, activityToken));
	}

	[Fact]
	public void Generate_PerAssemblyUniverses_EmitOneBlobEach ()
	{
		var u0 = Universe (
			external: new [] { ("android/app/Activity", new [] { ActivityProxy }) },
			proxy: new [] { ("Android.App.Activity, Mono.Android", ActivityProxy) });
		var u1 = Universe (
			external: new [] { ("android/widget/Button", new [] { ButtonProxy }) },
			proxy: new [] { ("Android.Widget.Button, App", ButtonProxy) });

		using var stream = Generate (u0, u1);
		using var pe = new PEReader (stream);
		var reader = pe.GetMetadataReader ();

		var blobs = ReadRvaBlobs (pe, reader);
		Assert.Equal (2, blobs.Count);

		AssertExternalResolvesTo (reader, blobs [0], "android/app/Activity", ActivityProxy);
		AssertExternalResolvesTo (reader, blobs [1], "android/widget/Button", ButtonProxy);
		// The Button entry must not appear in the Activity universe.
		Assert.False (PrecompiledTypeMapBlobFormat.TryGetExternalTokens (blobs [0], "android/widget/Button", out _, out _));
	}

	[Fact]
	public void Generate_AliasGroup_EmbedsAllProxyTokens ()
	{
		var collection = new PrecompiledProxyRef ("_App.TypeMap", "_TypeMap.Proxies.JavaCollection_Proxy");
		var collectionT = new PrecompiledProxyRef ("_App.TypeMap", "_TypeMap.Proxies.JavaCollection_1_Proxy");

		using var stream = Generate (Universe (
			external: new [] { ("java/util/Collection", new [] { collection, collectionT }) },
			proxy: Array.Empty<(string, PrecompiledProxyRef)> ()));
		using var pe = new PEReader (stream);
		var reader = pe.GetMetadataReader ();

		byte[] blob = ReadRvaBlobs (pe, reader).Single ();
		Assert.True (PrecompiledTypeMapBlobFormat.TryGetExternalTokens (blob, "java/util/Collection", out int count, out int offset));
		Assert.Equal (2, count);

		var names = Enumerable.Range (0, count)
			.Select (i => ResolveTypeRef (reader, PrecompiledTypeMapBlobFormat.ReadTokenAt (blob, offset, i)).Name)
			.ToArray ();
		Assert.Contains ("JavaCollection_Proxy", names);
		Assert.Contains ("JavaCollection_1_Proxy", names);
	}

	static void AssertExternalResolvesTo (MetadataReader reader, byte[] blob, string jniName, PrecompiledProxyRef expected)
	{
		Assert.True (PrecompiledTypeMapBlobFormat.TryGetExternalTokens (blob, jniName, out int count, out int offset));
		Assert.Equal (1, count);
		int token = PrecompiledTypeMapBlobFormat.ReadTokenAt (blob, offset, 0);
		var (ns, name, asm) = ResolveTypeRef (reader, token);
		int lastDot = expected.FullTypeName.LastIndexOf ('.');
		Assert.Equal (expected.FullTypeName.Substring (0, lastDot), ns);
		Assert.Equal (expected.FullTypeName.Substring (lastDot + 1), name);
		Assert.Equal (expected.AssemblyName, asm);
	}

	[Fact]
	public void Generate_SharedUniverse_InitializeIlIsWellFormed ()
	{
		using var stream = Generate (Universe (
			external: new [] { ("android/app/Activity", new [] { ActivityProxy }) },
			proxy: new [] { ("Android.App.Activity, Mono.Android", ActivityProxy) }));
		using var pe = new PEReader (stream);
		var reader = pe.GetMetadataReader ();

		var initialize = reader.TypeDefinitions
			.Select (reader.GetTypeDefinition)
			.Single (t => reader.GetString (t.Name) == "TypeMapLoader")
			.GetMethods ()
			.Select (reader.GetMethodDefinition)
			.Single (m => reader.GetString (m.Name) == "Initialize");

		var body = pe.GetMethodBody (initialize.RelativeVirtualAddress);
		Assert.True (body.MaxStack >= 3);
		Assert.True (body.LocalSignature.IsNil);

		var opcodes = DecodeOpCodes (body.GetILBytes ()!);
		Assert.Equal (
			new [] {
				ILOpCode.Ldsflda,   // &blob
				ILOpCode.Ldc_i4,    // length (normalized)
				ILOpCode.Ldtoken,   // typeof (TypeMapLoader)
				ILOpCode.Call,      // Type.GetTypeFromHandle
				ILOpCode.Callvirt,  // Type.get_Module
				ILOpCode.Call,      // TrimmableTypeMap.InitializePrecompiled (IntPtr, int, Module)
				ILOpCode.Ret,
			},
			opcodes);
	}

	// Minimal IL walker for the opcodes the generator emits; all ldc.i4* forms are normalized to
	// ILOpCode.Ldc_i4. Throws on any unexpected opcode so emission drift is caught.
	static List<ILOpCode> DecodeOpCodes (byte[] il)
	{
		var result = new List<ILOpCode> ();
		int i = 0;
		while (i < il.Length) {
			byte b = il [i++];
			switch (b) {
			case 0x25: result.Add (ILOpCode.Dup); break;
			case 0x2A: result.Add (ILOpCode.Ret); break;
			case 0xA2: result.Add (ILOpCode.Stelem_ref); break;
			case >= 0x16 and <= 0x1E: result.Add (ILOpCode.Ldc_i4); break; // ldc.i4.m1..8
			case 0x1F: result.Add (ILOpCode.Ldc_i4); i += 1; break;         // ldc.i4.s
			case 0x20: result.Add (ILOpCode.Ldc_i4); i += 4; break;         // ldc.i4
			case 0x28: result.Add (ILOpCode.Call); i += 4; break;
			case 0x6F: result.Add (ILOpCode.Callvirt); i += 4; break;
			case 0x73: result.Add (ILOpCode.Newobj); i += 4; break;
			case 0x7E: result.Add (ILOpCode.Ldsfld); i += 4; break;
			case 0x7F: result.Add (ILOpCode.Ldsflda); i += 4; break;
			case 0x8D: result.Add (ILOpCode.Newarr); i += 4; break;
			case 0xD0: result.Add (ILOpCode.Ldtoken); i += 4; break;
			default: throw new InvalidOperationException ($"Unexpected IL opcode 0x{b:X2} at offset {i - 1}.");
			}
		}
		return result;
	}
}
