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
	/// <param name="stream">Stream to write the output PE to.</param>
	/// <param name="assemblyName">Optional assembly name (defaults to _Microsoft.Android.TypeMaps).</param>
	/// <param name="moduleName">Optional module name for the PE metadata.</param>
	/// <param name="maxArrayRank">
	/// Maximum array rank for which the per-assembly typemaps emitted
	/// <c>__ArrayMapRank{N}</c> sentinels. The generated <c>TypeMapLoader.Initialize</c>
	/// will collect ranks 1..<paramref name="maxArrayRank"/> from each per-assembly
	/// typemap and pass the resulting jagged array to the 3-arg
	/// <c>TrimmableTypeMap.Initialize</c> overload. 0 disables array-dict plumbing
	/// (the generated loader calls the existing 2-arg <c>Initialize</c>).
	/// Should match the value passed to the per-assembly typemap generators.
	/// </param>
	public void Generate (IReadOnlyList<string> perAssemblyTypeMapNames, bool useSharedTypemapUniverse, Stream stream, string? assemblyName = null, string? moduleName = null, int maxArrayRank = 0)
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
		EmitTypeMapLoader (pe, anchorTypeHandle, perAssemblyTypeMapNames, useSharedTypemapUniverse, maxArrayRank);

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

	static void EmitTypeMapLoader (PEAssemblyBuilder pe, EntityHandle anchorTypeHandle, IReadOnlyList<string> perAssemblyTypeMapNames, bool useSharedTypemapUniverse, int maxArrayRank)
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
			if (maxArrayRank > 0) {
				// Merged-universe + array entries: not yet wired up. The aggregate path
				// covers both Debug and the (future) merged-with-arrays case; callers
				// should fall back to per-assembly until shared-universe array sentinels
				// are wired here.
				throw new NotSupportedException (
					"Merged-universe (Release) + array typemap entries is not yet wired up. " +
					"Use the aggregate/per-assembly path when maxArrayRank > 0.");
			}
			var initializeRef = AddInitializeSingleRef (pe, trimmableTypeMapRef, iReadOnlyDictOpenRef, systemTypeRef);
			EmitInitializeWithSingleTypeMap (pe, anchorTypeHandle, getExternalMemberRef, getProxyMemberRef, initializeRef);
		} else {
			var externalDictTypeSpec = MakeIReadOnlyDictTypeSpec (pe, iReadOnlyDictOpenRef, systemTypeRef, keyIsString: true);
			var proxyDictTypeSpec = MakeIReadOnlyDictTypeSpec (pe, iReadOnlyDictOpenRef, systemTypeRef, keyIsString: false);

			if (maxArrayRank > 0) {
				var initializeRef = AddInitializeAggregateWithArraysRef (pe, trimmableTypeMapRef, iReadOnlyDictOpenRef, systemTypeRef);
				var arrayMapsByUniverseElemSpec = MakeArrayOfIReadOnlyDictTypeSpec (pe, iReadOnlyDictOpenRef, systemTypeRef);
				EmitInitializeWithAggregateTypeMapAndArrays (pe, perAssemblyTypeMapNames, getExternalMemberRef, getProxyMemberRef, initializeRef, externalDictTypeSpec, proxyDictTypeSpec, arrayMapsByUniverseElemSpec, iReadOnlyDictOpenRef, systemTypeRef, maxArrayRank);
			} else {
				var initializeRef = AddInitializeAggregateRef (pe, trimmableTypeMapRef, iReadOnlyDictOpenRef, systemTypeRef);
				EmitInitializeWithAggregateTypeMap (pe, perAssemblyTypeMapNames, getExternalMemberRef, getProxyMemberRef, initializeRef, externalDictTypeSpec, proxyDictTypeSpec, iReadOnlyDictOpenRef, systemTypeRef);
			}
		}
	}

	/// <summary>
	/// Emits IL for the per-assembly + array-entries case. Builds three locals:
	///   <list type="bullet">
	///     <item><c>typeMaps</c>: <c>IReadOnlyDictionary&lt;string, Type&gt;[N]</c></item>
	///     <item><c>proxyMaps</c>: <c>IReadOnlyDictionary&lt;Type, Type&gt;[N]</c></item>
	///     <item><c>perUniverseArrayMaps</c>: <c>IReadOnlyDictionary&lt;string, Type&gt;?[N][]</c>
	///       — a jagged array of N arrays, each of length <paramref name="maxArrayRank"/>,
	///       indexed 0-based by (rank - 1).</item>
	///   </list>
	/// then passes them to the 3-arg aggregate <c>TrimmableTypeMap.Initialize</c>.
	/// </summary>
	static void EmitInitializeWithAggregateTypeMapAndArrays (PEAssemblyBuilder pe,
		IReadOnlyList<string> perAssemblyTypeMapNames,
		MemberReferenceHandle getExternalMemberRef, MemberReferenceHandle getProxyMemberRef,
		MemberReferenceHandle initializeRef,
		TypeSpecificationHandle externalDictTypeSpec, TypeSpecificationHandle proxyDictTypeSpec,
		TypeSpecificationHandle arrayMapsByUniverseElemSpec,
		TypeReferenceHandle iReadOnlyDictOpenRef, TypeReferenceHandle systemTypeRef,
		int maxArrayRank)
	{
		var count = perAssemblyTypeMapNames.Count;

		var getExternalSpecs = new EntityHandle [count];
		var getProxySpecs = new EntityHandle [count];

		// rankSpecs [universe, rankIndex] — 0-based rankIndex.
		var rankSpecs = new EntityHandle [count, maxArrayRank];

		for (int i = 0; i < count; i++) {
			var asmRef = pe.FindOrAddAssemblyRef (perAssemblyTypeMapNames [i]);
			var perAsmAnchorRef = pe.Metadata.AddTypeReference (asmRef,
				default, pe.Metadata.GetOrAddString ("__TypeMapAnchor"));
			getExternalSpecs [i] = MakeGenericMethodSpec (pe, getExternalMemberRef, perAsmAnchorRef);
			getProxySpecs [i] = MakeGenericMethodSpec (pe, getProxyMemberRef, perAsmAnchorRef);
			for (int r = 0; r < maxArrayRank; r++) {
				var rankRef = pe.Metadata.AddTypeReference (asmRef, default, pe.Metadata.GetOrAddString ($"__ArrayMapRank{r + 1}"));
				rankSpecs [i, r] = MakeGenericMethodSpec (pe, getExternalMemberRef, rankRef);
			}
		}

		pe.EmitBody ("Initialize",
			MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig,
			sig => sig.MethodSignature ().Parameters (0, rt => rt.Void (), p => { }),
			encoder => {
				// var typeMaps = new IReadOnlyDictionary<string, Type>[N];   (loc 0)
				EmitNewArrayLocal (encoder, count, externalDictTypeSpec, slot: 0);
				EmitFillArrayLocal (encoder, count, getExternalSpecs, slot: 0);

				// var proxyMaps = new IReadOnlyDictionary<Type, Type>[N];    (loc 1)
				EmitNewArrayLocal (encoder, count, proxyDictTypeSpec, slot: 1);
				EmitFillArrayLocal (encoder, count, getProxySpecs, slot: 1);

				// var perUniverseArrayMaps = new IReadOnlyDictionary<string, Type>?[][N];   (loc 2)
				EmitNewArrayLocal (encoder, count, arrayMapsByUniverseElemSpec, slot: 2);

				// Fill perUniverseArrayMaps[u] = new IReadOnlyDictionary<string, Type>?[maxArrayRank]
				// populated with GetOrCreateExternalTypeMapping<__ArrayMapRank{r+1}_u>()
				for (int u = 0; u < count; u++) {
					encoder.LoadLocal (2);
					encoder.LoadConstantI4 (u);
					encoder.LoadConstantI4 (maxArrayRank);
					encoder.OpCode (ILOpCode.Newarr);
					encoder.Token (externalDictTypeSpec);
					for (int r = 0; r < maxArrayRank; r++) {
						encoder.OpCode (ILOpCode.Dup);
						encoder.LoadConstantI4 (r);
						encoder.OpCode (ILOpCode.Call);
						encoder.Token (rankSpecs [u, r]);
						encoder.OpCode (ILOpCode.Stelem_ref);
					}
					encoder.OpCode (ILOpCode.Stelem_ref);
				}

				// TrimmableTypeMap.Initialize(typeMaps, proxyMaps, perUniverseArrayMaps)
				encoder.LoadLocal (0);
				encoder.LoadLocal (1);
				encoder.LoadLocal (2);
				encoder.OpCode (ILOpCode.Call);
				encoder.Token (initializeRef);
				encoder.OpCode (ILOpCode.Ret);
			},
			encodeLocals: localsSig => {
				localsSig.WriteByte (0x07); // LOCAL_SIG
				localsSig.WriteCompressedInteger (3); // count
				// loc 0: IReadOnlyDictionary<string, Type>[]
				localsSig.WriteByte (0x1D); // SZARRAY
				EncodeIReadOnlyDictType (localsSig, iReadOnlyDictOpenRef, systemTypeRef, keyIsString: true);
				// loc 1: IReadOnlyDictionary<Type, Type>[]
				localsSig.WriteByte (0x1D);
				EncodeIReadOnlyDictType (localsSig, iReadOnlyDictOpenRef, systemTypeRef, keyIsString: false);
				// loc 2: IReadOnlyDictionary<string, Type>?[][]   — outer SZARRAY of inner SZARRAY
				localsSig.WriteByte (0x1D);
				localsSig.WriteByte (0x1D);
				EncodeIReadOnlyDictType (localsSig, iReadOnlyDictOpenRef, systemTypeRef, keyIsString: true);
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
	/// Creates a TypeSpec for <c>IReadOnlyDictionary&lt;string, Type&gt;?[]</c> — used as
	/// the element type of the per-universe outer array.
	/// </summary>
	static TypeSpecificationHandle MakeArrayOfIReadOnlyDictTypeSpec (PEAssemblyBuilder pe,
		TypeReferenceHandle iReadOnlyDictOpenRef, TypeReferenceHandle systemTypeRef)
	{
		var blob = new BlobBuilder (32);
		blob.WriteByte (0x1D); // SZARRAY
		EncodeIReadOnlyDictType (blob, iReadOnlyDictOpenRef, systemTypeRef, keyIsString: true);
		return pe.Metadata.AddTypeSpecification (pe.Metadata.GetOrAddBlob (blob));
	}

	/// <summary>
	/// Creates a MemberRef for the 3-arg aggregate TrimmableTypeMap.Initialize
	/// (typeMaps[], proxyMaps[], perUniverseArrayMaps[][]).
	/// </summary>
	static MemberReferenceHandle AddInitializeAggregateWithArraysRef (PEAssemblyBuilder pe, TypeReferenceHandle trimmableTypeMapRef,
		TypeReferenceHandle iReadOnlyDictOpenRef, TypeReferenceHandle systemTypeRef)
	{
		var blob = new BlobBuilder (96);
		blob.WriteByte (0x00); // DEFAULT (static)
		blob.WriteCompressedInteger (3); // parameter count
		blob.WriteByte (0x01); // return type: void
		// Param 1: IReadOnlyDictionary<string, Type>[]
		blob.WriteByte (0x1D);
		EncodeIReadOnlyDictType (blob, iReadOnlyDictOpenRef, systemTypeRef, keyIsString: true);
		// Param 2: IReadOnlyDictionary<Type, Type>[]
		blob.WriteByte (0x1D);
		EncodeIReadOnlyDictType (blob, iReadOnlyDictOpenRef, systemTypeRef, keyIsString: false);
		// Param 3: IReadOnlyDictionary<string, Type>?[][]
		blob.WriteByte (0x1D);
		blob.WriteByte (0x1D);
		EncodeIReadOnlyDictType (blob, iReadOnlyDictOpenRef, systemTypeRef, keyIsString: true);
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
