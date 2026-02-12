using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

/// <summary>
/// Emits a TypeMap PE assembly from a <see cref="TypeMapAssemblyData"/>.
/// This is a mechanical translation — all decision logic lives in <see cref="ModelBuilder"/>.
/// </summary>
sealed class TypeMapAssemblyEmitter
{
	readonly Dictionary<string, AssemblyReferenceHandle> _asmRefCache = new (StringComparer.OrdinalIgnoreCase);
	readonly Dictionary<(string Assembly, string Type), EntityHandle> _typeRefCache = new ();

	// Reusable scratch BlobBuilders — avoids allocating a new one per method body / attribute / member ref.
	// Each is Clear()'d before use. Safe because all emission is single-threaded and non-reentrant.
	readonly BlobBuilder _sigBlob = new BlobBuilder (64);
	readonly BlobBuilder _codeBlob = new BlobBuilder (256);
	readonly BlobBuilder _attrBlob = new BlobBuilder (64);

	readonly Version _systemRuntimeVersion;

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
	TypeReferenceHandle _notSupportedExceptionRef;
	TypeReferenceHandle _runtimeHelpersRef;
	TypeReferenceHandle _jniEnvironmentRef;
	TypeReferenceHandle _jniTransitionRef;
	TypeReferenceHandle _jniRuntimeRef;
	TypeReferenceHandle _javaLangObjectRef;
	TypeReferenceHandle _jniEnvRef;
	TypeReferenceHandle _systemExceptionRef;
	TypeReferenceHandle _iJavaObjectRef;

	MemberReferenceHandle _baseCtorRef;
	MemberReferenceHandle _getTypeFromHandleRef;
	MemberReferenceHandle _getUninitializedObjectRef;
	MemberReferenceHandle _notSupportedExceptionCtorRef;
	MemberReferenceHandle _activateInstanceRef;
	MemberReferenceHandle _registerMethodRef;
	MemberReferenceHandle _ucoAttrCtorRef;
	BlobHandle _ucoAttrBlobHandle;
	MemberReferenceHandle _typeMapAttrCtorRef2Arg;
	MemberReferenceHandle _typeMapAttrCtorRef3Arg;
	MemberReferenceHandle _typeMapAssociationAttrCtorRef;
	MemberReferenceHandle _beginMarshalMethodRef;
	MemberReferenceHandle _endMarshalMethodRef;
	MemberReferenceHandle _onUserUnhandledExceptionRef;
	MemberReferenceHandle _jniEnvGetStringRef;
	MemberReferenceHandle _jniEnvNewStringRef;
	MemberReferenceHandle _jniEnvToLocalJniHandleRef;

	/// <summary>
	/// Creates a new emitter.
	/// </summary>
	/// <param name="systemRuntimeVersion">
	/// Version for System.Runtime assembly references.
	/// Will be derived from $(DotNetTargetVersion) MSBuild property in the build task.
	/// </param>
	public TypeMapAssemblyEmitter (Version systemRuntimeVersion)
	{
		_systemRuntimeVersion = systemRuntimeVersion ?? throw new ArgumentNullException (nameof (systemRuntimeVersion));
	}

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
		_typeRefCache.Clear ();

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

		foreach (var assoc in model.Associations) {
			EmitTypeMapAssociationAttribute (metadata, assoc);
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

	// Mono.Android strong name public key token (84e04ff9cfb79065)
	static readonly byte [] MonoAndroidPublicKeyToken = { 0x84, 0xe0, 0x4f, 0xf9, 0xcf, 0xb7, 0x90, 0x65 };

	void EmitAssemblyReferences (MetadataBuilder metadata)
	{
		_systemRuntimeRef = AddAssemblyRef (metadata, "System.Runtime", _systemRuntimeVersion);
		_monoAndroidRef = AddAssemblyRef (metadata, "Mono.Android", new Version (0, 0, 0, 0),
			publicKeyOrToken: MonoAndroidPublicKeyToken);
		_javaInteropRef = AddAssemblyRef (metadata, "Java.Interop", new Version (0, 0, 0, 0));
		_systemRuntimeInteropServicesRef = AddAssemblyRef (metadata, "System.Runtime.InteropServices", _systemRuntimeVersion);
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
		_notSupportedExceptionRef = metadata.AddTypeReference (_systemRuntimeRef,
			metadata.GetOrAddString ("System"), metadata.GetOrAddString ("NotSupportedException"));
		_runtimeHelpersRef = metadata.AddTypeReference (_systemRuntimeRef,
			metadata.GetOrAddString ("System.Runtime.CompilerServices"), metadata.GetOrAddString ("RuntimeHelpers"));
		_jniEnvironmentRef = metadata.AddTypeReference (_javaInteropRef,
			metadata.GetOrAddString ("Java.Interop"), metadata.GetOrAddString ("JniEnvironment"));
		_jniTransitionRef = metadata.AddTypeReference (_javaInteropRef,
			metadata.GetOrAddString ("Java.Interop"), metadata.GetOrAddString ("JniTransition"));
		_jniRuntimeRef = metadata.AddTypeReference (_javaInteropRef,
			metadata.GetOrAddString ("Java.Interop"), metadata.GetOrAddString ("JniRuntime"));
		_javaLangObjectRef = metadata.AddTypeReference (_monoAndroidRef,
			metadata.GetOrAddString ("Java.Lang"), metadata.GetOrAddString ("Object"));
		_jniEnvRef = metadata.AddTypeReference (_monoAndroidRef,
			metadata.GetOrAddString ("Android.Runtime"), metadata.GetOrAddString ("JNIEnv"));
		_systemExceptionRef = metadata.AddTypeReference (_systemRuntimeRef,
			metadata.GetOrAddString ("System"), metadata.GetOrAddString ("Exception"));
		_iJavaObjectRef = metadata.AddTypeReference (_javaInteropRef,
			metadata.GetOrAddString ("Java.Interop"), metadata.GetOrAddString ("IJavaObject"));
	}

	void EmitMemberReferences (MetadataBuilder metadata)
	{
		_baseCtorRef = AddMemberRef (metadata, _javaPeerProxyRef, ".ctor",
			sig => sig.MethodSignature (isInstanceMethod: true).Parameters (0, rt => rt.Void (), p => { }));

		_getTypeFromHandleRef = AddMemberRef (metadata, _systemTypeRef, "GetTypeFromHandle",
			sig => sig.MethodSignature ().Parameters (1,
				rt => rt.Type ().Type (_systemTypeRef, false),
				p => p.AddParameter ().Type ().Type (_runtimeTypeHandleRef, true)));

		_getUninitializedObjectRef = AddMemberRef (metadata, _runtimeHelpersRef, "GetUninitializedObject",
			sig => sig.MethodSignature ().Parameters (1,
				rt => rt.Type ().Object (),
				p => p.AddParameter ().Type ().Type (_systemTypeRef, false)));

		_notSupportedExceptionCtorRef = AddMemberRef (metadata, _notSupportedExceptionRef, ".ctor",
			sig => sig.MethodSignature (isInstanceMethod: true).Parameters (1,
				rt => rt.Void (),
				p => p.AddParameter ().Type ().String ()));

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

		// Pre-compute the UCO attribute blob — it's always the same 4 bytes (prolog + no named args)
		_attrBlob.Clear ();
		_attrBlob.WriteUInt16 (1);
		_attrBlob.WriteUInt16 (0);
		_ucoAttrBlobHandle = metadata.GetOrAddBlob (_attrBlob);

		// Marshal method support: BeginMarshalMethod, EndMarshalMethod, GetObject, GetString, etc.
		// BeginMarshalMethod(IntPtr jnienv, out JniTransition, out JniRuntime) : bool
		_beginMarshalMethodRef = AddMemberRef (metadata, _jniEnvironmentRef, "BeginMarshalMethod",
			sig => sig.MethodSignature ().Parameters (3,
				rt => rt.Type ().Boolean (),
				p => {
					p.AddParameter ().Type ().IntPtr ();
					p.AddParameter ().Type (isByRef: true).Type (_jniTransitionRef, true);
					p.AddParameter ().Type (isByRef: true).Type (_jniRuntimeRef, false);
				}));

		// EndMarshalMethod(ref JniTransition)
		_endMarshalMethodRef = AddMemberRef (metadata, _jniEnvironmentRef, "EndMarshalMethod",
			sig => sig.MethodSignature ().Parameters (1,
				rt => rt.Void (),
				p => p.AddParameter ().Type (isByRef: true).Type (_jniTransitionRef, true)));

		// JniRuntime.OnUserUnhandledException(ref JniTransition, Exception) — virtual instance method
		_onUserUnhandledExceptionRef = AddMemberRef (metadata, _jniRuntimeRef, "OnUserUnhandledException",
			sig => sig.MethodSignature (isInstanceMethod: true).Parameters (2,
				rt => rt.Void (),
				p => {
					p.AddParameter ().Type (isByRef: true).Type (_jniTransitionRef, true);
					p.AddParameter ().Type ().Type (_systemExceptionRef, false);
				}));

		// JNIEnv.GetString(IntPtr, JniHandleOwnership) : string
		_jniEnvGetStringRef = AddMemberRef (metadata, _jniEnvRef, "GetString",
			sig => sig.MethodSignature ().Parameters (2,
				rt => rt.Type ().String (),
				p => {
					p.AddParameter ().Type ().IntPtr ();
					p.AddParameter ().Type ().Type (_jniHandleOwnershipRef, true);
				}));

		// JNIEnv.NewString(string) : IntPtr
		_jniEnvNewStringRef = AddMemberRef (metadata, _jniEnvRef, "NewString",
			sig => sig.MethodSignature ().Parameters (1,
				rt => rt.Type ().IntPtr (),
				p => p.AddParameter ().Type ().String ()));

		// JNIEnv.ToLocalJniHandle(IJavaObject) : IntPtr
		_jniEnvToLocalJniHandleRef = AddMemberRef (metadata, _jniEnvRef, "ToLocalJniHandle",
			sig => sig.MethodSignature ().Parameters (1,
				rt => rt.Type ().IntPtr (),
				p => p.AddParameter ().Type ().Type (_iJavaObjectRef, false)));

		EmitTypeMapAttributeCtorRef (metadata);
		EmitTypeMapAssociationAttributeCtorRef (metadata);
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

	void EmitTypeMapAssociationAttributeCtorRef (MetadataBuilder metadata)
	{
		// TypeMapAssociationAttribute is in System.Runtime.InteropServices, takes 2 Type args:
		// TypeMapAssociation(Type sourceType, Type aliasProxyType)
		var typeMapAssociationAttrRef = metadata.AddTypeReference (_systemRuntimeInteropServicesRef,
			metadata.GetOrAddString ("System.Runtime.InteropServices"),
			metadata.GetOrAddString ("TypeMapAssociationAttribute"));

		_typeMapAssociationAttrCtorRef = AddMemberRef (metadata, typeMapAssociationAttrRef, ".ctor",
			sig => sig.MethodSignature (isInstanceMethod: true).Parameters (2,
				rt => rt.Void (),
				p => {
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

		if (proxy.IsAcw) {
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

		// Export marshal method wrappers (full marshal body)
		foreach (var export in proxy.ExportMarshalMethods) {
			var handle = EmitExportMarshalMethod (metadata, ilBuilder, export);
			wrapperHandles [export.WrapperName] = handle;
		}

		// RegisterNatives
		if (proxy.IsAcw) {
			EmitRegisterNatives (metadata, ilBuilder, proxy.NativeRegistrations, wrapperHandles);
		}
	}

	void EmitCreateInstance (MetadataBuilder metadata, BlobBuilder ilBuilder, JavaPeerProxyData proxy)
	{
		if (!proxy.HasActivation) {
			EmitCreateInstanceBody (metadata, ilBuilder, encoder => {
				encoder.OpCode (ILOpCode.Ldnull);
				encoder.OpCode (ILOpCode.Ret);
			});
			return;
		}

		// Generic type definitions cannot be instantiated
		if (proxy.IsGenericDefinition) {
			EmitCreateInstanceBody (metadata, ilBuilder, encoder => {
				encoder.LoadString (metadata.GetOrAddUserString ("Cannot create instance of open generic type."));
				encoder.OpCode (ILOpCode.Newobj);
				encoder.Token (_notSupportedExceptionCtorRef);
				encoder.OpCode (ILOpCode.Throw);
			});
			return;
		}

		// Interface with invoker: new TInvoker(IntPtr, JniHandleOwnership)
		if (proxy.InvokerType != null) {
			var invokerCtorRef = AddActivationCtorRef (metadata, ResolveTypeRef (metadata, proxy.InvokerType));
			EmitCreateInstanceBody (metadata, ilBuilder, encoder => {
				encoder.OpCode (ILOpCode.Ldarg_1);
				encoder.OpCode (ILOpCode.Ldarg_2);
				encoder.OpCode (ILOpCode.Newobj);
				encoder.Token (invokerCtorRef);
				encoder.OpCode (ILOpCode.Ret);
			});
			return;
		}

		// At this point, ActivationCtor is guaranteed non-null (HasActivation && InvokerType == null)
		var activationCtor = proxy.ActivationCtor ?? throw new InvalidOperationException ("ActivationCtor should not be null when HasActivation is true and InvokerType is null");
		var targetTypeRef = ResolveTypeRef (metadata, proxy.TargetType);

		if (activationCtor.IsOnLeafType) {
			// Leaf type has its own ctor: new T(IntPtr, JniHandleOwnership)
			var ctorRef = AddActivationCtorRef (metadata, targetTypeRef);
			EmitCreateInstanceBody (metadata, ilBuilder, encoder => {
				encoder.OpCode (ILOpCode.Ldarg_1);
				encoder.OpCode (ILOpCode.Ldarg_2);
				encoder.OpCode (ILOpCode.Newobj);
				encoder.Token (ctorRef);
				encoder.OpCode (ILOpCode.Ret);
			});
		} else {
			// Inherited ctor: GetUninitializedObject(typeof(T)) + call Base::.ctor(IntPtr, JniHandleOwnership)
			var baseActivationCtorRef = AddActivationCtorRef (metadata, ResolveTypeRef (metadata, activationCtor.DeclaringType));
			EmitCreateInstanceBody (metadata, ilBuilder, encoder => {
				encoder.OpCode (ILOpCode.Ldtoken);
				encoder.Token (targetTypeRef);
				encoder.Call (_getTypeFromHandleRef);
				encoder.Call (_getUninitializedObjectRef);
				encoder.OpCode (ILOpCode.Castclass);
				encoder.Token (targetTypeRef);

				encoder.OpCode (ILOpCode.Dup);
				encoder.OpCode (ILOpCode.Ldarg_1);
				encoder.OpCode (ILOpCode.Ldarg_2);
				encoder.Call (baseActivationCtorRef);

				encoder.OpCode (ILOpCode.Ret);
			});
		}
	}

	void EmitCreateInstanceBody (MetadataBuilder metadata, BlobBuilder ilBuilder, Action<InstructionEncoder> emitIL)
	{
		EmitBody (metadata, ilBuilder, "CreateInstance",
			MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig,
			sig => sig.MethodSignature (isInstanceMethod: true).Parameters (2,
				rt => rt.Type ().Type (_iJavaPeerableRef, false),
				p => {
					p.AddParameter ().Type ().IntPtr ();
					p.AddParameter ().Type ().Type (_jniHandleOwnershipRef, true);
				}),
			emitIL);
	}

	MemberReferenceHandle AddActivationCtorRef (MetadataBuilder metadata, EntityHandle declaringTypeRef)
	{
		return AddMemberRef (metadata, declaringTypeRef, ".ctor",
			sig => sig.MethodSignature (isInstanceMethod: true).Parameters (2,
				rt => rt.Void (),
				p => {
					p.AddParameter ().Type ().IntPtr ();
					p.AddParameter ().Type ().Type (_jniHandleOwnershipRef, true);
				}));
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

		Action<BlobEncoder> encodeSig = sig => sig.MethodSignature ().Parameters (paramCount,
			rt => { if (isVoid) rt.Void (); else JniSignatureHelper.EncodeClrType (rt.Type (), returnKind); },
			p => {
				p.AddParameter ().Type ().IntPtr ();
				p.AddParameter ().Type ().IntPtr ();
				for (int j = 0; j < jniParams.Count; j++)
					JniSignatureHelper.EncodeClrType (p.AddParameter ().Type (), jniParams [j]);
			});

		var callbackTypeHandle = ResolveTypeRef (metadata, uco.CallbackType);
		var callbackRef = AddMemberRef (metadata, callbackTypeHandle, uco.CallbackMethodName, encodeSig);

		var handle = EmitBody (metadata, ilBuilder, uco.WrapperName,
			MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig,
			encodeSig,
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

		// UCO constructor wrappers must match the JNI native method signature exactly.
		// The Java JCW declares e.g. "private native void nctor_0(Context p0)" and calls
		// it with arguments. JNI dispatches with (JNIEnv*, jobject, <ctor params...>),
		// so the wrapper signature must include all parameters to match the ABI.
		// Only jnienv (arg 0) and self (arg 1) are used — the constructor parameters
		// are not forwarded because ActivateInstance creates the managed peer using the
		// activation ctor (IntPtr, JniHandleOwnership), not the user-visible constructor.
		var jniParams = JniSignatureHelper.ParseParameterTypes (uco.JniSignature);
		int paramCount = 2 + jniParams.Count;

		var handle = EmitBody (metadata, ilBuilder, uco.WrapperName,
			MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig,
			sig => sig.MethodSignature ().Parameters (paramCount,
				rt => rt.Void (),
				p => {
					p.AddParameter ().Type ().IntPtr (); // jnienv
					p.AddParameter ().Type ().IntPtr (); // self
					for (int j = 0; j < jniParams.Count; j++)
						JniSignatureHelper.EncodeClrType (p.AddParameter ().Type (), jniParams [j]);
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

	// ---- Export marshal method wrappers ----

	/// <summary>
	/// Emits a full marshal method body for an [Export] method or constructor.
	/// Pattern:
	///   static RetType n_Method(IntPtr jnienv, IntPtr native__this, <JNI params...>) {
	///     if (!JniEnvironment.BeginMarshalMethod(jnienv, out var __envp, out var __r)) return default;
	///     try {
	///       var __this = Object.GetObject&lt;T&gt;(jnienv, native__this, DoNotTransfer);
	///       // unmarshal params, call managed method, marshal return
	///     } catch (Exception __e) {
	///       __r.OnUserUnhandledException(ref __envp, __e);
	///       return default;
	///     } finally {
	///       JniEnvironment.EndMarshalMethod(ref __envp);
	///     }
	///   }
	/// </summary>
	MethodDefinitionHandle EmitExportMarshalMethod (MetadataBuilder metadata, BlobBuilder ilBuilder, ExportMarshalMethodData export)
	{
		var jniParams = JniSignatureHelper.ParseParameterTypes (export.JniSignature);
		var returnKind = export.IsConstructor ? JniParamKind.Void : JniSignatureHelper.ParseReturnType (export.JniSignature);
		bool isVoid = returnKind == JniParamKind.Void;
		int jniParamCount = 2 + jniParams.Count; // jnienv + self + method params

		// Build the method signature
		Action<BlobEncoder> encodeSig = sig => sig.MethodSignature ().Parameters (jniParamCount,
			rt => { if (isVoid) rt.Void (); else JniSignatureHelper.EncodeClrType (rt.Type (), returnKind); },
			p => {
				p.AddParameter ().Type ().IntPtr (); // jnienv
				p.AddParameter ().Type ().IntPtr (); // native__this
				for (int j = 0; j < jniParams.Count; j++)
					JniSignatureHelper.EncodeClrType (p.AddParameter ().Type (), jniParams [j]);
			});

		// Build the locals signature:
		//   local 0: JniTransition __envp
		//   local 1: JniRuntime __r
		//   local 2: Exception __e
		//   local 3: <return type> __ret  (only for non-void methods)
		int localCount = isVoid ? 3 : 4;
		var localsBlob = new BlobBuilder (32);
		var localsEncoder = new BlobEncoder (localsBlob).LocalVariableSignature (localCount);
		localsEncoder.AddVariable ().Type ().Type (_jniTransitionRef, true);   // local 0
		localsEncoder.AddVariable ().Type ().Type (_jniRuntimeRef, false);     // local 1
		localsEncoder.AddVariable ().Type ().Type (_systemExceptionRef, false); // local 2
		if (!isVoid) {
			JniSignatureHelper.EncodeClrType (localsEncoder.AddVariable ().Type (), returnKind); // local 3
		}
		var localsSigHandle = metadata.AddStandaloneSignature (metadata.GetOrAddBlob (localsBlob));

		// Resolve managed type references
		var declaringTypeRef = ResolveTypeRef (metadata, export.DeclaringType);

		// Build GetObject<T> method spec — generic instantiation of Object.GetObject<T>
		var getObjectRef = BuildGetObjectMethodSpec (metadata, declaringTypeRef);

		// Resolve managed method to call
		MemberReferenceHandle managedMethodRef;
		if (export.IsConstructor) {
			managedMethodRef = BuildExportCtorRef (metadata, export, declaringTypeRef);
		} else {
			managedMethodRef = BuildExportMethodRef (metadata, export, declaringTypeRef);
		}

		// Build the IL with ControlFlowBuilder for try/catch/finally
		var cfBuilder = new ControlFlowBuilder ();
		_codeBlob.Clear ();
		var encoder = new InstructionEncoder (_codeBlob, cfBuilder);

		// Define labels
		var tryStartLabel = encoder.DefineLabel ();
		var tryEndLabel = encoder.DefineLabel ();
		var catchStartLabel = encoder.DefineLabel ();
		var catchEndLabel = encoder.DefineLabel ();
		var finallyStartLabel = encoder.DefineLabel ();
		var finallyEndLabel = encoder.DefineLabel ();
		var returnLabel = encoder.DefineLabel ();

		// --- if (!BeginMarshalMethod(jnienv, out __envp, out __r)) return default; ---
		encoder.LoadArgument (0); // jnienv
		encoder.OpCode (ILOpCode.Ldloca_s); encoder.CodeBuilder.WriteByte (0); // out __envp
		encoder.OpCode (ILOpCode.Ldloca_s); encoder.CodeBuilder.WriteByte (1); // out __r
		encoder.Call (_beginMarshalMethodRef);
		encoder.Branch (ILOpCode.Brtrue_s, tryStartLabel);
		// return default
		if (!isVoid) {
			EmitDefaultReturnValue (encoder, returnKind);
		}
		encoder.OpCode (ILOpCode.Ret);

		// --- try { ---
		encoder.MarkLabel (tryStartLabel);

		if (export.IsConstructor) {
			// For constructors: ActivateInstance first, then get the managed object and call ctor
			// ActivateInstance(native__this, typeof(T))
			encoder.LoadArgument (1); // native__this
			encoder.OpCode (ILOpCode.Ldtoken);
			encoder.Token (declaringTypeRef);
			encoder.Call (_getTypeFromHandleRef);
			encoder.Call (_activateInstanceRef);
		}

		// var __this = Object.GetObject<T>(jnienv, native__this, DoNotTransfer);
		encoder.LoadArgument (0); // jnienv
		encoder.LoadArgument (1); // native__this
		encoder.OpCode (ILOpCode.Ldc_i4_0); // JniHandleOwnership.DoNotTransfer = 0
		encoder.Call (getObjectRef);

		// Unmarshal each parameter
		for (int i = 0; i < export.ManagedParameters.Count; i++) {
			EmitParameterUnmarshal (encoder, metadata, export.ManagedParameters [i], jniParams [i], i + 2);
		}

		// Call managed method
		if (export.IsConstructor) {
			encoder.Call (managedMethodRef);
		} else {
			encoder.OpCode (ILOpCode.Callvirt);
			encoder.Token (managedMethodRef);
		}

		// Marshal return value and store in local 3
		if (!isVoid) {
			EmitReturnMarshal (encoder, returnKind, export.ManagedReturnType);
			encoder.OpCode (ILOpCode.Stloc_3);
		}

		// leave to after the handler
		encoder.Branch (ILOpCode.Leave_s, returnLabel);
		encoder.MarkLabel (tryEndLabel);

		// --- } catch (Exception __e) { ---
		encoder.MarkLabel (catchStartLabel);
		encoder.OpCode (ILOpCode.Stloc_2); // store exception in local 2
		encoder.OpCode (ILOpCode.Ldloc_1); // __r
		encoder.OpCode (ILOpCode.Ldloca_s); encoder.CodeBuilder.WriteByte (0); // ref __envp
		encoder.OpCode (ILOpCode.Ldloc_2); // __e
		encoder.OpCode (ILOpCode.Callvirt);
		encoder.Token (_onUserUnhandledExceptionRef);
		if (!isVoid) {
			EmitDefaultReturnValue (encoder, returnKind);
			encoder.OpCode (ILOpCode.Stloc_3);
		}
		encoder.Branch (ILOpCode.Leave_s, returnLabel);
		encoder.MarkLabel (catchEndLabel);

		// --- } finally { ---
		encoder.MarkLabel (finallyStartLabel);
		encoder.OpCode (ILOpCode.Ldloca_s); encoder.CodeBuilder.WriteByte (0); // ref __envp
		encoder.Call (_endMarshalMethodRef);
		encoder.OpCode (ILOpCode.Endfinally);
		encoder.MarkLabel (finallyEndLabel);

		// --- return ---
		encoder.MarkLabel (returnLabel);
		if (!isVoid) {
			encoder.OpCode (ILOpCode.Ldloc_3);
		}
		encoder.OpCode (ILOpCode.Ret);

		// Add exception regions
		cfBuilder.AddCatchRegion (tryStartLabel, tryEndLabel, catchStartLabel, catchEndLabel, _systemExceptionRef);
		cfBuilder.AddFinallyRegion (tryStartLabel, catchEndLabel, finallyStartLabel, finallyEndLabel);

		// Emit the method with fat body (locals + exception handlers)
		_sigBlob.Clear ();
		encodeSig (new BlobEncoder (_sigBlob));

		while (ilBuilder.Count % 4 != 0) {
			ilBuilder.WriteByte (0);
		}
		var bodyEncoder = new MethodBodyStreamEncoder (ilBuilder);
		int bodyOffset = bodyEncoder.AddMethodBody (encoder, maxStack: 8, localsSigHandle, MethodBodyAttributes.InitLocals);

		var handle = metadata.AddMethodDefinition (
			MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig,
			MethodImplAttributes.IL,
			metadata.GetOrAddString (export.WrapperName),
			metadata.GetOrAddBlob (_sigBlob),
			bodyOffset, default);

		AddUnmanagedCallersOnlyAttribute (metadata, handle);
		return handle;
	}

	/// <summary>
	/// Builds a MethodSpec for Object.GetObject&lt;T&gt;(IntPtr, IntPtr, JniHandleOwnership).
	/// </summary>
	EntityHandle BuildGetObjectMethodSpec (MetadataBuilder metadata, EntityHandle managedTypeRef)
	{
		// Object.GetObject<T>(IntPtr jnienv, IntPtr handle, JniHandleOwnership transfer) : T
		var openGetObjectRef = AddMemberRef (metadata, _javaLangObjectRef, "GetObject",
			sig => {
				var methodSig = sig.MethodSignature (genericParameterCount: 1);
				methodSig.Parameters (3,
					rt => rt.Type ().GenericMethodTypeParameter (0),
					p => {
						p.AddParameter ().Type ().IntPtr ();
						p.AddParameter ().Type ().IntPtr ();
						p.AddParameter ().Type ().Type (_jniHandleOwnershipRef, true);
					});
			});

		// Build generic instantiation blob: GetObject<DeclaringType>
		var instBlob = new BlobBuilder (16);
		instBlob.WriteByte (0x0A); // ELEMENT_TYPE_GENERICINST (for method)
		instBlob.WriteCompressedInteger (1); // 1 type argument
		instBlob.WriteByte (0x12); // ELEMENT_TYPE_CLASS
		instBlob.WriteCompressedInteger (CodedIndex.TypeDefOrRefOrSpec (managedTypeRef));

		return metadata.AddMethodSpecification (openGetObjectRef, metadata.GetOrAddBlob (instBlob));
	}

	MemberReferenceHandle BuildExportCtorRef (MetadataBuilder metadata, ExportMarshalMethodData export, EntityHandle declaringTypeRef)
	{
		int paramCount = export.ManagedParameters.Count;
		return AddMemberRef (metadata, declaringTypeRef, ".ctor",
			sig => sig.MethodSignature (isInstanceMethod: true).Parameters (paramCount,
				rt => rt.Void (),
				p => {
					foreach (var param in export.ManagedParameters)
						EncodeExportParamType (p, metadata, param);
				}));
	}

	MemberReferenceHandle BuildExportMethodRef (MetadataBuilder metadata, ExportMarshalMethodData export, EntityHandle declaringTypeRef)
	{
		int paramCount = export.ManagedParameters.Count;
		var returnKind = JniSignatureHelper.ParseReturnType (export.JniSignature);
		bool isVoid = returnKind == JniParamKind.Void;

		return AddMemberRef (metadata, declaringTypeRef, export.ManagedMethodName,
			sig => sig.MethodSignature (isInstanceMethod: true).Parameters (paramCount,
				rt => {
					if (isVoid) {
						rt.Void ();
					} else {
						EncodeExportReturnType (rt, metadata, export.ManagedReturnType, returnKind);
					}
				},
				p => {
					foreach (var param in export.ManagedParameters)
						EncodeExportParamType (p, metadata, param);
				}));
	}

	void EncodeExportParamType (ParametersEncoder p, MetadataBuilder metadata, ExportParamData param)
	{
		var jniKind = JniSignatureHelper.ParseSingleTypeFromDescriptor (param.JniType);
		if (jniKind != JniParamKind.Object) {
			JniSignatureHelper.EncodeClrType (p.AddParameter ().Type (), jniKind);
		} else if (param.ManagedTypeName == "System.String") {
			p.AddParameter ().Type ().String ();
		} else {
			var typeRef = ResolveTypeRef (metadata, new TypeRefData {
				ManagedTypeName = param.ManagedTypeName,
				AssemblyName = param.AssemblyName,
			});
			p.AddParameter ().Type ().Type (typeRef, false);
		}
	}

	void EncodeExportReturnType (ReturnTypeEncoder rt, MetadataBuilder metadata, string? managedReturnType, JniParamKind returnKind)
	{
		if (returnKind != JniParamKind.Object) {
			JniSignatureHelper.EncodeClrType (rt.Type (), returnKind);
		} else if (managedReturnType == "System.String") {
			rt.Type ().String ();
		} else if (managedReturnType != null) {
			// Resolve the managed return type for the method ref signature
			string typeName = managedReturnType;
			string assemblyName = "";
			int commaIndex = managedReturnType.IndexOf (", ", StringComparison.Ordinal);
			if (commaIndex >= 0) {
				assemblyName = managedReturnType.Substring (commaIndex + 2);
				typeName = managedReturnType.Substring (0, commaIndex);
			}
			if (assemblyName.Length > 0) {
				var typeRef = ResolveTypeRef (metadata, new TypeRefData {
					ManagedTypeName = typeName,
					AssemblyName = assemblyName,
				});
				rt.Type ().Type (typeRef, false);
			} else {
				// Fallback: no assembly info available, use IntPtr
				rt.Type ().IntPtr ();
			}
		} else {
			rt.Type ().IntPtr ();
		}
	}

	void EmitParameterUnmarshal (InstructionEncoder encoder, MetadataBuilder metadata, ExportParamData param, JniParamKind jniKind, int argIndex)
	{
		if (jniKind != JniParamKind.Object) {
			// Primitives: just load the argument directly
			encoder.LoadArgument (argIndex);
			return;
		}

		if (param.ManagedTypeName == "System.String") {
			// String: JNIEnv.GetString(handle, DoNotTransfer)
			encoder.LoadArgument (argIndex);
			encoder.OpCode (ILOpCode.Ldc_i4_0); // DoNotTransfer
			encoder.Call (_jniEnvGetStringRef);
			return;
		}

		// Java object: Object.GetObject<T>(handle, DoNotTransfer)
		// Use the 2-arg overload (without jnienv)
		var typeRef = ResolveTypeRef (metadata, new TypeRefData {
			ManagedTypeName = param.ManagedTypeName,
			AssemblyName = param.AssemblyName,
		});
		var getObjectRef2 = AddMemberRef (metadata, _javaLangObjectRef, "GetObject",
			sig => {
				var methodSig = sig.MethodSignature (genericParameterCount: 1);
				methodSig.Parameters (2,
					rt => rt.Type ().GenericMethodTypeParameter (0),
					p => {
						p.AddParameter ().Type ().IntPtr ();
						p.AddParameter ().Type ().Type (_jniHandleOwnershipRef, true);
					});
			});
		var instBlob = new BlobBuilder (16);
		instBlob.WriteByte (0x0A);
		instBlob.WriteCompressedInteger (1);
		instBlob.WriteByte (0x12);
		instBlob.WriteCompressedInteger (CodedIndex.TypeDefOrRefOrSpec (typeRef));
		var methodSpec = metadata.AddMethodSpecification (getObjectRef2, metadata.GetOrAddBlob (instBlob));

		encoder.LoadArgument (argIndex);
		encoder.OpCode (ILOpCode.Ldc_i4_0); // DoNotTransfer
		encoder.Call (methodSpec);
	}

	void EmitReturnMarshal (InstructionEncoder encoder, JniParamKind returnKind, string? managedReturnType)
	{
		if (returnKind != JniParamKind.Object) {
			// Primitives: return directly (value is already on the stack)
			return;
		}

		if (managedReturnType == "System.String") {
			// String: JNIEnv.NewString(result)
			encoder.Call (_jniEnvNewStringRef);
			return;
		}

		// Java object: JNIEnv.ToLocalJniHandle(result)
		encoder.Call (_jniEnvToLocalJniHandleRef);
	}

	static void EmitDefaultReturnValue (InstructionEncoder encoder, JniParamKind kind)
	{
		switch (kind) {
		case JniParamKind.Boolean:
		case JniParamKind.Byte:
		case JniParamKind.Char:
		case JniParamKind.Short:
		case JniParamKind.Int:
			encoder.OpCode (ILOpCode.Ldc_i4_0);
			break;
		case JniParamKind.Long:
			encoder.OpCode (ILOpCode.Ldc_i8);
			encoder.CodeBuilder.WriteInt64 (0);
			break;
		case JniParamKind.Float:
			encoder.OpCode (ILOpCode.Ldc_r4);
			encoder.CodeBuilder.WriteSingle (0);
			break;
		case JniParamKind.Double:
			encoder.OpCode (ILOpCode.Ldc_r8);
			encoder.CodeBuilder.WriteDouble (0);
			break;
		case JniParamKind.Object:
			encoder.OpCode (ILOpCode.Ldc_i4_0);
			encoder.OpCode (ILOpCode.Conv_i); // IntPtr.Zero
			break;
		}
	}

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
		_attrBlob.Clear ();
		_attrBlob.WriteUInt16 (0x0001); // Prolog
		_attrBlob.WriteSerializedString (entry.JniName);
		_attrBlob.WriteSerializedString (entry.ProxyTypeReference);
		if (!entry.IsUnconditional) {
			_attrBlob.WriteSerializedString (entry.TargetTypeReference!);
		}
		_attrBlob.WriteUInt16 (0x0000); // NumNamed

		var ctorRef = entry.IsUnconditional ? _typeMapAttrCtorRef2Arg : _typeMapAttrCtorRef3Arg;
		metadata.AddCustomAttribute (EntityHandle.AssemblyDefinition, ctorRef, metadata.GetOrAddBlob (_attrBlob));
	}

	void EmitTypeMapAssociationAttribute (MetadataBuilder metadata, TypeMapAssociationData assoc)
	{
		_attrBlob.Clear ();
		_attrBlob.WriteUInt16 (0x0001); // Prolog
		_attrBlob.WriteSerializedString (assoc.SourceTypeReference);
		_attrBlob.WriteSerializedString (assoc.AliasProxyTypeReference);
		_attrBlob.WriteUInt16 (0x0000); // NumNamed
		metadata.AddCustomAttribute (EntityHandle.AssemblyDefinition, _typeMapAssociationAttrCtorRef,
			metadata.GetOrAddBlob (_attrBlob));
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
			_attrBlob.Clear ();
			_attrBlob.WriteUInt16 (1);
			_attrBlob.WriteSerializedString (asmName);
			_attrBlob.WriteUInt16 (0);
			metadata.AddCustomAttribute (EntityHandle.AssemblyDefinition, ctorDef, metadata.GetOrAddBlob (_attrBlob));
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

	MemberReferenceHandle AddMemberRef (MetadataBuilder metadata, EntityHandle parent, string name,
		Action<BlobEncoder> encodeSig)
	{
		_sigBlob.Clear ();
		encodeSig (new BlobEncoder (_sigBlob));
		return metadata.AddMemberReference (parent, metadata.GetOrAddString (name), metadata.GetOrAddBlob (_sigBlob));
	}

	EntityHandle ResolveTypeRef (MetadataBuilder metadata, TypeRefData typeRef)
	{
		var cacheKey = (typeRef.AssemblyName, typeRef.ManagedTypeName);
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
		metadata.AddCustomAttribute (handle, _ucoAttrCtorRef, _ucoAttrBlobHandle);
	}

	/// <summary>Emits a method body and definition in one call.</summary>
	MethodDefinitionHandle EmitBody (MetadataBuilder metadata, BlobBuilder ilBuilder,
		string name, MethodAttributes attrs,
		Action<BlobEncoder> encodeSig, Action<InstructionEncoder> emitIL)
	{
		_sigBlob.Clear ();
		encodeSig (new BlobEncoder (_sigBlob));

		_codeBlob.Clear ();
		var encoder = new InstructionEncoder (_codeBlob);
		emitIL (encoder);

		while (ilBuilder.Count % 4 != 0) {
			ilBuilder.WriteByte (0);
		}
		var bodyEncoder = new MethodBodyStreamEncoder (ilBuilder);
		int bodyOffset = bodyEncoder.AddMethodBody (encoder);

		return metadata.AddMethodDefinition (
			attrs, MethodImplAttributes.IL,
			metadata.GetOrAddString (name),
			metadata.GetOrAddBlob (_sigBlob),
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
