using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

using ExternalEntry = Microsoft.Android.Sdk.TrimmableTypeMap.PrecompiledTypeMapBlobWriter.ExternalEntry;
using ProxyEntry = Microsoft.Android.Sdk.TrimmableTypeMap.PrecompiledTypeMapBlobWriter.ProxyEntry;
using ArrayEntry = Microsoft.Android.Sdk.TrimmableTypeMap.PrecompiledTypeMapBlobWriter.ArrayEntry;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

/// <summary>
/// Generates the root <c>_Microsoft.Android.TypeMaps.dll</c> for the <b>precompiled</b> trimmable
/// typemap path. Instead of emitting <c>[assembly: TypeMap]</c> data + <c>TypeMapping.GetOrCreate*</c>
/// calls (see <see cref="RootTypeMapAssemblyGenerator"/>), it blits one precompiled blob per universe
/// as RVA data and emits a trivial <c>TypeMapLoader.Initialize()</c> that hands the blob address(es)
/// to <c>TrimmableTypeMap.InitializePrecompiled</c>, which constructs the
/// <see cref="Microsoft.Android.Runtime.PrecompiledTypeMap"/> universe(s) inside the R2R-compiled
/// Mono.Android assembly. Keeping construction out of this generated glue lets crossgen2 R2R-compile
/// <c>Initialize()</c> (no <c>IgnoresAccessChecksTo</c> needed) so typemap init stays near zero-cost.
///
/// <para>Generated (pseudo-C#):</para>
/// <code>
/// public static class TypeMapLoader {
///     public static void Initialize () {
///         // shared universe:
///         TrimmableTypeMap.InitializePrecompiled (&amp;__typemap_0, len0, typeof (TypeMapLoader).Module);
///         // OR per-assembly universes:
///         TrimmableTypeMap.InitializePrecompiled (
///             new IntPtr [] { &amp;__typemap_0, &amp;__typemap_1 },
///             new int [] { len0, len1 },
///             typeof (TypeMapLoader).Module);
///     }
/// }
/// </code>
/// </summary>
public sealed class PrecompiledRootTypeMapAssemblyGenerator
{
	const string DefaultAssemblyName = "_Microsoft.Android.TypeMaps";

	readonly Version _systemRuntimeVersion;

	public PrecompiledRootTypeMapAssemblyGenerator (Version systemRuntimeVersion)
	{
		_systemRuntimeVersion = systemRuntimeVersion ?? throw new ArgumentNullException (nameof (systemRuntimeVersion));
	}

	internal void Generate (IReadOnlyList<PrecompiledUniverse> universes, Stream stream, string? assemblyName = null, string? moduleName = null)
	{
		_ = universes ?? throw new ArgumentNullException (nameof (universes));
		_ = stream ?? throw new ArgumentNullException (nameof (stream));
		if (universes.Count == 0) {
			throw new ArgumentException ("At least one universe must be provided.", nameof (universes));
		}

		assemblyName ??= DefaultAssemblyName;
		moduleName ??= assemblyName + ".dll";

		var pe = new PEAssemblyBuilder (_systemRuntimeVersion);
		// Generated after ILLink: reference System.Private.CoreLib directly (the System.Runtime facade is
		// trimmed away), stamped with its real version so the root loads and crossgen2 can R2R it.
		pe.EmitPreamble (assemblyName, moduleName, useCoreLibForBcl: true, coreLibVersion: _systemRuntimeVersion);

		// Phase 1 + 2: resolve a TypeRef token for every distinct proxy, then serialize each universe blob.
		var tokenCache = new Dictionary<PrecompiledProxyRef, int> ();
		int Token (PrecompiledProxyRef proxyRef)
		{
			if (!tokenCache.TryGetValue (proxyRef, out int token)) {
				token = pe.GetTypeRefToken (proxyRef.AssemblyName, proxyRef.FullTypeName);
				tokenCache [proxyRef] = token;
			}
			return token;
		}

		// Phase 3: blit each universe blob as an RVA field.
		var blobFields = new List<(FieldDefinitionHandle Field, int Length)> (universes.Count);
		foreach (var universe in universes) {
			byte[] blob = SerializeUniverse (universe, Token);
			blobFields.Add (pe.AddRvaDataField (blob));
		}

		// Runtime type/member references used by Initialize(). The generated root only calls the public
		// TrimmableTypeMap.InitializePrecompiled entry points (plus System.Type helpers); it constructs
		// no runtime types itself. That keeps this glue free of internal access (no IgnoresAccessChecksTo)
		// so crossgen2 can ReadyToRun-compile it, and moves PrecompiledTypeMap/AggregateTypeMap
		// construction into the (already R2R-compiled) Mono.Android assembly.
		var trimmableTypeMapRef = pe.Metadata.AddTypeReference (pe.MonoAndroidRef,
			pe.Metadata.GetOrAddString ("Microsoft.Android.Runtime"), pe.Metadata.GetOrAddString ("TrimmableTypeMap"));
		var systemTypeRef = pe.Metadata.AddTypeReference (pe.SystemRuntimeRef,
			pe.Metadata.GetOrAddString ("System"), pe.Metadata.GetOrAddString ("Type"));
		var runtimeTypeHandleRef = pe.Metadata.AddTypeReference (pe.SystemRuntimeRef,
			pe.Metadata.GetOrAddString ("System"), pe.Metadata.GetOrAddString ("RuntimeTypeHandle"));
		var moduleRef = pe.Metadata.AddTypeReference (pe.SystemRuntimeRef,
			pe.Metadata.GetOrAddString ("System.Reflection"), pe.Metadata.GetOrAddString ("Module"));
		var intPtrRef = pe.Metadata.AddTypeReference (pe.SystemRuntimeRef,
			pe.Metadata.GetOrAddString ("System"), pe.Metadata.GetOrAddString ("IntPtr"));
		var int32Ref = pe.Metadata.AddTypeReference (pe.SystemRuntimeRef,
			pe.Metadata.GetOrAddString ("System"), pe.Metadata.GetOrAddString ("Int32"));

		// TrimmableTypeMap.InitializePrecompiled (IntPtr blob, int length, Module tokenModule)
		var initScalarRef = pe.AddMemberRef (trimmableTypeMapRef, "InitializePrecompiled",
			sig => sig.MethodSignature ().Parameters (3,
				rt => rt.Void (),
				p => {
					p.AddParameter ().Type ().IntPtr ();
					p.AddParameter ().Type ().Int32 ();
					p.AddParameter ().Type ().Type (moduleRef, isValueType: false);
				}));
		// TrimmableTypeMap.InitializePrecompiled (IntPtr[] blobs, int[] lengths, Module tokenModule)
		var initArrayRef = pe.AddMemberRef (trimmableTypeMapRef, "InitializePrecompiled",
			sig => sig.MethodSignature ().Parameters (3,
				rt => rt.Void (),
				p => {
					p.AddParameter ().Type ().SZArray ().IntPtr ();
					p.AddParameter ().Type ().SZArray ().Int32 ();
					p.AddParameter ().Type ().Type (moduleRef, isValueType: false);
				}));
		var getTypeFromHandleRef = pe.AddMemberRef (systemTypeRef, "GetTypeFromHandle",
			sig => sig.MethodSignature ().Parameters (1,
				rt => rt.Type ().Type (systemTypeRef, isValueType: false),
				p => p.AddParameter ().Type ().Type (runtimeTypeHandleRef, isValueType: true)));
		var getModuleRef = pe.AddMemberRef (systemTypeRef, "get_Module",
			sig => sig.MethodSignature (isInstanceMethod: true).Parameters (0,
				rt => rt.Type ().Type (moduleRef, isValueType: false),
				p => { }));

		var typeMapLoaderHandle = pe.Metadata.AddTypeDefinition (
			TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Abstract | TypeAttributes.Class,
			pe.Metadata.GetOrAddString ("Microsoft.Android.Runtime"),
			pe.Metadata.GetOrAddString ("TypeMapLoader"),
			pe.Metadata.AddTypeReference (pe.SystemRuntimeRef,
				pe.Metadata.GetOrAddString ("System"), pe.Metadata.GetOrAddString ("Object")),
			MetadataTokens.FieldDefinitionHandle (pe.Metadata.GetRowCount (TableIndex.Field) + 1),
			MetadataTokens.MethodDefinitionHandle (pe.Metadata.GetRowCount (TableIndex.MethodDef) + 1));

		pe.EmitBody ("Initialize",
			MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig,
			sig => sig.MethodSignature ().Parameters (0, rt => rt.Void (), p => { }),
			encoder => {
				if (blobFields.Count == 1) {
					// TrimmableTypeMap.InitializePrecompiled (&blob, len, typeof (TypeMapLoader).Module);
					encoder.LoadStaticFieldAddress (blobFields [0].Field);
					encoder.LoadConstantI4 (blobFields [0].Length);
					EmitPushModule (encoder, typeMapLoaderHandle, getTypeFromHandleRef, getModuleRef);
					encoder.Call (initScalarRef, parameterCount: 3);
				} else {
					// IntPtr[] blobs = { &blob0, &blob1, ... };
					encoder.LoadConstantI4 (blobFields.Count);
					encoder.NewArray (intPtrRef);
					for (int i = 0; i < blobFields.Count; i++) {
						encoder.OpCode (ILOpCode.Dup);
						encoder.LoadConstantI4 (i);
						encoder.LoadStaticFieldAddress (blobFields [i].Field);
						encoder.OpCode (ILOpCode.Stelem_i);
					}
					// int[] lengths = { len0, len1, ... };
					encoder.LoadConstantI4 (blobFields.Count);
					encoder.NewArray (int32Ref);
					for (int i = 0; i < blobFields.Count; i++) {
						encoder.OpCode (ILOpCode.Dup);
						encoder.LoadConstantI4 (i);
						encoder.LoadConstantI4 (blobFields [i].Length);
						encoder.OpCode (ILOpCode.Stelem_i4);
					}
					EmitPushModule (encoder, typeMapLoaderHandle, getTypeFromHandleRef, getModuleRef);
					encoder.Call (initArrayRef, parameterCount: 3);
				}
				encoder.Return ();
			});

		pe.WritePE (stream);
	}

	// Pushes: typeof (TypeMapLoader).Module
	static void EmitPushModule (
		PEAssemblyBuilder.TrackedInstructionEncoder encoder,
		TypeDefinitionHandle typeMapLoaderHandle,
		MemberReferenceHandle getTypeFromHandleRef,
		MemberReferenceHandle getModuleRef)
	{
		encoder.LoadToken (typeMapLoaderHandle);
		encoder.Call (getTypeFromHandleRef, parameterCount: 1, returnsValue: true);
		encoder.Callvirt (getModuleRef, parameterCount: 0, returnsValue: true);
	}

	static byte[] SerializeUniverse (PrecompiledUniverse universe, Func<PrecompiledProxyRef, int> token)
	{
		var external = new List<ExternalEntry> (universe.External.Count);
		foreach (var entry in universe.External) {
			var tokens = new int [entry.Value.Count];
			for (int i = 0; i < entry.Value.Count; i++) {
				tokens [i] = token (entry.Value [i]);
			}
			external.Add (new ExternalEntry (entry.Key, tokens));
		}

		var proxy = new List<ProxyEntry> (universe.Proxy.Count);
		foreach (var entry in universe.Proxy) {
			proxy.Add (new ProxyEntry (entry.Key, token (entry.Value)));
		}

		var array = new List<ArrayEntry> (universe.Array.Count);
		foreach (var entry in universe.Array) {
			var rankTokens = new (int RankIndex, int ProxyToken) [entry.Value.Count];
			for (int i = 0; i < entry.Value.Count; i++) {
				rankTokens [i] = (entry.Value [i].RankIndex, token (entry.Value [i].Proxy));
			}
			array.Add (new ArrayEntry (entry.Key, rankTokens));
		}

		return PrecompiledTypeMapBlobWriter.Write (external, proxy, array);
	}
}
