using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

using TrackedInstructionEncoder = Microsoft.Android.Sdk.TrimmableTypeMap.PEAssemblyBuilder.TrackedInstructionEncoder;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

sealed partial class TypeMapAssemblyEmitter
{
	void EmitCreateInstance (JavaPeerProxyData proxy)
	{
		if (!proxy.HasActivation) {
			EmitCreateInstanceNoActivation ();
			return;
		}

		if (proxy.IsGenericDefinition) {
			EmitCreateInstanceGenericDefinition ();
			return;
		}

		if (proxy.InvokerType != null) {
			var invokerType = _pe.ResolveTypeRef (proxy.InvokerType);
			if (proxy.InvokerActivationCtorStyle == ActivationCtorStyle.JavaInterop) {
				EmitCreateInstanceViaJavaInteropNewobj (invokerType);
			} else {
				EmitCreateInstanceViaNewobj (invokerType);
			}
			return;
		}

		// JavaInterop-style activation ctors (ref JniObjectReference, JniObjectReferenceOptions)
		// require parameter conversion from (IntPtr, JniHandleOwnership).
		if (proxy.ActivationCtor?.Style == ActivationCtorStyle.JavaInterop) {
			var targetRef = _pe.ResolveTypeRef (proxy.TargetType);
			var jiCtor = proxy.ActivationCtor ?? throw new InvalidOperationException ("ActivationCtor should not be null");
			if (jiCtor.IsOnLeafType) {
				EmitCreateInstanceViaJavaInteropNewobj (targetRef);
			} else {
				// Legacy GetConstructor() doesn't find inherited ctors —
				// match that behavior by returning null.
				EmitCreateInstanceNoActivation ();
			}
			return;
		}

		// At this point, ActivationCtor is guaranteed non-null (HasActivation && InvokerType == null)
		var activationCtor = proxy.ActivationCtor ?? throw new InvalidOperationException ("ActivationCtor should not be null when HasActivation is true and InvokerType is null");
		var targetTypeRef = _pe.ResolveTypeRef (proxy.TargetType);

		if (activationCtor.IsOnLeafType) {
			EmitCreateInstanceViaNewobj (targetTypeRef);
		} else {
			// Legacy GetConstructor() doesn't find inherited ctors —
			// match that behavior by returning null.
			EmitCreateInstanceNoActivation ();
		}
	}

	void EmitCreateInstanceNoActivation ()
	{
		EmitCreateInstanceBody (encoder => {
			encoder.OpCode (ILOpCode.Ldnull);
			encoder.Return (returnsValue: true);
		});
	}

	void EmitCreateInstanceGenericDefinition ()
	{
		EmitCreateInstanceBody (encoder => {
			encoder.LoadString (_pe.Metadata.GetOrAddUserString ("Cannot create instance of open generic type."));
			encoder.NewObject (_notSupportedExceptionCtorRef, parameterCount: 1);
			encoder.Throw ();
		});
	}

	void EmitCreateInstanceViaNewobj (EntityHandle typeRef)
	{
		var ctorRef = AddActivationCtorRef (typeRef);
		EmitCreateInstanceBody (encoder => {
			encoder.OpCode (ILOpCode.Ldarg_1);
			encoder.OpCode (ILOpCode.Ldarg_2);
			encoder.NewObject (ctorRef, parameterCount: 2);
			encoder.Return (returnsValue: true);
		});
	}

	void EmitCreateInstanceInheritedCtor (EntityHandle targetTypeRef, ActivationCtorData activationCtor)
	{
		var baseActivationCtorRef = AddActivationCtorRef (_pe.ResolveTypeRef (activationCtor.DeclaringType));
		EmitCreateInstanceBody (encoder => {
			encoder.LoadToken (targetTypeRef);
			encoder.Call (_getTypeFromHandleRef, parameterCount: 1, returnsValue: true);
			encoder.Call (_getUninitializedObjectRef, parameterCount: 1, returnsValue: true);
			encoder.CastClass (targetTypeRef);

			encoder.OpCode (ILOpCode.Dup);
			encoder.OpCode (ILOpCode.Ldarg_1);
			encoder.OpCode (ILOpCode.Ldarg_2);
			encoder.Call (baseActivationCtorRef, parameterCount: 2, isInstance: true);

			encoder.Return (returnsValue: true);
		});
	}

	/// <summary>
	/// Emits CreateInstance for JavaInterop-style activation (leaf type):
	///   var jniRef = new JniObjectReference(handle);
	///   var result = new TargetType(ref jniRef, JniObjectReferenceOptions.Copy);
	///   JNIEnv.DeleteRef(handle, ownership);
	///   return result;
	/// </summary>
	void EmitCreateInstanceViaJavaInteropNewobj (EntityHandle typeRef)
	{
		var ctorRef = AddJavaInteropActivationCtorRef (typeRef);
		EmitCreateInstanceBodyWithLocals (
			EncodeJniObjectReferenceAndObjectLocals,
			encoder => {
				// var jniRef = new JniObjectReference(handle, JniObjectReferenceType.Invalid);
				encoder.LoadLocalAddress (0);
				encoder.OpCode (ILOpCode.Ldarg_1); // handle
				encoder.LoadConstantI4 (0); // JniObjectReferenceType.Invalid
				encoder.Call (_jniObjectReferenceCtorRef, parameterCount: 2, isInstance: true);

				// var result = new TargetType(ref jniRef, JniObjectReferenceOptions.Copy);
				encoder.LoadLocalAddress (0);
				encoder.LoadConstantI4 (1); // JniObjectReferenceOptions.Copy
				encoder.NewObject (ctorRef, parameterCount: 2);
				encoder.StoreLocal (1); // save result

				// JNIEnv.DeleteRef(handle, ownership);
				encoder.OpCode (ILOpCode.Ldarg_1); // handle
				encoder.OpCode (ILOpCode.Ldarg_2); // ownership
				encoder.Call (_jniEnvDeleteRefRef, parameterCount: 2);

				encoder.LoadLocal (1); // load result
				encoder.Return (returnsValue: true);
			});
	}

	/// <summary>
	/// Emits CreateInstance for JavaInterop-style activation (inherited ctor):
	///   var obj = (TargetType)RuntimeHelpers.GetUninitializedObject(typeof(TargetType));
	///   var jniRef = new JniObjectReference(handle);
	///   obj.BaseCtor(ref jniRef, JniObjectReferenceOptions.Copy);
	///   JNIEnv.DeleteRef(handle, ownership);
	///   return obj;
	/// </summary>
	void EmitCreateInstanceInheritedJavaInteropCtor (EntityHandle targetTypeRef, ActivationCtorData activationCtor)
	{
		var baseCtorRef = AddJavaInteropActivationCtorRef (_pe.ResolveTypeRef (activationCtor.DeclaringType));
		EmitCreateInstanceBodyWithLocals (
			EncodeJniObjectReferenceLocal,
			encoder => {
				// var obj = (TargetType)RuntimeHelpers.GetUninitializedObject(typeof(TargetType));
				encoder.LoadToken (targetTypeRef);
				encoder.Call (_getTypeFromHandleRef, parameterCount: 1, returnsValue: true);
				encoder.Call (_getUninitializedObjectRef, parameterCount: 1, returnsValue: true);
				encoder.CastClass (targetTypeRef);

				// dup obj (one copy for the call, one for the return)
				encoder.OpCode (ILOpCode.Dup);

				// var jniRef = new JniObjectReference(handle, JniObjectReferenceType.Invalid);
				encoder.LoadLocalAddress (0);
				encoder.OpCode (ILOpCode.Ldarg_1); // handle
				encoder.LoadConstantI4 (0); // JniObjectReferenceType.Invalid
				encoder.Call (_jniObjectReferenceCtorRef, parameterCount: 2, isInstance: true);

				// obj.BaseCtor(ref jniRef, JniObjectReferenceOptions.Copy);
				encoder.LoadLocalAddress (0);
				encoder.LoadConstantI4 (1); // JniObjectReferenceOptions.Copy
				encoder.Call (baseCtorRef, parameterCount: 2, isInstance: true);

				// JNIEnv.DeleteRef(handle, ownership);
				encoder.OpCode (ILOpCode.Ldarg_1); // handle
				encoder.OpCode (ILOpCode.Ldarg_2); // ownership
				encoder.Call (_jniEnvDeleteRefRef, parameterCount: 2);

				encoder.Return (returnsValue: true);
			});
	}

	void EncodeJniObjectReferenceLocal (BlobBuilder blob)
	{
		// LOCAL_SIG header (0x07), count = 1, ELEMENT_TYPE_VALUETYPE + compressed token
		blob.WriteByte (0x07); // LOCAL_SIG
		blob.WriteCompressedInteger (1); // 1 local variable
		blob.WriteByte (0x11); // ELEMENT_TYPE_VALUETYPE
		blob.WriteCompressedInteger (CodedIndex.TypeDefOrRefOrSpec (_jniObjectReferenceRef));
	}

	void EncodeJniObjectReferenceAndObjectLocals (BlobBuilder blob)
	{
		// LOCAL_SIG header (0x07), count = 2:
		//   local 0: JniObjectReference (valuetype)
		//   local 1: object (for storing the newobj result across the DeleteRef call)
		blob.WriteByte (0x07); // LOCAL_SIG
		blob.WriteCompressedInteger (2); // 2 local variables
		blob.WriteByte (0x11); // ELEMENT_TYPE_VALUETYPE
		blob.WriteCompressedInteger (CodedIndex.TypeDefOrRefOrSpec (_jniObjectReferenceRef));
		blob.WriteByte (0x1c); // ELEMENT_TYPE_OBJECT
	}

	MemberReferenceHandle AddJavaInteropActivationCtorRef (EntityHandle declaringTypeRef)
	{
		return _pe.AddMemberRef (declaringTypeRef, ".ctor",
			sig => sig.MethodSignature (isInstanceMethod: true).Parameters (2,
				rt => rt.Void (),
				p => {
					// ref JniObjectReference — encoded as byref valuetype
					p.AddParameter ().Type (isByRef: true).Type (_jniObjectReferenceRef, true);
					// JniObjectReferenceOptions — encoded as valuetype (enum)
					p.AddParameter ().Type ().Type (_jniObjectReferenceOptionsRef, true);
				}));
	}

	void EmitCreateInstanceBody (Action<TrackedInstructionEncoder> emitIL)
	{
		_pe.EmitBody ("CreateInstance",
			MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig,
			sig => sig.MethodSignature (isInstanceMethod: true).Parameters (2,
				rt => rt.Type ().Type (_iJavaPeerableRef, false),
				p => {
					p.AddParameter ().Type ().IntPtr ();
					p.AddParameter ().Type ().Type (_jniHandleOwnershipRef, true);
				}),
			emitIL);
	}

	void EmitCreateInstanceBodyWithLocals (Action<BlobBuilder> encodeLocals, Action<TrackedInstructionEncoder> emitIL)
	{
		_pe.EmitBody ("CreateInstance",
			MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig,
			sig => sig.MethodSignature (isInstanceMethod: true).Parameters (2,
				rt => rt.Type ().Type (_iJavaPeerableRef, false),
				p => {
					p.AddParameter ().Type ().IntPtr ();
					p.AddParameter ().Type ().Type (_jniHandleOwnershipRef, true);
				}),
			emitIL,
			encodeLocals);
	}

	MemberReferenceHandle AddActivationCtorRef (EntityHandle declaringTypeRef)
	{
		return _pe.AddMemberRef (declaringTypeRef, ".ctor",
			sig => sig.MethodSignature (isInstanceMethod: true).Parameters (2,
				rt => rt.Void (),
				p => {
					p.AddParameter ().Type ().IntPtr ();
					p.AddParameter ().Type ().Type (_jniHandleOwnershipRef, true);
				}));
	}

	MemberReferenceHandle AddManagedCtorRef (EntityHandle declaringTypeRef, IReadOnlyList<string> parameterTypes, string defaultAssemblyName)
	{
		var blob = new BlobBuilder (32);
		blob.WriteByte (0x20); // HASTHIS
		blob.WriteCompressedInteger (parameterTypes.Count);
		blob.WriteByte (0x01); // ELEMENT_TYPE_VOID
		foreach (var parameterType in parameterTypes) {
			WriteManagedTypeSignature (blob, parameterType, defaultAssemblyName);
		}
		return _pe.Metadata.AddMemberReference (declaringTypeRef, _pe.Metadata.GetOrAddString (".ctor"), _pe.Metadata.GetOrAddBlob (blob));
	}

	MethodDefinitionHandle EmitUcoMethod (UcoMethodData uco, JavaPeerProxyData proxy)
	{
		var jniParams = JniSignatureHelper.ParseParameterTypes (uco.JniSignature);
		var returnKind = JniSignatureHelper.ParseReturnType (uco.JniSignature);
		int paramCount = 2 + jniParams.Count;
		bool isVoid = returnKind == JniParamKind.Void;

		// UCO wrapper signature: uses JNI ABI types (byte for boolean)
		Action<BlobEncoder> encodeSig = sig => sig.MethodSignature ().Parameters (paramCount,
			rt => { if (isVoid) rt.Void (); else JniSignatureHelper.EncodeClrType (rt.Type (), returnKind); },
			p => {
				p.AddParameter ().Type ().IntPtr ();
				p.AddParameter ().Type ().IntPtr ();
				for (int j = 0; j < jniParams.Count; j++)
					JniSignatureHelper.EncodeClrType (p.AddParameter ().Type (), jniParams [j]);
			});

		// Callback member reference: uses MCW n_* types (sbyte for boolean)
		Action<BlobEncoder> encodeCallbackSig = sig => sig.MethodSignature ().Parameters (paramCount,
			rt => { if (isVoid) rt.Void (); else JniSignatureHelper.EncodeClrTypeForCallback (rt.Type (), returnKind); },
			p => {
				p.AddParameter ().Type ().IntPtr ();
				p.AddParameter ().Type ().IntPtr ();
				for (int j = 0; j < jniParams.Count; j++)
					JniSignatureHelper.EncodeClrTypeForCallback (p.AddParameter ().Type (), jniParams [j]);
			});

		var callbackTypeHandle = _pe.ResolveTypeRef (uco.CallbackType);
		var callbackRef = _pe.AddMemberRef (callbackTypeHandle, uco.CallbackMethodName, encodeCallbackSig);

		var handle = _pe.EmitBody (uco.WrapperName,
			MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig,
			encodeSig,
			(encoder, cfb) => EmitUcoForwarderBody (encoder, cfb, returnKind, enc => {
				for (int p = 0; p < paramCount; p++)
					enc.LoadArgument (p);
				enc.Call (callbackRef, paramCount, returnsValue: !isVoid);
			}),
			blob => EncodeUcoForwarderLegacyLocals (blob, returnKind));

		AddUnmanagedCallersOnlyAttribute (handle);
		return handle;
	}

	void EmitUcoForwarderBody (TrackedInstructionEncoder encoder, ControlFlowBuilder cfb, JniParamKind returnKind, Action<TrackedInstructionEncoder> emitCallback)
	{
		bool isVoid = returnKind == JniParamKind.Void;
		var tryStart = encoder.DefineLabel ();
		var catchStart = encoder.DefineLabel ();
		var afterAll = encoder.DefineLabel ();

		encoder.Call (_waitForBridgeProcessingRef, parameterCount: 0);
		encoder.MarkLabel (tryStart);
		emitCallback (encoder);
		if (!isVoid) {
			encoder.StoreLocal (0);
		}
		encoder.Branch (ILOpCode.Leave, afterAll);

		encoder.MarkLabel (catchStart, stackDepth: 1);
		encoder.StoreLocal (isVoid ? 0 : 1);
		encoder.LoadLocal (isVoid ? 0 : 1);
		encoder.Call (_androidEnvironmentUnhandledExceptionRef, parameterCount: 1);
		encoder.Branch (ILOpCode.Leave, afterAll);

		encoder.MarkLabel (afterAll);
		if (!isVoid) {
			encoder.LoadLocal (0);
		}
		encoder.Return (returnsValue: !isVoid);

		cfb.AddCatchRegion (tryStart, catchStart, catchStart, afterAll, _exceptionRef);
	}

	void EncodeUcoForwarderLegacyLocals (BlobBuilder blob, JniParamKind returnKind)
	{
		bool isVoid = returnKind == JniParamKind.Void;
		blob.WriteByte (0x07); // LOCAL_SIG
		blob.WriteCompressedInteger (isVoid ? 1 : 2);
		if (!isVoid) {
			JniSignatureHelper.EncodeClrType (new SignatureTypeEncoder (blob), returnKind);
		}
		blob.WriteByte (0x12); // ELEMENT_TYPE_CLASS
		blob.WriteCompressedInteger (CodedIndex.TypeDefOrRefOrSpec (_exceptionRef));
	}

	MethodDefinitionHandle EmitUcoConstructor (UcoConstructorData uco, JavaPeerProxyData proxy)
	{
		var targetTypeRef = _pe.ResolveTypeRef (uco.TargetType);
		var activationCtor = proxy.ActivationCtor ?? throw new InvalidOperationException (
			$"UCO constructor wrapper requires an activation ctor for '{uco.TargetType.ManagedTypeName}'");

		// UCO constructor wrappers must match the JNI native method signature exactly.
		// jnienv and self are followed by the Java constructor parameters, which are
		// forwarded when scanner metadata can identify the matching managed constructor.
		var jniParams = JniSignatureHelper.ParseParameterTypes (uco.JniSignature);
		int paramCount = 2 + jniParams.Count;

		Action<BlobEncoder> encodeSig = sig => sig.MethodSignature ().Parameters (paramCount,
			rt => rt.Void (),
			p => {
				p.AddParameter ().Type ().IntPtr (); // jnienv
				p.AddParameter ().Type ().IntPtr (); // self
				for (int j = 0; j < jniParams.Count; j++)
					JniSignatureHelper.EncodeClrType (p.AddParameter ().Type (), jniParams [j]);
			});

		// Open generic types can't be activated because Java construction cannot provide the type arguments.
		if (proxy.IsGenericDefinition) {
			var openGenericHandle = EmitUcoConstructorBody (uco.WrapperName, encodeSig,
				enc => {
					enc.LoadString (_pe.Metadata.GetOrAddUserString ("Constructing instances of generic types from Java is not supported, as the type parameters cannot be determined."));
					enc.NewObject (_notSupportedExceptionCtorRef, parameterCount: 1);
					enc.Throw ();
				},
				EncodeUcoConstructorLocals_Standard);
			AddUnmanagedCallersOnlyAttribute (openGenericHandle);
			return openGenericHandle;
		}

		if (proxy.InvokerType != null) {
			var invokerTypeRef = _pe.ResolveTypeRef (proxy.InvokerType);
			MethodDefinitionHandle invokerHandle;
			if (proxy.InvokerActivationCtorStyle == ActivationCtorStyle.JavaInterop) {
				var ctorRef = AddJavaInteropActivationCtorRef (invokerTypeRef);
				invokerHandle = EmitUcoConstructorBody (uco.WrapperName, encodeSig,
					enc => {
						enc.LoadLocalAddress (3); // jniRef
						enc.LoadArgument (1);     // self
						enc.LoadConstantI4 (0);   // JniObjectReferenceType.Invalid
						enc.Call (_jniObjectReferenceCtorRef, parameterCount: 2, isInstance: true);

						enc.LoadLocalAddress (3); // ref jniRef
						enc.LoadConstantI4 (1);   // JniObjectReferenceOptions.Copy
						enc.NewObject (ctorRef, parameterCount: 2);
						enc.PopValue ();

						enc.LoadArgument (1); // self
						enc.Call (_markActivationPeerReplaceableRef, parameterCount: 1);
					},
					EncodeUcoConstructorLocals_JavaInterop);
			} else {
				var ctorRef = AddActivationCtorRef (invokerTypeRef);
				invokerHandle = EmitUcoConstructorBody (uco.WrapperName, encodeSig,
					enc => {
						enc.LoadArgument (1);    // self
						enc.LoadConstantI4 (0);  // JniHandleOwnership.DoNotTransfer
						enc.NewObject (ctorRef, parameterCount: 2);
						enc.PopValue ();

						enc.LoadArgument (1); // self
						enc.Call (_markActivationPeerReplaceableRef, parameterCount: 1);
					},
					EncodeUcoConstructorLocals_Standard);
			}
			AddUnmanagedCallersOnlyAttribute (invokerHandle);
			return invokerHandle;
		}

		if (uco.HasManagedConstructor && uco.ManagedParameterTypes.Count == jniParams.Count) {
			var ctorRef = AddManagedCtorRef (targetTypeRef, uco.ManagedParameterTypes, uco.TargetType.AssemblyName);
			var managedCtorHandle = EmitUcoConstructorBody (uco.WrapperName, encodeSig,
				enc => EmitManagedConstructorActivation (enc, targetTypeRef, ctorRef, uco.ManagedParameterTypes, jniParams, uco.TargetType.AssemblyName),
				blob => EncodeUcoConstructorLocals_DefaultConstructor (blob, targetTypeRef));
			AddUnmanagedCallersOnlyAttribute (managedCtorHandle);
			return managedCtorHandle;
		}

		MethodDefinitionHandle handle;
		if (activationCtor.Style == ActivationCtorStyle.JavaInterop) {
			var ctorRef = AddJavaInteropActivationCtorRef (
				activationCtor.IsOnLeafType ? targetTypeRef : _pe.ResolveTypeRef (activationCtor.DeclaringType));

			// Locals:
			//   0: JniTransition  (envp)    — out-parameter for BeginMarshalMethod
			//   1: JniRuntime?    (runtime) — out-parameter for BeginMarshalMethod
			//   2: Exception      (e)       — catch variable
			//   3: JniObjectReference (jniRef) — needed for JavaInterop-style activation
			handle = EmitUcoConstructorBody (uco.WrapperName, encodeSig,
				enc => {
					if (!activationCtor.IsOnLeafType) {
						enc.LoadToken (targetTypeRef);
						enc.Call (_getTypeFromHandleRef, parameterCount: 1, returnsValue: true);
						enc.Call (_getUninitializedObjectRef, parameterCount: 1, returnsValue: true);
						enc.CastClass (targetTypeRef);
					}

					enc.LoadLocalAddress (3); // jniRef
					enc.LoadArgument (1);     // self
					enc.LoadConstantI4 (0);   // JniObjectReferenceType.Invalid
					enc.Call (_jniObjectReferenceCtorRef, parameterCount: 2, isInstance: true);

					if (activationCtor.IsOnLeafType) {
						enc.LoadLocalAddress (3); // ref jniRef
						enc.LoadConstantI4 (1);   // JniObjectReferenceOptions.Copy
						enc.NewObject (ctorRef, parameterCount: 2);
						enc.PopValue ();
					} else {
						enc.LoadLocalAddress (3); // ref jniRef
						enc.LoadConstantI4 (1);   // JniObjectReferenceOptions.Copy
						enc.Call (ctorRef, parameterCount: 2, isInstance: true);
					}
					enc.LoadArgument (1); // self
					enc.Call (_markActivationPeerReplaceableRef, parameterCount: 1);
				},
				EncodeUcoConstructorLocals_JavaInterop);
		} else {
			var ctorRef = AddActivationCtorRef (
				activationCtor.IsOnLeafType ? targetTypeRef : _pe.ResolveTypeRef (activationCtor.DeclaringType));

			// Locals:
			//   0: JniTransition  (envp)    — out-parameter for BeginMarshalMethod
			//   1: JniRuntime?    (runtime) — out-parameter for BeginMarshalMethod
			//   2: Exception      (e)       — catch variable
			handle = EmitUcoConstructorBody (uco.WrapperName, encodeSig,
				enc => {
					if (activationCtor.IsOnLeafType) {
						enc.LoadArgument (1);    // self
						enc.LoadConstantI4 (0);  // JniHandleOwnership.DoNotTransfer
						enc.NewObject (ctorRef, parameterCount: 2);
						enc.PopValue ();
					} else {
						enc.LoadToken (targetTypeRef);
						enc.Call (_getTypeFromHandleRef, parameterCount: 1, returnsValue: true);
						enc.Call (_getUninitializedObjectRef, parameterCount: 1, returnsValue: true);
						enc.CastClass (targetTypeRef);

						enc.LoadArgument (1);    // self
						enc.LoadConstantI4 (0);  // JniHandleOwnership.DoNotTransfer
						enc.Call (ctorRef, parameterCount: 2, isInstance: true);
					}
					enc.LoadArgument (1); // self
					enc.Call (_markActivationPeerReplaceableRef, parameterCount: 1);
				},
				EncodeUcoConstructorLocals_Standard);
		}
		AddUnmanagedCallersOnlyAttribute (handle);
		return handle;
	}

	MethodDefinitionHandle EmitUcoConstructorBody (
		string wrapperName,
		Action<BlobEncoder> encodeSig,
		Action<TrackedInstructionEncoder> emitActivation,
		Action<BlobBuilder> encodeLocals)
	{
		return _pe.EmitBody (wrapperName,
			MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig,
			encodeSig,
			(encoder, cfb) => EmitUcoConstructorBodyWithMarshal (encoder, cfb, emitActivation),
			encodeLocals);
	}

	void EmitManagedConstructorActivation (
		TrackedInstructionEncoder enc,
		EntityHandle targetTypeRef,
		MemberReferenceHandle ctorRef,
		IReadOnlyList<string> managedParameterTypes,
		IReadOnlyList<JniParamKind> jniParams,
		string defaultAssemblyName)
	{
		var havePeer = enc.DefineLabel ();

		enc.LoadArgument (1);
		enc.Call (_getActivationPeerRef, parameterCount: 1, returnsValue: true);
		enc.CastClass (targetTypeRef);
		enc.StoreLocal (4);

		enc.LoadLocal (4);
		enc.Branch (ILOpCode.Brtrue, havePeer);

		enc.LoadToken (targetTypeRef);
		enc.Call (_getTypeFromHandleRef, parameterCount: 1, returnsValue: true);
		enc.Call (_getUninitializedObjectRef, parameterCount: 1, returnsValue: true);
		enc.CastClass (targetTypeRef);
		enc.StoreLocal (4);

		enc.LoadLocal (4);
		enc.LoadArgument (1); // self
		enc.Call (_setActivationPeerReferenceRef, parameterCount: 2);

		enc.MarkLabel (havePeer);
		enc.LoadLocal (4);
		for (int i = 0; i < managedParameterTypes.Count; i++) {
			EmitManagedConstructorArgument (enc, managedParameterTypes [i], jniParams [i], i + 2, defaultAssemblyName);
		}
		enc.Call (ctorRef, managedParameterTypes.Count, isInstance: true);

		enc.LoadArgument (1); // self
		enc.Call (_markActivationPeerReplaceableRef, parameterCount: 1);
	}

	/// <summary>
	/// Emits the common try/catch/finally marshal-method wrapper pattern used by all
	/// non-generic UCO constructor bodies:
	/// <code>
	/// if (!JniEnvironment.BeginMarshalMethod(jnienv, out envp, out runtime)) return;
	/// try {
	///     if (!JavaPeerProxy.ShouldSkipActivation(self)) { [emitActivation] }
	/// } catch (Exception e) {
	///     runtime?.OnUserUnhandledException(ref envp, e);
	/// } finally {
	///     JniEnvironment.EndMarshalMethod(ref envp);
	/// }
	/// </code>
	/// Locals 0 (JniTransition envp) and 1 (JniRuntime? runtime) must be declared by the caller.
	/// Local 2 (Exception e) must also be declared. Any activation-specific locals start at index 3.
	/// </summary>
	void EmitUcoConstructorBodyWithMarshal (TrackedInstructionEncoder encoder, ControlFlowBuilder cfb, Action<TrackedInstructionEncoder> emitActivation)
	{
		var skipLabel = encoder.DefineLabel ();
		var tryStart = encoder.DefineLabel ();
		var catchStart = encoder.DefineLabel ();
		var finallyStart = encoder.DefineLabel ();
		var afterAll = encoder.DefineLabel ();
		var endCatch = encoder.DefineLabel ();

		// Preamble: call BeginMarshalMethod; skip everything if it returns false.
		encoder.LoadArgument (0);      // jnienv
		encoder.LoadLocalAddress (0);  // out JniTransition (local 0)
		encoder.LoadLocalAddress (1);  // out JniRuntime? (local 1)
		encoder.Call (_beginMarshalMethodRef, parameterCount: 3, returnsValue: true);
		encoder.Branch (ILOpCode.Brfalse, afterAll);

		// TRY — check ShouldSkipActivation, then run activation code.
		encoder.MarkLabel (tryStart);
		encoder.LoadArgument (1);      // self (IntPtr)
		encoder.Call (_shouldSkipActivationRef, parameterCount: 1, returnsValue: true);
		encoder.Branch (ILOpCode.Brtrue, skipLabel);

		emitActivation (encoder);

		encoder.MarkLabel (skipLabel);
		encoder.Branch (ILOpCode.Leave, afterAll);

		// CATCH (System.Exception e)
		encoder.MarkLabel (catchStart, stackDepth: 1);
		encoder.StoreLocal (2);              // e = exception (local 2)
		encoder.LoadLocal (1);               // load runtime (__r)
		encoder.Branch (ILOpCode.Brfalse, endCatch);
		encoder.LoadLocal (1);               // __r for callvirt
		encoder.LoadLocalAddress (0);        // ref envp
		encoder.LoadLocal (2);               // e
		encoder.Callvirt (_onUserUnhandledExceptionRef, parameterCount: 2);
		encoder.MarkLabel (endCatch);
		encoder.Branch (ILOpCode.Leave, afterAll);

		// FINALLY
		encoder.MarkLabel (finallyStart);
		encoder.LoadLocalAddress (0);        // ref envp
		encoder.Call (_endMarshalMethodRef, parameterCount: 1);
		encoder.OpCode (ILOpCode.Endfinally);

		// AFTER (both finallyEnd and the early-return target)
		encoder.MarkLabel (afterAll);
		encoder.Return ();

		// Register exception regions:
		// Catch region:   try [tryStart, catchStart),  handler [catchStart, finallyStart)
		// Finally region: try [tryStart, finallyStart), handler [finallyStart, afterAll)
		cfb.AddCatchRegion (tryStart, catchStart, catchStart, finallyStart, _exceptionRef);
		cfb.AddFinallyRegion (tryStart, finallyStart, finallyStart, afterAll);
	}

	void EmitManagedConstructorArgument (TrackedInstructionEncoder encoder, string managedType, JniParamKind jniKind, int argumentIndex, string defaultAssemblyName)
	{
		if (jniKind != JniParamKind.Object) {
			encoder.LoadArgument (argumentIndex);
			return;
		}

		if (managedType == "System.String") {
			encoder.LoadArgument (argumentIndex);
			encoder.LoadConstantI4 (0); // JniHandleOwnership.DoNotTransfer
			encoder.Call (_jniEnvGetStringRef, parameterCount: 2, returnsValue: true);
			return;
		}

		if (TryGetSzArrayElementType (managedType, out var elementType)) {
			var arrayType = ResolveManagedTypeHandle (managedType, defaultAssemblyName);
			var elementTypeHandle = ResolveManagedTypeHandle (elementType, defaultAssemblyName);

			encoder.LoadArgument (argumentIndex);
			encoder.LoadConstantI4 (0); // JniHandleOwnership.DoNotTransfer
			encoder.LoadToken (elementTypeHandle);
			encoder.Call (_getTypeFromHandleRef, parameterCount: 1, returnsValue: true);
			encoder.Call (_jniEnvGetArrayRef, parameterCount: 3, returnsValue: true);
			encoder.CastClass (arrayType);
			return;
		}

		var managedTypeHandle = ResolveManagedTypeHandle (managedType, defaultAssemblyName);
		encoder.LoadArgument (argumentIndex);
		encoder.LoadConstantI4 (0); // JniHandleOwnership.DoNotTransfer
		encoder.LoadToken (managedTypeHandle);
		encoder.Call (_getTypeFromHandleRef, parameterCount: 1, returnsValue: true);
		encoder.Call (_javaLangObjectGetObjectRef, parameterCount: 3, returnsValue: true);
		encoder.CastClass (managedTypeHandle);
	}

	EntityHandle ResolveManagedTypeHandle (string managedType, string defaultAssemblyName)
	{
		if (TryGetSzArrayElementType (managedType, out var elementType)) {
			var blob = new BlobBuilder (32);
			blob.WriteByte (0x1D); // ELEMENT_TYPE_SZARRAY
			WriteManagedTypeSignature (blob, elementType, defaultAssemblyName);
			return _pe.Metadata.AddTypeSpecification (_pe.Metadata.GetOrAddBlob (blob));
		}

		return _pe.ResolveTypeRef (new TypeRefData {
			ManagedTypeName = managedType,
			AssemblyName = GetAssemblyNameForManagedType (managedType, defaultAssemblyName),
		});
	}

	static bool TryGetSzArrayElementType (string managedType, out string elementType)
	{
		if (managedType.EndsWith ("[]", StringComparison.Ordinal)) {
			elementType = managedType.Substring (0, managedType.Length - 2);
			return true;
		}

		elementType = "";
		return false;
	}

	void WriteManagedTypeSignature (BlobBuilder blob, string managedType, string defaultAssemblyName)
	{
		if (TryGetSzArrayElementType (managedType, out var elementType)) {
			blob.WriteByte (0x1D); // ELEMENT_TYPE_SZARRAY
			WriteManagedTypeSignature (blob, elementType, defaultAssemblyName);
			return;
		}

		switch (managedType) {
		case "System.Boolean": blob.WriteByte (0x02); return;
		case "System.Char":    blob.WriteByte (0x03); return;
		case "System.SByte":   blob.WriteByte (0x04); return;
		case "System.Byte":    blob.WriteByte (0x05); return;
		case "System.Int16":   blob.WriteByte (0x06); return;
		case "System.UInt16":  blob.WriteByte (0x07); return;
		case "System.Int32":   blob.WriteByte (0x08); return;
		case "System.UInt32":  blob.WriteByte (0x09); return;
		case "System.Int64":   blob.WriteByte (0x0A); return;
		case "System.UInt64":  blob.WriteByte (0x0B); return;
		case "System.Single":  blob.WriteByte (0x0C); return;
		case "System.Double":  blob.WriteByte (0x0D); return;
		case "System.String":  blob.WriteByte (0x0E); return;
		case "System.Object":  blob.WriteByte (0x1C); return;
		}

		var typeHandle = ResolveManagedTypeHandle (managedType, defaultAssemblyName);
		blob.WriteByte (0x12); // ELEMENT_TYPE_CLASS
		blob.WriteCompressedInteger (CodedIndex.TypeDefOrRefOrSpec (typeHandle));
	}

	static string GetAssemblyNameForManagedType (string managedType, string defaultAssemblyName)
	{
		if (managedType.StartsWith ("System.", StringComparison.Ordinal)) {
			return "System.Runtime";
		}
		if (managedType.StartsWith ("Android.", StringComparison.Ordinal) ||
		    managedType.StartsWith ("Java.", StringComparison.Ordinal) ||
		    managedType.StartsWith ("Javax.", StringComparison.Ordinal)) {
			return "Mono.Android";
		}
		return defaultAssemblyName;
	}

	/// <summary>
	/// LOCAL_SIG for UCO constructors without JavaInterop-style activation.
	/// Locals: 0=JniTransition, 1=JniRuntime, 2=Exception.
	/// </summary>
	void EncodeUcoConstructorLocals_Standard (BlobBuilder blob)
	{
		blob.WriteByte (0x07); // LOCAL_SIG
		blob.WriteCompressedInteger (3);
		// local 0: JniTransition (valuetype)
		blob.WriteByte (0x11); // ELEMENT_TYPE_VALUETYPE
		blob.WriteCompressedInteger (CodedIndex.TypeDefOrRefOrSpec (_jniTransitionRef));
		// local 1: JniRuntime (class)
		blob.WriteByte (0x12); // ELEMENT_TYPE_CLASS
		blob.WriteCompressedInteger (CodedIndex.TypeDefOrRefOrSpec (_jniRuntimeRef));
		// local 2: Exception (class)
		blob.WriteByte (0x12); // ELEMENT_TYPE_CLASS
		blob.WriteCompressedInteger (CodedIndex.TypeDefOrRefOrSpec (_exceptionRef));
	}

	/// <summary>
	/// LOCAL_SIG for UCO constructors with JavaInterop-style activation.
	/// Locals: 0=JniTransition, 1=JniRuntime, 2=Exception, 3=JniObjectReference.
	/// </summary>
	void EncodeUcoConstructorLocals_JavaInterop (BlobBuilder blob)
	{
		blob.WriteByte (0x07); // LOCAL_SIG
		blob.WriteCompressedInteger (4);
		// local 0: JniTransition (valuetype)
		blob.WriteByte (0x11); // ELEMENT_TYPE_VALUETYPE
		blob.WriteCompressedInteger (CodedIndex.TypeDefOrRefOrSpec (_jniTransitionRef));
		// local 1: JniRuntime (class)
		blob.WriteByte (0x12); // ELEMENT_TYPE_CLASS
		blob.WriteCompressedInteger (CodedIndex.TypeDefOrRefOrSpec (_jniRuntimeRef));
		// local 2: Exception (class)
		blob.WriteByte (0x12); // ELEMENT_TYPE_CLASS
		blob.WriteCompressedInteger (CodedIndex.TypeDefOrRefOrSpec (_exceptionRef));
		// local 3: JniObjectReference (valuetype)
		blob.WriteByte (0x11); // ELEMENT_TYPE_VALUETYPE
		blob.WriteCompressedInteger (CodedIndex.TypeDefOrRefOrSpec (_jniObjectReferenceRef));
	}

	/// <summary>
	/// LOCAL_SIG for UCO constructors that invoke a no-arg managed constructor.
	/// Locals: 0=JniTransition, 1=JniRuntime, 2=Exception, 3=JniObjectReference, 4=target type.
	/// </summary>
	void EncodeUcoConstructorLocals_DefaultConstructor (BlobBuilder blob, EntityHandle targetTypeRef)
	{
		blob.WriteByte (0x07); // LOCAL_SIG
		blob.WriteCompressedInteger (5);
		// local 0: JniTransition (valuetype)
		blob.WriteByte (0x11); // ELEMENT_TYPE_VALUETYPE
		blob.WriteCompressedInteger (CodedIndex.TypeDefOrRefOrSpec (_jniTransitionRef));
		// local 1: JniRuntime (class)
		blob.WriteByte (0x12); // ELEMENT_TYPE_CLASS
		blob.WriteCompressedInteger (CodedIndex.TypeDefOrRefOrSpec (_jniRuntimeRef));
		// local 2: Exception (class)
		blob.WriteByte (0x12); // ELEMENT_TYPE_CLASS
		blob.WriteCompressedInteger (CodedIndex.TypeDefOrRefOrSpec (_exceptionRef));
		// local 3: JniObjectReference (valuetype)
		blob.WriteByte (0x11); // ELEMENT_TYPE_VALUETYPE
		blob.WriteCompressedInteger (CodedIndex.TypeDefOrRefOrSpec (_jniObjectReferenceRef));
		// local 4: target type (class)
		blob.WriteByte (0x12); // ELEMENT_TYPE_CLASS
		blob.WriteCompressedInteger (CodedIndex.TypeDefOrRefOrSpec (targetTypeRef));
	}

	void EmitRegisterNatives (JavaPeerProxyData proxy,
		Dictionary<string, MethodDefinitionHandle> wrapperHandles)
	{
		// Filter to only registrations that have corresponding wrapper methods
		var registrations = proxy.NativeRegistrations;
		var validRegs = new List<(NativeRegistrationData Reg, MethodDefinitionHandle Wrapper)> (registrations.Count);
		foreach (var reg in registrations) {
			if (wrapperHandles.TryGetValue (reg.WrapperMethodName, out var wrapperHandle)) {
				validRegs.Add ((reg, wrapperHandle));
			}
		}

		if (validRegs.Count == 0) {
			_pe.EmitBody ("RegisterNatives",
				MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig |
				MethodAttributes.NewSlot | MethodAttributes.Final,
				sig => sig.MethodSignature (isInstanceMethod: true).Parameters (1,
					rt => rt.Void (),
					p => p.AddParameter ().Type ().Type (_jniTypeRef, false)),
				encoder => encoder.Return ());
			return;
		}

		// Get or create deduplicated RVA fields for each unique name/signature string.
		var nameFields = new FieldDefinitionHandle [validRegs.Count];
		var sigFields = new FieldDefinitionHandle [validRegs.Count];
		for (int i = 0; i < validRegs.Count; i++) {
			nameFields [i] = _pe.GetOrAddUtf8Field (validRegs [i].Reg.JniMethodName);
			sigFields [i] = _pe.GetOrAddUtf8Field (validRegs [i].Reg.JniSignature);
		}

		int methodCount = validRegs.Count;

		_pe.EmitBody ("RegisterNatives",
			MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig |
			MethodAttributes.NewSlot | MethodAttributes.Final,
			sig => sig.MethodSignature (isInstanceMethod: true).Parameters (1,
				rt => rt.Void (),
				p => p.AddParameter ().Type ().Type (_jniTypeRef, false)),
			encoder => {
				// stackalloc JniNativeMethod[N]
				encoder.LoadConstantI4 (methodCount);
				encoder.SizeOf (_jniNativeMethodRef);
				encoder.OpCode (ILOpCode.Mul);
				encoder.OpCode (ILOpCode.Localloc);
				encoder.StoreLocal (0);

				for (int i = 0; i < methodCount; i++) {
					// &methods[i] — destination address for stobj
					encoder.LoadLocal (0);
					if (i > 0) {
						encoder.LoadConstantI4 (i);
						encoder.SizeOf (_jniNativeMethodRef);
						encoder.OpCode (ILOpCode.Mul);
						encoder.OpCode (ILOpCode.Add);
					}

					// byte* name — ldsflda of deduplicated field
					encoder.LoadStaticFieldAddress (nameFields [i]);

					// byte* signature
					encoder.LoadStaticFieldAddress (sigFields [i]);

					// IntPtr functionPointer
					encoder.LoadFunction (validRegs [i].Wrapper);

					// Construct the struct on the evaluation stack and store it
					// at the destination address. This matches the Roslyn pattern:
					//   newobj JniNativeMethod::.ctor(byte*, byte*, IntPtr)
					//   stobj  JniNativeMethod
					encoder.NewObject (_jniNativeMethodCtorRef, parameterCount: 3);
					encoder.StoreObject (_jniNativeMethodRef);
				}

				// JniObjectReference peerRef = jniType.PeerReference
				// JniType is a sealed reference type, so use ldarg + callvirt
				encoder.LoadArgument (1);
				encoder.Callvirt (_jniTypePeerReferenceRef, parameterCount: 0, returnsValue: true);
				encoder.StoreLocal (1);

				// new ReadOnlySpan<JniNativeMethod>(methods, count)
				encoder.LoadLocalAddress (2);
				encoder.LoadLocal (0);
				encoder.LoadConstantI4 (methodCount);
				encoder.Call (_readOnlySpanOfJniNativeMethodCtorRef, parameterCount: 2, isInstance: true);

				// JniEnvironment.Types.RegisterNatives(peerRef, span)
				encoder.LoadLocal (1);
				encoder.LoadLocal (2);
				encoder.Call (_jniEnvTypesRegisterNativesRef, parameterCount: 2);

				encoder.Return ();
			},
			encodeLocals: localSig => {
				localSig.WriteByte (0x07); // IMAGE_CEE_CS_CALLCONV_LOCAL_SIG
				localSig.WriteCompressedInteger (3);

				// local 0: native int (stackalloc pointer)
				localSig.WriteByte (0x18); // ELEMENT_TYPE_I

				// local 1: JniObjectReference
				localSig.WriteByte (0x11); // ELEMENT_TYPE_VALUETYPE
				localSig.WriteCompressedInteger (CodedIndex.TypeDefOrRefOrSpec (_jniObjectReferenceRef));

				// local 2: ReadOnlySpan<JniNativeMethod>
				EncodeGenericValueTypeInst (localSig, _readOnlySpanOpenRef, _jniNativeMethodRef);
			});
	}

	void AddUnmanagedCallersOnlyAttribute (MethodDefinitionHandle handle)
	{
		_pe.Metadata.AddCustomAttribute (handle, _ucoAttrCtorRef, _ucoAttrBlobHandle);
	}
}
