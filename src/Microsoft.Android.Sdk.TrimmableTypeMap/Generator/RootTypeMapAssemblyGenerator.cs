using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

/// <summary>
/// Generates the root <c>_Microsoft.Android.TypeMaps.dll</c> assembly that:
/// <list type="bullet">
/// <item>References all per-assembly typemap assemblies via <c>[assembly: TypeMapAssemblyTargetAttribute&lt;__TypeMapAnchor&gt;("name")]</c>.</item>
/// <item>Emits a <c>TypeMapLoader</c> class whose <c>Initialize()</c> method calls
/// <see cref="Microsoft.Android.Runtime.TrimmableTypeMap.Initialize"/> with the appropriate
/// type mapping dictionaries.</item>
/// </list>
/// </summary>
/// <remarks>
/// <para>The generated assembly looks like this (pseudo-C#):</para>
/// <code>
/// internal class __TypeMapAnchor { }
///
/// // One attribute per per-assembly typemap assembly:
/// [assembly: TypeMapAssemblyTarget&lt;__TypeMapAnchor&gt;("_Mono.Android.TypeMap")]
/// [assembly: TypeMapAssemblyTarget&lt;__TypeMapAnchor&gt;("_MyApp.TypeMap")]
///
/// namespace Microsoft.Android.Runtime
/// {
///     // Called directly from JNIEnvInit.Initialize():
///     public static class TypeMapLoader
///     {
///         public static void Initialize ()
///         {
///             // Option A: Shared universe
///             TrimmableTypeMap.Initialize(
///                 TypeMapping.GetOrCreateExternalTypeMapping&lt;Java.Lang.Object&gt;(),
///                 TypeMapping.GetOrCreateProxyTypeMapping&lt;Java.Lang.Object&gt;());
///
///             // Option B: Per-assembly universes (aggregated)
///             var typeMaps = new IReadOnlyDictionary&lt;string, Type&gt;[] {
///                 TypeMapping.GetOrCreateExternalTypeMapping&lt;_Mono_Android_TypeMap.__TypeMapAnchor&gt;(),
///                 TypeMapping.GetOrCreateExternalTypeMapping&lt;_MyApp_TypeMap.__TypeMapAnchor&gt;(),
///             };
///             var proxyMaps = new IReadOnlyDictionary&lt;Type, Type&gt;[] {
///                 TypeMapping.GetOrCreateProxyTypeMapping&lt;_Mono_Android_TypeMap.__TypeMapAnchor&gt;(),
///                 TypeMapping.GetOrCreateProxyTypeMapping&lt;_MyApp_TypeMap.__TypeMapAnchor&gt;(),
///             };
///             TrimmableTypeMap.Initialize(typeMaps, proxyMaps);
///         }
///     }
/// }
/// </code>
/// </remarks>
public sealed class RootTypeMapAssemblyGenerator
{
	const string DefaultAssemblyName = "_Microsoft.Android.TypeMaps";

	readonly Version _systemRuntimeVersion;

	/// <param name="systemRuntimeVersion">Version for System.Runtime assembly references.</param>
	public RootTypeMapAssemblyGenerator (Version systemRuntimeVersion)
	{
		_systemRuntimeVersion = systemRuntimeVersion ?? throw new ArgumentNullException (nameof (systemRuntimeVersion));
	}

	/// <summary>
	/// Generates the root typemap assembly and writes it to the given stream.
	/// </summary>
	/// <param name="perAssemblyTypeMapNames">Names of per-assembly typemap assemblies to reference.</param>
	/// <param name="useSharedTypemapUniverse">True to merge all assemblies into a single typemap universe, false for per-assembly universes.</param>
	/// <param name="emitArrayEntries">
	/// True when the per-assembly typemaps were generated with rank sentinels
	/// (<c>__ArrayMapRank{1,2,3}</c>). The generated <c>TypeMapLoader.Initialize</c>
	/// will then build per-rank dictionaries from each typemap and pass them to the
	/// 5-arg <c>TrimmableTypeMap.Initialize</c> overload. Should match the
	/// <c>emitArrayEntries</c> flag passed to the per-assembly typemap generators.
	/// </param>
	/// <param name="stream">Stream to write the output PE to.</param>
	/// <param name="assemblyName">Optional assembly name (defaults to _Microsoft.Android.TypeMaps).</param>
	/// <param name="moduleName">Optional module name for the PE metadata.</param>
	public void Generate (IReadOnlyList<string> perAssemblyTypeMapNames, bool useSharedTypemapUniverse, Stream stream, string? assemblyName = null, string? moduleName = null, bool emitArrayEntries = false)
	{
		if (perAssemblyTypeMapNames is null) {
			throw new ArgumentNullException (nameof (perAssemblyTypeMapNames));
		}
		if (stream is null) {
			throw new ArgumentNullException (nameof (stream));
		}

		assemblyName ??= DefaultAssemblyName;
		moduleName ??= assemblyName + ".dll";

		var pe = new PEAssemblyBuilder (_systemRuntimeVersion);
		pe.EmitPreamble (assemblyName, moduleName);

		EntityHandle anchorTypeHandle;
		if (useSharedTypemapUniverse) {
			// In merged mode, all per-assembly typemaps use Java.Lang.Object as the shared
			// anchor type, so the root assembly must also use Java.Lang.Object.
			anchorTypeHandle = pe.Metadata.AddTypeReference (pe.MonoAndroidRef,
				pe.Metadata.GetOrAddString ("Java.Lang"),
				pe.Metadata.GetOrAddString ("Object"));
		} else {
			// In aggregate mode, each per-assembly typemap has its own __TypeMapAnchor.
			// The root also defines its own for TypeMapAssemblyTargetAttribute grouping.
			var objectRef = pe.Metadata.AddTypeReference (pe.SystemRuntimeRef,
				pe.Metadata.GetOrAddString ("System"), pe.Metadata.GetOrAddString ("Object"));
			anchorTypeHandle = pe.Metadata.AddTypeDefinition (
				TypeAttributes.NotPublic | TypeAttributes.Sealed | TypeAttributes.Class,
				default,
				pe.Metadata.GetOrAddString ("__TypeMapAnchor"),
				objectRef,
				MetadataTokens.FieldDefinitionHandle (pe.Metadata.GetRowCount (TableIndex.Field) + 1),
				MetadataTokens.MethodDefinitionHandle (pe.Metadata.GetRowCount (TableIndex.MethodDef) + 1));
		}

		// Emit [assembly: TypeMapAssemblyTargetAttribute<__TypeMapAnchor>("name")] for each per-assembly typemap
		EmitAssemblyTargetAttributes (pe, anchorTypeHandle, perAssemblyTypeMapNames);

		// Emit [assembly: IgnoresAccessChecksTo("...")] so TypeMapLoader.Initialize() can access
		// internal types (SingleUniverseTypeMap, AggregateTypeMap in Mono.Android,
		// and __TypeMapAnchor in each per-assembly typemap DLL).
		var accessTargets = new List<string> { "Mono.Android" };
		if (!useSharedTypemapUniverse) {
			accessTargets.AddRange (perAssemblyTypeMapNames);
		}
		pe.EmitIgnoresAccessChecksToAttribute (accessTargets);

		// Emit TypeMapLoader class with Initialize() method
		EmitTypeMapLoader (pe, anchorTypeHandle, perAssemblyTypeMapNames, useSharedTypemapUniverse, emitArrayEntries);

		pe.WritePE (stream);
	}

	static void EmitAssemblyTargetAttributes (PEAssemblyBuilder pe, EntityHandle anchorTypeHandle, IReadOnlyList<string> perAssemblyTypeMapNames)
	{
		var openAttrRef = pe.Metadata.AddTypeReference (pe.SystemRuntimeInteropServicesRef,
			pe.Metadata.GetOrAddString ("System.Runtime.InteropServices"),
			pe.Metadata.GetOrAddString ("TypeMapAssemblyTargetAttribute`1"));

		var closedAttrTypeSpec = pe.MakeGenericTypeSpec (openAttrRef, anchorTypeHandle);

		var ctorRef = pe.AddMemberRef (closedAttrTypeSpec, ".ctor",
			sig => sig.MethodSignature (isInstanceMethod: true).Parameters (1,
				rt => rt.Void (),
				p => p.AddParameter ().Type ().String ()));

		foreach (var name in perAssemblyTypeMapNames) {
			var blobHandle = pe.BuildAttributeBlob (blob => blob.WriteSerializedString (name));
			pe.Metadata.AddCustomAttribute (EntityHandle.AssemblyDefinition, ctorRef, blobHandle);
		}
	}

	static void EmitTypeMapLoader (PEAssemblyBuilder pe, EntityHandle anchorTypeHandle, IReadOnlyList<string> perAssemblyTypeMapNames, bool useSharedTypemapUniverse, bool emitArrayEntries)
	{
		var metadata = pe.Metadata;

		// Type references
		var iReadOnlyDictOpenRef = metadata.AddTypeReference (pe.SystemRuntimeRef,
			metadata.GetOrAddString ("System.Collections.Generic"), metadata.GetOrAddString ("IReadOnlyDictionary`2"));
		var systemTypeRef = metadata.AddTypeReference (pe.SystemRuntimeRef,
			metadata.GetOrAddString ("System"), metadata.GetOrAddString ("Type"));

		var trimmableTypeMapRef = metadata.AddTypeReference (pe.MonoAndroidRef,
			metadata.GetOrAddString ("Microsoft.Android.Runtime"), metadata.GetOrAddString ("TrimmableTypeMap"));

		var typeMappingRef = metadata.AddTypeReference (pe.SystemRuntimeInteropServicesRef,
			metadata.GetOrAddString ("System.Runtime.InteropServices"), metadata.GetOrAddString ("TypeMapping"));

		// Build MemberRefs for TypeMapping methods using manual signature encoding
		// TypeMapping.GetOrCreateExternalTypeMapping<T>() returns IReadOnlyDictionary<string, Type>
		var getExternalMemberRef = AddTypeMappingMethodRef (pe, typeMappingRef, "GetOrCreateExternalTypeMapping",
			iReadOnlyDictOpenRef, systemTypeRef, keyIsString: true);

		// TypeMapping.GetOrCreateProxyTypeMapping<T>() returns IReadOnlyDictionary<Type, Type>
		var getProxyMemberRef = AddTypeMappingMethodRef (pe, typeMappingRef, "GetOrCreateProxyTypeMapping",
			iReadOnlyDictOpenRef, systemTypeRef, keyIsString: false);

		// Define the TypeMapLoader type (public static class in Microsoft.Android.Runtime namespace)
		metadata.AddTypeDefinition (
			TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Abstract | TypeAttributes.Class,
			metadata.GetOrAddString ("Microsoft.Android.Runtime"),
			metadata.GetOrAddString ("TypeMapLoader"),
			metadata.AddTypeReference (pe.SystemRuntimeRef,
				metadata.GetOrAddString ("System"), metadata.GetOrAddString ("Object")),
			MetadataTokens.FieldDefinitionHandle (metadata.GetRowCount (TableIndex.Field) + 1),
			MetadataTokens.MethodDefinitionHandle (metadata.GetRowCount (TableIndex.MethodDef) + 1));

		if (useSharedTypemapUniverse) {
			if (emitArrayEntries) {
				// 5-arg Initialize(typeMap, proxyMap, arrayMapRank1, arrayMapRank2, arrayMapRank3)
				var initializeRef = AddInitializeSingleWithArraysRef (pe, trimmableTypeMapRef, iReadOnlyDictOpenRef, systemTypeRef);
				EmitInitializeWithSingleTypeMapAndArrays (pe, anchorTypeHandle, getExternalMemberRef, initializeRef, getProxyMemberRef);
			} else {
				// TrimmableTypeMap.Initialize(IReadOnlyDictionary<string, Type>, IReadOnlyDictionary<Type, Type>)
				var initializeRef = AddInitializeSingleRef (pe, trimmableTypeMapRef, iReadOnlyDictOpenRef, systemTypeRef);
				EmitInitializeWithSingleTypeMap (pe, anchorTypeHandle, getExternalMemberRef, getProxyMemberRef, initializeRef);
			}
		} else {
			var externalDictTypeSpec = MakeIReadOnlyDictTypeSpec (pe, iReadOnlyDictOpenRef, systemTypeRef, keyIsString: true);
			var proxyDictTypeSpec = MakeIReadOnlyDictTypeSpec (pe, iReadOnlyDictOpenRef, systemTypeRef, keyIsString: false);

			if (emitArrayEntries) {
				var initializeRef = AddInitializeAggregateWithArraysRef (pe, trimmableTypeMapRef, iReadOnlyDictOpenRef, systemTypeRef);
				EmitInitializeWithAggregateTypeMapAndArrays (pe, perAssemblyTypeMapNames, getExternalMemberRef, getProxyMemberRef, initializeRef, externalDictTypeSpec, proxyDictTypeSpec, iReadOnlyDictOpenRef, systemTypeRef);
			} else {
				// TrimmableTypeMap.Initialize(IReadOnlyDictionary<string, Type>[], IReadOnlyDictionary<Type, Type>[])
				var initializeRef = AddInitializeAggregateRef (pe, trimmableTypeMapRef, iReadOnlyDictOpenRef, systemTypeRef);
				EmitInitializeWithAggregateTypeMap (pe, perAssemblyTypeMapNames, getExternalMemberRef, getProxyMemberRef, initializeRef, externalDictTypeSpec, proxyDictTypeSpec, iReadOnlyDictOpenRef, systemTypeRef);
			}
		}
	}

	/// <summary>
	/// Emits IL for the merged-universe + array-entries case. Calls
	/// <c>TypeMapping.GetOrCreateExternalTypeMapping&lt;T&gt;</c> five times — once each
	/// for the proxy-map anchor, external-map anchor, and the three rank sentinels —
	/// and passes all five results to the 5-arg <c>TrimmableTypeMap.Initialize</c>.
	/// </summary>
	static void EmitInitializeWithSingleTypeMapAndArrays (PEAssemblyBuilder pe, EntityHandle anchorTypeHandle,
		MemberReferenceHandle getExternalMemberRef, MemberReferenceHandle initializeRef,
		MemberReferenceHandle getProxyMemberRef)
	{
		// In merged-universe mode all per-assembly typemaps share the same anchor
		// (Java.Lang.Object). The rank sentinels live in each per-assembly typemap
		// dll, but in shared-universe mode there's only one such dll (the merged
		// one) — and the merged one's rank sentinels are referenced via the same
		// per-assembly anchor namespace. However, the merged dll's rank sentinels
		// are emitted there as TypeDefs, so we need a TypeRef from this root dll
		// back to those sentinels.
		//
		// In practice, when useSharedTypemapUniverse is true, perAssemblyTypeMapNames
		// has exactly one entry (the merged dll). The sentinels are emitted into
		// that dll. We'd need that dll's name to construct TypeRefs.
		//
		// For now, the merged-universe + array-entries path is unsupported because
		// the existing single-universe loader doesn't iterate perAssemblyTypeMapNames.
		// The aggregate path covers both Debug (per-assembly) and the future
		// merged-but-with-array case. Throw at generation time so misconfiguration
		// surfaces immediately.
		throw new NotSupportedException (
			"Merged-universe (Release) + array typemap entries is not yet wired up. " +
			"The aggregate/per-assembly path covers both Debug and the (future) merged case; " +
			"callers should use that path when emitArrayEntries is true.");
	}

	/// <summary>
	/// Emits IL for the per-assembly + array-entries case. Builds five
	/// <c>IReadOnlyDictionary&lt;...&gt;[]</c> arrays (typeMaps, proxyMaps,
	/// arrayMapsRank1, arrayMapsRank2, arrayMapsRank3) by querying each
	/// per-assembly typemap's <c>__TypeMapAnchor</c> + <c>__ArrayMapRank{1,2,3}</c>
	/// sentinels via <c>TypeMapping.GetOrCreateExternalTypeMapping&lt;T&gt;</c>, then
	/// passes them to the 5-arg <c>TrimmableTypeMap.Initialize</c>.
	/// </summary>
	static void EmitInitializeWithAggregateTypeMapAndArrays (PEAssemblyBuilder pe,
		IReadOnlyList<string> perAssemblyTypeMapNames,
		MemberReferenceHandle getExternalMemberRef, MemberReferenceHandle getProxyMemberRef,
		MemberReferenceHandle initializeRef,
		TypeSpecificationHandle externalDictTypeSpec, TypeSpecificationHandle proxyDictTypeSpec,
		TypeReferenceHandle iReadOnlyDictOpenRef, TypeReferenceHandle systemTypeRef)
	{
		var count = perAssemblyTypeMapNames.Count;

		var getExternalSpecs = new EntityHandle [count];
		var getProxySpecs = new EntityHandle [count];
		var getArrayRank1Specs = new EntityHandle [count];
		var getArrayRank2Specs = new EntityHandle [count];
		var getArrayRank3Specs = new EntityHandle [count];
		for (int i = 0; i < count; i++) {
			var asmRef = pe.FindOrAddAssemblyRef (perAssemblyTypeMapNames [i]);
			var perAsmAnchorRef = pe.Metadata.AddTypeReference (asmRef,
				default, pe.Metadata.GetOrAddString ("__TypeMapAnchor"));
			var rank1Ref = pe.Metadata.AddTypeReference (asmRef, default, pe.Metadata.GetOrAddString ("__ArrayMapRank1"));
			var rank2Ref = pe.Metadata.AddTypeReference (asmRef, default, pe.Metadata.GetOrAddString ("__ArrayMapRank2"));
			var rank3Ref = pe.Metadata.AddTypeReference (asmRef, default, pe.Metadata.GetOrAddString ("__ArrayMapRank3"));
			getExternalSpecs [i] = MakeGenericMethodSpec (pe, getExternalMemberRef, perAsmAnchorRef);
			getProxySpecs [i] = MakeGenericMethodSpec (pe, getProxyMemberRef, perAsmAnchorRef);
			getArrayRank1Specs [i] = MakeGenericMethodSpec (pe, getExternalMemberRef, rank1Ref);
			getArrayRank2Specs [i] = MakeGenericMethodSpec (pe, getExternalMemberRef, rank2Ref);
			getArrayRank3Specs [i] = MakeGenericMethodSpec (pe, getExternalMemberRef, rank3Ref);
		}

		pe.EmitBody ("Initialize",
			MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig,
			sig => sig.MethodSignature ().Parameters (0, rt => rt.Void (), p => { }),
			encoder => {
				// var typeMaps        = new IReadOnlyDictionary<string, Type>[N];   (loc 0)
				EmitNewArrayLocal (encoder, count, externalDictTypeSpec, slot: 0);
				EmitFillArrayLocal (encoder, count, getExternalSpecs, slot: 0);

				// var proxyMaps       = new IReadOnlyDictionary<Type, Type>[N];     (loc 1)
				EmitNewArrayLocal (encoder, count, proxyDictTypeSpec, slot: 1);
				EmitFillArrayLocal (encoder, count, getProxySpecs, slot: 1);

				// var arrayMapsRank1  = new IReadOnlyDictionary<string, Type>[N];   (loc 2)
				EmitNewArrayLocal (encoder, count, externalDictTypeSpec, slot: 2);
				EmitFillArrayLocal (encoder, count, getArrayRank1Specs, slot: 2);

				// var arrayMapsRank2  = new IReadOnlyDictionary<string, Type>[N];   (loc 3)
				EmitNewArrayLocal (encoder, count, externalDictTypeSpec, slot: 3);
				EmitFillArrayLocal (encoder, count, getArrayRank2Specs, slot: 3);

				// var arrayMapsRank3  = new IReadOnlyDictionary<string, Type>[N];   (loc 4)
				EmitNewArrayLocal (encoder, count, externalDictTypeSpec, slot: 4);
				EmitFillArrayLocal (encoder, count, getArrayRank3Specs, slot: 4);

				// TrimmableTypeMap.Initialize(typeMaps, proxyMaps, arrayMapsRank1, arrayMapsRank2, arrayMapsRank3)
				encoder.LoadLocal (0);
				encoder.LoadLocal (1);
				encoder.LoadLocal (2);
				encoder.LoadLocal (3);
				encoder.LoadLocal (4);
				encoder.OpCode (ILOpCode.Call);
				encoder.Token (initializeRef);
				encoder.OpCode (ILOpCode.Ret);
			},
			encodeLocals: localsSig => {
				localsSig.WriteByte (0x07); // LOCAL_SIG
				localsSig.WriteCompressedInteger (5); // count
				for (int i = 0; i < 5; i++) {
					// Each local: IReadOnlyDictionary<string|Type, Type>[]
					// Slot 1 (proxyMaps) is keyed by Type; the rest by string.
					localsSig.WriteByte (0x1D); // SZARRAY
					EncodeIReadOnlyDictType (localsSig, iReadOnlyDictOpenRef, systemTypeRef, keyIsString: i != 1);
				}
			});
	}

	static void EmitNewArrayLocal (InstructionEncoder encoder, int count, TypeSpecificationHandle elemSpec, int slot)
	{
		encoder.LoadConstantI4 (count);
		encoder.OpCode (ILOpCode.Newarr);
		encoder.Token (elemSpec);
		encoder.StoreLocal (slot);
	}

	static void EmitFillArrayLocal (InstructionEncoder encoder, int count, EntityHandle[] specs, int slot)
	{
		for (int i = 0; i < count; i++) {
			encoder.LoadLocal (slot);
			encoder.LoadConstantI4 (i);
			encoder.OpCode (ILOpCode.Call);
			encoder.Token (specs [i]);
			encoder.OpCode (ILOpCode.Stelem_ref);
		}
	}

	/// <summary>
	/// Creates a MemberRef for the 5-arg single-universe TrimmableTypeMap.Initialize
	/// (typeMap, proxyMap, arrayMapRank1, arrayMapRank2, arrayMapRank3).
	/// All three array maps are nullable.
	/// </summary>
	static MemberReferenceHandle AddInitializeSingleWithArraysRef (PEAssemblyBuilder pe, TypeReferenceHandle trimmableTypeMapRef,
		TypeReferenceHandle iReadOnlyDictOpenRef, TypeReferenceHandle systemTypeRef)
	{
		var blob = new BlobBuilder (96);
		blob.WriteByte (0x00); // DEFAULT (static)
		blob.WriteCompressedInteger (5); // parameter count
		blob.WriteByte (0x01); // return type: void
		EncodeIReadOnlyDictType (blob, iReadOnlyDictOpenRef, systemTypeRef, keyIsString: true);
		EncodeIReadOnlyDictType (blob, iReadOnlyDictOpenRef, systemTypeRef, keyIsString: false);
		EncodeIReadOnlyDictType (blob, iReadOnlyDictOpenRef, systemTypeRef, keyIsString: true);
		EncodeIReadOnlyDictType (blob, iReadOnlyDictOpenRef, systemTypeRef, keyIsString: true);
		EncodeIReadOnlyDictType (blob, iReadOnlyDictOpenRef, systemTypeRef, keyIsString: true);
		return pe.Metadata.AddMemberReference (trimmableTypeMapRef,
			pe.Metadata.GetOrAddString ("Initialize"), pe.Metadata.GetOrAddBlob (blob));
	}

	/// <summary>
	/// Creates a MemberRef for the 5-arg aggregate TrimmableTypeMap.Initialize
	/// (typeMaps[], proxyMaps[], arrayMapsRank1[], arrayMapsRank2[], arrayMapsRank3[]).
	/// </summary>
	static MemberReferenceHandle AddInitializeAggregateWithArraysRef (PEAssemblyBuilder pe, TypeReferenceHandle trimmableTypeMapRef,
		TypeReferenceHandle iReadOnlyDictOpenRef, TypeReferenceHandle systemTypeRef)
	{
		var blob = new BlobBuilder (96);
		blob.WriteByte (0x00); // DEFAULT (static)
		blob.WriteCompressedInteger (5); // parameter count
		blob.WriteByte (0x01); // return type: void
		// Param 1: IReadOnlyDictionary<string, Type>[]
		blob.WriteByte (0x1D); EncodeIReadOnlyDictType (blob, iReadOnlyDictOpenRef, systemTypeRef, keyIsString: true);
		// Param 2: IReadOnlyDictionary<Type, Type>[]
		blob.WriteByte (0x1D); EncodeIReadOnlyDictType (blob, iReadOnlyDictOpenRef, systemTypeRef, keyIsString: false);
		// Params 3-5: IReadOnlyDictionary<string, Type>?[]
		for (int i = 0; i < 3; i++) {
			blob.WriteByte (0x1D);
			EncodeIReadOnlyDictType (blob, iReadOnlyDictOpenRef, systemTypeRef, keyIsString: true);
		}
		return pe.Metadata.AddMemberReference (trimmableTypeMapRef,
			pe.Metadata.GetOrAddString ("Initialize"), pe.Metadata.GetOrAddBlob (blob));
	}

	static void EmitInitializeWithSingleTypeMap (PEAssemblyBuilder pe, EntityHandle anchorTypeHandle,
		MemberReferenceHandle getExternalMemberRef, MemberReferenceHandle getProxyMemberRef,
		MemberReferenceHandle initializeRef)
	{
		var getExternalSpec = MakeGenericMethodSpec (pe, getExternalMemberRef, anchorTypeHandle);
		var getProxySpec = MakeGenericMethodSpec (pe, getProxyMemberRef, anchorTypeHandle);

		pe.EmitBody ("Initialize",
			MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig,
			sig => sig.MethodSignature ().Parameters (0, rt => rt.Void (), p => { }),
			encoder => {
				// TypeMapping.GetOrCreateExternalTypeMapping<__TypeMapAnchor>()
				encoder.OpCode (ILOpCode.Call);
				encoder.Token (getExternalSpec);
				// TypeMapping.GetOrCreateProxyTypeMapping<__TypeMapAnchor>()
				encoder.OpCode (ILOpCode.Call);
				encoder.Token (getProxySpec);
				// TrimmableTypeMap.Initialize(typeMap, proxyMap)
				encoder.OpCode (ILOpCode.Call);
				encoder.Token (initializeRef);
				encoder.OpCode (ILOpCode.Ret);
			});
	}

	static void EmitInitializeWithAggregateTypeMap (PEAssemblyBuilder pe,
		IReadOnlyList<string> perAssemblyTypeMapNames,
		MemberReferenceHandle getExternalMemberRef, MemberReferenceHandle getProxyMemberRef,
		MemberReferenceHandle initializeRef,
		TypeSpecificationHandle externalDictTypeSpec, TypeSpecificationHandle proxyDictTypeSpec,
		TypeReferenceHandle iReadOnlyDictOpenRef, TypeReferenceHandle systemTypeRef)
	{
		var count = perAssemblyTypeMapNames.Count;

		var getExternalSpecs = new EntityHandle [count];
		var getProxySpecs = new EntityHandle [count];
		for (int i = 0; i < count; i++) {
			var asmRef = pe.FindOrAddAssemblyRef (perAssemblyTypeMapNames [i]);
			var perAsmAnchorRef = pe.Metadata.AddTypeReference (asmRef,
				default, pe.Metadata.GetOrAddString ("__TypeMapAnchor"));
			getExternalSpecs [i] = MakeGenericMethodSpec (pe, getExternalMemberRef, perAsmAnchorRef);
			getProxySpecs [i] = MakeGenericMethodSpec (pe, getProxyMemberRef, perAsmAnchorRef);
		}

		pe.EmitBody ("Initialize",
			MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig,
			sig => sig.MethodSignature ().Parameters (0, rt => rt.Void (), p => { }),
			encoder => {
				// var typeMaps = new IReadOnlyDictionary<string, Type>[N];
				encoder.LoadConstantI4 (count);
				encoder.OpCode (ILOpCode.Newarr);
				encoder.Token (externalDictTypeSpec);
				encoder.OpCode (ILOpCode.Stloc_0);

				for (int i = 0; i < count; i++) {
					encoder.OpCode (ILOpCode.Ldloc_0);
					encoder.LoadConstantI4 (i);
					encoder.OpCode (ILOpCode.Call);
					encoder.Token (getExternalSpecs [i]);
					encoder.OpCode (ILOpCode.Stelem_ref);
				}

				// var proxyMaps = new IReadOnlyDictionary<Type, Type>[N];
				encoder.LoadConstantI4 (count);
				encoder.OpCode (ILOpCode.Newarr);
				encoder.Token (proxyDictTypeSpec);
				encoder.OpCode (ILOpCode.Stloc_1);

				for (int i = 0; i < count; i++) {
					encoder.OpCode (ILOpCode.Ldloc_1);
					encoder.LoadConstantI4 (i);
					encoder.OpCode (ILOpCode.Call);
					encoder.Token (getProxySpecs [i]);
					encoder.OpCode (ILOpCode.Stelem_ref);
				}

				// TrimmableTypeMap.Initialize(typeMaps, proxyMaps)
				encoder.OpCode (ILOpCode.Ldloc_0);
				encoder.OpCode (ILOpCode.Ldloc_1);
				encoder.OpCode (ILOpCode.Call);
				encoder.Token (initializeRef);
				encoder.OpCode (ILOpCode.Ret);
			},
			encodeLocals: localsSig => {
				// LOCAL_SIG header + 2 locals
				localsSig.WriteByte (0x07); // LOCAL_SIG
				localsSig.WriteCompressedInteger (2); // count
				// local 0: IReadOnlyDictionary<string, Type>[]
				localsSig.WriteByte (0x1D); // SZARRAY
				EncodeIReadOnlyDictType (localsSig, iReadOnlyDictOpenRef, systemTypeRef, keyIsString: true);
				// local 1: IReadOnlyDictionary<Type, Type>[]
				localsSig.WriteByte (0x1D); // SZARRAY
				EncodeIReadOnlyDictType (localsSig, iReadOnlyDictOpenRef, systemTypeRef, keyIsString: false);
			});
	}

	/// <summary>
	/// Creates a MethodSpec for a generic method instantiation with a specific type argument.
	/// </summary>
	static MethodSpecificationHandle MakeGenericMethodSpec (PEAssemblyBuilder pe, MemberReferenceHandle openMethodRef, EntityHandle typeArg)
	{
		var blob = new BlobBuilder (16);
		blob.WriteByte (0x0A); // GENMETHOD_INST
		blob.WriteCompressedInteger (1); // generic arity
		blob.WriteByte (0x12); // ELEMENT_TYPE_CLASS
		blob.WriteCompressedInteger (CodedIndex.TypeDefOrRefOrSpec (typeArg));
		return pe.Metadata.AddMethodSpecification (openMethodRef, pe.Metadata.GetOrAddBlob (blob));
	}

	/// <summary>
	/// Creates a MemberRef for a TypeMapping generic method with the correct return type signature.
	/// The method signature is: generic arity 1, 0 params, returns IReadOnlyDictionary&lt;K, V&gt;.
	/// </summary>
	static MemberReferenceHandle AddTypeMappingMethodRef (PEAssemblyBuilder pe, TypeReferenceHandle typeMappingRef, string methodName,
		TypeReferenceHandle iReadOnlyDictOpenRef, TypeReferenceHandle systemTypeRef, bool keyIsString)
	{
		var blob = new BlobBuilder (64);
		// Method signature: GENERIC, arity=1, param count=0, return type
		blob.WriteByte (0x10); // IMAGE_CEE_CS_CALLCONV_GENERIC
		blob.WriteCompressedInteger (1); // generic parameter count
		blob.WriteCompressedInteger (0); // parameter count
		// Return type: IReadOnlyDictionary<K, Type>
		EncodeIReadOnlyDictType (blob, iReadOnlyDictOpenRef, systemTypeRef, keyIsString);
		return pe.Metadata.AddMemberReference (typeMappingRef,
			pe.Metadata.GetOrAddString (methodName), pe.Metadata.GetOrAddBlob (blob));
	}

	/// <summary>
	/// Creates a MemberRef for TrimmableTypeMap.Initialize(IReadOnlyDictionary&lt;string, Type&gt;, IReadOnlyDictionary&lt;Type, Type&gt;).
	/// </summary>
	static MemberReferenceHandle AddInitializeSingleRef (PEAssemblyBuilder pe, TypeReferenceHandle trimmableTypeMapRef,
		TypeReferenceHandle iReadOnlyDictOpenRef, TypeReferenceHandle systemTypeRef)
	{
		var blob = new BlobBuilder (64);
		blob.WriteByte (0x00); // DEFAULT (static)
		blob.WriteCompressedInteger (2); // parameter count
		blob.WriteByte (0x01); // return type: void
		EncodeIReadOnlyDictType (blob, iReadOnlyDictOpenRef, systemTypeRef, keyIsString: true);
		EncodeIReadOnlyDictType (blob, iReadOnlyDictOpenRef, systemTypeRef, keyIsString: false);
		return pe.Metadata.AddMemberReference (trimmableTypeMapRef,
			pe.Metadata.GetOrAddString ("Initialize"), pe.Metadata.GetOrAddBlob (blob));
	}

	/// <summary>
	/// Creates a MemberRef for TrimmableTypeMap.Initialize(IReadOnlyDictionary&lt;string, Type&gt;[], IReadOnlyDictionary&lt;Type, Type&gt;[]).
	/// </summary>
	static MemberReferenceHandle AddInitializeAggregateRef (PEAssemblyBuilder pe, TypeReferenceHandle trimmableTypeMapRef,
		TypeReferenceHandle iReadOnlyDictOpenRef, TypeReferenceHandle systemTypeRef)
	{
		var blob = new BlobBuilder (64);
		blob.WriteByte (0x00); // DEFAULT (static)
		blob.WriteCompressedInteger (2); // parameter count
		blob.WriteByte (0x01); // return type: void
		// Param 1: IReadOnlyDictionary<string, Type>[]
		blob.WriteByte (0x1D); // SZARRAY
		EncodeIReadOnlyDictType (blob, iReadOnlyDictOpenRef, systemTypeRef, keyIsString: true);
		// Param 2: IReadOnlyDictionary<Type, Type>[]
		blob.WriteByte (0x1D); // SZARRAY
		EncodeIReadOnlyDictType (blob, iReadOnlyDictOpenRef, systemTypeRef, keyIsString: false);
		return pe.Metadata.AddMemberReference (trimmableTypeMapRef,
			pe.Metadata.GetOrAddString ("Initialize"), pe.Metadata.GetOrAddBlob (blob));
	}

	/// <summary>
	/// Creates a TypeSpec for a closed IReadOnlyDictionary&lt;K, V&gt; generic type (for newarr).
	/// </summary>
	static TypeSpecificationHandle MakeIReadOnlyDictTypeSpec (PEAssemblyBuilder pe,
		TypeReferenceHandle iReadOnlyDictOpenRef, TypeReferenceHandle systemTypeRef, bool keyIsString)
	{
		var blob = new BlobBuilder (32);
		EncodeIReadOnlyDictType (blob, iReadOnlyDictOpenRef, systemTypeRef, keyIsString);
		return pe.Metadata.AddTypeSpecification (pe.Metadata.GetOrAddBlob (blob));
	}

	static void EncodeIReadOnlyDictType (BlobBuilder blob, TypeReferenceHandle iReadOnlyDictOpenRef, TypeReferenceHandle systemTypeRef, bool keyIsString)
	{
		blob.WriteByte (0x15); // ELEMENT_TYPE_GENERICINST
		blob.WriteByte (0x12); // ELEMENT_TYPE_CLASS
		blob.WriteCompressedInteger (CodedIndex.TypeDefOrRefOrSpec (iReadOnlyDictOpenRef));
		blob.WriteCompressedInteger (2); // generic arity = 2
		if (keyIsString) {
			blob.WriteByte (0x0E); // ELEMENT_TYPE_STRING
		} else {
			blob.WriteByte (0x12); // ELEMENT_TYPE_CLASS
			blob.WriteCompressedInteger (CodedIndex.TypeDefOrRefOrSpec (systemTypeRef));
		}
		blob.WriteByte (0x12); // ELEMENT_TYPE_CLASS
		blob.WriteCompressedInteger (CodedIndex.TypeDefOrRefOrSpec (systemTypeRef));
	}
}
