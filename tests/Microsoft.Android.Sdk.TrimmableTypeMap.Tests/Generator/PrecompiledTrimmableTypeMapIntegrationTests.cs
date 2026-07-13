using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

using Microsoft.Android.Runtime;

using Xunit;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.Tests;

public class PrecompiledTrimmableTypeMapIntegrationTests : FixtureTestBase
{
	readonly List<string> _log = new ();

	TrimmableTypeMapResult Execute (bool precompile, int maxArrayRank = 0, int maxReferenceArrayRank = 0) =>
		new TrimmableTypeMapGenerator (new SilentLogger (_log)).Execute (
			new [] { new AssemblyInput ("TestFixtures", "", CreateFixtureReader ()) },
			new Version (11, 0, 0, 0),
			new HashSet<string> (),
			useSharedTypemapUniverse: true,
			maxArrayRank: maxArrayRank,
			maxReferenceArrayRank: maxReferenceArrayRank,
			precompileTypeMap: precompile);

	static PEReader CreateFixtureReader ()
	{
		var dir = Path.GetDirectoryName (typeof (FixtureTestBase).Assembly.Location)!;
		return new PEReader (File.OpenRead (Path.Combine (dir, "TestFixtures.dll")));
	}

	static byte[] RootBytes (TrimmableTypeMapResult result) =>
		result.GeneratedAssemblies.Single (a => a.Name == "_Microsoft.Android.TypeMaps").Content.ToArray ();

	static byte[] SingleRvaBlob (PEReader pe, MetadataReader reader)
	{
		var blobs = reader.FieldDefinitions
			.Select (reader.GetFieldDefinition)
			.Where (f => (f.Attributes & System.Reflection.FieldAttributes.HasFieldRVA) != 0)
			.Select (f => pe.GetSectionData (f.GetRelativeVirtualAddress ()).GetContent ().ToArray ())
			.ToList ();
		return Assert.Single (blobs);
	}

	[Fact]
	public void PrecompiledRoot_EmbedsBlob_WhereNormalRootDoesNot ()
	{
		using var normalPe = new PEReader (new MemoryStream (RootBytes (Execute (precompile: false))));
		var normalReader = normalPe.GetMetadataReader ();
		Assert.DoesNotContain (normalReader.FieldDefinitions.Select (normalReader.GetFieldDefinition),
			f => (f.Attributes & System.Reflection.FieldAttributes.HasFieldRVA) != 0);

		using var precompiledPe = new PEReader (new MemoryStream (RootBytes (Execute (precompile: true))));
		var precompiledReader = precompiledPe.GetMetadataReader ();
		byte[] blob = SingleRvaBlob (precompiledPe, precompiledReader);
		Assert.True (PrecompiledTypeMapBlobFormat.IsValid (blob));
	}

	[Fact]
	public void PrecompiledBlob_ResolvesRealFixtureProxies ()
	{
		var result = Execute (precompile: true);
		byte[] blob;
		Dictionary<int, (string Namespace, string Name, string Assembly)> typeRefs;
		using (var pe = new PEReader (new MemoryStream (RootBytes (result)))) {
			var reader = pe.GetMetadataReader ();
			blob = SingleRvaBlob (pe, reader);
			typeRefs = reader.TypeReferences.ToDictionary (
				h => MetadataTokens.GetToken (h),
				h => {
					var tr = reader.GetTypeReference (h);
					var asm = reader.GetAssemblyReference ((AssemblyReferenceHandle) tr.ResolutionScope);
					return (reader.GetString (tr.Namespace), reader.GetString (tr.Name), reader.GetString (asm.Name));
				});
		}

		int externalCount = (int) BinaryPrimitives.ReadUInt32LittleEndian (blob.AsSpan (PrecompiledTypeMapBlobFormat.OffExternalCount, 4));
		int proxyCount = (int) BinaryPrimitives.ReadUInt32LittleEndian (blob.AsSpan (PrecompiledTypeMapBlobFormat.OffProxyCount, 4));
		Assert.True (externalCount > 0, "Expected the precompiled blob to contain JNI-name entries from the fixtures.");
		Assert.True (proxyCount > 0, "Expected the precompiled blob to contain managed-type entries from the fixtures.");

		// Every scanned fixture peer with an activation constructor is guaranteed a proxy, so its JNI
		// name must resolve in the external map, and the resolved token must be a proxy TypeRef in the
		// fixtures' typemap assembly.
		var peersWithProxies = result.AllPeers
			.Where (p => p.ActivationCtor is not null && !p.IsInterface)
			.ToList ();
		Assert.NotEmpty (peersWithProxies);

		int verified = 0;
		foreach (var peer in peersWithProxies) {
			if (!PrecompiledTypeMapBlobFormat.TryGetExternalTokens (blob, peer.JavaName, out int count, out int offset)) {
				continue; // alias/merge edge cases are covered by unit tests; skip here.
			}
			for (int i = 0; i < count; i++) {
				int token = PrecompiledTypeMapBlobFormat.ReadTokenAt (blob, offset, i);
				var (ns, _, asm) = typeRefs [token];
				Assert.Equal ("_TypeMap.Proxies", ns);
				Assert.Equal ("_TestFixtures.TypeMap", asm);
			}

			Assert.True (PrecompiledTypeMapBlobFormat.TryGetProxyToken (blob, $"{peer.ManagedTypeName}, TestFixtures", out int proxyToken),
				$"Proxy map is missing managed type '{peer.ManagedTypeName}'.");
			Assert.Equal ("_TypeMap.Proxies", typeRefs [proxyToken].Namespace);
			verified++;
		}

		Assert.True (verified >= 5, $"Expected to verify several fixture proxies, only verified {verified}.");
	}

	[Fact]
	public void PrecompiledBlob_ResolvesArrayProxies ()
	{
		// Force reference array-proxy emission (disabled by default). Each scanned reference peer that
		// generates array entries gets a rank-1 array proxy keyed by its element type.
		var result = Execute (precompile: true, maxReferenceArrayRank: 1);
		byte[] blob;
		Dictionary<int, (string Namespace, string Name, string Assembly)> typeRefs;
		using (var pe = new PEReader (new MemoryStream (RootBytes (result)))) {
			var reader = pe.GetMetadataReader ();
			blob = SingleRvaBlob (pe, reader);
			typeRefs = reader.TypeReferences.ToDictionary (
				h => MetadataTokens.GetToken (h),
				h => {
					var tr = reader.GetTypeReference (h);
					var asm = reader.GetAssemblyReference ((AssemblyReferenceHandle) tr.ResolutionScope);
					return (reader.GetString (tr.Namespace), reader.GetString (tr.Name), reader.GetString (asm.Name));
				});
		}

		int arrayCount = (int) BinaryPrimitives.ReadUInt32LittleEndian (blob.AsSpan (PrecompiledTypeMapBlobFormat.OffArrayCount, 4));
		Assert.True (arrayCount > 0, "Expected the precompiled blob to contain array-element entries.");

		// The element key mirrors the runtime managed-type key: "Namespace.Type, AssemblyName".
		int verified = 0;
		foreach (var peer in result.AllPeers) {
			string elementKey = $"{peer.ManagedTypeName}, {peer.AssemblyName}";
			if (!PrecompiledTypeMapBlobFormat.TryGetArrayToken (blob, elementKey, 0, out int token)) {
				continue; // Not every peer generates array entries.
			}
			Assert.Equal ("_TypeMap.ArrayProxies", typeRefs [token].Namespace);
			Assert.Equal ("_TestFixtures.TypeMap", typeRefs [token].Assembly);
			// Only rank 1 (index 0) was emitted; rank 2 (index 1) must not resolve.
			Assert.False (PrecompiledTypeMapBlobFormat.TryGetArrayToken (blob, elementKey, 1, out _));
			verified++;
		}

		Assert.True (verified > 0, "Expected to verify at least one fixture array proxy.");
	}

	[Fact]
	public void PrecompiledBlob_WithoutArrayRanks_HasEmptyArrayMap ()
	{
		var result = Execute (precompile: true);
		using var pe = new PEReader (new MemoryStream (RootBytes (result)));
		var reader = pe.GetMetadataReader ();
		byte[] blob = SingleRvaBlob (pe, reader);

		int arrayCount = (int) BinaryPrimitives.ReadUInt32LittleEndian (blob.AsSpan (PrecompiledTypeMapBlobFormat.OffArrayCount, 4));
		Assert.Equal (0, arrayCount);
		Assert.False (PrecompiledTypeMapBlobFormat.TryGetArrayToken (blob, "System.Int32, System.Runtime", 0, out _));
	}

	sealed class SilentLogger (List<string> log) : ITrimmableTypeMapLogger
	{
		public void LogNoJavaPeerTypesFound () { }
		public void LogJavaPeerScanInfo (int assemblyCount, int peerCount) => log.Add ($"scan {assemblyCount} {peerCount}");
		public void LogGeneratingJcwFilesInfo (int jcwPeerCount, int totalPeerCount) { }
		public void LogDeferredRegistrationTypesInfo (int typeCount) { }
		public void LogGeneratedTypeMapAssemblyInfo (string assemblyName, int typeCount) { }
		public void LogGeneratedRootTypeMapInfo (int assemblyReferenceCount) { }
		public void LogGeneratedTypeMapAssembliesInfo (int assemblyCount) { }
		public void LogGeneratedJcwFilesInfo (int sourceCount) { }
		public void LogRootingManifestReferencedTypeInfo (string javaTypeName, string managedTypeName) { }
		public void LogManifestReferencedTypeNotFoundWarning (string javaTypeName) { }
		public void LogLibraryManifestMergeWarning (string message) { }
		public void LogInvalidManifestPlaceholderWarning (string placeholders) { }
		public void LogUnresolvableJavaPeerSkippedWarning (string managedTypeName, string assemblyName, string unresolvedTypeName, string unresolvedAssemblyName, string unresolvedAssemblyPath) { }
		public void LogJniAddNativeMethodRegistrationAttributeError (string managedTypeName) { }
	}
}
