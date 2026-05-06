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
/// // Assembly-level TypeMap attributes — one per Java peer type.
/// // The anchor type T is Java.Lang.Object in merged mode (Release) or
/// // a per-assembly __TypeMapAnchor in per-assembly mode (Debug):
/// [assembly: TypeMap&lt;T&gt;("android/app/Activity", typeof(Activity_Proxy))]                              // unconditional (ACW)
/// [assembly: TypeMap&lt;T&gt;("android/widget/TextView", typeof(TextView_Proxy), typeof(TextView))]          // trimmable (MCW)
/// [assembly: TypeMapAssociation&lt;T&gt;(typeof(MyTextView), typeof(Android_Widget_TextView_Proxy))]          // managed → proxy
///
/// // One proxy type per Java peer that needs activation or UCO wrappers:
/// public sealed class Activity_Proxy : JavaPeerProxy&lt;Activity&gt;, IAndroidCallableWrapper   // IAndroidCallableWrapper for ACWs only
/// {
///     public Activity_Proxy() : base("android/app/Activity", null) { }
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
///     // JniName / TargetType / InvokerType are supplied by the base JavaPeerProxy constructor.
///
///     // UCO wrappers — [UnmanagedCallersOnly] entry points for JNI native methods (ACWs only):
///     public static void n_OnCreate_uco_0(IntPtr jnienv, IntPtr self, IntPtr p0)
///     {
///         AndroidRuntimeInternal.WaitForBridgeProcessing();
///         try {
///             Activity.n_OnCreate(jnienv, self, p0);
///         } catch (Exception e) {
///             AndroidEnvironmentInternal.UnhandledException(e);
///         }
///     }
///
///     [UnmanagedCallersOnly]
///     public static void nctor_0_uco(IntPtr jnienv, IntPtr self)
///         =&gt; new Activity(self, JniHandleOwnership.DoNotTransfer);
///         // or: var obj = (Activity)RuntimeHelpers.GetUninitializedObject(typeof(Activity));
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

	TypeReferenceHandle _javaPeerProxyRef;
	TypeReferenceHandle _javaPeerProxyNonGenericRef;
	TypeReferenceHandle _iJavaPeerableRef;
	TypeReferenceHandle _jniHandleOwnershipRef;
	TypeReferenceHandle _jniObjectReferenceRef;
	TypeReferenceHandle _jniObjectReferenceTypeRef;
	TypeReferenceHandle _jniObjectReferenceOptionsRef;
	TypeReferenceHandle _iAndroidCallableWrapperRef;
	TypeReferenceHandle _jniEnvRef;
	TypeReferenceHandle _systemTypeRef;
	TypeReferenceHandle _runtimeTypeHandleRef;
	TypeReferenceHandle _jniTypeRef;
	TypeReferenceHandle _notSupportedExceptionRef;
	TypeReferenceHandle _runtimeHelpersRef;
	TypeReferenceHandle _javaPeerAliasesAttrRef;
	MemberReferenceHandle _javaPeerAliasesAttrCtorRef;

	MemberReferenceHandle _getTypeFromHandleRef;
	MemberReferenceHandle _getUninitializedObjectRef;
	MemberReferenceHandle _notSupportedExceptionCtorRef;
	MemberReferenceHandle _jniObjectReferenceCtorRef;
	MemberReferenceHandle _jniEnvDeleteRefRef;
	MemberReferenceHandle _shouldSkipActivationRef;
	MemberReferenceHandle _waitForBridgeProcessingRef;
	MemberReferenceHandle _androidEnvironmentUnhandledExceptionRef;
	MemberReferenceHandle _ucoAttrCtorRef;
	BlobHandle _ucoAttrBlobHandle;
	MemberReferenceHandle _typeMapAttrCtorRef2Arg;
	MemberReferenceHandle _typeMapAttrCtorRef3Arg;
	MemberReferenceHandle _typeMapAssociationAttrCtorRef;

	// RegisterNatives with JniNativeMethod
	TypeReferenceHandle _jniNativeMethodRef;
	TypeReferenceHandle _jniEnvironmentRef;
	TypeReferenceHandle _jniEnvironmentTypesRef;
	TypeReferenceHandle _jniTransitionRef;
	TypeReferenceHandle _jniRuntimeRef;
	TypeReferenceHandle _exceptionRef;
	TypeReferenceHandle _androidRuntimeInternalRef;
	TypeReferenceHandle _androidEnvironmentInternalRef;

	MemberReferenceHandle _beginMarshalMethodRef;
	MemberReferenceHandle _endMarshalMethodRef;
	MemberReferenceHandle _onUserUnhandledExceptionRef;
	TypeReferenceHandle _readOnlySpanOpenRef;
	TypeSpecificationHandle _readOnlySpanOfJniNativeMethodSpec;
	MemberReferenceHandle _jniNativeMethodCtorRef;
	MemberReferenceHandle _jniTypePeerReferenceRef;
	MemberReferenceHandle _jniEnvTypesRegisterNativesRef;
	MemberReferenceHandle _readOnlySpanOfJniNativeMethodCtorRef;

	EntityHandle _anchorTypeHandle;

	// Per-rank array sentinel TypeDefs, 0-indexed by (rank - 1). Empty when array entries
	// aren't emitted.
	EntityHandle [] _rankAnchorHandles = [];

	// Per-anchor TypeMap<TGroup>(string, Type, Type) ctor refs, lazily built.
	readonly Dictionary<EntityHandle, MemberReferenceHandle> _typeMapAttr3ArgCtorRefByAnchor = new ();

	// Cached open TypeMapAttribute`1 ref shared across closed TypeSpecs.
	TypeReferenceHandle _typeMapAttrOpenRef;

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
	/// <param name="useSharedTypemapUniverse">
	/// When true, uses <c>Java.Lang.Object</c> as the shared anchor type so all assemblies
	/// share a single typemap universe. When false, emits a per-assembly <c>__TypeMapAnchor</c>.
	/// </param>
	public void Emit (TypeMapAssemblyData model, Stream stream, bool useSharedTypemapUniverse = false)
	{
		if (model is null) {
			throw new ArgumentNullException (nameof (model));
		}
		if (stream is null) {
			throw new ArgumentNullException (nameof (stream));
		}

		EmitCore (model, useSharedTypemapUniverse);
		_pe.WritePE (stream);
	}

	void EmitCore (TypeMapAssemblyData model, bool useSharedTypemapUniverse)
	{
		_pe.EmitPreamble (model.AssemblyName, model.ModuleName, MetadataHelper.ComputeContentFingerprint (model));

		_javaInteropRef = _pe.AddAssemblyRef ("Java.Interop", new Version (0, 0, 0, 0));

		EmitTypeReferences ();
		if (useSharedTypemapUniverse) {
			// Use Java.Lang.Object as the shared anchor so all assemblies share a single
			// typemap universe that can be merged at startup.
			_anchorTypeHandle = _pe.Metadata.AddTypeReference (_pe.MonoAndroidRef,
				_pe.Metadata.GetOrAddString ("Java.Lang"),
				_pe.Metadata.GetOrAddString ("Object"));
		} else {
			EmitAnchorType ();
		}
		EmitRankSentinels (model);
		EmitMemberReferences ();

		// Track wrapper method names → handles for RegisterNatives
		var wrapperHandles = new Dictionary<string, MethodDefinitionHandle> ();

		foreach (var proxy in model.ProxyTypes) {
			EmitProxyType (proxy, wrapperHandles);
		}

		foreach (var holder in model.AliasHolders) {
			EmitAliasHolderType (holder);
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
		_javaPeerProxyRef = metadata.AddTypeReference (_pe.MonoAndroidRef,
			metadata.GetOrAddString ("Java.Interop"), metadata.GetOrAddString ("JavaPeerProxy`1"));
		_javaPeerProxyNonGenericRef = metadata.AddTypeReference (_pe.MonoAndroidRef,
			metadata.GetOrAddString ("Java.Interop"), metadata.GetOrAddString ("JavaPeerProxy"));
		_iJavaPeerableRef = metadata.AddTypeReference (_javaInteropRef,
			metadata.GetOrAddString ("Java.Interop"), metadata.GetOrAddString ("IJavaPeerable"));
		_jniHandleOwnershipRef = metadata.AddTypeReference (_pe.MonoAndroidRef,
			metadata.GetOrAddString ("Android.Runtime"), metadata.GetOrAddString ("JniHandleOwnership"));
		_jniEnvRef = metadata.AddTypeReference (_pe.MonoAndroidRef,
			metadata.GetOrAddString ("Android.Runtime"), metadata.GetOrAddString ("JNIEnv"));
		_jniObjectReferenceRef = metadata.AddTypeReference (_javaInteropRef,
			metadata.GetOrAddString ("Java.Interop"), metadata.GetOrAddString ("JniObjectReference"));
		_jniObjectReferenceTypeRef = metadata.AddTypeReference (_javaInteropRef,
			metadata.GetOrAddString ("Java.Interop"), metadata.GetOrAddString ("JniObjectReferenceType"));
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
		_runtimeHelpersRef = metadata.AddTypeReference (_pe.SystemRuntimeRef,
			metadata.GetOrAddString ("System.Runtime.CompilerServices"), metadata.GetOrAddString ("RuntimeHelpers"));
		_javaPeerAliasesAttrRef = metadata.AddTypeReference (_pe.MonoAndroidRef,
			metadata.GetOrAddString ("Java.Interop"), metadata.GetOrAddString ("JavaPeerAliasesAttribute"));

		_jniNativeMethodRef = metadata.AddTypeReference (_javaInteropRef,
			metadata.GetOrAddString ("Java.Interop"), metadata.GetOrAddString ("JniNativeMethod"));
		_jniEnvironmentRef = metadata.AddTypeReference (_javaInteropRef,
			metadata.GetOrAddString ("Java.Interop"), metadata.GetOrAddString ("JniEnvironment"));
		_jniEnvironmentTypesRef = metadata.AddTypeReference (_jniEnvironmentRef,
			default, metadata.GetOrAddString ("Types"));
		_jniTransitionRef = metadata.AddTypeReference (_javaInteropRef,
			metadata.GetOrAddString ("Java.Interop"), metadata.GetOrAddString ("JniTransition"));
		_jniRuntimeRef = metadata.AddTypeReference (_javaInteropRef,
			metadata.GetOrAddString ("Java.Interop"), metadata.GetOrAddString ("JniRuntime"));
		_exceptionRef = metadata.AddTypeReference (_pe.SystemRuntimeRef,
			metadata.GetOrAddString ("System"), metadata.GetOrAddString ("Exception"));
		var monoAndroidRuntimeRef = _pe.AddAssemblyRef ("Mono.Android.Runtime", new Version (0, 0, 0, 0));
		_androidRuntimeInternalRef = metadata.AddTypeReference (monoAndroidRuntimeRef,
			metadata.GetOrAddString ("Android.Runtime"), metadata.GetOrAddString ("AndroidRuntimeInternal"));
		_androidEnvironmentInternalRef = metadata.AddTypeReference (monoAndroidRuntimeRef,
			metadata.GetOrAddString ("Android.Runtime"), metadata.GetOrAddString ("AndroidEnvironmentInternal"));

		// ReadOnlySpan<JniNativeMethod> — TypeSpec for generic instantiation
		_readOnlySpanOpenRef = metadata.AddTypeReference (_pe.SystemRuntimeRef,
			metadata.GetOrAddString ("System"), metadata.GetOrAddString ("ReadOnlySpan`1"));
		_readOnlySpanOfJniNativeMethodSpec = MakeGenericTypeSpec_ValueType (_readOnlySpanOpenRef, _jniNativeMethodRef);
	}

	/// <summary>
	/// Emits an internal <c>__TypeMapAnchor</c> class used as the group type parameter
	/// for <c>TypeMap&lt;T&gt;</c> and <c>TypeMapAssociation&lt;T&gt;</c>. Each per-assembly
	/// typemap DLL gets its own anchor, creating an isolated typemap universe.
	/// </summary>
	void EmitAnchorType ()
	{
		var metadata = _pe.Metadata;
		var objectRef = metadata.AddTypeReference (_pe.SystemRuntimeRef,
			metadata.GetOrAddString ("System"), metadata.GetOrAddString ("Object"));

		_anchorTypeHandle = metadata.AddTypeDefinition (
			TypeAttributes.NotPublic | TypeAttributes.Sealed | TypeAttributes.Class,
			default,
			metadata.GetOrAddString ("__TypeMapAnchor"),
			objectRef,
			MetadataTokens.FieldDefinitionHandle (metadata.GetRowCount (TableIndex.Field) + 1),
			MetadataTokens.MethodDefinitionHandle (metadata.GetRowCount (TableIndex.MethodDef) + 1));
	}

	/// <summary>
	/// Populates <c>_rankAnchorHandles</c> with TypeRefs to the shared
	/// <c>Microsoft.Android.Runtime.__ArrayMapRank{N}</c> types in Mono.Android. All per-asm
	/// typemap DLLs reference the same anchors so each rank's entries merge into one dict
	/// at runtime via <c>TypeMapping.GetOrCreateExternalTypeMapping&lt;__ArrayMapRank{N}&gt;()</c>.
	/// </summary>
	void EmitRankSentinels (TypeMapAssemblyData model)
	{
		if (model.MaxArrayRank <= 0) {
			return;
		}

		_rankAnchorHandles = new EntityHandle [model.MaxArrayRank];
		var ns = _pe.Metadata.GetOrAddString ("Microsoft.Android.Runtime");
		for (int i = 0; i < model.MaxArrayRank; i++) {
			_rankAnchorHandles [i] = _pe.Metadata.AddTypeReference (
				_pe.MonoAndroidRef, ns,
				_pe.Metadata.GetOrAddString ($"__ArrayMapRank{i + 1}"));
		}
	}

	void EmitMemberReferences ()
	{
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

		// JniObjectReference..ctor(IntPtr handle, JniObjectReferenceType type)
		// Note: The C# constructor has a default parameter (type = Invalid), but in IL there is only
		// the 2-parameter overload. We must emit both parameters explicitly.
		_jniObjectReferenceCtorRef = _pe.AddMemberRef (_jniObjectReferenceRef, ".ctor",
			sig => sig.MethodSignature (isInstanceMethod: true).Parameters (2,
				rt => rt.Void (),
				p => {
					p.AddParameter ().Type ().IntPtr ();
					p.AddParameter ().Type ().Type (_jniObjectReferenceTypeRef, true);
				}));

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

		// JavaPeerProxy.ShouldSkipActivation(IntPtr) -> bool (static method)
		_shouldSkipActivationRef = _pe.AddMemberRef (_javaPeerProxyNonGenericRef, "ShouldSkipActivation",
			sig => sig.MethodSignature ().Parameters (1,
				rt => rt.Type ().Boolean (),
				p => { p.AddParameter ().Type ().IntPtr (); }));

		_waitForBridgeProcessingRef = _pe.AddMemberRef (_androidRuntimeInternalRef, "WaitForBridgeProcessing",
			sig => sig.MethodSignature ().Parameters (0, rt => rt.Void (), p => { }));

		_androidEnvironmentUnhandledExceptionRef = _pe.AddMemberRef (_androidEnvironmentInternalRef, "UnhandledException",
			sig => sig.MethodSignature ().Parameters (1,
				rt => rt.Void (),
				p => p.AddParameter ().Type ().Type (_exceptionRef, false)));

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

		// Legacy marshal-method UCO wrappers use the default unmanaged calling convention.
		_ucoAttrBlobHandle = _pe.BuildAttributeBlob (b => { });

		// JniEnvironment.BeginMarshalMethod(nint jnienv, out JniTransition, out JniRuntime?) -> bool
		_beginMarshalMethodRef = _pe.AddMemberRef (_jniEnvironmentRef, "BeginMarshalMethod",
			sig => sig.MethodSignature ().Parameters (3,
				rt => rt.Type ().Boolean (),
				p => {
					p.AddParameter ().Type ().IntPtr ();
					p.AddParameter ().Type (isByRef: true).Type (_jniTransitionRef, true);
					p.AddParameter ().Type (isByRef: true).Type (_jniRuntimeRef, false);
				}));

		// JniEnvironment.EndMarshalMethod(ref JniTransition) -> void
		_endMarshalMethodRef = _pe.AddMemberRef (_jniEnvironmentRef, "EndMarshalMethod",
			sig => sig.MethodSignature ().Parameters (1,
				rt => rt.Void (),
				p => p.AddParameter ().Type (isByRef: true).Type (_jniTransitionRef, true)));

		// JniRuntime.OnUserUnhandledException(ref JniTransition, Exception) -> void
		_onUserUnhandledExceptionRef = _pe.AddMemberRef (_jniRuntimeRef, "OnUserUnhandledException",
			sig => sig.MethodSignature (isInstanceMethod: true).Parameters (2,
				rt => rt.Void (),
				p => {
					p.AddParameter ().Type (isByRef: true).Type (_jniTransitionRef, true);
					p.AddParameter ().Type ().Type (_exceptionRef, false);
				}));

		EmitTypeMapAttributeCtorRef ();
		EmitTypeMapAssociationAttributeCtorRef ();
		EmitJavaPeerAliasesAttributeCtorRef ();
	}

	void EmitTypeMapAttributeCtorRef ()
	{
		var metadata = _pe.Metadata;
		_typeMapAttrOpenRef = metadata.AddTypeReference (_pe.SystemRuntimeInteropServicesRef,
			metadata.GetOrAddString ("System.Runtime.InteropServices"),
			metadata.GetOrAddString ("TypeMapAttribute`1"));

		var closedAttrTypeSpec = _pe.MakeGenericTypeSpec (_typeMapAttrOpenRef, _anchorTypeHandle);

		// 2-arg: TypeMap(string jniName, Type proxyType) — unconditional. Default anchor only;
		// rank-anchored entries are always conditional (3-arg) so no per-rank 2-arg ctor is
		// needed today.
		_typeMapAttrCtorRef2Arg = _pe.AddMemberRef (closedAttrTypeSpec, ".ctor",
			sig => sig.MethodSignature (isInstanceMethod: true).Parameters (2,
				rt => rt.Void (),
				p => {
					p.AddParameter ().Type ().String ();
					p.AddParameter ().Type ().Type (_systemTypeRef, false);
				}));

		// 3-arg: TypeMap(string jniName, Type proxyType, Type targetType) — trimmable.
		// Cache by anchor so rank-anchored entries can build their own closed ctor on demand.
		_typeMapAttrCtorRef3Arg = AddTypeMapAttr3ArgCtorRef (_anchorTypeHandle);
		_typeMapAttr3ArgCtorRefByAnchor [_anchorTypeHandle] = _typeMapAttrCtorRef3Arg;
	}

	/// <summary>Cached 3-arg <c>TypeMap&lt;TGroup&gt;</c> ctor ref for the given anchor, built on first use.</summary>
	MemberReferenceHandle GetOrAddTypeMapAttr3ArgCtorRef (EntityHandle anchor)
	{
		if (_typeMapAttr3ArgCtorRefByAnchor.TryGetValue (anchor, out var cached)) {
			return cached;
		}
		var ctorRef = AddTypeMapAttr3ArgCtorRef (anchor);
		_typeMapAttr3ArgCtorRefByAnchor [anchor] = ctorRef;
		return ctorRef;
	}

	MemberReferenceHandle AddTypeMapAttr3ArgCtorRef (EntityHandle anchor)
	{
		var closedAttrTypeSpec = _pe.MakeGenericTypeSpec (_typeMapAttrOpenRef, anchor);
		return _pe.AddMemberRef (closedAttrTypeSpec, ".ctor",
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
		var closedAttrTypeSpec = _pe.MakeGenericTypeSpec (typeMapAssociationAttrOpenRef, _anchorTypeHandle);

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

		// Open generic definitions derive from the non-generic `JavaPeerProxy` abstract base.
		// Using `JavaPeerProxy<T>` with an open T would force the CLR to resolve a generic
		// argument that isn't available via the TypeMapLazyDictionary loader, and using a
		// placeholder like `Java.Lang.Object` leaks an incorrect TargetType into the typemap.
		// The non-generic base takes `targetType` as a ctor parameter, so we can pass the real
		// open-generic type token (a TypeRef, not a closed TypeSpec) and keep TargetType correct.
		EntityHandle proxyBaseType;
		MemberReferenceHandle baseCtorRef;
		if (proxy.IsGenericDefinition) {
			proxyBaseType = _javaPeerProxyNonGenericRef;
			baseCtorRef = _pe.AddMemberRef (_javaPeerProxyNonGenericRef, ".ctor",
				sig => sig.MethodSignature (isInstanceMethod: true).Parameters (3,
					rt => rt.Void (),
					p => {
						p.AddParameter ().Type ().String ();
						p.AddParameter ().Type ().Type (_systemTypeRef, false);
						p.AddParameter ().Type ().Type (_systemTypeRef, false);
					}));
		} else {
			var genericProxyBase = _pe.MakeGenericTypeSpec (_javaPeerProxyRef, targetTypeRef);
			proxyBaseType = genericProxyBase;
			baseCtorRef = _pe.AddMemberRef (genericProxyBase, ".ctor",
				sig => sig.MethodSignature (isInstanceMethod: true).Parameters (2,
					rt => rt.Void (),
					p => {
						p.AddParameter ().Type ().String ();
						p.AddParameter ().Type ().Type (_systemTypeRef, false);
					}));
		}

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

		// .ctor — pass the resolved JNI name, (for generic-definition base) target type, and
		// optional invoker type to the base proxy constructor.
		var selfAttrCtorDef = _pe.EmitBody (".ctor",
			MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
			sig => sig.MethodSignature (isInstanceMethod: true).Parameters (0, rt => rt.Void (), p => { }),
			encoder => {
				encoder.OpCode (ILOpCode.Ldarg_0);
				encoder.LoadString (metadata.GetOrAddUserString (proxy.JniName));
				if (proxy.IsGenericDefinition) {
					// Non-generic base ctor signature: (string, Type, Type?). Push the open-generic
					// target type as the second argument.
					encoder.OpCode (ILOpCode.Ldtoken);
					encoder.Token (targetTypeRef);
					encoder.Call (_getTypeFromHandleRef);
				}
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

		// Self-apply: the proxy type is its own [JavaPeerProxy] attribute.
		// This enables type.GetCustomAttribute<JavaPeerProxy>() to instantiate the proxy
		// at runtime for AOT-safe type resolution.
		var selfAttrBlob = _pe.BuildAttributeBlob (b => { });
		metadata.AddCustomAttribute (typeDefHandle, selfAttrCtorDef, selfAttrBlob);

		// CreateInstance
		EmitCreateInstance (proxy);

		// UCO wrappers
		foreach (var uco in proxy.UcoMethods) {
			var handle = EmitUcoMethod (uco, proxy);
			wrapperHandles [uco.WrapperName] = handle;
		}

		foreach (var uco in proxy.UcoConstructors) {
			var handle = EmitUcoConstructor (uco, proxy);
			wrapperHandles [uco.WrapperName] = handle;
		}

		// RegisterNatives
		if (proxy.IsAcw) {
			EmitRegisterNatives (proxy, wrapperHandles);
		}
	}

	void EmitAliasHolderType (AliasHolderData holder)
	{
		var metadata = _pe.Metadata;

		// Alias holders are plain classes (NOT JavaPeerProxy subclasses).
		// GetCustomAttribute<JavaPeerProxy>() returns null for these — the fast path
		// stays clean. Aliases are discovered via [JavaPeerAliases] attribute only when needed.
		var objectRef = metadata.AddTypeReference (_pe.SystemRuntimeRef,
			metadata.GetOrAddString ("System"), metadata.GetOrAddString ("Object"));

		var typeDefHandle = metadata.AddTypeDefinition (
			TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Class,
			metadata.GetOrAddString (holder.Namespace),
			metadata.GetOrAddString (holder.TypeName),
			objectRef,
			MetadataTokens.FieldDefinitionHandle (metadata.GetRowCount (TableIndex.Field) + 1),
			MetadataTokens.MethodDefinitionHandle (metadata.GetRowCount (TableIndex.MethodDef) + 1));

		// Apply [JavaPeerAliases("key[0]", "key[1]", ...)] to the type
		EmitJavaPeerAliasesAttribute (typeDefHandle, holder.AliasKeys);
	}

	void EmitJavaPeerAliasesAttributeCtorRef ()
	{
		// JavaPeerAliasesAttribute(params string[] aliases) — in Mono.Android, Java.Interop namespace
		_javaPeerAliasesAttrCtorRef = _pe.AddMemberRef (_javaPeerAliasesAttrRef, ".ctor",
			sig => sig.MethodSignature (isInstanceMethod: true).Parameters (1,
				rt => rt.Void (),
				p => p.AddParameter ().Type ().SZArray ().String ()));
	}

	void EmitJavaPeerAliasesAttribute (TypeDefinitionHandle typeDefHandle, List<string> aliasKeys)
	{
		// Encode the attribute blob: prolog (0x0001), then packed string array, then NumNamed (0x0000).
		// The params string[] is encoded as: element count (uint32), then each string as SerializedString.
		var blobBuilder = new BlobBuilder ();
		blobBuilder.WriteUInt16 (1); // prolog
		blobBuilder.WriteInt32 (aliasKeys.Count); // array length
		foreach (var key in aliasKeys) {
			WriteSerializedString (blobBuilder, key);
		}
		blobBuilder.WriteUInt16 (0); // NumNamed

		_pe.Metadata.AddCustomAttribute (typeDefHandle, _javaPeerAliasesAttrCtorRef, _pe.Metadata.GetOrAddBlob (blobBuilder));
	}

	static void WriteSerializedString (BlobBuilder builder, string value)
	{
		var bytes = System.Text.Encoding.UTF8.GetBytes (value);
		builder.WriteCompressedInteger (bytes.Length);
		builder.WriteBytes (bytes);
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
				encoder.OpCode (ILOpCode.Ldtoken);
				encoder.Token (targetTypeRef);
				encoder.Call (_getTypeFromHandleRef);
				encoder.Call (_getUninitializedObjectRef);
				encoder.OpCode (ILOpCode.Castclass);
				encoder.Token (targetTypeRef);

				// dup obj (one copy for the call, one for the return)
				encoder.OpCode (ILOpCode.Dup);

				// var jniRef = new JniObjectReference(handle, JniObjectReferenceType.Invalid);
				encoder.LoadLocalAddress (0);
				encoder.OpCode (ILOpCode.Ldarg_1); // handle
				encoder.LoadConstantI4 (0); // JniObjectReferenceType.Invalid
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
				enc.Call (callbackRef);
			}),
			blob => EncodeUcoForwarderLegacyLocals (blob, returnKind));

		AddUnmanagedCallersOnlyAttribute (handle);
		return handle;
	}

	void EmitUcoForwarderBody (InstructionEncoder encoder, ControlFlowBuilder cfb, JniParamKind returnKind, Action<InstructionEncoder> emitCallback)
	{
		bool isVoid = returnKind == JniParamKind.Void;
		var tryStart = encoder.DefineLabel ();
		var catchStart = encoder.DefineLabel ();
		var afterAll = encoder.DefineLabel ();

		encoder.Call (_waitForBridgeProcessingRef);
		encoder.MarkLabel (tryStart);
		emitCallback (encoder);
		if (!isVoid) {
			encoder.StoreLocal (0);
		}
		encoder.Branch (ILOpCode.Leave, afterAll);

		encoder.MarkLabel (catchStart);
		encoder.StoreLocal (isVoid ? 0 : 1);
		encoder.LoadLocal (isVoid ? 0 : 1);
		encoder.Call (_androidEnvironmentUnhandledExceptionRef);
		encoder.Branch (ILOpCode.Leave, afterAll);

		encoder.MarkLabel (afterAll);
		if (!isVoid) {
			encoder.LoadLocal (0);
		}
		encoder.OpCode (ILOpCode.Ret);

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

		// Open generic types can't be activated because Java construction cannot provide the type arguments.
		if (proxy.IsGenericDefinition) {
			var openGenericHandle = _pe.EmitBody (uco.WrapperName,
				MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig,
				encodeSig,
				(encoder, cfb) => EmitUcoConstructorBodyWithMarshal (encoder, cfb, enc => {
					enc.LoadString (_pe.Metadata.GetOrAddUserString ("Constructing instances of generic types from Java is not supported, as the type parameters cannot be determined."));
					enc.OpCode (ILOpCode.Newobj);
					enc.Token (_notSupportedExceptionCtorRef);
					enc.OpCode (ILOpCode.Throw);
				}),
				EncodeUcoConstructorLocals_Standard);
			AddUnmanagedCallersOnlyAttribute (openGenericHandle);
			return openGenericHandle;
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
			handle = _pe.EmitBody (uco.WrapperName,
				MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig,
				encodeSig,
				(encoder, cfb) => EmitUcoConstructorBodyWithMarshal (encoder, cfb, enc => {
					if (!activationCtor.IsOnLeafType) {
						enc.OpCode (ILOpCode.Ldtoken);
						enc.Token (targetTypeRef);
						enc.Call (_getTypeFromHandleRef);
						enc.Call (_getUninitializedObjectRef);
						enc.OpCode (ILOpCode.Castclass);
						enc.Token (targetTypeRef);
					}

					enc.LoadLocalAddress (3); // jniRef
					enc.LoadArgument (1);     // self
					enc.LoadConstantI4 (0);   // JniObjectReferenceType.Invalid
					enc.Call (_jniObjectReferenceCtorRef);

					if (activationCtor.IsOnLeafType) {
						enc.LoadLocalAddress (3); // ref jniRef
						enc.LoadConstantI4 (1);   // JniObjectReferenceOptions.Copy
						enc.OpCode (ILOpCode.Newobj);
						enc.Token (ctorRef);
						enc.OpCode (ILOpCode.Pop);
					} else {
						enc.LoadLocalAddress (3); // ref jniRef
						enc.LoadConstantI4 (1);   // JniObjectReferenceOptions.Copy
						enc.Call (ctorRef);
					}
				}),
				EncodeUcoConstructorLocals_JavaInterop);
		} else {
			var ctorRef = AddActivationCtorRef (
				activationCtor.IsOnLeafType ? targetTypeRef : _pe.ResolveTypeRef (activationCtor.DeclaringType));

			// Locals:
			//   0: JniTransition  (envp)    — out-parameter for BeginMarshalMethod
			//   1: JniRuntime?    (runtime) — out-parameter for BeginMarshalMethod
			//   2: Exception      (e)       — catch variable
			handle = _pe.EmitBody (uco.WrapperName,
				MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig,
				encodeSig,
				(encoder, cfb) => EmitUcoConstructorBodyWithMarshal (encoder, cfb, enc => {
					if (activationCtor.IsOnLeafType) {
						enc.LoadArgument (1);    // self
						enc.LoadConstantI4 (0);  // JniHandleOwnership.DoNotTransfer
						enc.OpCode (ILOpCode.Newobj);
						enc.Token (ctorRef);
						enc.OpCode (ILOpCode.Pop);
					} else {
						enc.OpCode (ILOpCode.Ldtoken);
						enc.Token (targetTypeRef);
						enc.Call (_getTypeFromHandleRef);
						enc.Call (_getUninitializedObjectRef);
						enc.OpCode (ILOpCode.Castclass);
						enc.Token (targetTypeRef);

						enc.LoadArgument (1);    // self
						enc.LoadConstantI4 (0);  // JniHandleOwnership.DoNotTransfer
						enc.Call (ctorRef);
					}
				}),
				EncodeUcoConstructorLocals_Standard);
		}
		AddUnmanagedCallersOnlyAttribute (handle);
		return handle;
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
	void EmitUcoConstructorBodyWithMarshal (InstructionEncoder encoder, ControlFlowBuilder cfb, Action<InstructionEncoder> emitActivation)
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
		encoder.Call (_beginMarshalMethodRef);
		encoder.Branch (ILOpCode.Brfalse, afterAll);

		// TRY — check ShouldSkipActivation, then run activation code.
		encoder.MarkLabel (tryStart);
		encoder.LoadArgument (1);      // self (IntPtr)
		encoder.Call (_shouldSkipActivationRef);
		encoder.Branch (ILOpCode.Brtrue, skipLabel);

		emitActivation (encoder);

		encoder.MarkLabel (skipLabel);
		encoder.Branch (ILOpCode.Leave, afterAll);

		// CATCH (System.Exception e)
		encoder.MarkLabel (catchStart);
		encoder.StoreLocal (2);              // e = exception (local 2)
		encoder.LoadLocal (1);               // load runtime (__r)
		encoder.Branch (ILOpCode.Brfalse, endCatch);
		encoder.LoadLocal (1);               // __r for callvirt
		encoder.LoadLocalAddress (0);        // ref envp
		encoder.LoadLocal (2);               // e
		encoder.OpCode (ILOpCode.Callvirt);
		encoder.Token (_onUserUnhandledExceptionRef);
		encoder.MarkLabel (endCatch);
		encoder.Branch (ILOpCode.Leave, afterAll);

		// FINALLY
		encoder.MarkLabel (finallyStart);
		encoder.LoadLocalAddress (0);        // ref envp
		encoder.Call (_endMarshalMethodRef);
		encoder.OpCode (ILOpCode.Endfinally);

		// AFTER (both finallyEnd and the early-return target)
		encoder.MarkLabel (afterAll);
		encoder.OpCode (ILOpCode.Ret);

		// Register exception regions:
		// Catch region:   try [tryStart, catchStart),  handler [catchStart, finallyStart)
		// Finally region: try [tryStart, finallyStart), handler [finallyStart, afterAll)
		cfb.AddCatchRegion (tryStart, catchStart, catchStart, finallyStart, _exceptionRef);
		cfb.AddFinallyRegion (tryStart, finallyStart, finallyStart, afterAll);
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
		MemberReferenceHandle ctorRef;
		if (entry.AnchorRank is int rank) {
			if (entry.IsUnconditional) {
				throw new InvalidOperationException (
					$"Rank-anchored TypeMap entries must be conditional (3-arg). Entry '{entry.JniName}' rank={rank}.");
			}
			int anchorIndex = rank - 1;
			if ((uint)anchorIndex >= (uint)_rankAnchorHandles.Length) {
				throw new InvalidOperationException (
					$"No rank-{rank} anchor available for entry '{entry.JniName}'. " +
					$"Ensure TypeMapAssemblyData.MaxArrayRank was >= {rank} before emit.");
			}
			ctorRef = GetOrAddTypeMapAttr3ArgCtorRef (_rankAnchorHandles [anchorIndex]);
		} else {
			ctorRef = entry.IsUnconditional ? _typeMapAttrCtorRef2Arg : _typeMapAttrCtorRef3Arg;
		}

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
