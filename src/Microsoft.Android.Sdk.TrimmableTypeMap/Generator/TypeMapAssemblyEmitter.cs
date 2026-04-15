using System;
using System.Collections.Generic;
using System.IO;
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
/// [assembly: TypeMapAssociation&lt;Java.Lang.Object&gt;(typeof(MyTextView), typeof(Android_Widget_TextView_Proxy))]          // managed → proxy
///
/// // One proxy type per Java peer that needs activation or UCO wrappers:
/// public sealed class Activity_Proxy : JavaPeerProxy&lt;Activity&gt;, IAndroidCallableWrapper   // IAndroidCallableWrapper for ACWs only
/// {
///     public Activity_Proxy() : base("android/app/Activity", null) { }
///
///     // Creates the managed peer when Java calls into .NET
///     public override IJavaPeerable CreateInstance(IntPtr handle, JniHandleOwnership ownership)
///         =&gt; new Activity(handle, ownership);                        // leaf ctor
///         // or: (Activity)CreateActivatedPeer(typeof(Activity), handle);
///         //     obj.BaseCtor(handle, ownership);                     // inherited ctor
///         // or: new IOnClickListenerInvoker(handle, ownership);      // interface invoker
///         // or: null;                                                // no activation
///         // or: throw new NotSupportedException(...);                // open generic
///
///     // JniName / TargetType / InvokerType are supplied by the base JavaPeerProxy constructor.
///
///     // UCO wrappers — [UnmanagedCallersOnly] entry points for JNI native methods (ACWs only):
///     [UnmanagedCallersOnly]
///     public static void n_OnCreate_uco_0(IntPtr jnienv, IntPtr self, IntPtr p0)
///         =&gt; Activity.n_OnCreate(jnienv, self, p0);
///
///     [UnmanagedCallersOnly]
///     public static void nctor_0_uco(IntPtr jnienv, IntPtr self)
///         =&gt; new Activity(self, JniHandleOwnership.DoNotTransfer);
///         // or: var obj = (Activity)CreateActivatedPeer(typeof(Activity), self);
///         //     obj.BaseCtor(self, JniHandleOwnership.DoNotTransfer);
///
///     // Registers JNI native methods (ACWs only):
///     public void RegisterNatives(JniType jniType)
///     {
///         JniNativeMethod* methods = stackalloc JniNativeMethod[2];
///         methods[0] = new JniNativeMethod(&amp;__utf8_0, &amp;__utf8_1, &amp;n_OnCreate_uco_0);
///         methods[1] = new JniNativeMethod(&amp;__utf8_2, &amp;__utf8_3, &amp;nctor_0_uco);
///         JniEnvironment.Types.RegisterNatives(jniType.PeerReference, new ReadOnlySpan&lt;JniNativeMethod&gt;(methods, 2));
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

	readonly PEAssemblyBuilder _pe;

	AssemblyReferenceHandle _javaInteropRef;

	TypeReferenceHandle _javaPeerProxyBaseRef;
	TypeReferenceHandle _javaPeerProxyRef;
	TypeReferenceHandle _iJavaPeerableRef;
	TypeReferenceHandle _jniHandleOwnershipRef;
	TypeReferenceHandle _jniObjectReferenceRef;
	TypeReferenceHandle _jniObjectReferenceOptionsRef;
	TypeReferenceHandle _iAndroidCallableWrapperRef;
	TypeReferenceHandle _jniEnvRef;
	TypeReferenceHandle _systemTypeRef;
	TypeReferenceHandle _runtimeTypeHandleRef;
	TypeReferenceHandle _jniTypeRef;
	TypeReferenceHandle _notSupportedExceptionRef;

	MemberReferenceHandle _getTypeFromHandleRef;
	MemberReferenceHandle _createActivatedPeerRef;
	MemberReferenceHandle _notSupportedExceptionCtorRef;
	MemberReferenceHandle _jniObjectReferenceCtorRef;
	MemberReferenceHandle _jniEnvDeleteRefRef;
	MemberReferenceHandle _withinNewObjectScopeRef;
	MemberReferenceHandle _ucoAttrCtorRef;
	BlobHandle _ucoAttrBlobHandle;
	MemberReferenceHandle _typeMapAttrCtorRef2Arg;
	MemberReferenceHandle _typeMapAttrCtorRef3Arg;
	MemberReferenceHandle _typeMapAssociationAttrCtorRef;

	// RegisterNatives with JniNativeMethod
	TypeReferenceHandle _jniNativeMethodRef;
	TypeReferenceHandle _jniEnvironmentRef;
	TypeReferenceHandle _jniEnvironmentTypesRef;
	TypeReferenceHandle _readOnlySpanOpenRef;
	TypeSpecificationHandle _readOnlySpanOfJniNativeMethodSpec;
	MemberReferenceHandle _jniNativeMethodCtorRef;
	MemberReferenceHandle _jniTypePeerReferenceRef;
	MemberReferenceHandle _jniEnvTypesRegisterNativesRef;
	MemberReferenceHandle _readOnlySpanOfJniNativeMethodCtorRef;

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
		_pe = new PEAssemblyBuilder (_systemRuntimeVersion);
	}

	/// <summary>
	/// Emits a PE assembly from the given model and writes it to <paramref name="stream"/>.
	/// </summary>
	public void Emit (TypeMapAssemblyData model, Stream stream)
	{
		if (model is null) {
			throw new ArgumentNullException (nameof (model));
		}
		if (stream is null) {
			throw new ArgumentNullException (nameof (stream));
		}

		EmitCore (model);
		_pe.WritePE (stream);
	}

	void EmitCore (TypeMapAssemblyData model)
	{
		_pe.EmitPreamble (model.AssemblyName, model.ModuleName, MetadataHelper.ComputeContentFingerprint (model));

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
	}

	void EmitTypeReferences ()
	{
		var metadata = _pe.Metadata;
		_javaPeerProxyBaseRef = metadata.AddTypeReference (_pe.MonoAndroidRef,
			metadata.GetOrAddString ("Java.Interop"), metadata.GetOrAddString ("JavaPeerProxy"));
		_javaPeerProxyRef = metadata.AddTypeReference (_pe.MonoAndroidRef,
			metadata.GetOrAddString ("Java.Interop"), metadata.GetOrAddString ("JavaPeerProxy`1"));
		_iJavaPeerableRef = metadata.AddTypeReference (_javaInteropRef,
			metadata.GetOrAddString ("Java.Interop"), metadata.GetOrAddString ("IJavaPeerable"));
		_jniHandleOwnershipRef = metadata.AddTypeReference (_pe.MonoAndroidRef,
			metadata.GetOrAddString ("Android.Runtime"), metadata.GetOrAddString ("JniHandleOwnership"));
		_jniEnvRef = metadata.AddTypeReference (_pe.MonoAndroidRef,
			metadata.GetOrAddString ("Android.Runtime"), metadata.GetOrAddString ("JNIEnv"));
		_jniObjectReferenceRef = metadata.AddTypeReference (_javaInteropRef,
			metadata.GetOrAddString ("Java.Interop"), metadata.GetOrAddString ("JniObjectReference"));
		_jniObjectReferenceOptionsRef = metadata.AddTypeReference (_javaInteropRef,
			metadata.GetOrAddString ("Java.Interop"), metadata.GetOrAddString ("JniObjectReferenceOptions"));
		_iAndroidCallableWrapperRef = metadata.AddTypeReference (_pe.MonoAndroidRef,
			metadata.GetOrAddString ("Java.Interop"), metadata.GetOrAddString ("IAndroidCallableWrapper"));
		_systemTypeRef = metadata.AddTypeReference (_pe.SystemRuntimeRef,
			metadata.GetOrAddString ("System"), metadata.GetOrAddString ("Type"));
		_runtimeTypeHandleRef = metadata.AddTypeReference (_pe.SystemRuntimeRef,
			metadata.GetOrAddString ("System"), metadata.GetOrAddString ("RuntimeTypeHandle"));
		_jniTypeRef = metadata.AddTypeReference (_javaInteropRef,
			metadata.GetOrAddString ("Java.Interop"), metadata.GetOrAddString ("JniType"));
		_notSupportedExceptionRef = metadata.AddTypeReference (_pe.SystemRuntimeRef,
			metadata.GetOrAddString ("System"), metadata.GetOrAddString ("NotSupportedException"));

		_jniNativeMethodRef = metadata.AddTypeReference (_javaInteropRef,
			metadata.GetOrAddString ("Java.Interop"), metadata.GetOrAddString ("JniNativeMethod"));
		_jniEnvironmentRef = metadata.AddTypeReference (_javaInteropRef,
			metadata.GetOrAddString ("Java.Interop"), metadata.GetOrAddString ("JniEnvironment"));
		_jniEnvironmentTypesRef = metadata.AddTypeReference (_jniEnvironmentRef,
			default, metadata.GetOrAddString ("Types"));

		// ReadOnlySpan<JniNativeMethod> — TypeSpec for generic instantiation
		_readOnlySpanOpenRef = metadata.AddTypeReference (_pe.SystemRuntimeRef,
			metadata.GetOrAddString ("System"), metadata.GetOrAddString ("ReadOnlySpan`1"));
		_readOnlySpanOfJniNativeMethodSpec = MakeGenericTypeSpec_ValueType (_readOnlySpanOpenRef, _jniNativeMethodRef);
	}

	void EmitMemberReferences ()
	{
		_getTypeFromHandleRef = _pe.AddMemberRef (_systemTypeRef, "GetTypeFromHandle",
			sig => sig.MethodSignature ().Parameters (1,
				rt => rt.Type ().Type (_systemTypeRef, false),
				p => p.AddParameter ().Type ().Type (_runtimeTypeHandleRef, true)));

		_createActivatedPeerRef = _pe.AddMemberRef (_javaPeerProxyBaseRef, "CreateActivatedPeer",
			sig => sig.MethodSignature ().Parameters (2,
				rt => rt.Type ().Type (_iJavaPeerableRef, false),
				p => {
					p.AddParameter ().Type ().Type (_systemTypeRef, false);
					p.AddParameter ().Type ().IntPtr ();
				}));

		_notSupportedExceptionCtorRef = _pe.AddMemberRef (_notSupportedExceptionRef, ".ctor",
			sig => sig.MethodSignature (isInstanceMethod: true).Parameters (1,
				rt => rt.Void (),
				p => p.AddParameter ().Type ().String ()));

		_jniObjectReferenceCtorRef = _pe.AddMemberRef (_jniObjectReferenceRef, ".ctor",
			sig => sig.MethodSignature (isInstanceMethod: true).Parameters (1,
				rt => rt.Void (),
				p => p.AddParameter ().Type ().IntPtr ()));

		// JNIEnv.DeleteRef(IntPtr, JniHandleOwnership) — static, internal
		// Used by JI-style activation to clean up the original handle after constructing the peer.
		// Matches the legacy TypeManager.CreateProxy behavior.
		_jniEnvDeleteRefRef = _pe.AddMemberRef (_jniEnvRef, "DeleteRef",
			sig => sig.MethodSignature ().Parameters (2,
				rt => rt.Void (),
				p => {
					p.AddParameter ().Type ().IntPtr ();
					p.AddParameter ().Type ().Type (_jniHandleOwnershipRef, true);
				}));

		// JniEnvironment.get_WithinNewObjectScope() -> bool (static property)
		_withinNewObjectScopeRef = _pe.AddMemberRef (_jniEnvironmentRef, "get_WithinNewObjectScope",
			sig => sig.MethodSignature ().Parameters (0,
				rt => rt.Type ().Boolean (),
				p => { }));

		// JniNativeMethod..ctor(byte*, byte*, IntPtr)
		_jniNativeMethodCtorRef = _pe.AddMemberRef (_jniNativeMethodRef, ".ctor",
			sig => sig.MethodSignature (isInstanceMethod: true).Parameters (3,
				rt => rt.Void (),
				p => {
					p.AddParameter ().Type ().Pointer ().Byte ();
					p.AddParameter ().Type ().Pointer ().Byte ();
					p.AddParameter ().Type ().IntPtr ();
				}));

		// JniType.get_PeerReference() -> JniObjectReference
		_jniTypePeerReferenceRef = _pe.AddMemberRef (_jniTypeRef, "get_PeerReference",
			sig => sig.MethodSignature (isInstanceMethod: true).Parameters (0,
				rt => rt.Type ().Type (_jniObjectReferenceRef, true),
				p => { }));

		// JniEnvironment.Types.RegisterNatives(JniObjectReference, ReadOnlySpan<JniNativeMethod>)
		_jniEnvTypesRegisterNativesRef = _pe.AddMemberRef (_jniEnvironmentTypesRef, "RegisterNatives",
			sig => sig.MethodSignature ().Parameters (2,
				rt => rt.Void (),
				p => {
					p.AddParameter ().Type ().Type (_jniObjectReferenceRef, true);
					// ReadOnlySpan<JniNativeMethod> — must encode as GENERICINST manually
					EncodeReadOnlySpanOfJniNativeMethod (p.AddParameter ().Type ());
				}));

		// ReadOnlySpan<JniNativeMethod>..ctor(void*, int)
		_readOnlySpanOfJniNativeMethodCtorRef = _pe.AddMemberRef (_readOnlySpanOfJniNativeMethodSpec, ".ctor",
			sig => sig.MethodSignature (isInstanceMethod: true).Parameters (2,
				rt => rt.Void (),
				p => {
					p.AddParameter ().Type ().VoidPointer ();
					p.AddParameter ().Type ().Int32 ();
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
		var typeMapAssociationAttrOpenRef = metadata.AddTypeReference (_pe.SystemRuntimeInteropServicesRef,
			metadata.GetOrAddString ("System.Runtime.InteropServices"),
			metadata.GetOrAddString ("TypeMapAssociationAttribute`1"));
		var javaLangObjectRef = metadata.AddTypeReference (_pe.MonoAndroidRef,
			metadata.GetOrAddString ("Java.Lang"), metadata.GetOrAddString ("Object"));
		var closedAttrTypeSpec = _pe.MakeGenericTypeSpec (typeMapAssociationAttrOpenRef, javaLangObjectRef);

		_typeMapAssociationAttrCtorRef = _pe.AddMemberRef (closedAttrTypeSpec, ".ctor",
			sig => sig.MethodSignature (isInstanceMethod: true).Parameters (2,
				rt => rt.Void (),
				p => {
					p.AddParameter ().Type ().Type (_systemTypeRef, false);
					p.AddParameter ().Type ().Type (_systemTypeRef, false);
				}));
	}

	void EmitProxyType (JavaPeerProxyData proxy, Dictionary<string, MethodDefinitionHandle> wrapperHandles)
	{
		if (proxy.IsAcw) {
			// RegisterNatives uses RVA-backed UTF-8 fields under <PrivateImplementationDetails>.
			// Materialize those helper types before adding the proxy TypeDef, otherwise the
			// later RegisterNatives method can be attached to the helper type instead.
			foreach (var reg in proxy.NativeRegistrations) {
				_pe.GetOrAddUtf8Field (reg.JniMethodName);
				_pe.GetOrAddUtf8Field (reg.JniSignature);
			}
		}

		var metadata = _pe.Metadata;
		var targetTypeRef = _pe.ResolveTypeRef (proxy.TargetType);
		var proxyBaseType = _pe.MakeGenericTypeSpec (_javaPeerProxyRef, targetTypeRef);
		var baseCtorRef = _pe.AddMemberRef (proxyBaseType, ".ctor",
			sig => sig.MethodSignature (isInstanceMethod: true).Parameters (2,
				rt => rt.Void (),
				p => {
					p.AddParameter ().Type ().String ();
					p.AddParameter ().Type ().Type (_systemTypeRef, false);
				}));

		var typeDefHandle = metadata.AddTypeDefinition (
			TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Class,
			metadata.GetOrAddString (proxy.Namespace),
			metadata.GetOrAddString (proxy.TypeName),
			proxyBaseType,
			MetadataTokens.FieldDefinitionHandle (metadata.GetRowCount (TableIndex.Field) + 1),
			MetadataTokens.MethodDefinitionHandle (metadata.GetRowCount (TableIndex.MethodDef) + 1));

		if (proxy.IsAcw) {
			metadata.AddInterfaceImplementation (typeDefHandle, _iAndroidCallableWrapperRef);
		}

		// .ctor — pass the resolved JNI name and optional invoker type to the generic base proxy
		_pe.EmitBody (".ctor",
			MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
			sig => sig.MethodSignature (isInstanceMethod: true).Parameters (0, rt => rt.Void (), p => { }),
			encoder => {
				encoder.OpCode (ILOpCode.Ldarg_0);
				encoder.LoadString (metadata.GetOrAddUserString (proxy.JniName));
				if (proxy.InvokerType != null) {
					encoder.OpCode (ILOpCode.Ldtoken);
					encoder.Token (_pe.ResolveTypeRef (proxy.InvokerType));
					encoder.Call (_getTypeFromHandleRef);
				} else {
					encoder.OpCode (ILOpCode.Ldnull);
				}
				encoder.Call (baseCtorRef);
				encoder.OpCode (ILOpCode.Ret);
			});

		// CreateInstance
		EmitCreateInstance (proxy);

		// UCO wrappers
		foreach (var uco in proxy.UcoMethods) {
			var handle = EmitUcoMethod (uco);
			wrapperHandles [uco.WrapperName] = handle;
		}

		foreach (var uco in proxy.UcoConstructors) {
			var handle = EmitUcoConstructor (uco, proxy);
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
			EmitCreateInstanceNoActivation ();
			return;
		}

		if (proxy.IsGenericDefinition) {
			EmitCreateInstanceGenericDefinition ();
			return;
		}

		// JavaInterop-style activation ctors (ref JniObjectReference, JniObjectReferenceOptions)
		// require parameter conversion from (IntPtr, JniHandleOwnership).
		if (proxy.ActivationCtor?.Style == ActivationCtorStyle.JavaInterop) {
			if (proxy.InvokerType != null) {
				EmitCreateInstanceViaJavaInteropNewobj (_pe.ResolveTypeRef (proxy.InvokerType));
			} else {
				var targetRef = _pe.ResolveTypeRef (proxy.TargetType);
				var jiCtor = proxy.ActivationCtor ?? throw new InvalidOperationException ("ActivationCtor should not be null");
				if (jiCtor.IsOnLeafType) {
					EmitCreateInstanceViaJavaInteropNewobj (targetRef);
				} else {
					EmitCreateInstanceInheritedJavaInteropCtor (targetRef, jiCtor);
				}
			}
			return;
		}

		if (proxy.InvokerType != null) {
			EmitCreateInstanceViaNewobj (_pe.ResolveTypeRef (proxy.InvokerType));
			return;
		}

		// At this point, ActivationCtor is guaranteed non-null (HasActivation && InvokerType == null)
		var activationCtor = proxy.ActivationCtor ?? throw new InvalidOperationException ("ActivationCtor should not be null when HasActivation is true and InvokerType is null");
		var targetTypeRef = _pe.ResolveTypeRef (proxy.TargetType);

		if (activationCtor.IsOnLeafType) {
			EmitCreateInstanceViaNewobj (targetTypeRef);
		} else {
			EmitCreateInstanceInheritedCtor (targetTypeRef, activationCtor);
		}
	}

	void EmitCreateInstanceNoActivation ()
	{
		EmitCreateInstanceBody (encoder => {
			encoder.OpCode (ILOpCode.Ldnull);
			encoder.OpCode (ILOpCode.Ret);
		});
	}

	void EmitCreateInstanceGenericDefinition ()
	{
		EmitCreateInstanceBody (encoder => {
			encoder.LoadString (_pe.Metadata.GetOrAddUserString ("Cannot create instance of open generic type."));
			encoder.OpCode (ILOpCode.Newobj);
			encoder.Token (_notSupportedExceptionCtorRef);
			encoder.OpCode (ILOpCode.Throw);
		});
	}

	void EmitCreateInstanceViaNewobj (EntityHandle typeRef)
	{
		var ctorRef = AddActivationCtorRef (typeRef);
		EmitCreateInstanceBody (encoder => {
			encoder.OpCode (ILOpCode.Ldarg_1);
			encoder.OpCode (ILOpCode.Ldarg_2);
			encoder.OpCode (ILOpCode.Newobj);
			encoder.Token (ctorRef);
			encoder.OpCode (ILOpCode.Ret);
		});
	}

	void EmitCreateInstanceInheritedCtor (EntityHandle targetTypeRef, ActivationCtorData activationCtor)
	{
		var baseActivationCtorRef = AddActivationCtorRef (_pe.ResolveTypeRef (activationCtor.DeclaringType));
		EmitCreateInstanceBody (encoder => {
			encoder.OpCode (ILOpCode.Ldtoken);
			encoder.Token (targetTypeRef);
			encoder.Call (_getTypeFromHandleRef);
			encoder.OpCode (ILOpCode.Ldarg_1);
			encoder.Call (_createActivatedPeerRef);
			encoder.OpCode (ILOpCode.Castclass);
			encoder.Token (targetTypeRef);

			encoder.OpCode (ILOpCode.Dup);
			encoder.OpCode (ILOpCode.Ldarg_1);
			encoder.OpCode (ILOpCode.Ldarg_2);
			encoder.Call (baseActivationCtorRef);

			encoder.OpCode (ILOpCode.Ret);
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
				// var jniRef = new JniObjectReference(handle);
				encoder.LoadLocalAddress (0);
				encoder.OpCode (ILOpCode.Ldarg_1); // handle
				encoder.Call (_jniObjectReferenceCtorRef);

				// var result = new TargetType(ref jniRef, JniObjectReferenceOptions.Copy);
				encoder.LoadLocalAddress (0);
				encoder.LoadConstantI4 (1); // JniObjectReferenceOptions.Copy
				encoder.OpCode (ILOpCode.Newobj);
				encoder.Token (ctorRef);
				encoder.StoreLocal (1); // save result

				// JNIEnv.DeleteRef(handle, ownership);
				encoder.OpCode (ILOpCode.Ldarg_1); // handle
				encoder.OpCode (ILOpCode.Ldarg_2); // ownership
				encoder.Call (_jniEnvDeleteRefRef);

				encoder.LoadLocal (1); // load result
				encoder.OpCode (ILOpCode.Ret);
			});
	}

	/// <summary>
	/// Emits CreateInstance for JavaInterop-style activation (inherited ctor):
	///   var obj = (TargetType)CreateActivatedPeer(typeof(TargetType), handle);
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
				// var obj = (TargetType)CreateActivatedPeer(typeof(TargetType), handle);
				encoder.OpCode (ILOpCode.Ldtoken);
				encoder.Token (targetTypeRef);
				encoder.Call (_getTypeFromHandleRef);
				encoder.OpCode (ILOpCode.Ldarg_1); // handle
				encoder.Call (_createActivatedPeerRef);
				encoder.OpCode (ILOpCode.Castclass);
				encoder.Token (targetTypeRef);

				// dup obj (one copy for the call, one for the return)
				encoder.OpCode (ILOpCode.Dup);

				// var jniRef = new JniObjectReference(handle);
				encoder.LoadLocalAddress (0);
				encoder.OpCode (ILOpCode.Ldarg_1); // handle
				encoder.Call (_jniObjectReferenceCtorRef);

				// obj.BaseCtor(ref jniRef, JniObjectReferenceOptions.Copy);
				encoder.LoadLocalAddress (0);
				encoder.LoadConstantI4 (1); // JniObjectReferenceOptions.Copy
				encoder.Call (baseCtorRef);

				// JNIEnv.DeleteRef(handle, ownership);
				encoder.OpCode (ILOpCode.Ldarg_1); // handle
				encoder.OpCode (ILOpCode.Ldarg_2); // ownership
				encoder.Call (_jniEnvDeleteRefRef);

				encoder.OpCode (ILOpCode.Ret);
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

	void EmitCreateInstanceBodyWithLocals (Action<BlobBuilder> encodeLocals, Action<InstructionEncoder> emitIL)
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

	MethodDefinitionHandle EmitUcoConstructor (UcoConstructorData uco, JavaPeerProxyData proxy)
	{
		var targetTypeRef = _pe.ResolveTypeRef (uco.TargetType);
		var activationCtor = proxy.ActivationCtor ?? throw new InvalidOperationException (
			$"UCO constructor wrapper requires an activation ctor for '{uco.TargetType.ManagedTypeName}'");

		// UCO constructor wrappers must match the JNI native method signature exactly.
		// Only jnienv (arg 0) and self (arg 1) are used — the constructor parameters
		// are not forwarded because we create the managed peer using the
		// activation ctor (IntPtr, JniHandleOwnership), not the user-visible constructor.
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

		// Open generic types can't be activated — emit a no-op UCO.
		if (proxy.IsGenericDefinition) {
			var noopHandle = _pe.EmitBody (uco.WrapperName,
				MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig,
				encodeSig,
				encoder => {
					encoder.OpCode (ILOpCode.Ret);
				});
			AddUnmanagedCallersOnlyAttribute (noopHandle);
			return noopHandle;
		}

		MethodDefinitionHandle handle;
		if (activationCtor.Style == ActivationCtorStyle.JavaInterop) {
			var ctorRef = AddJavaInteropActivationCtorRef (
				activationCtor.IsOnLeafType ? targetTypeRef : _pe.ResolveTypeRef (activationCtor.DeclaringType));

			handle = _pe.EmitBody (uco.WrapperName,
				MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig,
				encodeSig,
				encoder => {
					// Skip activation if the object is being created from managed code
					// (e.g., JNIEnv.StartCreateInstance / JNIEnv.NewObject).
					var skipLabel = encoder.DefineLabel ();
					encoder.Call (_withinNewObjectScopeRef);
					encoder.Branch (ILOpCode.Brtrue, skipLabel);

					if (!activationCtor.IsOnLeafType) {
						encoder.OpCode (ILOpCode.Ldtoken);
						encoder.Token (targetTypeRef);
						encoder.Call (_getTypeFromHandleRef);
						encoder.LoadArgument (1); // self
						encoder.Call (_createActivatedPeerRef);
						encoder.OpCode (ILOpCode.Castclass);
						encoder.Token (targetTypeRef);
					}

					encoder.LoadLocalAddress (0);
					encoder.LoadArgument (1); // self
					encoder.Call (_jniObjectReferenceCtorRef);

					if (activationCtor.IsOnLeafType) {
						encoder.LoadLocalAddress (0);
						encoder.LoadConstantI4 (1); // JniObjectReferenceOptions.Copy
						encoder.OpCode (ILOpCode.Newobj);
						encoder.Token (ctorRef);
						encoder.OpCode (ILOpCode.Pop);
					} else {
						encoder.LoadLocalAddress (0);
						encoder.LoadConstantI4 (1); // JniObjectReferenceOptions.Copy
						encoder.Call (ctorRef);
					}

					encoder.MarkLabel (skipLabel);
					encoder.OpCode (ILOpCode.Ret);
				},
				EncodeJniObjectReferenceLocal,
				useBranches: true);
		} else {
			var ctorRef = AddActivationCtorRef (
				activationCtor.IsOnLeafType ? targetTypeRef : _pe.ResolveTypeRef (activationCtor.DeclaringType));

			handle = _pe.EmitBody (uco.WrapperName,
				MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig,
				encodeSig,
				encoder => {
					// Skip activation if the object is being created from managed code
					var skipLabel = encoder.DefineLabel ();
					encoder.Call (_withinNewObjectScopeRef);
					encoder.Branch (ILOpCode.Brtrue, skipLabel);

					if (activationCtor.IsOnLeafType) {
						encoder.LoadArgument (1); // self
						encoder.LoadConstantI4 (0); // JniHandleOwnership.DoNotTransfer
						encoder.OpCode (ILOpCode.Newobj);
						encoder.Token (ctorRef);
						encoder.OpCode (ILOpCode.Pop);
					} else {
						encoder.OpCode (ILOpCode.Ldtoken);
						encoder.Token (targetTypeRef);
						encoder.Call (_getTypeFromHandleRef);
						encoder.LoadArgument (1); // self
						encoder.Call (_createActivatedPeerRef);
						encoder.OpCode (ILOpCode.Castclass);
						encoder.Token (targetTypeRef);

						encoder.LoadArgument (1); // self
						encoder.LoadConstantI4 (0); // JniHandleOwnership.DoNotTransfer
						encoder.Call (ctorRef);
					}

					encoder.MarkLabel (skipLabel);
					encoder.OpCode (ILOpCode.Ret);
				},
				encodeLocals: null,
				useBranches: true);
		}
		AddUnmanagedCallersOnlyAttribute (handle);
		return handle;
	}

	void EmitRegisterNatives (List<NativeRegistrationData> registrations,
		Dictionary<string, MethodDefinitionHandle> wrapperHandles)
	{
		// Filter to only registrations that have corresponding wrapper methods
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
				encoder => encoder.OpCode (ILOpCode.Ret));
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
				encoder.OpCode (ILOpCode.Sizeof);
				encoder.Token (_jniNativeMethodRef);
				encoder.OpCode (ILOpCode.Mul);
				encoder.OpCode (ILOpCode.Localloc);
				encoder.StoreLocal (0);

				for (int i = 0; i < methodCount; i++) {
					// &methods[i] — destination address for stobj
					encoder.LoadLocal (0);
					if (i > 0) {
						encoder.LoadConstantI4 (i);
						encoder.OpCode (ILOpCode.Sizeof);
						encoder.Token (_jniNativeMethodRef);
						encoder.OpCode (ILOpCode.Mul);
						encoder.OpCode (ILOpCode.Add);
					}

					// byte* name — ldsflda of deduplicated field
					encoder.OpCode (ILOpCode.Ldsflda);
					encoder.Token (nameFields [i]);

					// byte* signature
					encoder.OpCode (ILOpCode.Ldsflda);
					encoder.Token (sigFields [i]);

					// IntPtr functionPointer
					encoder.OpCode (ILOpCode.Ldftn);
					encoder.Token (validRegs [i].Wrapper);

					// Construct the struct on the evaluation stack and store it
					// at the destination address. This matches the Roslyn pattern:
					//   newobj JniNativeMethod::.ctor(byte*, byte*, IntPtr)
					//   stobj  JniNativeMethod
					encoder.OpCode (ILOpCode.Newobj);
					encoder.Token (_jniNativeMethodCtorRef);
					encoder.OpCode (ILOpCode.Stobj);
					encoder.Token (_jniNativeMethodRef);
				}

				// JniObjectReference peerRef = jniType.PeerReference
				// JniType is a sealed reference type, so use ldarg + callvirt
				encoder.LoadArgument (1);
				encoder.OpCode (ILOpCode.Callvirt);
				encoder.Token (_jniTypePeerReferenceRef);
				encoder.StoreLocal (1);

				// new ReadOnlySpan<JniNativeMethod>(methods, count)
				encoder.LoadLocalAddress (2);
				encoder.LoadLocal (0);
				encoder.LoadConstantI4 (methodCount);
				encoder.Call (_readOnlySpanOfJniNativeMethodCtorRef);

				// JniEnvironment.Types.RegisterNatives(peerRef, span)
				encoder.LoadLocal (1);
				encoder.LoadLocal (2);
				encoder.Call (_jniEnvTypesRegisterNativesRef);

				encoder.OpCode (ILOpCode.Ret);
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

	void EmitTypeMapAttribute (TypeMapAttributeData entry)
	{
		var ctorRef = entry.IsUnconditional ? _typeMapAttrCtorRef2Arg : _typeMapAttrCtorRef3Arg;
		var blob = _pe.BuildAttributeBlob (b => {
			b.WriteSerializedString (entry.JniName);
			b.WriteSerializedString (entry.ProxyTypeReference);
			if (!entry.IsUnconditional) {
				if (entry.TargetTypeReference is null) {
					throw new InvalidOperationException ($"TargetTypeReference must not be null for conditional entry '{entry.JniName}'");
				}
				b.WriteSerializedString (entry.TargetTypeReference);
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

	/// <summary>
	/// Writes the ECMA-335 blob for a closed generic value type with a single value-type argument.
	/// E.g., <c>ReadOnlySpan&lt;JniNativeMethod&gt;</c>.
	/// </summary>
	static void EncodeGenericValueTypeInst (BlobBuilder builder, EntityHandle openType, EntityHandle valueTypeArg)
	{
		builder.WriteByte (0x15); // ELEMENT_TYPE_GENERICINST
		builder.WriteByte (0x11); // ELEMENT_TYPE_VALUETYPE
		builder.WriteCompressedInteger (CodedIndex.TypeDefOrRefOrSpec (openType));
		builder.WriteCompressedInteger (1); // generic arity = 1
		builder.WriteByte (0x11); // ELEMENT_TYPE_VALUETYPE
		builder.WriteCompressedInteger (CodedIndex.TypeDefOrRefOrSpec (valueTypeArg));
	}

	/// <summary>
	/// Builds a <c>TypeSpec</c> for a closed generic type with a single value-type argument.
	/// E.g., <c>ReadOnlySpan&lt;JniNativeMethod&gt;</c>.
	/// </summary>
	TypeSpecificationHandle MakeGenericTypeSpec_ValueType (EntityHandle openType, EntityHandle valueTypeArg)
	{
		var sigBlob = new BlobBuilder (32);
		EncodeGenericValueTypeInst (sigBlob, openType, valueTypeArg);
		return _pe.Metadata.AddTypeSpecification (_pe.Metadata.GetOrAddBlob (sigBlob));
	}

	/// <summary>
	/// Encodes <c>ReadOnlySpan&lt;JniNativeMethod&gt;</c> directly into a signature type encoder.
	/// Required because <see cref="SignatureTypeEncoder.Type"/> doesn't accept TypeSpec handles.
	/// </summary>
	void EncodeReadOnlySpanOfJniNativeMethod (SignatureTypeEncoder encoder)
	{
		EncodeGenericValueTypeInst (encoder.Builder, _readOnlySpanOpenRef, _jniNativeMethodRef);
	}
}
