using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

/// <summary>
/// Emits a per-assembly TypeMap PE assembly from a <see cref="TypeMapAssemblyData"/>.
/// This is a mechanical translation — all decision logic lives in <see cref="ModelBuilder"/>.
/// </summary>
/// <remarks>
/// <para>The generated assembly looks like this (pseudo-C#):</para>
/// <code>
/// // Assembly-level TypeMap attributes — one per Java peer type:
/// [assembly: TypeMap&lt;Java.Lang.Object&gt;("android/app/Activity", typeof(Activity_Proxy))]                              // unconditional (ACW)
/// [assembly: TypeMap&lt;Java.Lang.Object&gt;("android/widget/TextView", typeof(TextView_Proxy), typeof(TextView))]          // trimmable (MCW)
/// [assembly: TypeMapAssociation(typeof(MyTextView), typeof(Android_Widget_TextView_Proxy))]                              // alias
///
/// // One proxy type per Java peer that needs activation or UCO wrappers:
/// public sealed class Activity_Proxy : JavaPeerProxy, IAndroidCallableWrapper   // IAndroidCallableWrapper for ACWs only
/// {
///     public Activity_Proxy() : base() { }
///
///     // Creates the managed peer when Java calls into .NET
///     public override IJavaPeerable CreateInstance(IntPtr handle, JniHandleOwnership ownership)
///         =&gt; new Activity(handle, ownership);                        // leaf ctor
///         // or: (Activity)RuntimeHelpers.GetUninitializedObject(typeof(Activity));
///         //     obj.BaseCtor(handle, ownership);                     // inherited ctor
///         // or: new IOnClickListenerInvoker(handle, ownership);      // interface invoker
///         // or: null;                                                // no activation
///         // or: throw new NotSupportedException(...);                // open generic
///
///     public override Type TargetType =&gt; typeof(Activity);
///     public Type InvokerType =&gt; typeof(IOnClickListenerInvoker);    // interfaces only
///
///     // UCO wrappers — [UnmanagedCallersOnly] entry points for JNI native methods (ACWs only):
///     [UnmanagedCallersOnly]
///     public static void n_OnCreate_uco_0(IntPtr jnienv, IntPtr self, IntPtr p0)
///         =&gt; Activity.n_OnCreate(jnienv, self, p0);
///
///     [UnmanagedCallersOnly]
///     public static void nctor_0_uco(IntPtr jnienv, IntPtr self)
///         =&gt; TrimmableNativeRegistration.ActivateInstance(self, typeof(Activity));
///
///     // Registers JNI native methods (ACWs only):
///     public void RegisterNatives(JniType jniType)
///     {
///         TrimmableNativeRegistration.RegisterMethod(jniType, "n_OnCreate", "(Landroid/os/Bundle;)V", &amp;n_OnCreate_uco_0);
///         TrimmableNativeRegistration.RegisterMethod(jniType, "nctor_0", "()V", &amp;nctor_0_uco);
///     }
/// }
///
/// // Emitted so the proxy assembly can access internal n_* callbacks in the target assembly:
/// [assembly: IgnoresAccessChecksTo("Mono.Android")]
/// </code>
/// </remarks>
sealed class TypeMapAssemblyEmitter
{
	readonly Version _systemRuntimeVersion;

	PEAssemblyBuilder _pe = null!;

	AssemblyReferenceHandle _javaInteropRef;

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

		_pe = new PEAssemblyBuilder (_systemRuntimeVersion);
		_pe.EmitPreamble (model.AssemblyName, model.ModuleName);

		_javaInteropRef = _pe.AddAssemblyRef ("Java.Interop", new Version (0, 0, 0, 0));

		EmitTypeReferences ();
		EmitMemberReferences ();

		// Track wrapper method names → handles for RegisterNatives
		var wrapperHandles = new Dictionary<string, MethodDefinitionHandle> ();

		foreach (var proxy in model.ProxyTypes) {
			EmitProxyType (proxy, wrapperHandles);
		}

		foreach (var entry in model.Entries) {
			EmitTypeMapAttribute (entry);
		}

		foreach (var assoc in model.Associations) {
			EmitTypeMapAssociationAttribute (assoc);
		}

		_pe.EmitIgnoresAccessChecksToAttribute (model.IgnoresAccessChecksTo);
		_pe.WritePE (outputPath);
	}

	void EmitTypeReferences ()
	{
		var metadata = _pe.Metadata;
		_javaPeerProxyRef = metadata.AddTypeReference (_pe.MonoAndroidRef,
			metadata.GetOrAddString ("Java.Interop"), metadata.GetOrAddString ("JavaPeerProxy"));
		_iJavaPeerableRef = metadata.AddTypeReference (_javaInteropRef,
			metadata.GetOrAddString ("Java.Interop"), metadata.GetOrAddString ("IJavaPeerable"));
		_jniHandleOwnershipRef = metadata.AddTypeReference (_pe.MonoAndroidRef,
			metadata.GetOrAddString ("Android.Runtime"), metadata.GetOrAddString ("JniHandleOwnership"));
		_iAndroidCallableWrapperRef = metadata.AddTypeReference (_pe.MonoAndroidRef,
			metadata.GetOrAddString ("Java.Interop"), metadata.GetOrAddString ("IAndroidCallableWrapper"));
		_systemTypeRef = metadata.AddTypeReference (_pe.SystemRuntimeRef,
			metadata.GetOrAddString ("System"), metadata.GetOrAddString ("Type"));
		_runtimeTypeHandleRef = metadata.AddTypeReference (_pe.SystemRuntimeRef,
			metadata.GetOrAddString ("System"), metadata.GetOrAddString ("RuntimeTypeHandle"));
		_jniTypeRef = metadata.AddTypeReference (_javaInteropRef,
			metadata.GetOrAddString ("Java.Interop"), metadata.GetOrAddString ("JniType"));
		_trimmableNativeRegistrationRef = metadata.AddTypeReference (_pe.MonoAndroidRef,
			metadata.GetOrAddString ("Android.Runtime"), metadata.GetOrAddString ("TrimmableNativeRegistration"));
		_notSupportedExceptionRef = metadata.AddTypeReference (_pe.SystemRuntimeRef,
			metadata.GetOrAddString ("System"), metadata.GetOrAddString ("NotSupportedException"));
		_runtimeHelpersRef = metadata.AddTypeReference (_pe.SystemRuntimeRef,
			metadata.GetOrAddString ("System.Runtime.CompilerServices"), metadata.GetOrAddString ("RuntimeHelpers"));
	}

	void EmitMemberReferences ()
	{
		_baseCtorRef = _pe.AddMemberRef (_javaPeerProxyRef, ".ctor",
			sig => sig.MethodSignature (isInstanceMethod: true).Parameters (0, rt => rt.Void (), p => { }));

		_getTypeFromHandleRef = _pe.AddMemberRef (_systemTypeRef, "GetTypeFromHandle",
			sig => sig.MethodSignature ().Parameters (1,
				rt => rt.Type ().Type (_systemTypeRef, false),
				p => p.AddParameter ().Type ().Type (_runtimeTypeHandleRef, true)));

		_getUninitializedObjectRef = _pe.AddMemberRef (_runtimeHelpersRef, "GetUninitializedObject",
			sig => sig.MethodSignature ().Parameters (1,
				rt => rt.Type ().Object (),
				p => p.AddParameter ().Type ().Type (_systemTypeRef, false)));

		_notSupportedExceptionCtorRef = _pe.AddMemberRef (_notSupportedExceptionRef, ".ctor",
			sig => sig.MethodSignature (isInstanceMethod: true).Parameters (1,
				rt => rt.Void (),
				p => p.AddParameter ().Type ().String ()));

		_activateInstanceRef = _pe.AddMemberRef (_trimmableNativeRegistrationRef, "ActivateInstance",
			sig => sig.MethodSignature ().Parameters (2,
				rt => rt.Void (),
				p => {
					p.AddParameter ().Type ().IntPtr ();
					p.AddParameter ().Type ().Type (_systemTypeRef, false);
				}));

		_registerMethodRef = _pe.AddMemberRef (_trimmableNativeRegistrationRef, "RegisterMethod",
			sig => sig.MethodSignature ().Parameters (4,
				rt => rt.Void (),
				p => {
					p.AddParameter ().Type ().Type (_jniTypeRef, false);
					p.AddParameter ().Type ().String ();
					p.AddParameter ().Type ().String ();
					p.AddParameter ().Type ().IntPtr ();
				}));

		var ucoAttrTypeRef = _pe.Metadata.AddTypeReference (_pe.SystemRuntimeInteropServicesRef,
			_pe.Metadata.GetOrAddString ("System.Runtime.InteropServices"),
			_pe.Metadata.GetOrAddString ("UnmanagedCallersOnlyAttribute"));
		_ucoAttrCtorRef = _pe.AddMemberRef (ucoAttrTypeRef, ".ctor",
			sig => sig.MethodSignature (isInstanceMethod: true).Parameters (0, rt => rt.Void (), p => { }));

		// Pre-compute the UCO attribute blob — it's always the same 4 bytes (prolog + no named args)
		_ucoAttrBlobHandle = _pe.BuildAttributeBlob (b => { });

		EmitTypeMapAttributeCtorRef ();
		EmitTypeMapAssociationAttributeCtorRef ();
	}

	void EmitTypeMapAttributeCtorRef ()
	{
		var metadata = _pe.Metadata;
		var typeMapAttrOpenRef = metadata.AddTypeReference (_pe.SystemRuntimeInteropServicesRef,
			metadata.GetOrAddString ("System.Runtime.InteropServices"),
			metadata.GetOrAddString ("TypeMapAttribute`1"));
		var javaLangObjectRef = metadata.AddTypeReference (_pe.MonoAndroidRef,
			metadata.GetOrAddString ("Java.Lang"), metadata.GetOrAddString ("Object"));

		var closedAttrTypeSpec = _pe.MakeGenericTypeSpec (typeMapAttrOpenRef, javaLangObjectRef);

		// 2-arg: TypeMap(string jniName, Type proxyType) — unconditional
		_typeMapAttrCtorRef2Arg = _pe.AddMemberRef (closedAttrTypeSpec, ".ctor",
			sig => sig.MethodSignature (isInstanceMethod: true).Parameters (2,
				rt => rt.Void (),
				p => {
					p.AddParameter ().Type ().String ();
					p.AddParameter ().Type ().Type (_systemTypeRef, false);
				}));

		// 3-arg: TypeMap(string jniName, Type proxyType, Type targetType) — trimmable
		_typeMapAttrCtorRef3Arg = _pe.AddMemberRef (closedAttrTypeSpec, ".ctor",
			sig => sig.MethodSignature (isInstanceMethod: true).Parameters (3,
				rt => rt.Void (),
				p => {
					p.AddParameter ().Type ().String ();
					p.AddParameter ().Type ().Type (_systemTypeRef, false);
					p.AddParameter ().Type ().Type (_systemTypeRef, false);
				}));
	}

	void EmitTypeMapAssociationAttributeCtorRef ()
	{
		var metadata = _pe.Metadata;
		// TypeMapAssociationAttribute is in System.Runtime.InteropServices, takes 2 Type args:
		// TypeMapAssociation(Type sourceType, Type aliasProxyType)
		var typeMapAssociationAttrRef = metadata.AddTypeReference (_pe.SystemRuntimeInteropServicesRef,
			metadata.GetOrAddString ("System.Runtime.InteropServices"),
			metadata.GetOrAddString ("TypeMapAssociationAttribute"));

		_typeMapAssociationAttrCtorRef = _pe.AddMemberRef (typeMapAssociationAttrRef, ".ctor",
			sig => sig.MethodSignature (isInstanceMethod: true).Parameters (2,
				rt => rt.Void (),
				p => {
					p.AddParameter ().Type ().Type (_systemTypeRef, false);
					p.AddParameter ().Type ().Type (_systemTypeRef, false);
				}));
	}


	void EmitProxyType (JavaPeerProxyData proxy, Dictionary<string, MethodDefinitionHandle> wrapperHandles)
	{
		var metadata = _pe.Metadata;
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
		_pe.EmitBody (".ctor",
			MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
			sig => sig.MethodSignature (isInstanceMethod: true).Parameters (0, rt => rt.Void (), p => { }),
			encoder => {
				encoder.OpCode (ILOpCode.Ldarg_0);
				encoder.Call (_baseCtorRef);
				encoder.OpCode (ILOpCode.Ret);
			});

		// CreateInstance
		EmitCreateInstance (proxy);

		// get_TargetType
		EmitTypeGetter ("get_TargetType", proxy.TargetType,
			MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.SpecialName | MethodAttributes.HideBySig);

		// get_InvokerType
		if (proxy.InvokerType != null) {
			EmitTypeGetter ("get_InvokerType", proxy.InvokerType,
				MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig);
		}

		// UCO wrappers
		foreach (var uco in proxy.UcoMethods) {
			var handle = EmitUcoMethod (uco);
			wrapperHandles [uco.WrapperName] = handle;
		}

		foreach (var uco in proxy.UcoConstructors) {
			var handle = EmitUcoConstructor (uco);
			wrapperHandles [uco.WrapperName] = handle;
		}

		// RegisterNatives
		if (proxy.IsAcw) {
			EmitRegisterNatives (proxy.NativeRegistrations, wrapperHandles);
		}
	}

	void EmitCreateInstance (JavaPeerProxyData proxy)
	{
		if (!proxy.HasActivation) {
			EmitCreateInstanceBody (encoder => {
				encoder.OpCode (ILOpCode.Ldnull);
				encoder.OpCode (ILOpCode.Ret);
			});
			return;
		}

		// Generic type definitions cannot be instantiated
		if (proxy.IsGenericDefinition) {
			EmitCreateInstanceBody (encoder => {
				encoder.LoadString (_pe.Metadata.GetOrAddUserString ("Cannot create instance of open generic type."));
				encoder.OpCode (ILOpCode.Newobj);
				encoder.Token (_notSupportedExceptionCtorRef);
				encoder.OpCode (ILOpCode.Throw);
			});
			return;
		}

		// Interface with invoker: new TInvoker(IntPtr, JniHandleOwnership)
		if (proxy.InvokerType != null) {
			var invokerCtorRef = AddActivationCtorRef (_pe.ResolveTypeRef (proxy.InvokerType));
			EmitCreateInstanceBody (encoder => {
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
		var targetTypeRef = _pe.ResolveTypeRef (proxy.TargetType);

		if (activationCtor.IsOnLeafType) {
			// Leaf type has its own ctor: new T(IntPtr, JniHandleOwnership)
			var ctorRef = AddActivationCtorRef (targetTypeRef);
			EmitCreateInstanceBody (encoder => {
				encoder.OpCode (ILOpCode.Ldarg_1);
				encoder.OpCode (ILOpCode.Ldarg_2);
				encoder.OpCode (ILOpCode.Newobj);
				encoder.Token (ctorRef);
				encoder.OpCode (ILOpCode.Ret);
			});
		} else {
			// Inherited ctor: GetUninitializedObject(typeof(T)) + call Base::.ctor(IntPtr, JniHandleOwnership)
			var baseActivationCtorRef = AddActivationCtorRef (_pe.ResolveTypeRef (activationCtor.DeclaringType));
			EmitCreateInstanceBody (encoder => {
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

	void EmitCreateInstanceBody (Action<InstructionEncoder> emitIL)
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

	void EmitTypeGetter (string methodName, TypeRefData typeRef, MethodAttributes attrs)
	{
		var handle = _pe.ResolveTypeRef (typeRef);

		_pe.EmitBody (methodName, attrs,
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

	MethodDefinitionHandle EmitUcoMethod (UcoMethodData uco)
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

		var callbackTypeHandle = _pe.ResolveTypeRef (uco.CallbackType);
		var callbackRef = _pe.AddMemberRef (callbackTypeHandle, uco.CallbackMethodName, encodeSig);

		var handle = _pe.EmitBody (uco.WrapperName,
			MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig,
			encodeSig,
			encoder => {
				for (int p = 0; p < paramCount; p++)
					encoder.LoadArgument (p);
				encoder.Call (callbackRef);
				encoder.OpCode (ILOpCode.Ret);
			});

		AddUnmanagedCallersOnlyAttribute (handle);
		return handle;
	}

	MethodDefinitionHandle EmitUcoConstructor (UcoConstructorData uco)
	{
		var userTypeRef = _pe.ResolveTypeRef (uco.TargetType);

		// UCO constructor wrappers must match the JNI native method signature exactly.
		// The Java JCW declares e.g. "private native void nctor_0(Context p0)" and calls
		// it with arguments. JNI dispatches with (JNIEnv*, jobject, <ctor params...>),
		// so the wrapper signature must include all parameters to match the ABI.
		// Only jnienv (arg 0) and self (arg 1) are used — the constructor parameters
		// are not forwarded because ActivateInstance creates the managed peer using the
		// activation ctor (IntPtr, JniHandleOwnership), not the user-visible constructor.
		var jniParams = JniSignatureHelper.ParseParameterTypes (uco.JniSignature);
		int paramCount = 2 + jniParams.Count;

		var handle = _pe.EmitBody (uco.WrapperName,
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

		AddUnmanagedCallersOnlyAttribute (handle);
		return handle;
	}

	void EmitRegisterNatives (List<NativeRegistrationData> registrations,
		Dictionary<string, MethodDefinitionHandle> wrapperHandles)
	{
		_pe.EmitBody ("RegisterNatives",
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
					encoder.LoadString (_pe.Metadata.GetOrAddUserString (reg.JniMethodName));
					encoder.LoadString (_pe.Metadata.GetOrAddUserString (reg.JniSignature));
					encoder.OpCode (ILOpCode.Ldftn);
					encoder.Token (wrapperHandle);
					encoder.Call (_registerMethodRef);
				}
				encoder.OpCode (ILOpCode.Ret);
			});
	}

	void AddUnmanagedCallersOnlyAttribute (MethodDefinitionHandle handle)
	{
		_pe.Metadata.AddCustomAttribute (handle, _ucoAttrCtorRef, _ucoAttrBlobHandle);
	}

	void EmitTypeMapAttribute (TypeMapAttributeData entry)
	{
		var ctorRef = entry.IsUnconditional ? _typeMapAttrCtorRef2Arg : _typeMapAttrCtorRef3Arg;
		var blob = _pe.BuildAttributeBlob (b => {
			b.WriteSerializedString (entry.JniName);
			b.WriteSerializedString (entry.ProxyTypeReference);
			if (!entry.IsUnconditional) {
				b.WriteSerializedString (entry.TargetTypeReference!);
			}
		});
		_pe.Metadata.AddCustomAttribute (EntityHandle.AssemblyDefinition, ctorRef, blob);
	}

	void EmitTypeMapAssociationAttribute (TypeMapAssociationData assoc)
	{
		var blob = _pe.BuildAttributeBlob (b => {
			b.WriteSerializedString (assoc.SourceTypeReference);
			b.WriteSerializedString (assoc.AliasProxyTypeReference);
		});
		_pe.Metadata.AddCustomAttribute (EntityHandle.AssemblyDefinition, _typeMapAssociationAttrCtorRef, blob);
	}

}
