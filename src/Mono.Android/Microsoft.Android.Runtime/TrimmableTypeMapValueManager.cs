using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using Android.Runtime;
using Java.Interop;

namespace Microsoft.Android.Runtime;

sealed partial class TrimmableTypeMapValueManager : JniRuntime.JniValueManager
{
	const DynamicallyAccessedMemberTypes Constructors = DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors;
	const JniObjectReferenceOptions DoNotRegisterTarget = JniObjectReferenceOptions.CopyAndDoNotRegister & ~JniObjectReferenceOptions.Copy;

	public TrimmableTypeMapValueManager ()
	{
		JavaMarshalRegisteredPeers.InitializeIfNeeded ();
	}

	public override void WaitForGCBridgeProcessing ()
	{
		// Intentionally empty. The Mono runtime's own implementation acknowledges this
		// pattern is fundamentally flawed (see FIXME in sgen-bridge.c): a thread that
		// passes the check can still race with bridge processing that starts immediately
		// after. The wait cannot prevent the race, only reduce its window. On CoreCLR,
		// JNI wrapper threads hold their own handle copies via JniObjectReference, so
		// they are not affected by the bridge swapping control_block handles.
	}

	public override void CollectPeers ()
	{
		JavaMarshalRegisteredPeers.CollectPeers ();
	}

	public override void AddPeer (IJavaPeerable value)
	{
		JavaMarshalRegisteredPeers.AddPeer (value);
	}

	public override IJavaPeerable? PeekPeer (JniObjectReference reference)
	{
		return JavaMarshalRegisteredPeers.PeekPeer (reference);
	}

	public override void RemovePeer (IJavaPeerable value)
	{
		JavaMarshalRegisteredPeers.RemovePeer (value);
	}

	public override void FinalizePeer (IJavaPeerable value)
	{
		JavaMarshalRegisteredPeers.FinalizePeer (value);
	}

	public override List<JniSurfacedPeerInfo> GetSurfacedPeers ()
	{
		return JavaMarshalRegisteredPeers.GetSurfacedPeers ();
	}

	public override void ActivatePeer (JniObjectReference reference, Type type, ConstructorInfo cinfo, object?[]? argumentValues)
	{
		throw new PlatformNotSupportedException ("Activating Java peers through the value manager is not supported when TrimmableTypeMap is enabled.");
	}

	protected override void ConstructPeerCore (
		IJavaPeerable peer,
		ref JniObjectReference reference,
		JniObjectReferenceOptions options)
	{
		ArgumentNullException.ThrowIfNull (peer);

		var newRef = peer.PeerReference;
		if (newRef.IsValid) {
			JniObjectReference.Dispose (ref reference, options);

			// Instance was already added, don't add again
			if (peer.JniManagedPeerState.HasFlag (JniManagedPeerStates.Activatable)) {
				return;
			}
			var orig = newRef;
			newRef = orig.NewGlobalRef ();
			JniObjectReference.Dispose (ref orig);
		} else if (options == JniObjectReferenceOptions.None) {
			// `reference` is likely *InvalidJniObjectReference, and can't be touched
			return;
		} else if (!reference.IsValid) {
			throw new ArgumentException ("JNI Object Reference is invalid.", nameof (reference));
		} else {
			newRef = reference;

			if ((options & JniObjectReferenceOptions.Copy) == JniObjectReferenceOptions.Copy) {
				newRef = reference.NewGlobalRef ();
			}

			JniObjectReference.Dispose (ref reference, options);
		}

		peer.SetPeerReference (newRef);
		peer.SetJniIdentityHashCode (JniEnvironment.References.GetIdentityHashCode (newRef));

		var o = Runtime.ObjectReferenceManager;
		if (o.LogGlobalReferenceMessages) {
			o.WriteGlobalReferenceLine ("Created PeerReference={0} IdentityHashCode=0x{1} Instance=0x{2} Instance.Type={3}, Java.Type={4}",
					newRef.ToString (),
					peer.JniIdentityHashCode.ToString ("x", CultureInfo.InvariantCulture),
					RuntimeHelpers.GetHashCode (peer).ToString ("x", CultureInfo.InvariantCulture),
					peer.GetType ().FullName,
					JniEnvironment.Types.GetJniTypeNameFromInstance (newRef));
		}

		if ((options & DoNotRegisterTarget) != DoNotRegisterTarget) {
			AddPeer (peer);
		}
	}

	public override IJavaPeerable? CreatePeer (
		ref JniObjectReference reference,
		JniObjectReferenceOptions transfer,
		[DynamicallyAccessedMembers (Constructors)]
		Type? targetType)
	{
		EnsureNotDisposed ();

		if (!reference.IsValid) {
			return null;
		}

		try {
			var resolvedTargetType = ResolvePeerType (targetType);
			return TrimmableTypeMap.Instance.CreateInstance (reference.Handle, resolvedTargetType)
				?? NotFoundFallback (ref reference, targetType, resolvedTargetType);
		} finally {
			JniObjectReference.Dispose (ref reference, transfer);
		}

		[return: DynamicallyAccessedMembers (Constructors)]
		static Type? ResolvePeerType ([DynamicallyAccessedMembers (Constructors)] Type? type)
		{
			if (type is null) {
				return null;
			}
			if (type == typeof (object) || type == typeof (IJavaPeerable)) {
				return typeof (global::Java.Interop.JavaObject);
			}
			if (type == typeof (Exception)) {
				return typeof (JavaException);
			}
			return type;
		}

		static IJavaPeerable? NotFoundFallback (ref JniObjectReference reference, Type? targetType, Type? resolvedTargetType)
		{
			// Disambiguate the failure — match the contract of the base
			// JniRuntime.JniValueManager.CreatePeer so JavaCast / JavaAs
			// surface the right exception (or null) to callers:
			//
			//  (a) target type has no Java mapping at all → ArgumentException
			//  (b) Java instance is not assignable to the target's Java class
			//      → return null (JavaAs returns null; JavaCast wraps to
			//      InvalidCastException via its `??` clause)
			//  (c) classes are compatible but no proxy / activation failed
			//      → NotSupportedException (genuine generator gap)
			if (targetType is not null && resolvedTargetType is not null) {
				if (!TrimmableTypeMap.Instance.TryGetJniNameForManagedType (resolvedTargetType, out var targetJniName)) {
					throw new ArgumentException (
						$"Could not determine Java type corresponding to '{targetType.AssemblyQualifiedName}'.",
						nameof (targetType));
				}

				if (IsIncompatibleCast (targetJniName, ref reference, resolvedTargetType)) {
					return null;
				}
			}

			var targetName = resolvedTargetType?.AssemblyQualifiedName ?? "<null>";
			var javaType = JniEnvironment.Types.GetJniTypeNameFromInstance (reference);

			throw new NotSupportedException (
				$"No generated {nameof (JavaPeerProxy)} was found for Java type '{javaType}' " +
				$"with targetType '{targetName}' while {nameof (RuntimeFeature.TrimmableTypeMap)} is enabled. " +
				$"This indicates a missing trimmable typemap proxy or association and should be fixed in the generator.");
		}

		static bool IsIncompatibleCast (
			string targetJniName,
			ref JniObjectReference reference,
			Type targetType)
		{
			var instanceClass = JniEnvironment.Types.GetObjectClass (reference);
			JniObjectReference targetClass = default;
			try {
				targetClass = JniEnvironment.Types.FindClass (targetJniName);

				if (!JniEnvironment.Types.IsAssignableFrom (instanceClass, targetClass)) {
					// Match the legacy cast diagnostic when assembly logging is enabled.
					if (Logger.LogAssembly) {
						var targetSig = JniRuntime.CurrentRuntime.TypeManager.GetTypeSignature (targetType);
						var message = $"Handle 0x{reference.Handle:x} is of type '{JNIEnv.GetClassNameFromInstance (reference.Handle)}' which is not assignable to '{targetSig.SimpleReference}'";
						Logger.Log (LogLevel.Debug, "monodroid-assembly", message);
					}

					if (RuntimeFeature.IsAssignableFromCheck) {
						return true;
					}
				}
			} catch (Java.Lang.ClassNotFoundException e) {
				throw new ArgumentException (
					$"Could not find Java class '{targetJniName}'.",
					nameof (targetType), e);
			} finally {
				JniObjectReference.Dispose (ref instanceClass);
				JniObjectReference.Dispose (ref targetClass);
			}

			// Compatible classes mean a proxy/activation gap.
			return false;
		}

	}

	[return: MaybeNull]
	protected override T CreateValueCore<[DynamicallyAccessedMembers (Constructors)] T> (
		ref JniObjectReference reference,
		JniObjectReferenceOptions options,
		[DynamicallyAccessedMembers (Constructors)]
		Type? targetType = null)
	{
		EnsureNotDisposed ();
		if (!reference.IsValid) {
			return default;
		}

		if (targetType != null && !typeof (T).IsAssignableFrom (targetType)) {
			throw new ArgumentException (
				string.Format (CultureInfo.InvariantCulture, "Requested runtime type '{0}' is not compatible with requested compile-time type T of '{1}'.",
					targetType,
					typeof (T)),
				nameof (targetType));
		}

		var boxed = PeekBoxedObject (reference);
		if (boxed != null) {
			JniObjectReference.Dispose (ref reference, options);
			return (T) Convert.ChangeType (boxed, targetType ?? typeof (T), CultureInfo.InvariantCulture);
		}

		targetType ??= typeof (T);

		if (typeof (IJavaPeerable).IsAssignableFrom (targetType)) {
			return (T?) CreatePeer (ref reference, options, targetType);
		}

		if (PrimitiveArrayInfo.TryCreateWrapper (ref reference, options, targetType, out var arrayWrapper)) {
			return (T) arrayWrapper;
		}

		var value = JavaConvert.FromObjectReference (ref reference, options, targetType);
		if (value is null) {
			return default;
		}
		return (T) value;
	}

	protected override object? CreateValueCore (
		ref JniObjectReference reference,
		JniObjectReferenceOptions options,
		[DynamicallyAccessedMembers (Constructors)]
		Type? targetType = null)
	{
		EnsureNotDisposed ();
		if (!reference.IsValid) {
			return null;
		}

		if (targetType != null && typeof (IJavaPeerable).IsAssignableFrom (targetType)) {
			return CreatePeer (ref reference, options, targetType);
		}

		var boxed = PeekBoxedObject (reference);
		if (boxed != null) {
			JniObjectReference.Dispose (ref reference, options);
			if (targetType != null) {
				return Convert.ChangeType (boxed, targetType, CultureInfo.InvariantCulture);
			}
			return boxed;
		}

		if (targetType != null && PrimitiveArrayInfo.TryCreateWrapper (ref reference, options, targetType, out var arrayWrapper)) {
			return arrayWrapper;
		}

		return JavaConvert.FromObjectReference (ref reference, options, targetType);
	}

	[return: MaybeNull]
	protected override T GetValueCore<[DynamicallyAccessedMembers (Constructors)] T> (
		ref JniObjectReference reference,
		JniObjectReferenceOptions options,
		[DynamicallyAccessedMembers (Constructors)]
		Type? targetType = null)
	{
		EnsureNotDisposed ();
		if (!reference.IsValid) {
			return default;
		}

		if (targetType != null && !typeof (T).IsAssignableFrom (targetType)) {
			throw new ArgumentException (
				$"Requested runtime type '{targetType}' is not compatible with requested compile-time type T of '{typeof (T)}'.",
				nameof (targetType));
		}

		targetType ??= typeof (T);

		var existing = PeekValue (reference);
		if (existing != null && targetType.IsAssignableFrom (existing.GetType ())) {
			JniObjectReference.Dispose (ref reference, options);
			return (T) existing;
		}

		var value = CreateValueCore<T> (ref reference, options, targetType);
		if (value is null) {
			return default;
		}
		return value;
	}

	protected override object? GetValueCore (
		ref JniObjectReference reference,
		JniObjectReferenceOptions options,
		[DynamicallyAccessedMembers (Constructors)]
		Type? targetType = null)
	{
		EnsureNotDisposed ();
		if (!reference.IsValid) {
			return null;
		}

		var existing = PeekValue (reference);
		if (existing != null && (targetType == null || targetType.IsAssignableFrom (existing.GetType ()))) {
			JniObjectReference.Dispose (ref reference, options);
			return existing;
		}

		return CreateValueCore (ref reference, options, targetType);
	}

	object? PeekBoxedObject (JniObjectReference reference)
	{
		var peer = PeekPeer (reference);
		if (peer == null) {
			return null;
		}
		return TryUnboxPeerObject (peer, out var result) ? result : null;
	}

	protected override bool TryUnboxPeerObject (IJavaPeerable value, [NotNullWhen (true)] out object? result)
	{
		if (value is TrimmableJavaProxyObject proxy) {
			result = proxy.Value;
			return true;
		}

		return base.TryUnboxPeerObject (value, out result);
	}

	protected override JniObjectReference CreateLocalObjectReferenceArgumentCore (Type type, object? value)
	{
		if (value == null) {
			return new JniObjectReference ();
		}

		if (PrimitiveArrayInfo.TryCreateObjectReference (value, out var primitiveArrayReference)) {
			return primitiveArrayReference;
		}

		if (value is IJavaPeerable peerable) {
			return peerable.PeerReference.IsValid
				? peerable.PeerReference.NewLocalRef ()
				: new JniObjectReference ();
		}

		if (JavaConvert.TryConvertKnownValueToLocalJniHandle (value, out var handle)) {
			return handle == IntPtr.Zero
				? new JniObjectReference ()
				: new JniObjectReference (handle, JniObjectReferenceType.Local);
		}

		var proxy = TrimmableJavaProxyObject.GetProxy (value);
		return proxy.PeerReference.NewLocalRef ();
	}

	protected override JniValueMarshaler GetValueMarshalerCore (Type type)
		=> throw new NotSupportedException ($"{nameof (GetValueMarshalerCore)} should not be called in the trimmable typemap path.");

	protected override JniValueMarshaler<T> GetValueMarshalerCore<T> ()
		=> throw new NotSupportedException ($"{nameof (GetValueMarshalerCore)} should not be called in the trimmable typemap path.");

	// Trimmable proxies use Java identity semantics: equals/hashCode/toString are NOT overridden
	// and therefore do not delegate to the wrapped .NET object. This matches the trimmable Java
	// runtime copy of JavaProxyObject and avoids the reflection-based native method registration
	// that is unsupported in the trimmable typemap path.
	[Register ("net/dot/jni/internal/TrimmableJavaProxyObject")]
	private sealed class TrimmableJavaProxyObject : Java.Lang.Object
	{
		static readonly ConditionalWeakTable<object, TrimmableJavaProxyObject> CachedValues = new ();

		private TrimmableJavaProxyObject (object value) => Value = value;

		// This class is not meant to be instantiated from the Java side, so make the parameterless constructor
		// private to prevent the generator from generating the default Java ctor.
		private TrimmableJavaProxyObject () => throw new UnreachableException ();

		public object Value { get; }

		public static TrimmableJavaProxyObject GetProxy (object value)
		{
			ArgumentNullException.ThrowIfNull (value);

			lock (CachedValues) {
				return CachedValues.GetOrAdd (value, static (value) => new TrimmableJavaProxyObject (value));
			}
		}
	}
}
