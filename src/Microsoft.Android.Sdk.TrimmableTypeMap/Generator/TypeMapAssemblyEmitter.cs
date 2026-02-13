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
	TypeReferenceHandle _charSequenceRef;
	TypeReferenceHandle _systemExceptionRef;
	TypeReferenceHandle _iJavaObjectRef;

	MemberReferenceHandle _baseCtorRef;
	MemberReferenceHandle _getTypeFromHandleRef;
	MemberReferenceHandle _getUninitializedObjectRef;
	MemberReferenceHandle _notSupportedExceptionCtorRef;
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
	MemberReferenceHandle _jniEnvGetCharSequenceRef;
	MemberReferenceHandle _jniEnvNewStringRef;
	MemberReferenceHandle _jniEnvToLocalJniHandleRef;
	MemberReferenceHandle _charSequenceToLocalJniHandleStringRef;
	MemberReferenceHandle _jniEnvGetArrayOpenRef;
	MemberReferenceHandle _jniEnvNewArrayOpenRef;
	MemberReferenceHandle _jniEnvCopyArrayOpenRef;
	MemberReferenceHandle _setHandleRef;

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
		_charSequenceRef = metadata.AddTypeReference (_monoAndroidRef,
			metadata.GetOrAddString ("Android.Runtime"), metadata.GetOrAddString ("CharSequence"));
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

		// JNIEnv.GetCharSequence(IntPtr, JniHandleOwnership) : string
		_jniEnvGetCharSequenceRef = AddMemberRef (metadata, _jniEnvRef, "GetCharSequence",
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

		// CharSequence.ToLocalJniHandle(string) : IntPtr
		_charSequenceToLocalJniHandleStringRef = AddMemberRef (metadata, _charSequenceRef, "ToLocalJniHandle",
			sig => sig.MethodSignature ().Parameters (1,
				rt => rt.Type ().IntPtr (),
				p => p.AddParameter ().Type ().String ()));

		// JNIEnv.ToLocalJniHandle(IJavaObject) : IntPtr
		_jniEnvToLocalJniHandleRef = AddMemberRef (metadata, _jniEnvRef, "ToLocalJniHandle",
			sig => sig.MethodSignature ().Parameters (1,
				rt => rt.Type ().IntPtr (),
				p => p.AddParameter ().Type ().Type (_iJavaObjectRef, false)));

		// JNIEnv.GetArray<T>(IntPtr) : T[]
		_jniEnvGetArrayOpenRef = AddMemberRef (metadata, _jniEnvRef, "GetArray",
			sig => {
				var methodSig = sig.MethodSignature (genericParameterCount: 1);
				methodSig.Parameters (1,
					rt => rt.Type ().SZArray ().GenericMethodTypeParameter (0),
					p => p.AddParameter ().Type ().IntPtr ());
			});

		// JNIEnv.NewArray<T>(T[]) : IntPtr
		_jniEnvNewArrayOpenRef = AddMemberRef (metadata, _jniEnvRef, "NewArray",
			sig => {
				var methodSig = sig.MethodSignature (genericParameterCount: 1);
				methodSig.Parameters (1,
					rt => rt.Type ().IntPtr (),
					p => p.AddParameter ().Type ().SZArray ().GenericMethodTypeParameter (0));
			});

		// JNIEnv.CopyArray<T>(T[], IntPtr) : void
		_jniEnvCopyArrayOpenRef = AddMemberRef (metadata, _jniEnvRef, "CopyArray",
			sig => {
				var methodSig = sig.MethodSignature (genericParameterCount: 1);
				methodSig.Parameters (2,
					rt => rt.Void (),
					p => {
						p.AddParameter ().Type ().SZArray ().GenericMethodTypeParameter (0);
						p.AddParameter ().Type ().IntPtr ();
					});
			});

		// Java.Lang.Object.SetHandle(IntPtr, JniHandleOwnership) : void — protected instance method
		_setHandleRef = AddMemberRef (metadata, _javaLangObjectRef, "SetHandle",
			sig => sig.MethodSignature (isInstanceMethod: true).Parameters (2,
				rt => rt.Void (),
				p => {
					p.AddParameter ().Type ().IntPtr ();
					p.AddParameter ().Type ().Type (_jniHandleOwnershipRef, true);
				}));

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

		// UCO wrappers (methods and constructors with [Register] connectors)
		foreach (var uco in proxy.UcoMethods) {
			var handle = EmitUcoMethod (metadata, ilBuilder, uco);
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
			// Inherited ctor: GetUninitializedObject(typeof(T)) then call BaseType::.ctor(IntPtr, JniHandleOwnership)
			// The base ctor does SetHandle + any other initialization the base class needs.
			var baseTypeRef = ResolveTypeRef (metadata, activationCtor.DeclaringType);
			var baseCtorRef = AddActivationCtorRef (metadata, baseTypeRef);
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
				encoder.Call (baseCtorRef);

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

	// ---- Export marshal method wrappers ----

	/// <summary>
	/// Emits a full marshal method body for an [Export] method or constructor.
	/// Pattern for methods:
	///   static RetType n_Method(IntPtr jnienv, IntPtr native__this, &lt;JNI params...&gt;) {
	///     if (!JniEnvironment.BeginMarshalMethod(jnienv, out var __envp, out var __r)) return default;
	///     try {
	///       var __this = Object.GetObject&lt;T&gt;(jnienv, native__this, DoNotTransfer);
	///       // unmarshal params, call managed method, marshal return
	///     } catch / finally ...
	///   }
	/// Pattern for constructors:
	///   static void nctor_N_uco(IntPtr jnienv, IntPtr native__this, &lt;ctor params...&gt;) {
	///     if (!JniEnvironment.BeginMarshalMethod(jnienv, out var __envp, out var __r)) return;
	///     try {
	///       var __this = (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
	///       __this.SetHandle(native__this, DoNotTransfer);  // registers peer with runtime
	///       __this..ctor(params...);                        // user constructor
	///     } catch / finally ...
	///   }
	/// </summary>
	MethodDefinitionHandle EmitExportMarshalMethod (MetadataBuilder metadata, BlobBuilder ilBuilder, ExportMarshalMethodData export)
	{
		var jniParams = JniSignatureHelper.ParseParameterTypes (export.JniSignature);
		var jniParamTypes = JniSignatureHelper.ParseParameterTypeStrings (export.JniSignature);
		var jniReturnType = export.IsConstructor ? "V" : JniSignatureHelper.ParseReturnTypeString (export.JniSignature);
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

		// Resolve managed type references (needed early for locals signature in constructor case)
		var declaringTypeRef = ResolveTypeRef (metadata, export.DeclaringType);

		// Build the locals signature:
		//   local 0: JniTransition __envp
		//   local 1: JniRuntime __r
		//   local 2: Exception __e
		//   local 3: <return type> __ret  (only for non-void methods)
		//   local 3 (ctor): T __this  (for constructors — holds the uninitialized object)
		int fixedLocalCount = export.IsConstructor || !isVoid ? 4 : 3;
		int parameterLocalStart = fixedLocalCount;
		int[] parameterLocals = new int [export.ManagedParameters.Count];
		int localCount = fixedLocalCount + export.ManagedParameters.Count;
		var localsBlob = new BlobBuilder (32);
		var localsEncoder = new BlobEncoder (localsBlob).LocalVariableSignature (localCount);
		localsEncoder.AddVariable ().Type ().Type (_jniTransitionRef, true);   // local 0
		localsEncoder.AddVariable ().Type ().Type (_jniRuntimeRef, false);     // local 1
		localsEncoder.AddVariable ().Type ().Type (_systemExceptionRef, false); // local 2
		if (export.IsConstructor) {
			localsEncoder.AddVariable ().Type ().Type (declaringTypeRef, false); // local 3: T __this
		} else if (!isVoid) {
			JniSignatureHelper.EncodeClrType (localsEncoder.AddVariable ().Type (), returnKind); // local 3: __ret
		}
		for (int i = 0; i < export.ManagedParameters.Count; i++) {
			parameterLocals [i] = parameterLocalStart + i;
			EncodeManagedTypeForExportCall (localsEncoder.AddVariable ().Type (), metadata,
				export.ManagedParameters [i].ManagedTypeName, export.ManagedParameters [i].AssemblyName, jniParams [i], export.ManagedParameters [i].JniType);
		}
		var localsSigHandle = metadata.AddStandaloneSignature (metadata.GetOrAddBlob (localsBlob));

		// Build GetObject<T> method spec — generic instantiation of Object.GetObject<T>
		// Not needed for static methods or constructors
		EntityHandle getObjectRef = default;
		if (!export.IsStatic && !export.IsConstructor) {
			getObjectRef = BuildGetObjectMethodSpec (metadata, declaringTypeRef);
		}

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
			// Constructor: create uninitialized object, call activation ctor, then user ctor
			// var __this = (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
			encoder.OpCode (ILOpCode.Ldtoken);
			encoder.Token (declaringTypeRef);
			encoder.Call (_getTypeFromHandleRef);
			encoder.Call (_getUninitializedObjectRef);
			encoder.OpCode (ILOpCode.Castclass);
			encoder.Token (declaringTypeRef);
			encoder.OpCode (ILOpCode.Stloc_3); // store in local 3: __this

			// __this.SetHandle(native__this, JniHandleOwnership.DoNotTransfer)
			// — registers the peer with the runtime and sets up the JNI handle association
			encoder.OpCode (ILOpCode.Ldloc_3); // __this
			encoder.LoadArgument (1); // native__this
			encoder.OpCode (ILOpCode.Ldc_i4_0); // JniHandleOwnership.DoNotTransfer = 0
			encoder.Call (_setHandleRef);
		} else if (!export.IsStatic) {
			// Instance method: get managed object from JNI handle
			encoder.LoadArgument (0); // jnienv
			encoder.LoadArgument (1); // native__this
			encoder.OpCode (ILOpCode.Ldc_i4_0); // JniHandleOwnership.DoNotTransfer = 0
			encoder.Call (getObjectRef);
		}

		// Unmarshal each parameter into locals
		for (int i = 0; i < export.ManagedParameters.Count; i++) {
			EmitParameterUnmarshal (encoder, metadata, export.ManagedParameters [i], jniParams [i], jniParamTypes [i], i + 2);
			StoreLocal (encoder, parameterLocals [i]);
		}

		// Load target + managed parameters for the managed call
		if (export.IsConstructor) {
			encoder.OpCode (ILOpCode.Ldloc_3);
		}
		for (int i = 0; i < export.ManagedParameters.Count; i++) {
			LoadLocal (encoder, parameterLocals [i]);
		}

		// Call managed method: static → call, instance ctor → call, instance method → callvirt
		if (export.IsStatic || export.IsConstructor) {
			encoder.Call (managedMethodRef);
		} else {
			encoder.OpCode (ILOpCode.Callvirt);
			encoder.Token (managedMethodRef);
		}

		// Marshal return value and store in local 3
		if (!isVoid) {
			EmitReturnMarshal (encoder, metadata, returnKind, jniReturnType, export.ManagedReturnType);
			encoder.OpCode (ILOpCode.Stloc_3);
		}

		// Copy back array parameter changes to JNI arrays
		for (int i = 0; i < export.ManagedParameters.Count; i++) {
			if (IsManagedArrayType (export.ManagedParameters [i].ManagedTypeName)) {
				EmitArrayParameterCopyBack (encoder, metadata, export.ManagedParameters [i], parameterLocals [i], i + 2);
			}
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
			sig => sig.MethodSignature (isInstanceMethod: !export.IsStatic).Parameters (paramCount,
				rt => {
					if (isVoid) {
						rt.Void ();
					} else {
						EncodeExportReturnType (rt, metadata, export.ManagedReturnType, returnKind, JniSignatureHelper.ParseReturnTypeString (export.JniSignature));
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
		if (!string.IsNullOrEmpty (param.ManagedTypeName)) {
			EncodeManagedTypeForExportCall (p.AddParameter ().Type (), metadata, param.ManagedTypeName, param.AssemblyName, jniKind, param.JniType);
		} else if (jniKind != JniParamKind.Object) {
			JniSignatureHelper.EncodeClrType (p.AddParameter ().Type (), jniKind);
		} else {
			p.AddParameter ().Type ().IntPtr ();
		}
	}

	void EncodeExportReturnType (ReturnTypeEncoder rt, MetadataBuilder metadata, string? managedReturnType, JniParamKind returnKind, string jniReturnType)
	{
		if (!string.IsNullOrEmpty (managedReturnType)) {
			string typeName = managedReturnType!;
			string assemblyName = "";
			int commaIndex = typeName.IndexOf (", ", StringComparison.Ordinal);
			if (commaIndex >= 0) {
				assemblyName = typeName.Substring (commaIndex + 2);
				typeName = typeName.Substring (0, commaIndex);
			}
			EncodeManagedTypeForExportCall (rt.Type (), metadata, typeName, assemblyName, returnKind, jniReturnType);
		} else if (returnKind != JniParamKind.Object) {
			JniSignatureHelper.EncodeClrType (rt.Type (), returnKind);
		} else {
			rt.Type ().IntPtr ();
		}
	}

	void EncodeManagedTypeForExportCall (SignatureTypeEncoder encoder, MetadataBuilder metadata,
		string managedTypeName, string assemblyName, JniParamKind jniKind, string jniType)
	{
		if (TryEncodeManagedPrimitiveType (encoder, managedTypeName)) {
			return;
		}

		if (managedTypeName == "System.String") {
			encoder.String ();
			return;
		}

		if (IsManagedArrayType (managedTypeName)) {
			EncodeManagedArrayType (encoder, metadata, managedTypeName, assemblyName, jniType);
			return;
		}

		var typeRef = ResolveTypeRef (metadata, new TypeRefData {
			ManagedTypeName = managedTypeName,
			AssemblyName = assemblyName,
		});
		encoder.Type (typeRef, IsEnumManagedType (managedTypeName, jniKind));
	}

	void EncodeManagedArrayType (SignatureTypeEncoder encoder, MetadataBuilder metadata, string managedArrayTypeName, string assemblyName, string jniType)
	{
		string elementType = managedArrayTypeName.Substring (0, managedArrayTypeName.Length - 2);
		var arrayEncoder = encoder.SZArray ();
		if (TryEncodeManagedPrimitiveType (arrayEncoder, elementType)) {
			return;
		}
		if (elementType == "System.String") {
			arrayEncoder.String ();
			return;
		}
		var elementRef = ResolveTypeRef (metadata, new TypeRefData {
			ManagedTypeName = elementType,
			AssemblyName = assemblyName,
		});
		var elementJniKind = jniType.StartsWith ("[", StringComparison.Ordinal)
			? JniSignatureHelper.ParseSingleTypeFromDescriptor (jniType.Substring (1))
			: JniParamKind.Object;
		arrayEncoder.Type (elementRef, IsEnumManagedType (elementType, elementJniKind));
	}

	static bool TryEncodeManagedPrimitiveType (SignatureTypeEncoder encoder, string managedTypeName)
	{
		switch (managedTypeName) {
		case "System.Boolean": encoder.Boolean (); return true;
		case "System.SByte": encoder.SByte (); return true;
		case "System.Byte": encoder.Byte (); return true;
		case "System.Char": encoder.Char (); return true;
		case "System.Int16": encoder.Int16 (); return true;
		case "System.UInt16": encoder.UInt16 (); return true;
		case "System.Int32": encoder.Int32 (); return true;
		case "System.UInt32": encoder.UInt32 (); return true;
		case "System.Int64": encoder.Int64 (); return true;
		case "System.UInt64": encoder.UInt64 (); return true;
		case "System.Single": encoder.Single (); return true;
		case "System.Double": encoder.Double (); return true;
		case "System.IntPtr": encoder.IntPtr (); return true;
		case "System.UIntPtr": encoder.UIntPtr (); return true;
		default: return false;
		}
	}

	static bool IsManagedArrayType (string managedTypeName)
		=> managedTypeName.EndsWith ("[]", StringComparison.Ordinal);

	static bool IsEnumManagedType (string managedTypeName, JniParamKind jniKind)
	{
		if (jniKind == JniParamKind.Object || IsManagedArrayType (managedTypeName)) {
			return false;
		}
		return !string.Equals (managedTypeName, "System.Boolean", StringComparison.Ordinal) &&
			!string.Equals (managedTypeName, "System.SByte", StringComparison.Ordinal) &&
			!string.Equals (managedTypeName, "System.Byte", StringComparison.Ordinal) &&
			!string.Equals (managedTypeName, "System.Char", StringComparison.Ordinal) &&
			!string.Equals (managedTypeName, "System.Int16", StringComparison.Ordinal) &&
			!string.Equals (managedTypeName, "System.UInt16", StringComparison.Ordinal) &&
			!string.Equals (managedTypeName, "System.Int32", StringComparison.Ordinal) &&
			!string.Equals (managedTypeName, "System.UInt32", StringComparison.Ordinal) &&
			!string.Equals (managedTypeName, "System.Int64", StringComparison.Ordinal) &&
			!string.Equals (managedTypeName, "System.UInt64", StringComparison.Ordinal) &&
			!string.Equals (managedTypeName, "System.Single", StringComparison.Ordinal) &&
			!string.Equals (managedTypeName, "System.Double", StringComparison.Ordinal) &&
			!string.Equals (managedTypeName, "System.IntPtr", StringComparison.Ordinal) &&
			!string.Equals (managedTypeName, "System.UIntPtr", StringComparison.Ordinal);
	}

	void EmitParameterUnmarshal (InstructionEncoder encoder, MetadataBuilder metadata, ExportParamData param, JniParamKind jniKind, string jniType, int argIndex)
	{
		if (IsManagedArrayType (param.ManagedTypeName)) {
			// Arrays: JNIEnv.GetArray<T>(handle)
			var getArraySpec = BuildArrayMethodSpec (metadata, _jniEnvGetArrayOpenRef, param.ManagedTypeName, param.AssemblyName, param.JniType);
			encoder.LoadArgument (argIndex);
			encoder.Call (getArraySpec);
			return;
		}

		if (jniKind != JniParamKind.Object) {
			encoder.LoadArgument (argIndex);
			if (jniKind == JniParamKind.Boolean && param.ManagedTypeName == "System.Boolean") {
				// JNI jboolean is byte; managed bool expects 0/1 semantics.
				encoder.OpCode (ILOpCode.Ldc_i4_0);
				encoder.OpCode (ILOpCode.Cgt_un);
			}
			return;
		}

		if (param.ManagedTypeName == "System.String") {
			// String: GetString or GetCharSequence depending on JNI descriptor.
			encoder.LoadArgument (argIndex);
			encoder.OpCode (ILOpCode.Ldc_i4_0); // DoNotTransfer
			encoder.Call (jniType == "Ljava/lang/CharSequence;" ? _jniEnvGetCharSequenceRef : _jniEnvGetStringRef);
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

	void EmitReturnMarshal (InstructionEncoder encoder, MetadataBuilder metadata, JniParamKind returnKind, string jniReturnType, string? managedReturnType)
	{
		string? managedTypeName = null;
		string managedAssemblyName = "";
		if (!string.IsNullOrEmpty (managedReturnType)) {
			(managedTypeName, managedAssemblyName) = SplitManagedTypeNameAndAssembly (managedReturnType!);
		}

		if (!string.IsNullOrEmpty (managedTypeName) && IsManagedArrayType (managedTypeName!)) {
			// Managed array -> JNI array
			var newArraySpec = BuildArrayMethodSpec (metadata, _jniEnvNewArrayOpenRef, managedTypeName!, managedAssemblyName, jniReturnType);
			encoder.Call (newArraySpec);
			return;
		}

		if (returnKind != JniParamKind.Object) {
			// Enum return values are marshaled as int (legacy behavior).
			if (!string.IsNullOrEmpty (managedTypeName) && IsEnumManagedType (managedTypeName!, returnKind)) {
				encoder.OpCode (ILOpCode.Conv_i4);
			}
			return;
		}

		if (managedTypeName == "System.String") {
			encoder.Call (jniReturnType == "Ljava/lang/CharSequence;" ? _charSequenceToLocalJniHandleStringRef : _jniEnvNewStringRef);
			return;
		}

		// Java object: JNIEnv.ToLocalJniHandle(result)
		encoder.Call (_jniEnvToLocalJniHandleRef);
	}

	void EmitArrayParameterCopyBack (InstructionEncoder encoder, MetadataBuilder metadata, ExportParamData param, int localIndex, int argIndex)
	{
		var skipLabel = encoder.DefineLabel ();
		LoadLocal (encoder, localIndex);
		encoder.Branch (ILOpCode.Brfalse_s, skipLabel);
		LoadLocal (encoder, localIndex);
		encoder.LoadArgument (argIndex);
		var copyArraySpec = BuildArrayMethodSpec (metadata, _jniEnvCopyArrayOpenRef, param.ManagedTypeName, param.AssemblyName, param.JniType);
		encoder.Call (copyArraySpec);
		encoder.MarkLabel (skipLabel);
	}

	EntityHandle BuildArrayMethodSpec (MetadataBuilder metadata, MemberReferenceHandle openMethodRef, string managedArrayTypeName, string assemblyName, string jniType)
	{
		string elementTypeName = managedArrayTypeName.EndsWith ("[]", StringComparison.Ordinal)
			? managedArrayTypeName.Substring (0, managedArrayTypeName.Length - 2)
			: managedArrayTypeName;

		var instBlob = new BlobBuilder (16);
		instBlob.WriteByte (0x0A); // ELEMENT_TYPE_GENERICINST (method)
		instBlob.WriteCompressedInteger (1); // one type argument
		WriteGenericTypeArgument (instBlob, metadata, elementTypeName, assemblyName,
			IsEnumManagedType (elementTypeName, JniSignatureHelper.ParseSingleTypeFromDescriptor (jniType.StartsWith ("[", StringComparison.Ordinal) ? jniType.Substring (1) : jniType)));
		return metadata.AddMethodSpecification (openMethodRef, metadata.GetOrAddBlob (instBlob));
	}

	void WriteGenericTypeArgument (BlobBuilder blob, MetadataBuilder metadata, string managedTypeName, string assemblyName, bool isValueType)
	{
		if (TryGetPrimitiveElementTypeCode (managedTypeName, out byte primitiveCode)) {
			blob.WriteByte (primitiveCode);
			return;
		}

		if (managedTypeName == "System.String") {
			blob.WriteByte (0x0E); // ELEMENT_TYPE_STRING
			return;
		}

		var typeRef = ResolveTypeRef (metadata, new TypeRefData {
			ManagedTypeName = managedTypeName,
			AssemblyName = assemblyName,
		});
		blob.WriteByte (isValueType ? (byte) 0x11 : (byte) 0x12); // VALUETYPE | CLASS
		blob.WriteCompressedInteger (CodedIndex.TypeDefOrRefOrSpec (typeRef));
	}

	static bool TryGetPrimitiveElementTypeCode (string managedTypeName, out byte typeCode)
	{
		switch (managedTypeName) {
		case "System.Boolean": typeCode = 0x02; return true;
		case "System.Char": typeCode = 0x03; return true;
		case "System.SByte": typeCode = 0x04; return true;
		case "System.Byte": typeCode = 0x05; return true;
		case "System.Int16": typeCode = 0x06; return true;
		case "System.UInt16": typeCode = 0x07; return true;
		case "System.Int32": typeCode = 0x08; return true;
		case "System.UInt32": typeCode = 0x09; return true;
		case "System.Int64": typeCode = 0x0A; return true;
		case "System.UInt64": typeCode = 0x0B; return true;
		case "System.Single": typeCode = 0x0C; return true;
		case "System.Double": typeCode = 0x0D; return true;
		default:
			typeCode = 0;
			return false;
		}
	}

	static (string managedTypeName, string assemblyName) SplitManagedTypeNameAndAssembly (string managedType)
	{
		int commaIndex = managedType.IndexOf (", ", StringComparison.Ordinal);
		if (commaIndex < 0) {
			return (managedType, "");
		}
		return (managedType.Substring (0, commaIndex), managedType.Substring (commaIndex + 2));
	}

	static void LoadLocal (InstructionEncoder encoder, int localIndex)
	{
		switch (localIndex) {
		case 0: encoder.OpCode (ILOpCode.Ldloc_0); return;
		case 1: encoder.OpCode (ILOpCode.Ldloc_1); return;
		case 2: encoder.OpCode (ILOpCode.Ldloc_2); return;
		case 3: encoder.OpCode (ILOpCode.Ldloc_3); return;
		default:
			encoder.OpCode (ILOpCode.Ldloc_s);
			encoder.CodeBuilder.WriteByte ((byte) localIndex);
			return;
		}
	}

	static void StoreLocal (InstructionEncoder encoder, int localIndex)
	{
		switch (localIndex) {
		case 0: encoder.OpCode (ILOpCode.Stloc_0); return;
		case 1: encoder.OpCode (ILOpCode.Stloc_1); return;
		case 2: encoder.OpCode (ILOpCode.Stloc_2); return;
		case 3: encoder.OpCode (ILOpCode.Stloc_3); return;
		default:
			encoder.OpCode (ILOpCode.Stloc_s);
			encoder.CodeBuilder.WriteByte ((byte) localIndex);
			return;
		}
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
