using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace Microsoft.Android.Build.TypeMap;

/// <summary>
/// Emits a TypeMap PE assembly from a <see cref="TypeMapAssemblyData"/>.
/// This is a mechanical translation — all decision logic lives in <see cref="ModelBuilder"/>.
/// </summary>
sealed class TypeMapAssemblyEmitter
{
	readonly Dictionary<string, AssemblyReferenceHandle> _asmRefCache = new (StringComparer.OrdinalIgnoreCase);
	readonly Dictionary<string, EntityHandle> _typeRefCache = new (StringComparer.Ordinal);

	AssemblyReferenceHandle _systemRuntimeRef;
	AssemblyReferenceHandle _monoAndroidRef;
	AssemblyReferenceHandle _javaInteropRef;
	AssemblyReferenceHandle _systemRuntimeInteropServicesRef;

	TypeReferenceHandle _javaPeerProxyRef;
	TypeReferenceHandle _iJavaPeerableRef;
	TypeReferenceHandle _jniHandleOwnershipRef;
	TypeReferenceHandle _iAndroidCallableWrapperRef;
	TypeReferenceHandle _systemTypeRef;
	TypeReferenceHandle _runtimeTypeHandleRef;
	TypeReferenceHandle _jniTypeRef;
	TypeReferenceHandle _trimmableNativeRegistrationRef;

	MemberReferenceHandle _baseCtorRef;
	MemberReferenceHandle _getTypeFromHandleRef;
	MemberReferenceHandle _createManagedPeerRef;
	MemberReferenceHandle _activateInstanceRef;
	MemberReferenceHandle _registerMethodRef;
	MemberReferenceHandle _ucoAttrCtorRef;
	MemberReferenceHandle _typeMapAttrCtorRef2Arg;
	MemberReferenceHandle _typeMapAttrCtorRef3Arg;

	/// <summary>
	/// Emits a PE assembly from the given model and writes it to <paramref name="outputPath"/>.
	/// </summary>
	public void Emit (TypeMapAssemblyData model, string outputPath)
	{
		if (model is null) {
			throw new ArgumentNullException (nameof (model));
		}
		if (outputPath is null) {
			throw new ArgumentNullException (nameof (outputPath));
		}

		_asmRefCache.Clear ();

		var dir = Path.GetDirectoryName (outputPath);
		if (!string.IsNullOrEmpty (dir)) {
			Directory.CreateDirectory (dir);
		}

		var metadata = new MetadataBuilder ();
		var ilBuilder = new BlobBuilder ();

		EmitAssemblyAndModule (metadata, model);
		EmitAssemblyReferences (metadata);
		EmitTypeReferences (metadata);
		EmitMemberReferences (metadata);
		EmitModuleType (metadata);

		// Track wrapper method names → handles for RegisterNatives
		var wrapperHandles = new Dictionary<string, MethodDefinitionHandle> ();

		foreach (var proxy in model.ProxyTypes) {
			EmitProxyType (metadata, ilBuilder, proxy, wrapperHandles);
		}

		foreach (var entry in model.Entries) {
			EmitTypeMapAttribute (metadata, entry);
		}

		EmitIgnoresAccessChecksToAttribute (metadata, ilBuilder, model.IgnoresAccessChecksTo);
		WritePE (metadata, ilBuilder, outputPath);
	}

	// ---- Assembly / Module ----

	void EmitAssemblyAndModule (MetadataBuilder metadata, TypeMapAssemblyData model)
	{
		metadata.AddAssembly (
			metadata.GetOrAddString (model.AssemblyName),
			new Version (1, 0, 0, 0),
			culture: default,
			publicKey: default,
			flags: 0,
			hashAlgorithm: AssemblyHashAlgorithm.None);

		metadata.AddModule (
			generation: 0,
			metadata.GetOrAddString (model.ModuleName),
			metadata.GetOrAddGuid (Guid.NewGuid ()),
			encId: default,
			encBaseId: default);
	}

	void EmitAssemblyReferences (MetadataBuilder metadata)
	{
		_systemRuntimeRef = AddAssemblyRef (metadata, "System.Runtime", new Version (11, 0, 0, 0));
		_monoAndroidRef = AddAssemblyRef (metadata, "Mono.Android", new Version (0, 0, 0, 0),
			publicKeyOrToken: new byte [] { 0x84, 0xe0, 0x4f, 0xf9, 0xcf, 0xb7, 0x90, 0x65 });
		_javaInteropRef = AddAssemblyRef (metadata, "Java.Interop", new Version (0, 0, 0, 0));
		_systemRuntimeInteropServicesRef = AddAssemblyRef (metadata, "System.Runtime.InteropServices", new Version (11, 0, 0, 0));
	}

	void EmitTypeReferences (MetadataBuilder metadata)
	{
		_javaPeerProxyRef = metadata.AddTypeReference (_monoAndroidRef,
			metadata.GetOrAddString ("Java.Interop"), metadata.GetOrAddString ("JavaPeerProxy"));
		_iJavaPeerableRef = metadata.AddTypeReference (_javaInteropRef,
			metadata.GetOrAddString ("Java.Interop"), metadata.GetOrAddString ("IJavaPeerable"));
		_jniHandleOwnershipRef = metadata.AddTypeReference (_monoAndroidRef,
			metadata.GetOrAddString ("Android.Runtime"), metadata.GetOrAddString ("JniHandleOwnership"));
		_iAndroidCallableWrapperRef = metadata.AddTypeReference (_monoAndroidRef,
			metadata.GetOrAddString ("Java.Interop"), metadata.GetOrAddString ("IAndroidCallableWrapper"));
		_systemTypeRef = metadata.AddTypeReference (_systemRuntimeRef,
			metadata.GetOrAddString ("System"), metadata.GetOrAddString ("Type"));
		_runtimeTypeHandleRef = metadata.AddTypeReference (_systemRuntimeRef,
			metadata.GetOrAddString ("System"), metadata.GetOrAddString ("RuntimeTypeHandle"));
		_jniTypeRef = metadata.AddTypeReference (_javaInteropRef,
			metadata.GetOrAddString ("Java.Interop"), metadata.GetOrAddString ("JniType"));
		_trimmableNativeRegistrationRef = metadata.AddTypeReference (_monoAndroidRef,
			metadata.GetOrAddString ("Android.Runtime"), metadata.GetOrAddString ("TrimmableNativeRegistration"));
	}

	void EmitMemberReferences (MetadataBuilder metadata)
	{
		_baseCtorRef = AddMemberRef (metadata, _javaPeerProxyRef, ".ctor",
			sig => sig.MethodSignature (isInstanceMethod: true).Parameters (0, rt => rt.Void (), p => { }));

		_getTypeFromHandleRef = AddMemberRef (metadata, _systemTypeRef, "GetTypeFromHandle",
			sig => sig.MethodSignature ().Parameters (1,
				rt => rt.Type ().Type (_systemTypeRef, false),
				p => p.AddParameter ().Type ().Type (_runtimeTypeHandleRef, true)));

		_createManagedPeerRef = AddMemberRef (metadata, _trimmableNativeRegistrationRef, "CreateManagedPeer",
			sig => sig.MethodSignature ().Parameters (3,
				rt => rt.Type ().Type (_iJavaPeerableRef, false),
				p => {
					p.AddParameter ().Type ().IntPtr ();
					p.AddParameter ().Type ().Type (_jniHandleOwnershipRef, true);
					p.AddParameter ().Type ().Type (_systemTypeRef, false);
				}));

		_activateInstanceRef = AddMemberRef (metadata, _trimmableNativeRegistrationRef, "ActivateInstance",
			sig => sig.MethodSignature ().Parameters (2,
				rt => rt.Void (),
				p => {
					p.AddParameter ().Type ().IntPtr ();
					p.AddParameter ().Type ().Type (_systemTypeRef, false);
				}));

		_registerMethodRef = AddMemberRef (metadata, _trimmableNativeRegistrationRef, "RegisterMethod",
			sig => sig.MethodSignature ().Parameters (4,
				rt => rt.Void (),
				p => {
					p.AddParameter ().Type ().Type (_jniTypeRef, false);
					p.AddParameter ().Type ().String ();
					p.AddParameter ().Type ().String ();
					p.AddParameter ().Type ().IntPtr ();
				}));

		var ucoAttrTypeRef = metadata.AddTypeReference (_systemRuntimeInteropServicesRef,
			metadata.GetOrAddString ("System.Runtime.InteropServices"),
			metadata.GetOrAddString ("UnmanagedCallersOnlyAttribute"));
		_ucoAttrCtorRef = AddMemberRef (metadata, ucoAttrTypeRef, ".ctor",
			sig => sig.MethodSignature (isInstanceMethod: true).Parameters (0, rt => rt.Void (), p => { }));

		EmitTypeMapAttributeCtorRef (metadata);
	}

	void EmitTypeMapAttributeCtorRef (MetadataBuilder metadata)
	{
		var typeMapAttrOpenRef = metadata.AddTypeReference (_systemRuntimeInteropServicesRef,
			metadata.GetOrAddString ("System.Runtime.InteropServices"),
			metadata.GetOrAddString ("TypeMapAttribute`1"));
		var javaLangObjectRef = metadata.AddTypeReference (_monoAndroidRef,
			metadata.GetOrAddString ("Java.Lang"), metadata.GetOrAddString ("Object"));

		var genericInstBlob = new BlobBuilder ();
		genericInstBlob.WriteByte (0x15); // ELEMENT_TYPE_GENERICINST
		genericInstBlob.WriteByte (0x12); // ELEMENT_TYPE_CLASS
		genericInstBlob.WriteCompressedInteger (CodedIndex.TypeDefOrRefOrSpec (typeMapAttrOpenRef));
		genericInstBlob.WriteCompressedInteger (1);
		genericInstBlob.WriteByte (0x12); // ELEMENT_TYPE_CLASS
		genericInstBlob.WriteCompressedInteger (CodedIndex.TypeDefOrRefOrSpec (javaLangObjectRef));
		var closedAttrTypeSpec = metadata.AddTypeSpecification (metadata.GetOrAddBlob (genericInstBlob));

		// 2-arg: TypeMap(string jniName, Type proxyType) — unconditional
		_typeMapAttrCtorRef2Arg = AddMemberRef (metadata, closedAttrTypeSpec, ".ctor",
			sig => sig.MethodSignature (isInstanceMethod: true).Parameters (2,
				rt => rt.Void (),
				p => {
					p.AddParameter ().Type ().String ();
					p.AddParameter ().Type ().Type (_systemTypeRef, false);
				}));

		// 3-arg: TypeMap(string jniName, Type proxyType, Type targetType) — trimmable
		_typeMapAttrCtorRef3Arg = AddMemberRef (metadata, closedAttrTypeSpec, ".ctor",
			sig => sig.MethodSignature (isInstanceMethod: true).Parameters (3,
				rt => rt.Void (),
				p => {
					p.AddParameter ().Type ().String ();
					p.AddParameter ().Type ().Type (_systemTypeRef, false);
					p.AddParameter ().Type ().Type (_systemTypeRef, false);
				}));
	}

	void EmitModuleType (MetadataBuilder metadata)
	{
		metadata.AddTypeDefinition (
			default, default,
			metadata.GetOrAddString ("<Module>"),
			default,
			MetadataTokens.FieldDefinitionHandle (1),
			MetadataTokens.MethodDefinitionHandle (1));
	}

	// ---- Proxy types ----

	void EmitProxyType (MetadataBuilder metadata, BlobBuilder ilBuilder, JavaPeerProxyData proxy,
		Dictionary<string, MethodDefinitionHandle> wrapperHandles)
	{
		var typeDefHandle = metadata.AddTypeDefinition (
			TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Class,
			metadata.GetOrAddString (proxy.Namespace),
			metadata.GetOrAddString (proxy.TypeName),
			_javaPeerProxyRef,
			MetadataTokens.FieldDefinitionHandle (metadata.GetRowCount (TableIndex.Field) + 1),
			MetadataTokens.MethodDefinitionHandle (metadata.GetRowCount (TableIndex.MethodDef) + 1));

		if (proxy.ImplementsIAndroidCallableWrapper) {
			metadata.AddInterfaceImplementation (typeDefHandle, _iAndroidCallableWrapperRef);
		}

		// .ctor
		EmitBody (metadata, ilBuilder, ".ctor",
			MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
			sig => sig.MethodSignature (isInstanceMethod: true).Parameters (0, rt => rt.Void (), p => { }),
			encoder => {
				encoder.OpCode (ILOpCode.Ldarg_0);
				encoder.Call (_baseCtorRef);
				encoder.OpCode (ILOpCode.Ret);
			});

		// CreateInstance
		EmitCreateInstance (metadata, ilBuilder, proxy);

		// get_TargetType
		EmitTypeGetter (metadata, ilBuilder, "get_TargetType", proxy.TargetType,
			MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.SpecialName | MethodAttributes.HideBySig);

		// get_InvokerType
		if (proxy.InvokerType != null) {
			EmitTypeGetter (metadata, ilBuilder, "get_InvokerType", proxy.InvokerType,
				MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig);
		}

		// UCO wrappers
		foreach (var uco in proxy.UcoMethods) {
			var handle = EmitUcoMethod (metadata, ilBuilder, uco);
			wrapperHandles [uco.WrapperName] = handle;
		}

		foreach (var uco in proxy.UcoConstructors) {
			var handle = EmitUcoConstructor (metadata, ilBuilder, uco);
			wrapperHandles [uco.WrapperName] = handle;
		}

		// RegisterNatives
		if (proxy.IsAcw) {
			EmitRegisterNatives (metadata, ilBuilder, proxy.NativeRegistrations, wrapperHandles);
		}
	}

	void EmitCreateInstance (MetadataBuilder metadata, BlobBuilder ilBuilder, JavaPeerProxyData proxy)
	{
		if (!proxy.HasActivation) {
			EmitBody (metadata, ilBuilder, "CreateInstance",
				MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig,
				sig => sig.MethodSignature (isInstanceMethod: true).Parameters (2,
					rt => rt.Type ().Type (_iJavaPeerableRef, false),
					p => {
						p.AddParameter ().Type ().IntPtr ();
						p.AddParameter ().Type ().Type (_jniHandleOwnershipRef, true);
					}),
				encoder => {
					encoder.OpCode (ILOpCode.Ldnull);
					encoder.OpCode (ILOpCode.Ret);
				});
			return;
		}

		var userTypeRef = ResolveTypeRef (metadata, proxy.TargetType);

		EmitBody (metadata, ilBuilder, "CreateInstance",
			MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig,
			sig => sig.MethodSignature (isInstanceMethod: true).Parameters (2,
				rt => rt.Type ().Type (_iJavaPeerableRef, false),
				p => {
					p.AddParameter ().Type ().IntPtr ();
					p.AddParameter ().Type ().Type (_jniHandleOwnershipRef, true);
				}),
			encoder => {
				encoder.OpCode (ILOpCode.Ldarg_1);
				encoder.OpCode (ILOpCode.Ldarg_2);
				encoder.OpCode (ILOpCode.Ldtoken);
				encoder.Token (userTypeRef);
				encoder.Call (_getTypeFromHandleRef);
				encoder.Call (_createManagedPeerRef);
				encoder.OpCode (ILOpCode.Ret);
			});
	}

	void EmitTypeGetter (MetadataBuilder metadata, BlobBuilder ilBuilder, string methodName,
		TypeRefData typeRef, MethodAttributes attrs)
	{
		var handle = ResolveTypeRef (metadata, typeRef);

		EmitBody (metadata, ilBuilder, methodName, attrs,
			sig => sig.MethodSignature (isInstanceMethod: true).Parameters (0,
				rt => rt.Type ().Type (_systemTypeRef, false),
				p => { }),
			encoder => {
				encoder.OpCode (ILOpCode.Ldtoken);
				encoder.Token (handle);
				encoder.Call (_getTypeFromHandleRef);
				encoder.OpCode (ILOpCode.Ret);
			});
	}

	// ---- UCO wrappers ----

	MethodDefinitionHandle EmitUcoMethod (MetadataBuilder metadata, BlobBuilder ilBuilder, UcoMethodData uco)
	{
		var jniParams = JniSignatureHelper.ParseParameterTypes (uco.JniSignature);
		var returnKind = JniSignatureHelper.ParseReturnType (uco.JniSignature);
		int paramCount = 2 + jniParams.Count;
		bool isVoid = returnKind == JniParamKind.Void;

		// Callback method reference
		var callbackTypeHandle = ResolveTypeRef (metadata, uco.CallbackType);
		var callbackRef = AddMemberRef (metadata, callbackTypeHandle, uco.CallbackMethodName,
			sig => sig.MethodSignature ().Parameters (paramCount,
				rt => { if (isVoid) rt.Void (); else JniSignatureHelper.EncodeClrType (rt.Type (), returnKind); },
				p => {
					p.AddParameter ().Type ().IntPtr ();
					p.AddParameter ().Type ().IntPtr ();
					for (int j = 0; j < jniParams.Count; j++)
						JniSignatureHelper.EncodeClrType (p.AddParameter ().Type (), jniParams [j]);
				}));

		var handle = EmitBody (metadata, ilBuilder, uco.WrapperName,
			MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig,
			sig => sig.MethodSignature ().Parameters (paramCount,
				rt => { if (isVoid) rt.Void (); else JniSignatureHelper.EncodeClrType (rt.Type (), returnKind); },
				p => {
					p.AddParameter ().Type ().IntPtr ();
					p.AddParameter ().Type ().IntPtr ();
					for (int j = 0; j < jniParams.Count; j++)
						JniSignatureHelper.EncodeClrType (p.AddParameter ().Type (), jniParams [j]);
				}),
			encoder => {
				for (int p = 0; p < paramCount; p++)
					encoder.LoadArgument (p);
				encoder.Call (callbackRef);
				encoder.OpCode (ILOpCode.Ret);
			});

		AddUnmanagedCallersOnlyAttribute (metadata, handle);
		return handle;
	}

	MethodDefinitionHandle EmitUcoConstructor (MetadataBuilder metadata, BlobBuilder ilBuilder, UcoConstructorData uco)
	{
		var userTypeRef = ResolveTypeRef (metadata, uco.TargetType);

		// UCO constructor wrappers always take exactly (IntPtr jnienv, IntPtr self) regardless
		// of the actual JNI constructor signature. The JNI parameters are not forwarded —
		// ActivateInstance only needs the jobject handle to create the managed peer.
		// The correct JNI signature is still used in RegisterNatives so the JNI runtime
		// dispatches to this wrapper for the right constructor overload.

		var handle = EmitBody (metadata, ilBuilder, uco.WrapperName,
			MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig,
			sig => sig.MethodSignature ().Parameters (2,
				rt => rt.Void (),
				p => {
					p.AddParameter ().Type ().IntPtr ();
					p.AddParameter ().Type ().IntPtr ();
				}),
			encoder => {
				encoder.LoadArgument (1); // self
				encoder.OpCode (ILOpCode.Ldtoken);
				encoder.Token (userTypeRef);
				encoder.Call (_getTypeFromHandleRef);
				encoder.Call (_activateInstanceRef);
				encoder.OpCode (ILOpCode.Ret);
			});

		AddUnmanagedCallersOnlyAttribute (metadata, handle);
		return handle;
	}

	// ---- RegisterNatives ----

	void EmitRegisterNatives (MetadataBuilder metadata, BlobBuilder ilBuilder,
		List<NativeRegistrationData> registrations, Dictionary<string, MethodDefinitionHandle> wrapperHandles)
	{
		EmitBody (metadata, ilBuilder, "RegisterNatives",
			MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig |
			MethodAttributes.NewSlot | MethodAttributes.Final,
			sig => sig.MethodSignature (isInstanceMethod: true).Parameters (1,
				rt => rt.Void (),
				p => p.AddParameter ().Type ().Type (_jniTypeRef, false)),
			encoder => {
				foreach (var reg in registrations) {
					if (!wrapperHandles.TryGetValue (reg.WrapperMethodName, out var wrapperHandle)) {
						continue;
					}
					encoder.LoadArgument (1);
					encoder.LoadString (metadata.GetOrAddUserString (reg.JniMethodName));
					encoder.LoadString (metadata.GetOrAddUserString (reg.JniSignature));
					encoder.OpCode (ILOpCode.Ldftn);
					encoder.Token (wrapperHandle);
					encoder.Call (_registerMethodRef);
				}
				encoder.OpCode (ILOpCode.Ret);
			});
	}

	// ---- TypeMap attributes ----

	void EmitTypeMapAttribute (MetadataBuilder metadata, TypeMapAttributeData entry)
	{
		// Per ECMA-335 §II.23.3, System.Type-typed constructor arguments are encoded
		// as SerString (assembly-qualified type name), not as TypeDefOrRef tokens.
		var attrBlob = new BlobBuilder ();
		attrBlob.WriteUInt16 (0x0001); // Prolog

		if (entry.IsUnconditional) {
			// 2-arg: TypeMap(jniName, proxyType) — always preserved
			attrBlob.WriteSerializedString (entry.JniName);
			attrBlob.WriteSerializedString (entry.ProxyTypeReference);
			attrBlob.WriteUInt16 (0x0000); // NumNamed
			metadata.AddCustomAttribute (EntityHandle.AssemblyDefinition, _typeMapAttrCtorRef2Arg,
				metadata.GetOrAddBlob (attrBlob));
		} else {
			// 3-arg: TypeMap(jniName, proxyType, targetType) — trimmable
			attrBlob.WriteSerializedString (entry.JniName);
			attrBlob.WriteSerializedString (entry.ProxyTypeReference);
			attrBlob.WriteSerializedString (entry.TargetTypeReference!);
			attrBlob.WriteUInt16 (0x0000); // NumNamed
			metadata.AddCustomAttribute (EntityHandle.AssemblyDefinition, _typeMapAttrCtorRef3Arg,
				metadata.GetOrAddBlob (attrBlob));
		}
	}

	// ---- IgnoresAccessChecksTo ----

	void EmitIgnoresAccessChecksToAttribute (MetadataBuilder metadata, BlobBuilder ilBuilder, List<string> assemblyNames)
	{
		var attributeTypeRef = metadata.AddTypeReference (_systemRuntimeRef,
			metadata.GetOrAddString ("System"), metadata.GetOrAddString ("Attribute"));

		int typeFieldStart = metadata.GetRowCount (TableIndex.Field) + 1;
		int typeMethodStart = metadata.GetRowCount (TableIndex.MethodDef) + 1;

		var baseAttrCtorRef = AddMemberRef (metadata, attributeTypeRef, ".ctor",
			sig => sig.MethodSignature (isInstanceMethod: true).Parameters (0, rt => rt.Void (), p => { }));

		var ctorDef = EmitBody (metadata, ilBuilder, ".ctor",
			MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
			sig => sig.MethodSignature (isInstanceMethod: true).Parameters (1,
				rt => rt.Void (),
				p => p.AddParameter ().Type ().String ()),
			encoder => {
				encoder.LoadArgument (0);
				encoder.Call (baseAttrCtorRef);
				encoder.OpCode (ILOpCode.Ret);
			});

		metadata.AddTypeDefinition (
			TypeAttributes.NotPublic | TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit,
			metadata.GetOrAddString ("System.Runtime.CompilerServices"),
			metadata.GetOrAddString ("IgnoresAccessChecksToAttribute"),
			attributeTypeRef,
			MetadataTokens.FieldDefinitionHandle (typeFieldStart),
			MetadataTokens.MethodDefinitionHandle (typeMethodStart));

		foreach (var asmName in assemblyNames) {
			var attrBlob = new BlobBuilder ();
			attrBlob.WriteUInt16 (1);
			attrBlob.WriteSerializedString (asmName);
			attrBlob.WriteUInt16 (0);
			metadata.AddCustomAttribute (EntityHandle.AssemblyDefinition, ctorDef, metadata.GetOrAddBlob (attrBlob));
		}
	}

	// ---- Plumbing helpers ----

	AssemblyReferenceHandle AddAssemblyRef (MetadataBuilder metadata, string name, Version version,
		byte []? publicKeyOrToken = null)
	{
		var handle = metadata.AddAssemblyReference (
			metadata.GetOrAddString (name), version, default,
			publicKeyOrToken != null ? metadata.GetOrAddBlob (publicKeyOrToken) : default, 0, default);
		_asmRefCache [name] = handle;
		return handle;
	}

	AssemblyReferenceHandle FindOrAddAssemblyReference (MetadataBuilder metadata, string assemblyName)
	{
		if (_asmRefCache.TryGetValue (assemblyName, out var handle)) {
			return handle;
		}
		return AddAssemblyRef (metadata, assemblyName, new Version (0, 0, 0, 0));
	}

	static MemberReferenceHandle AddMemberRef (MetadataBuilder metadata, EntityHandle parent, string name,
		Action<BlobEncoder> encodeSig)
	{
		var blob = new BlobBuilder ();
		encodeSig (new BlobEncoder (blob));
		return metadata.AddMemberReference (parent, metadata.GetOrAddString (name), metadata.GetOrAddBlob (blob));
	}

	EntityHandle ResolveTypeRef (MetadataBuilder metadata, TypeRefData typeRef)
	{
		// Cache key: "AssemblyName:ManagedTypeName" to avoid duplicate TypeRef rows
		var cacheKey = $"{typeRef.AssemblyName}:{typeRef.ManagedTypeName}";
		if (_typeRefCache.TryGetValue (cacheKey, out var cached)) {
			return cached;
		}
		var asmRef = FindOrAddAssemblyReference (metadata, typeRef.AssemblyName);
		var result = MakeTypeRefForManagedName (metadata, asmRef, typeRef.ManagedTypeName);
		_typeRefCache [cacheKey] = result;
		return result;
	}

	TypeReferenceHandle MakeTypeRefForManagedName (MetadataBuilder metadata, EntityHandle scope, string managedTypeName)
	{
		int plusIndex = managedTypeName.IndexOf ('+');
		if (plusIndex >= 0) {
			var outerRef = MakeTypeRefForManagedName (metadata, scope, managedTypeName.Substring (0, plusIndex));
			return MakeTypeRefForManagedName (metadata, outerRef, managedTypeName.Substring (plusIndex + 1));
		}
		int lastDot = managedTypeName.LastIndexOf ('.');
		var ns = lastDot >= 0 ? managedTypeName.Substring (0, lastDot) : "";
		var name = lastDot >= 0 ? managedTypeName.Substring (lastDot + 1) : managedTypeName;
		return metadata.AddTypeReference (scope, metadata.GetOrAddString (ns), metadata.GetOrAddString (name));
	}

	void AddUnmanagedCallersOnlyAttribute (MetadataBuilder metadata, MethodDefinitionHandle handle)
	{
		var attrBlob = new BlobBuilder ();
		attrBlob.WriteUInt16 (1);
		attrBlob.WriteUInt16 (0);
		metadata.AddCustomAttribute (handle, _ucoAttrCtorRef, metadata.GetOrAddBlob (attrBlob));
	}

	/// <summary>Emits a method body and definition in one call.</summary>
	MethodDefinitionHandle EmitBody (MetadataBuilder metadata, BlobBuilder ilBuilder,
		string name, MethodAttributes attrs,
		Action<BlobEncoder> encodeSig, Action<InstructionEncoder> emitIL)
	{
		var sigBlob = new BlobBuilder ();
		encodeSig (new BlobEncoder (sigBlob));

		var codeBuilder = new BlobBuilder ();
		var encoder = new InstructionEncoder (codeBuilder);
		emitIL (encoder);

		while (ilBuilder.Count % 4 != 0) {
			ilBuilder.WriteByte (0);
		}
		var bodyEncoder = new MethodBodyStreamEncoder (ilBuilder);
		int bodyOffset = bodyEncoder.AddMethodBody (encoder);

		return metadata.AddMethodDefinition (
			attrs, MethodImplAttributes.IL,
			metadata.GetOrAddString (name),
			metadata.GetOrAddBlob (sigBlob),
			bodyOffset, default);
	}

	static void WritePE (MetadataBuilder metadata, BlobBuilder ilBuilder, string outputPath)
	{
		var peBuilder = new ManagedPEBuilder (
			new PEHeaderBuilder (imageCharacteristics: Characteristics.Dll),
			new MetadataRootBuilder (metadata),
			ilBuilder);
		var peBlob = new BlobBuilder ();
		peBuilder.Serialize (peBlob);
		using var fs = File.Create (outputPath);
		peBlob.WriteContentTo (fs);
	}
}
