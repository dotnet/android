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
/// <item>Emits a <c>StartupHook</c> class whose <c>Initialize()</c> method constructs the
/// appropriate <see cref="Microsoft.Android.Runtime.ITypeMapWithAliasing"/> implementation
/// and registers it via <see cref="Microsoft.Android.Runtime.TrimmableTypeMap.Initialize"/>.</item>
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
/// // Startup hook — called by DOTNET_STARTUP_HOOKS before TrimmableTypeMap.Initialize():
/// internal static class StartupHook
/// {
///     internal static void Initialize ()
///     {
///         // Debug (per-assembly universes):
///         var u0 = new SingleUniverseTypeMap(
///             TypeMapping.GetOrCreateExternalTypeMapping&lt;_Mono_Android_TypeMap.__TypeMapAnchor&gt;(),
///             TypeMapping.GetOrCreateProxyTypeMapping&lt;_Mono_Android_TypeMap.__TypeMapAnchor&gt;());
///         var u1 = new SingleUniverseTypeMap(...);
///         var aggregate = new AggregateTypeMap(new[] { u0, u1 });
///         TrimmableTypeMap.Initialize(aggregate);
///
///         // Release (single merged universe):
///         var single = new SingleUniverseTypeMap(
///             TypeMapping.GetOrCreateExternalTypeMapping&lt;__TypeMapAnchor&gt;(),
///             TypeMapping.GetOrCreateProxyTypeMapping&lt;__TypeMapAnchor&gt;());
///         TrimmableTypeMap.Initialize(single);
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
	/// <param name="isRelease">True for Release (single merged universe), false for Debug (per-assembly universes).</param>
	/// <param name="stream">Stream to write the output PE to.</param>
	/// <param name="assemblyName">Optional assembly name (defaults to _Microsoft.Android.TypeMaps).</param>
	/// <param name="moduleName">Optional module name for the PE metadata.</param>
	public void Generate (IReadOnlyList<string> perAssemblyTypeMapNames, bool isRelease, Stream stream, string? assemblyName = null, string? moduleName = null)
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

		// Emit __TypeMapAnchor type definition (used as group type for root assembly)
		var objectRef = pe.Metadata.AddTypeReference (pe.SystemRuntimeRef,
			pe.Metadata.GetOrAddString ("System"), pe.Metadata.GetOrAddString ("Object"));

		var anchorTypeHandle = pe.Metadata.AddTypeDefinition (
			TypeAttributes.NotPublic | TypeAttributes.Sealed | TypeAttributes.Class,
			default,
			pe.Metadata.GetOrAddString ("__TypeMapAnchor"),
			objectRef,
			MetadataTokens.FieldDefinitionHandle (pe.Metadata.GetRowCount (TableIndex.Field) + 1),
			MetadataTokens.MethodDefinitionHandle (pe.Metadata.GetRowCount (TableIndex.MethodDef) + 1));

		// Emit [assembly: TypeMapAssemblyTargetAttribute<__TypeMapAnchor>("name")] for each per-assembly typemap
		EmitAssemblyTargetAttributes (pe, anchorTypeHandle, perAssemblyTypeMapNames);

		// Emit [assembly: IgnoresAccessChecksTo("...")] so the startup hook can access
		// internal types (SingleUniverseTypeMap, AggregateTypeMap, TrimmableTypeMap in Mono.Android,
		// and __TypeMapAnchor in each per-assembly typemap DLL).
		var accessTargets = new List<string> { "Mono.Android" };
		if (!isRelease) {
			accessTargets.AddRange (perAssemblyTypeMapNames);
		}
		pe.EmitIgnoresAccessChecksToAttribute (accessTargets);

		// Emit StartupHook class with Initialize() method
		EmitStartupHook (pe, anchorTypeHandle, perAssemblyTypeMapNames, isRelease);

		pe.WritePE (stream);
	}

	static void EmitAssemblyTargetAttributes (PEAssemblyBuilder pe, TypeDefinitionHandle anchorTypeHandle, IReadOnlyList<string> perAssemblyTypeMapNames)
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

	static void EmitStartupHook (PEAssemblyBuilder pe, TypeDefinitionHandle anchorTypeHandle, IReadOnlyList<string> perAssemblyTypeMapNames, bool isRelease)
	{
		var metadata = pe.Metadata;

		// Type references
		var iReadOnlyDictOpenRef = metadata.AddTypeReference (pe.SystemRuntimeRef,
			metadata.GetOrAddString ("System.Collections.Generic"), metadata.GetOrAddString ("IReadOnlyDictionary`2"));
		var systemTypeRef = metadata.AddTypeReference (pe.SystemRuntimeRef,
			metadata.GetOrAddString ("System"), metadata.GetOrAddString ("Type"));

		var singleUniverseTypeMapRef = metadata.AddTypeReference (pe.MonoAndroidRef,
			metadata.GetOrAddString ("Microsoft.Android.Runtime"), metadata.GetOrAddString ("SingleUniverseTypeMap"));
		var aggregateTypeMapRef = metadata.AddTypeReference (pe.MonoAndroidRef,
			metadata.GetOrAddString ("Microsoft.Android.Runtime"), metadata.GetOrAddString ("AggregateTypeMap"));
		var iTypeMapWithAliasingRef = metadata.AddTypeReference (pe.MonoAndroidRef,
			metadata.GetOrAddString ("Microsoft.Android.Runtime"), metadata.GetOrAddString ("ITypeMapWithAliasing"));
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

		// SingleUniverseTypeMap..ctor(IReadOnlyDictionary<string, Type>, IReadOnlyDictionary<Type, Type>)
		var singleCtorRef = AddSingleUniverseCtorRef (pe, singleUniverseTypeMapRef, iReadOnlyDictOpenRef, systemTypeRef);

		// AggregateTypeMap..ctor(SingleUniverseTypeMap[])
		var aggregateCtorRef = pe.AddMemberRef (aggregateTypeMapRef, ".ctor",
			sig => sig.MethodSignature (isInstanceMethod: true).Parameters (1,
				rt => rt.Void (),
				p => p.AddParameter ().Type ().SZArray ().Type (singleUniverseTypeMapRef, false)));

		// TrimmableTypeMap.Initialize(ITypeMapWithAliasing)
		var initializeRef = pe.AddMemberRef (trimmableTypeMapRef, "Initialize",
			sig => sig.MethodSignature ().Parameters (1,
				rt => rt.Void (),
				p => p.AddParameter ().Type ().Type (iTypeMapWithAliasingRef, false)));

		// Define the StartupHook type
		metadata.AddTypeDefinition (
			TypeAttributes.NotPublic | TypeAttributes.Sealed | TypeAttributes.Abstract | TypeAttributes.Class,
			default,
			metadata.GetOrAddString ("StartupHook"),
			metadata.AddTypeReference (pe.SystemRuntimeRef,
				metadata.GetOrAddString ("System"), metadata.GetOrAddString ("Object")),
			MetadataTokens.FieldDefinitionHandle (metadata.GetRowCount (TableIndex.Field) + 1),
			MetadataTokens.MethodDefinitionHandle (metadata.GetRowCount (TableIndex.MethodDef) + 1));

		if (isRelease) {
			EmitReleaseInitialize (pe, anchorTypeHandle, getExternalMemberRef, getProxyMemberRef, singleCtorRef, initializeRef);
		} else {
			EmitDebugInitialize (pe, perAssemblyTypeMapNames, getExternalMemberRef, getProxyMemberRef, singleCtorRef, aggregateCtorRef, singleUniverseTypeMapRef, initializeRef);
		}
	}

	static void EmitReleaseInitialize (PEAssemblyBuilder pe, TypeDefinitionHandle anchorTypeHandle,
		MemberReferenceHandle getExternalMemberRef, MemberReferenceHandle getProxyMemberRef,
		MemberReferenceHandle singleCtorRef, MemberReferenceHandle initializeRef)
	{
		var getExternalSpec = MakeGenericMethodSpec (pe, getExternalMemberRef, anchorTypeHandle);
		var getProxySpec = MakeGenericMethodSpec (pe, getProxyMemberRef, anchorTypeHandle);

		pe.EmitBody ("Initialize",
			MethodAttributes.Assembly | MethodAttributes.Static | MethodAttributes.HideBySig,
			sig => sig.MethodSignature ().Parameters (0, rt => rt.Void (), p => { }),
			encoder => {
				// var typeMap = TypeMapping.GetOrCreateExternalTypeMapping<__TypeMapAnchor>();
				encoder.OpCode (ILOpCode.Call);
				encoder.Token (getExternalSpec);
				// var proxyMap = TypeMapping.GetOrCreateProxyTypeMapping<__TypeMapAnchor>();
				encoder.OpCode (ILOpCode.Call);
				encoder.Token (getProxySpec);
				// var single = new SingleUniverseTypeMap(typeMap, proxyMap);
				encoder.OpCode (ILOpCode.Newobj);
				encoder.Token (singleCtorRef);
				// TrimmableTypeMap.Initialize(single);
				encoder.OpCode (ILOpCode.Call);
				encoder.Token (initializeRef);
				encoder.OpCode (ILOpCode.Ret);
			});
	}

	static void EmitDebugInitialize (PEAssemblyBuilder pe,
		IReadOnlyList<string> perAssemblyTypeMapNames,
		MemberReferenceHandle getExternalMemberRef, MemberReferenceHandle getProxyMemberRef,
		MemberReferenceHandle singleCtorRef,
		MemberReferenceHandle aggregateCtorRef, TypeReferenceHandle singleUniverseTypeMapRef,
		MemberReferenceHandle initializeRef)
	{
		var count = perAssemblyTypeMapNames.Count;

		var getExternalSpecs = new EntityHandle[count];
		var getProxySpecs = new EntityHandle[count];
		for (int i = 0; i < count; i++) {
			var asmRef = pe.FindOrAddAssemblyRef (perAssemblyTypeMapNames [i]);
			var perAsmAnchorRef = pe.Metadata.AddTypeReference (asmRef,
				default, pe.Metadata.GetOrAddString ("__TypeMapAnchor"));
			getExternalSpecs [i] = MakeGenericMethodSpec (pe, getExternalMemberRef, perAsmAnchorRef);
			getProxySpecs [i] = MakeGenericMethodSpec (pe, getProxyMemberRef, perAsmAnchorRef);
		}

		pe.EmitBody ("Initialize",
			MethodAttributes.Assembly | MethodAttributes.Static | MethodAttributes.HideBySig,
			sig => sig.MethodSignature ().Parameters (0, rt => rt.Void (), p => { }),
			encoder => {
				// var universes = new SingleUniverseTypeMap[N];
				encoder.LoadConstantI4 (count);
				encoder.OpCode (ILOpCode.Newarr);
				encoder.Token (singleUniverseTypeMapRef);

				for (int i = 0; i < count; i++) {
					encoder.OpCode (ILOpCode.Dup);
					encoder.LoadConstantI4 (i);
					// TypeMapping.GetOrCreateExternalTypeMapping<_X.TypeMap.__TypeMapAnchor>()
					encoder.OpCode (ILOpCode.Call);
					encoder.Token (getExternalSpecs [i]);
					// TypeMapping.GetOrCreateProxyTypeMapping<_X.TypeMap.__TypeMapAnchor>()
					encoder.OpCode (ILOpCode.Call);
					encoder.Token (getProxySpecs [i]);
					// new SingleUniverseTypeMap(typeMap, proxyMap)
					encoder.OpCode (ILOpCode.Newobj);
					encoder.Token (singleCtorRef);
					encoder.OpCode (ILOpCode.Stelem_ref);
				}

				// var aggregate = new AggregateTypeMap(universes);
				encoder.OpCode (ILOpCode.Newobj);
				encoder.Token (aggregateCtorRef);
				// TrimmableTypeMap.Initialize(aggregate);
				encoder.OpCode (ILOpCode.Call);
				encoder.Token (initializeRef);
				encoder.OpCode (ILOpCode.Ret);
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
	/// Creates a MemberRef for SingleUniverseTypeMap..ctor(IReadOnlyDictionary&lt;string, Type&gt;, IReadOnlyDictionary&lt;Type, Type&gt;).
	/// </summary>
	static MemberReferenceHandle AddSingleUniverseCtorRef (PEAssemblyBuilder pe, TypeReferenceHandle singleUniverseTypeMapRef,
		TypeReferenceHandle iReadOnlyDictOpenRef, TypeReferenceHandle systemTypeRef)
	{
		var blob = new BlobBuilder (64);
		// Instance method signature: HASTHIS | DEFAULT
		blob.WriteByte (0x20); // IMAGE_CEE_CS_CALLCONV_HASTHIS
		blob.WriteCompressedInteger (2); // parameter count
		blob.WriteByte (0x01); // return type: void
		// Param 1: IReadOnlyDictionary<string, Type>
		EncodeIReadOnlyDictType (blob, iReadOnlyDictOpenRef, systemTypeRef, keyIsString: true);
		// Param 2: IReadOnlyDictionary<Type, Type>
		EncodeIReadOnlyDictType (blob, iReadOnlyDictOpenRef, systemTypeRef, keyIsString: false);
		return pe.Metadata.AddMemberReference (singleUniverseTypeMapRef,
			pe.Metadata.GetOrAddString (".ctor"), pe.Metadata.GetOrAddBlob (blob));
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
