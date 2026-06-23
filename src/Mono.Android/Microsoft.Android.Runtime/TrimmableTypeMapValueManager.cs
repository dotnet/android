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
	const JniObjectReferenceOptions DoNotRegisterTarget = (JniObjectReferenceOptions)(1 << 2);

	readonly JavaMarshalRegisteredPeers registeredPeers = new ();

	protected override void Dispose (bool disposing)
	{
		registeredPeers.Dispose ();
		base.Dispose (disposing);
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
		registeredPeers.CollectPeers ();
	}

	public override void AddPeer (IJavaPeerable value)
	{
		registeredPeers.AddPeer (value);
	}

	public override IJavaPeerable? PeekPeer (JniObjectReference reference)
	{
		return registeredPeers.PeekPeer (reference);
	}

	public override void RemovePeer (IJavaPeerable value)
	{
		registeredPeers.RemovePeer (value);
	}

	public override void FinalizePeer (IJavaPeerable value)
	{
		registeredPeers.FinalizePeer (value);
	}

	public override List<JniSurfacedPeerInfo> GetSurfacedPeers ()
	{
		return registeredPeers.GetSurfacedPeers ();
	}

	public override void ActivatePeer (JniObjectReference reference, [DynamicallyAccessedMembers (Constructors)] Type type, ConstructorInfo cinfo, object?[]? argumentValues)
	{
		throw new PlatformNotSupportedException ("Activating Java peers through the value manager is not supported when TrimmableTypeMap is enabled.");
	}

	protected override void ConstructPeerCore (
		IJavaPeerable peer,
		ref JniObjectReference reference,
		JniObjectReferenceOptions options)
	{
		if (peer == null)
			throw new ArgumentNullException (nameof (peer));

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
			// Mirror legacy GetPeerType: callers commonly request universal
			// interfaces / boxes (IJavaPeerable, object, Exception) — map these
			// to a concrete peer type so the proxy lookup can succeed.
			var resolvedTargetType = JavaMarshalValueManagerHelper.ResolvePeerType (targetType);

			var typeMap = TrimmableTypeMap.Instance;
			var peer = typeMap.CreateInstance (reference.Handle, resolvedTargetType);
			if (peer is not null) {
				return peer;
			}

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
				if (!typeMap.TryGetJniNameForManagedType (resolvedTargetType, out var targetJniName)) {
					throw new ArgumentException (
						$"Could not determine Java type corresponding to '{targetType.AssemblyQualifiedName}'.",
						nameof (targetType));
				}

				if (JavaMarshalValueManagerHelper.IsIncompatibleCast (targetJniName, ref reference, resolvedTargetType)) {
					return null;
				}
			}

			var targetName = resolvedTargetType?.AssemblyQualifiedName ?? "<null>";
			var javaType = JniEnvironment.Types.GetJniTypeNameFromInstance (reference);

			throw new NotSupportedException (
				$"No generated {nameof (JavaPeerProxy)} was found for Java type '{javaType}' " +
				$"with targetType '{targetName}' while {nameof (RuntimeFeature.TrimmableTypeMap)} is enabled. " +
				$"This indicates a missing trimmable typemap proxy or association and should be fixed in the generator.");
		} finally {
			JniObjectReference.Dispose (ref reference, transfer);
		}
	}

	[return: MaybeNull]
	protected override T CreateValueCore<[DynamicallyAccessedMembers (Constructors)] T> (
		ref JniObjectReference reference,
		JniObjectReferenceOptions options,
		[DynamicallyAccessedMembers (Constructors)]
		Type? targetType = null)
	{
		return GetValueCore<T> (ref reference, options, targetType);
	}

	protected override object? CreateValueCore (
		ref JniObjectReference reference,
		JniObjectReferenceOptions options,
		[DynamicallyAccessedMembers (Constructors)]
		Type? targetType = null)
	{
		return GetValueCore (ref reference, options, targetType);
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
				string.Format (CultureInfo.InvariantCulture, "Requested runtime '{0}' value of '{1}' is not compatible with requested compile-time type T of '{2}'.",
					nameof (targetType),
					targetType,
					typeof (T)),
				nameof (targetType));
		}

		var value = GetValueCore (ref reference, options, targetType ?? typeof (T));
		if (value is null) {
			return default;
		}
		return (T) value;
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

		if (targetType != null && TryCreatePrimitiveArrayWrapper (ref reference, options, targetType, out var arrayWrapper)) {
			return arrayWrapper;
		}

		if (targetType != null && typeof (IJavaPeerable).IsAssignableFrom (targetType)) {
			return CreatePeer (ref reference, options, targetType);
		}

		if (TryUnwrapNullable (targetType, out var innerType)) {
			targetType = innerType;
		}

		return JavaConvert.FromObjectReference (ref reference, options, targetType);
	}

	static bool TryUnwrapNullable (
		[DynamicallyAccessedMembers (Constructors)]
		Type? targetType,
		[NotNullWhen (true)]
		[DynamicallyAccessedMembers (Constructors)]
		out Type? innerType)
	{
		if (targetType is not null
			&& targetType.IsGenericType
			&& targetType.GetGenericTypeDefinition () == typeof (Nullable<>))
		{
			innerType = Nullable.GetUnderlyingType (targetType);
			return innerType is not null;
		}

		innerType = null;
		return false;
	}

	protected override bool TryUnboxPeerObject (IJavaPeerable value, [NotNullWhen (true)] out object? result)
	{
		if (value is TrimmableJavaProxyObject proxy) {
			result = proxy.Value;
			return true;
		}

		return base.TryUnboxPeerObject (value, out result);
	}

	protected override JniObjectReference CreateLocalObjectReferenceArgumentCore (
		[DynamicallyAccessedMembers (Constructors)]
		Type type,
		object? value)
	{
		if (value == null) {
			return new JniObjectReference ();
		}

		if (TryCreatePrimitiveArrayObjectReference (value, out var primitiveArrayReference)) {
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

	protected override JniValueMarshaler<T> GetValueMarshalerCore<[DynamicallyAccessedMembers (Constructors)] T> ()
		=> throw new NotSupportedException ($"{nameof (GetValueMarshalerCore)} should not be called in the trimmable typemap path.");

	[Register ("net/dot/jni/internal/TrimmableJavaProxyObject")]
	private sealed class TrimmableJavaProxyObject : Java.Lang.Object, IEquatable<TrimmableJavaProxyObject>
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

		public bool Equals (TrimmableJavaProxyObject? other) => Equals (Value, other?.Value);

		[Register ("hashCode", "()I", "GetGetHashCodeHandler")]
		public override int GetHashCode () => Value.GetHashCode ();

		[Register ("equals", "(Ljava/lang/Object;)Z", "GetEquals_Ljava_lang_Object_Handler")]
		public override bool Equals (Java.Lang.Object? obj) => Equals (Value, obj);

		[Register ("toString", "()Ljava/lang/String;", "GetToStringHandler")]
		public override string ToString () => Value.ToString () ?? "";
	}
}
