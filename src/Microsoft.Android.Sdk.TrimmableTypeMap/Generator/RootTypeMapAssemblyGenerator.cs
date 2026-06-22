using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

using TrackedInstructionEncoder = Microsoft.Android.Sdk.TrimmableTypeMap.PEAssemblyBuilder.TrackedInstructionEncoder;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

/// <summary>
/// Generates the root <c>_Microsoft.Android.TypeMaps.dll</c> assembly that:
/// <list type="bullet">
/// <item>References all per-assembly typemap assemblies via <c>[assembly: TypeMapAssemblyTargetAttribute&lt;T&gt;("name")]</c>.</item>
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
///                 TypeMapping.GetOrCreateProxyTypeMapping&lt;Java.Lang.Object&gt;(),
///                 arrayMapsByAssemblyAndRank);
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
///             TrimmableTypeMap.Initialize(typeMaps, proxyMaps, arrayMapsByAssemblyAndRank);
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
	/// Maximum array rank for which per-assembly typemaps emitted <c>__ArrayMapRank{N}</c>
	/// sentinels. Must match the value passed to the per-assembly generators. 0 means
	/// no array sentinels were emitted; the loader passes <c>null</c> for array maps.
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
		pe.EmitPreamble (assemblyName, moduleName, MetadataHelper.ComputeRootContentFingerprint (perAssemblyTypeMapNames, useSharedTypemapUniverse, maxArrayRank));

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

		// Emit [assembly: TypeMapAssemblyTargetAttribute<T>("name")] for each per-assembly typemap.
		// T must match the group type later passed to TypeMapping.GetOrCreate*TypeMapping<T>().
		if (useSharedTypemapUniverse) {
			EmitSharedUniverseAssemblyTargetAttributes (pe, anchorTypeHandle, perAssemblyTypeMapNames);
		} else {
			EmitPerAssemblyUniverseAssemblyTargetAttributes (pe, perAssemblyTypeMapNames);
		}
		EmitArrayAssemblyTargetAttributes (pe, perAssemblyTypeMapNames, maxArrayRank);

		// Emit [assembly: IgnoresAccessChecksTo("...")] so TypeMapLoader.Initialize() can access
		// internal types (TrimmableTypeMap and friends in Mono.Android, and private anchors
		// in each per-assembly typemap DLL when aggregate universes or array maps are used).
		var accessTargets = new List<string> { "Mono.Android" };
		if (!useSharedTypemapUniverse || maxArrayRank > 0) {
			accessTargets.AddRange (perAssemblyTypeMapNames);
		}
		pe.EmitIgnoresAccessChecksToAttribute (accessTargets);

		// Emit TypeMapLoader class with Initialize() method
		EmitTypeMapLoader (pe, anchorTypeHandle, perAssemblyTypeMapNames, useSharedTypemapUniverse, maxArrayRank, assemblyName);

		pe.WritePE (stream);
	}

	static void EmitSharedUniverseAssemblyTargetAttributes (PEAssemblyBuilder pe, EntityHandle anchorTypeHandle, IReadOnlyList<string> perAssemblyTypeMapNames)
	{
		var openAttrRef = GetTypeMapAssemblyTargetAttributeRef (pe);
		var ctorRef = GetTypeMapAssemblyTargetAttributeCtorRef (pe, openAttrRef, anchorTypeHandle);
		foreach (var name in perAssemblyTypeMapNames) {
			EmitAssemblyTargetAttribute (pe, ctorRef, name);
		}
	}

	static void EmitPerAssemblyUniverseAssemblyTargetAttributes (PEAssemblyBuilder pe, IReadOnlyList<string> perAssemblyTypeMapNames)
	{
		var openAttrRef = GetTypeMapAssemblyTargetAttributeRef (pe);
		foreach (var name in perAssemblyTypeMapNames) {
			var asmRef = pe.FindOrAddAssemblyRef (name);
			var perAssemblyAnchorRef = pe.Metadata.AddTypeReference (asmRef,
				default, pe.Metadata.GetOrAddString ("__TypeMapAnchor"));
			var ctorRef = GetTypeMapAssemblyTargetAttributeCtorRef (pe, openAttrRef, perAssemblyAnchorRef);
			EmitAssemblyTargetAttribute (pe, ctorRef, name);
		}
	}

	static void EmitArrayAssemblyTargetAttributes (PEAssemblyBuilder pe, IReadOnlyList<string> perAssemblyTypeMapNames, int maxArrayRank)
	{
		if (maxArrayRank == 0) {
			return;
		}

		var openAttrRef = GetTypeMapAssemblyTargetAttributeRef (pe);
		foreach (var name in perAssemblyTypeMapNames) {
			var asmRef = pe.FindOrAddAssemblyRef (name);
			for (int rank = 1; rank <= maxArrayRank; rank++) {
				var rankAnchorRef = pe.Metadata.AddTypeReference (asmRef,
					default, pe.Metadata.GetOrAddString ($"__ArrayMapRank{rank}"));
				var ctorRef = GetTypeMapAssemblyTargetAttributeCtorRef (pe, openAttrRef, rankAnchorRef);
				EmitAssemblyTargetAttribute (pe, ctorRef, name);
			}
		}
	}

	static TypeReferenceHandle GetTypeMapAssemblyTargetAttributeRef (PEAssemblyBuilder pe)
	{
		return pe.Metadata.AddTypeReference (pe.SystemRuntimeInteropServicesRef,
			pe.Metadata.GetOrAddString ("System.Runtime.InteropServices"),
			pe.Metadata.GetOrAddString ("TypeMapAssemblyTargetAttribute`1"));
	}

	static MemberReferenceHandle GetTypeMapAssemblyTargetAttributeCtorRef (PEAssemblyBuilder pe, EntityHandle openAttrRef, EntityHandle anchorTypeHandle)
	{
		var closedAttrTypeSpec = pe.MakeGenericTypeSpec (openAttrRef, anchorTypeHandle);
		return pe.AddMemberRef (closedAttrTypeSpec, ".ctor",
			sig => sig.MethodSignature (isInstanceMethod: true).Parameters (1,
				rt => rt.Void (),
				p => p.AddParameter ().Type ().String ()));
	}

	static void EmitAssemblyTargetAttribute (PEAssemblyBuilder pe, MemberReferenceHandle ctorRef, string name)
	{
		var blobHandle = pe.BuildAttributeBlob (blob => blob.WriteSerializedString (name));
		pe.Metadata.AddCustomAttribute (EntityHandle.AssemblyDefinition, ctorRef, blobHandle);
	}

	static void EmitTypeMapLoader (PEAssemblyBuilder pe, EntityHandle anchorTypeHandle, IReadOnlyList<string> perAssemblyTypeMapNames, bool useSharedTypemapUniverse, int maxArrayRank, string assemblyName)
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

		var externalDictTypeSpec = MakeIReadOnlyDictTypeSpec (pe, iReadOnlyDictOpenRef, systemTypeRef, keyIsString: true);
		var externalDictArrayTypeSpec = MakeIReadOnlyDictArrayTypeSpec (pe, iReadOnlyDictOpenRef, systemTypeRef, keyIsString: true);

		if (useSharedTypemapUniverse) {
			if (maxArrayRank > 0) {
				var initializeRef = AddInitializeSingleWithArraysRef (pe, trimmableTypeMapRef, iReadOnlyDictOpenRef, systemTypeRef);
				EmitInitializeWithSingleTypeMap (pe, anchorTypeHandle, getExternalMemberRef, getProxyMemberRef,
					initializeRef, externalDictTypeSpec, externalDictArrayTypeSpec, perAssemblyTypeMapNames, maxArrayRank, assemblyName);
			} else {
				var initializeRef = AddInitializeSingleNoArraysRef (pe, trimmableTypeMapRef, iReadOnlyDictOpenRef, systemTypeRef);
				EmitInitializeWithSingleTypeMapNoArrays (pe, anchorTypeHandle, getExternalMemberRef, getProxyMemberRef, initializeRef, assemblyName);
			}
		} else {
			var proxyDictTypeSpec = MakeIReadOnlyDictTypeSpec (pe, iReadOnlyDictOpenRef, systemTypeRef, keyIsString: false);
			if (maxArrayRank > 0) {
				var initializeRef = AddInitializeAggregateWithArraysRef (pe, trimmableTypeMapRef, iReadOnlyDictOpenRef, systemTypeRef);
				EmitInitializeWithAggregateTypeMap (pe, perAssemblyTypeMapNames, getExternalMemberRef, getProxyMemberRef,
					initializeRef, externalDictTypeSpec, proxyDictTypeSpec, externalDictArrayTypeSpec, iReadOnlyDictOpenRef, systemTypeRef, maxArrayRank, assemblyName);
			} else {
				var initializeRef = AddInitializeAggregateNoArraysRef (pe, trimmableTypeMapRef, iReadOnlyDictOpenRef, systemTypeRef);
				EmitInitializeWithAggregateTypeMapNoArrays (pe, perAssemblyTypeMapNames, getExternalMemberRef, getProxyMemberRef,
					initializeRef, externalDictTypeSpec, proxyDictTypeSpec, iReadOnlyDictOpenRef, systemTypeRef, assemblyName);
			}
		}
	}

	/// <summary>
	/// Aggregate IL emit. Builds <c>typeMaps[N]</c>, <c>proxyMaps[N]</c>, and either
	/// <c>arrayMapsByAssemblyAndRank[N][maxArrayRank]</c> from per-assembly
	/// <c>__ArrayMapRank{N}</c> anchors or <c>null</c> when <paramref name="maxArrayRank"/> is 0.
	/// </summary>
	static void EmitInitializeWithAggregateTypeMap (PEAssemblyBuilder pe,
		IReadOnlyList<string> perAssemblyTypeMapNames,
		MemberReferenceHandle getExternalMemberRef, MemberReferenceHandle getProxyMemberRef,
		MemberReferenceHandle initializeRef,
		TypeSpecificationHandle externalDictTypeSpec, TypeSpecificationHandle proxyDictTypeSpec,
		TypeSpecificationHandle externalDictArrayTypeSpec,
		TypeReferenceHandle iReadOnlyDictOpenRef, TypeReferenceHandle systemTypeRef,
		int maxArrayRank,
		string assemblyName)
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
				EmitSetTypeMappingEntryAssembly (pe, encoder, assemblyName);
				// var typeMaps = new IReadOnlyDictionary<string, Type>[N];   (loc 0)
				EmitNewArrayLocal (encoder, count, externalDictTypeSpec, slot: 0);
				EmitFillArrayLocal (encoder, count, getExternalSpecs, slot: 0);

				// var proxyMaps = new IReadOnlyDictionary<Type, Type>[N];    (loc 1)
				EmitNewArrayLocal (encoder, count, proxyDictTypeSpec, slot: 1);
				EmitFillArrayLocal (encoder, count, getProxySpecs, slot: 1);

				// TrimmableTypeMap.Initialize(typeMaps, proxyMaps, arrayMapsByAssemblyAndRank-or-null)
				encoder.LoadLocal (0);
				encoder.LoadLocal (1);
				EmitArrayMapsByAssemblyAndRankOrNull (pe, encoder, perAssemblyTypeMapNames, getExternalMemberRef, externalDictTypeSpec, externalDictArrayTypeSpec, maxArrayRank);
				encoder.Call (initializeRef, parameterCount: 3);
				encoder.Return ();
			},
			encodeLocals: localsSig => {
				localsSig.WriteByte (0x07); // LOCAL_SIG
				localsSig.WriteCompressedInteger (2); // count
				// loc 0: IReadOnlyDictionary<string, Type>[]
				localsSig.WriteByte (0x1D);
				EncodeIReadOnlyDictType (localsSig, iReadOnlyDictOpenRef, systemTypeRef, keyIsString: true);
				// loc 1: IReadOnlyDictionary<Type, Type>[]
				localsSig.WriteByte (0x1D);
				EncodeIReadOnlyDictType (localsSig, iReadOnlyDictOpenRef, systemTypeRef, keyIsString: false);
			});
	}

	static void EmitNewArrayLocal (TrackedInstructionEncoder encoder, int count, TypeSpecificationHandle elemSpec, int slot)
	{
		encoder.LoadConstantI4 (count);
		encoder.NewArray (elemSpec);
		encoder.StoreLocal (slot);
	}

	static void EmitFillArrayLocal (TrackedInstructionEncoder encoder, int count, EntityHandle[] specs, int slot)
	{
		for (int i = 0; i < count; i++) {
			encoder.LoadLocal (slot);
			encoder.LoadConstantI4 (i);
			encoder.Call (specs [i], parameterCount: 0, returnsValue: true);
			encoder.OpCode (ILOpCode.Stelem_ref);
		}
	}

	/// <summary>
	/// Aggregate IL emit without array maps. Calls the 2-arg overload:
	/// <c>TrimmableTypeMap.Initialize(typeMaps[], proxyMaps[])</c>.
	/// </summary>
	static void EmitInitializeWithAggregateTypeMapNoArrays (PEAssemblyBuilder pe,
		IReadOnlyList<string> perAssemblyTypeMapNames,
		MemberReferenceHandle getExternalMemberRef, MemberReferenceHandle getProxyMemberRef,
		MemberReferenceHandle initializeRef,
		TypeSpecificationHandle externalDictTypeSpec, TypeSpecificationHandle proxyDictTypeSpec,
		TypeReferenceHandle iReadOnlyDictOpenRef, TypeReferenceHandle systemTypeRef,
		string assemblyName)
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
				EmitSetTypeMappingEntryAssembly (pe, encoder, assemblyName);
				EmitNewArrayLocal (encoder, count, externalDictTypeSpec, slot: 0);
				EmitFillArrayLocal (encoder, count, getExternalSpecs, slot: 0);

				EmitNewArrayLocal (encoder, count, proxyDictTypeSpec, slot: 1);
				EmitFillArrayLocal (encoder, count, getProxySpecs, slot: 1);

				encoder.LoadLocal (0);
				encoder.LoadLocal (1);
				encoder.Call (initializeRef, parameterCount: 2);
				encoder.Return ();
			},
			encodeLocals: localsSig => {
				localsSig.WriteByte (0x07); // LOCAL_SIG
				localsSig.WriteCompressedInteger (2);
				localsSig.WriteByte (0x1D);
				EncodeIReadOnlyDictType (localsSig, iReadOnlyDictOpenRef, systemTypeRef, keyIsString: true);
				localsSig.WriteByte (0x1D);
				EncodeIReadOnlyDictType (localsSig, iReadOnlyDictOpenRef, systemTypeRef, keyIsString: false);
			});
	}

	/// <summary>MemberRef for <c>TrimmableTypeMap.Initialize(typeMaps[], proxyMaps[])</c> (2-arg, no array maps).</summary>
	static MemberReferenceHandle AddInitializeAggregateNoArraysRef (PEAssemblyBuilder pe, TypeReferenceHandle trimmableTypeMapRef,
		TypeReferenceHandle iReadOnlyDictOpenRef, TypeReferenceHandle systemTypeRef)
	{
		var blob = new BlobBuilder (64);
		blob.WriteByte (0x00); // DEFAULT (static)
		blob.WriteCompressedInteger (2); // parameter count
		blob.WriteByte (0x01); // return type: void
		blob.WriteByte (0x1D);
		EncodeIReadOnlyDictType (blob, iReadOnlyDictOpenRef, systemTypeRef, keyIsString: true);
		blob.WriteByte (0x1D);
		EncodeIReadOnlyDictType (blob, iReadOnlyDictOpenRef, systemTypeRef, keyIsString: false);
		return pe.Metadata.AddMemberReference (trimmableTypeMapRef,
			pe.Metadata.GetOrAddString ("Initialize"), pe.Metadata.GetOrAddBlob (blob));
	}

	/// <summary>MemberRef for <c>TrimmableTypeMap.Initialize(typeMaps[], proxyMaps[], arrayMapsByAssemblyAndRank[][])</c>.</summary>
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

	/// <summary>
	/// Shared-universe IL emit. Single merged main map (anchored on <c>Java.Lang.Object</c>)
	/// plus either <c>arrayMapsByAssemblyAndRank[N][maxArrayRank]</c> from per-assembly
	/// <c>__ArrayMapRank{N}</c> anchors or <c>null</c> when <paramref name="maxArrayRank"/> is 0.
	/// </summary>
	static void EmitInitializeWithSingleTypeMap (PEAssemblyBuilder pe, EntityHandle anchorTypeHandle,
		MemberReferenceHandle getExternalMemberRef, MemberReferenceHandle getProxyMemberRef,
		MemberReferenceHandle initializeRef,
		TypeSpecificationHandle externalDictTypeSpec, TypeSpecificationHandle externalDictArrayTypeSpec,
		IReadOnlyList<string> perAssemblyTypeMapNames,
		int maxArrayRank,
		string assemblyName)
	{
		var getExternalSpec = MakeGenericMethodSpec (pe, getExternalMemberRef, anchorTypeHandle);
		var getProxySpec = MakeGenericMethodSpec (pe, getProxyMemberRef, anchorTypeHandle);

		pe.EmitBody ("Initialize",
			MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig,
			sig => sig.MethodSignature ().Parameters (0, rt => rt.Void (), p => { }),
			encoder => {
				EmitSetTypeMappingEntryAssembly (pe, encoder, assemblyName);
				// TrimmableTypeMap.Initialize(GetExternal<JL.Object>(), GetProxy<JL.Object>(), arrayMapsByAssemblyAndRank-or-null)
				encoder.Call (getExternalSpec, parameterCount: 0, returnsValue: true);
				encoder.Call (getProxySpec, parameterCount: 0, returnsValue: true);
				EmitArrayMapsByAssemblyAndRankOrNull (pe, encoder, perAssemblyTypeMapNames, getExternalMemberRef, externalDictTypeSpec, externalDictArrayTypeSpec, maxArrayRank);
				encoder.Call (initializeRef, parameterCount: 3);
				encoder.Return ();
			});
	}

	/// <summary>
	/// Shared-universe IL emit without array maps. Calls the simpler 2-arg overload:
	/// <c>TrimmableTypeMap.Initialize(typeMap, proxyMap)</c>.
	/// </summary>
	static void EmitInitializeWithSingleTypeMapNoArrays (PEAssemblyBuilder pe, EntityHandle anchorTypeHandle,
		MemberReferenceHandle getExternalMemberRef, MemberReferenceHandle getProxyMemberRef,
		MemberReferenceHandle initializeRef,
		string assemblyName)
	{
		var getExternalSpec = MakeGenericMethodSpec (pe, getExternalMemberRef, anchorTypeHandle);
		var getProxySpec = MakeGenericMethodSpec (pe, getProxyMemberRef, anchorTypeHandle);

		pe.EmitBody ("Initialize",
			MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig,
			sig => sig.MethodSignature ().Parameters (0, rt => rt.Void (), p => { }),
			encoder => {
				EmitSetTypeMappingEntryAssembly (pe, encoder, assemblyName);
				encoder.Call (getExternalSpec, parameterCount: 0, returnsValue: true);
				encoder.Call (getProxySpec, parameterCount: 0, returnsValue: true);
				encoder.Call (initializeRef, parameterCount: 2);
				encoder.Return ();
			});
	}

	/// <summary>MemberRef for <c>TrimmableTypeMap.Initialize(typeMap, proxyMap)</c> (2-arg, no array maps).</summary>
	static MemberReferenceHandle AddInitializeSingleNoArraysRef (PEAssemblyBuilder pe, TypeReferenceHandle trimmableTypeMapRef,
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

	static void EmitSetTypeMappingEntryAssembly (PEAssemblyBuilder pe, TrackedInstructionEncoder encoder, string assemblyName)
	{
		var appContextRef = pe.Metadata.AddTypeReference (pe.SystemRuntimeRef,
			pe.Metadata.GetOrAddString ("System"), pe.Metadata.GetOrAddString ("AppContext"));
		var setDataRef = pe.AddMemberRef (appContextRef, "SetData",
			sig => sig.MethodSignature ().Parameters (2,
				rt => rt.Void (),
				p => {
					p.AddParameter ().Type ().String ();
					p.AddParameter ().Type ().Object ();
				}));
		encoder.LoadString (pe.Metadata.GetOrAddUserString ("System.Runtime.InteropServices.TypeMappingEntryAssembly"));
		encoder.LoadString (pe.Metadata.GetOrAddUserString (assemblyName));
		encoder.Call (setDataRef, parameterCount: 2);
	}

	/// <summary>MemberRef for <c>TrimmableTypeMap.Initialize(typeMap, proxyMap, arrayMapsByAssemblyAndRank[][])</c>.</summary>
	static MemberReferenceHandle AddInitializeSingleWithArraysRef (PEAssemblyBuilder pe, TypeReferenceHandle trimmableTypeMapRef,
		TypeReferenceHandle iReadOnlyDictOpenRef, TypeReferenceHandle systemTypeRef)
	{
		var blob = new BlobBuilder (96);
		blob.WriteByte (0x00); // DEFAULT (static)
		blob.WriteCompressedInteger (3); // parameter count
		blob.WriteByte (0x01); // return type: void
		EncodeIReadOnlyDictType (blob, iReadOnlyDictOpenRef, systemTypeRef, keyIsString: true);
		EncodeIReadOnlyDictType (blob, iReadOnlyDictOpenRef, systemTypeRef, keyIsString: false);
		blob.WriteByte (0x1D);
		blob.WriteByte (0x1D);
		EncodeIReadOnlyDictType (blob, iReadOnlyDictOpenRef, systemTypeRef, keyIsString: true);
		return pe.Metadata.AddMemberReference (trimmableTypeMapRef,
			pe.Metadata.GetOrAddString ("Initialize"), pe.Metadata.GetOrAddBlob (blob));
	}

	/// <summary>
	/// Emits IL that pushes either a fresh
	/// <c>IReadOnlyDictionary&lt;string, Type&gt;?[assemblyCount][maxArrayRank]</c>
	/// (when <paramref name="maxArrayRank"/> &gt; 0) or <c>ldnull</c>.
	/// </summary>
	static void EmitArrayMapsByAssemblyAndRankOrNull (PEAssemblyBuilder pe, TrackedInstructionEncoder encoder,
		IReadOnlyList<string> perAssemblyTypeMapNames,
		MemberReferenceHandle getExternalMemberRef,
		TypeSpecificationHandle externalDictTypeSpec, TypeSpecificationHandle externalDictArrayTypeSpec,
		int maxArrayRank)
	{
		if (maxArrayRank == 0) {
			encoder.OpCode (ILOpCode.Ldnull);
			return;
		}

		encoder.LoadConstantI4 (perAssemblyTypeMapNames.Count);
		encoder.NewArray (externalDictArrayTypeSpec);
		for (int i = 0; i < perAssemblyTypeMapNames.Count; i++) {
			var asmRef = pe.FindOrAddAssemblyRef (perAssemblyTypeMapNames [i]);
			encoder.OpCode (ILOpCode.Dup);
			encoder.LoadConstantI4 (i);
			EmitArrayMapsByRank (pe, encoder, asmRef, getExternalMemberRef, externalDictTypeSpec, maxArrayRank);
			encoder.OpCode (ILOpCode.Stelem_ref);
		}
	}

	static void EmitArrayMapsByRank (PEAssemblyBuilder pe, TrackedInstructionEncoder encoder,
		AssemblyReferenceHandle assemblyRef,
		MemberReferenceHandle getExternalMemberRef, TypeSpecificationHandle externalDictTypeSpec, int maxArrayRank)
	{
		encoder.LoadConstantI4 (maxArrayRank);
		encoder.NewArray (externalDictTypeSpec);
		for (int r = 0; r < maxArrayRank; r++) {
			var rankRef = pe.Metadata.AddTypeReference (assemblyRef, default,
				pe.Metadata.GetOrAddString ($"__ArrayMapRank{r + 1}"));
			var rankSpec = MakeGenericMethodSpec (pe, getExternalMemberRef, rankRef);
			encoder.OpCode (ILOpCode.Dup);
			encoder.LoadConstantI4 (r);
			encoder.Call (rankSpec, parameterCount: 0, returnsValue: true);
			encoder.OpCode (ILOpCode.Stelem_ref);
		}
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
	/// Creates a TypeSpec for a closed IReadOnlyDictionary&lt;K, V&gt; generic type (for newarr).
	/// </summary>
	static TypeSpecificationHandle MakeIReadOnlyDictTypeSpec (PEAssemblyBuilder pe,
		TypeReferenceHandle iReadOnlyDictOpenRef, TypeReferenceHandle systemTypeRef, bool keyIsString)
	{
		var blob = new BlobBuilder (32);
		EncodeIReadOnlyDictType (blob, iReadOnlyDictOpenRef, systemTypeRef, keyIsString);
		return pe.Metadata.AddTypeSpecification (pe.Metadata.GetOrAddBlob (blob));
	}

	/// <summary>
	/// Creates a TypeSpec for <c>IReadOnlyDictionary&lt;K, V&gt;[]</c> (for jagged-array <c>newarr</c>).
	/// </summary>
	static TypeSpecificationHandle MakeIReadOnlyDictArrayTypeSpec (PEAssemblyBuilder pe,
		TypeReferenceHandle iReadOnlyDictOpenRef, TypeReferenceHandle systemTypeRef, bool keyIsString)
	{
		var blob = new BlobBuilder (32);
		blob.WriteByte (0x1D);
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
