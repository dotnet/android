using System;
using System.Collections.Generic;
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
	const string ExpressionRequiresUnreferencedCode = "System.Linq.Expression usage may trim away required code.";

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
				if (!typeMap.TryGetJniNameForManagedType (targetType, out var targetJniName)) {
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
			return default (T);
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
			return default (T);
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

		if (TryCreateJavaArrayWrapper (ref reference, options, targetType, out var arrayWrapper)) {
			return arrayWrapper;
		}

		if (targetType != null && typeof (IJavaPeerable).IsAssignableFrom (targetType)) {
			return CreatePeer (ref reference, options, targetType);
		}

		var transfer = ToJniHandleOwnership (reference, options);
		var value = JavaConvert.FromJniHandle (reference.Handle, transfer, GetValueConversionTargetType (targetType));
		if (transfer != JniHandleOwnership.DoNotTransfer) {
			reference = default;
		}
		return value;
	}

	[return: DynamicallyAccessedMembers (Constructors)]
	static Type? GetValueConversionTargetType ([DynamicallyAccessedMembers (Constructors)] Type? targetType)
	{
		if (targetType == typeof (sbyte?)) {
			return typeof (sbyte);
		}

		return targetType;
	}

	static bool TryCreateJavaArrayWrapper (
		ref JniObjectReference reference,
		JniObjectReferenceOptions options,
		[DynamicallyAccessedMembers (Constructors)]
		Type? targetType,
		[NotNullWhen (true)] out object? value)
	{
		if (targetType != null && TryCreatePrimitiveArrayWrapper (ref reference, options, targetType, out value)) {
			return true;
		}

		value = null;
		return false;
	}

	protected override JniValueMarshalerState CreateValueMarshalerStateCore (Type type, object? value, ParameterAttributes synchronize)
	{
		EnsureNotDisposed ();
		if (value == null) {
			return new JniValueMarshalerState ();
		}
		if (TrimmableValueMarshalerHelper.IsPrimitiveJniValueType (type)) {
			return new JniValueMarshalerState (TrimmableValueMarshalerHelper.CreatePrimitiveArgumentValue (value, type));
		}
		return CreateObjectReferenceValueMarshalerStateCore (type, value, synchronize);
	}

	protected override JniValueMarshalerState CreateValueMarshalerStateCore<[DynamicallyAccessedMembers (Constructors)] T> ([MaybeNull] T value, ParameterAttributes synchronize)
	{
		EnsureNotDisposed ();
		if (value == null) {
			return new JniValueMarshalerState ();
		}
		if (TrimmableValueMarshalerHelper.IsPrimitiveJniValueType (typeof (T))) {
			return new JniValueMarshalerState (TrimmableValueMarshalerHelper.CreatePrimitiveArgumentValue (value, typeof (T)));
		}
		return CreateObjectReferenceValueMarshalerStateCore (value, synchronize);
	}

	protected override JniValueMarshalerState CreateObjectReferenceValueMarshalerStateCore (Type type, object? value, ParameterAttributes synchronize)
	{
		EnsureNotDisposed ();
		if (value == null) {
			return new JniValueMarshalerState ();
		}
		if (TryCreatePrimitiveArrayArgumentState (value, synchronize, out var primitiveArrayState)) {
			return primitiveArrayState;
		}
		if (value is IJavaPeerable peerable) {
			return PeerableValueMarshaler.CreateObjectReferenceArgumentState (peerable, synchronize);
		}

		var handle = JavaConvert.ToLocalJniHandle (value);
		return handle == IntPtr.Zero
			? new JniValueMarshalerState ()
			: new JniValueMarshalerState (new JniObjectReference (handle, JniObjectReferenceType.Local));
	}

	protected override JniValueMarshalerState CreateObjectReferenceValueMarshalerStateCore<[DynamicallyAccessedMembers (Constructors)] T> ([MaybeNull] T value, ParameterAttributes synchronize)
	{
		EnsureNotDisposed ();
		if (value == null) {
			return new JniValueMarshalerState ();
		}
		if (TryCreatePrimitiveArrayArgumentState (value, synchronize, out var primitiveArrayState)) {
			return primitiveArrayState;
		}
		if (value is IJavaPeerable peerable) {
			return PeerableValueMarshaler.CreateObjectReferenceArgumentState (peerable, synchronize);
		}

		var handle = JavaConvert.ToLocalJniHandle (value);
		return handle == IntPtr.Zero
			? new JniValueMarshalerState ()
			: new JniValueMarshalerState (new JniObjectReference (handle, JniObjectReferenceType.Local));
	}

	protected override void DestroyValueMarshalerStateCore (Type type, object? value, ref JniValueMarshalerState state, ParameterAttributes synchronize)
	{
		EnsureNotDisposed ();
		if (TryDestroyPrimitiveArrayArgumentState (value, ref state, synchronize)) {
			return;
		}
		DisposeReferenceState (ref state);
	}

	protected override void DestroyValueMarshalerStateCore<[DynamicallyAccessedMembers (Constructors)] T> ([AllowNull] T value, ref JniValueMarshalerState state, ParameterAttributes synchronize)
	{
		EnsureNotDisposed ();
		if (TryDestroyPrimitiveArrayArgumentState (value, ref state, synchronize)) {
			return;
		}
		DisposeReferenceState (ref state);
	}

	protected override JniValueMarshaler GetValueMarshalerCore (Type type)
	{
		throw new NotSupportedException ($"{nameof (GetValueMarshalerCore)} should not be called in the trimmable typemap path.");
	}

	protected override JniValueMarshaler<T> GetValueMarshalerCore<[DynamicallyAccessedMembers (Constructors)] T> ()
	{
		throw new NotSupportedException ($"{nameof (GetValueMarshalerCore)} should not be called in the trimmable typemap path.");
	}

	static JniHandleOwnership ToJniHandleOwnership (JniObjectReference reference, JniObjectReferenceOptions options)
	{
		const JniObjectReferenceOptions DisposeSource = (JniObjectReferenceOptions)(1 << 1);
		if ((options & DisposeSource) != DisposeSource) {
			return JniHandleOwnership.DoNotTransfer;
		}
		return reference.Type switch {
			JniObjectReferenceType.Local => JniHandleOwnership.TransferLocalRef,
			JniObjectReferenceType.Global => JniHandleOwnership.TransferGlobalRef,
			_ => JniHandleOwnership.DoNotTransfer,
		};
	}

	static void DisposeReferenceState (ref JniValueMarshalerState state)
	{
		var r = state.ReferenceValue;
		JniObjectReference.Dispose (ref r);
		state = new JniValueMarshalerState ();
	}
}
