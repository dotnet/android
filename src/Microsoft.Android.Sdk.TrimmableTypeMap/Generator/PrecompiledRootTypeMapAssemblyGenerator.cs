using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

using ExternalEntry = Microsoft.Android.Sdk.TrimmableTypeMap.PrecompiledTypeMapBlobWriter.ExternalEntry;
using ProxyEntry = Microsoft.Android.Sdk.TrimmableTypeMap.PrecompiledTypeMapBlobWriter.ProxyEntry;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

/// <summary>
/// Generates the root <c>_Microsoft.Android.TypeMaps.dll</c> for the <b>precompiled</b> trimmable
/// typemap path. Instead of emitting <c>[assembly: TypeMap]</c> data + <c>TypeMapping.GetOrCreate*</c>
/// calls (see <see cref="RootTypeMapAssemblyGenerator"/>), it blits one precompiled blob per universe
/// as RVA data and emits a <c>TypeMapLoader.Initialize()</c> that hydrates
/// <see cref="Microsoft.Android.Runtime.PrecompiledTypeMap"/> instances in place.
///
/// <para>Generated (pseudo-C#):</para>
/// <code>
/// public static class TypeMapLoader {
///     public static void Initialize () {
///         // shared universe:
///         TrimmableTypeMap.Initialize (new PrecompiledTypeMap (&amp;__typemap_0, len0, typeof (TypeMapLoader).Module));
///         // OR per-assembly universes:
///         TrimmableTypeMap.Initialize (new AggregateTypeMap (new ITypeMap [] {
///             new PrecompiledTypeMap (&amp;__typemap_0, len0, module),
///             new PrecompiledTypeMap (&amp;__typemap_1, len1, module),
///         }));
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

		// Runtime type/member references used by Initialize().
		var precompiledTypeMapRef = pe.Metadata.AddTypeReference (pe.MonoAndroidRef,
			pe.Metadata.GetOrAddString ("Microsoft.Android.Runtime"), pe.Metadata.GetOrAddString ("PrecompiledTypeMap"));
		var aggregateTypeMapRef = pe.Metadata.AddTypeReference (pe.MonoAndroidRef,
			pe.Metadata.GetOrAddString ("Microsoft.Android.Runtime"), pe.Metadata.GetOrAddString ("AggregateTypeMap"));
		var iTypeMapRef = pe.Metadata.AddTypeReference (pe.MonoAndroidRef,
			pe.Metadata.GetOrAddString ("Microsoft.Android.Runtime"), pe.Metadata.GetOrAddString ("ITypeMap"));
		var trimmableTypeMapRef = pe.Metadata.AddTypeReference (pe.MonoAndroidRef,
			pe.Metadata.GetOrAddString ("Microsoft.Android.Runtime"), pe.Metadata.GetOrAddString ("TrimmableTypeMap"));
		var systemTypeRef = pe.Metadata.AddTypeReference (pe.SystemRuntimeRef,
			pe.Metadata.GetOrAddString ("System"), pe.Metadata.GetOrAddString ("Type"));
		var runtimeTypeHandleRef = pe.Metadata.AddTypeReference (pe.SystemRuntimeRef,
			pe.Metadata.GetOrAddString ("System"), pe.Metadata.GetOrAddString ("RuntimeTypeHandle"));
		var moduleRef = pe.Metadata.AddTypeReference (pe.SystemRuntimeRef,
			pe.Metadata.GetOrAddString ("System.Reflection"), pe.Metadata.GetOrAddString ("Module"));

		var precompiledCtorRef = pe.AddMemberRef (precompiledTypeMapRef, ".ctor",
			sig => sig.MethodSignature (isInstanceMethod: true).Parameters (3,
				rt => rt.Void (),
				p => {
					p.AddParameter ().Type ().VoidPointer ();
					p.AddParameter ().Type ().Int32 ();
					p.AddParameter ().Type ().Type (moduleRef, isValueType: false);
				}));
		var aggregateCtorRef = pe.AddMemberRef (aggregateTypeMapRef, ".ctor",
			sig => sig.MethodSignature (isInstanceMethod: true).Parameters (1,
				rt => rt.Void (),
				p => p.AddParameter ().Type ().SZArray ().Type (iTypeMapRef, isValueType: false)));
		var initializeRef = pe.AddMemberRef (trimmableTypeMapRef, "Initialize",
			sig => sig.MethodSignature ().Parameters (1,
				rt => rt.Void (),
				p => p.AddParameter ().Type ().Type (iTypeMapRef, isValueType: false)));
		var getTypeFromHandleRef = pe.AddMemberRef (systemTypeRef, "GetTypeFromHandle",
			sig => sig.MethodSignature ().Parameters (1,
				rt => rt.Type ().Type (systemTypeRef, isValueType: false),
				p => p.AddParameter ().Type ().Type (runtimeTypeHandleRef, isValueType: true)));
		var getModuleRef = pe.AddMemberRef (systemTypeRef, "get_Module",
			sig => sig.MethodSignature (isInstanceMethod: true).Parameters (0,
				rt => rt.Type ().Type (moduleRef, isValueType: false),
				p => { }));

		// TypeMapLoader.Initialize() calls internal Mono.Android members.
		pe.EmitIgnoresAccessChecksToAttribute (new List<string> { "Mono.Android" });

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
					EmitNewPrecompiledTypeMap (encoder, blobFields [0], typeMapLoaderHandle, getTypeFromHandleRef, getModuleRef, precompiledCtorRef);
				} else {
					encoder.LoadConstantI4 (blobFields.Count);
					encoder.NewArray (iTypeMapRef);
					for (int i = 0; i < blobFields.Count; i++) {
						encoder.OpCode (ILOpCode.Dup);
						encoder.LoadConstantI4 (i);
						EmitNewPrecompiledTypeMap (encoder, blobFields [i], typeMapLoaderHandle, getTypeFromHandleRef, getModuleRef, precompiledCtorRef);
						encoder.OpCode (ILOpCode.Stelem_ref);
					}
					encoder.NewObject (aggregateCtorRef, parameterCount: 1);
				}
				encoder.Call (initializeRef, parameterCount: 1);
				encoder.Return ();
			});

		pe.WritePE (stream);
	}

	// Pushes: new PrecompiledTypeMap (&blobField, length, typeof (TypeMapLoader).Module)
	static void EmitNewPrecompiledTypeMap (
		PEAssemblyBuilder.TrackedInstructionEncoder encoder,
		(FieldDefinitionHandle Field, int Length) blob,
		TypeDefinitionHandle typeMapLoaderHandle,
		MemberReferenceHandle getTypeFromHandleRef,
		MemberReferenceHandle getModuleRef,
		MemberReferenceHandle precompiledCtorRef)
	{
		encoder.LoadStaticFieldAddress (blob.Field);
		encoder.LoadConstantI4 (blob.Length);
		encoder.LoadToken (typeMapLoaderHandle);
		encoder.Call (getTypeFromHandleRef, parameterCount: 1, returnsValue: true);
		encoder.Callvirt (getModuleRef, parameterCount: 0, returnsValue: true);
		encoder.NewObject (precompiledCtorRef, parameterCount: 3);
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

		return PrecompiledTypeMapBlobWriter.Write (external, proxy);
	}
}
