using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Android.Runtime;
using Java.Interop;

namespace Microsoft.Android.Runtime;

class JavaMarshalValueManager : AndroidReflectionJniValueManager
{
	const DynamicallyAccessedMemberTypes Constructors = DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors;
	const BindingFlags ActivationConstructorBindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

	static readonly Type[] XAConstructorSignature = new Type [] { typeof (IntPtr), typeof (JniHandleOwnership) };

	readonly JavaMarshalRegisteredPeers registeredPeers = JavaMarshalRegisteredPeers.Instance;

	bool disposed;

	protected override void Dispose (bool disposing)
	{
		disposed = true;
		base.Dispose (disposing);
	}

	void ThrowIfDisposed ()
	{
		if (disposed)
			throw new ObjectDisposedException (nameof (JavaMarshalValueManager));
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

	public override void ActivatePeer (JniObjectReference reference, [DynamicallyAccessedMembers (Constructors)] Type type, ConstructorInfo cinfo, object?[]? argumentValues)
	{
		if (RuntimeFeature.TrimmableTypeMap)
			throw new PlatformNotSupportedException ("Activating Java peers is not supported when TrimmableTypeMap is enabled.");

		base.ActivatePeer (reference, type, cinfo, argumentValues);
	}

	public override List<JniSurfacedPeerInfo> GetSurfacedPeers ()
	{
		return registeredPeers.GetSurfacedPeers ();
	}

	public override IJavaPeerable? CreatePeer (
			ref JniObjectReference reference,
			JniObjectReferenceOptions transfer,
			[DynamicallyAccessedMembers (Constructors)]
			Type? targetType)
	{
		ThrowIfDisposed ();

		if (!reference.IsValid) {
			return null;
		}

		if (RuntimeFeature.TrimmableTypeMap) {
			try {
				// Mirror legacy GetPeerType: callers commonly request universal
				// interfaces / boxes (IJavaPeerable, object, Exception) — map these
				// to a concrete peer type so the proxy lookup can succeed.
				var resolvedTargetType = ResolvePeerType (targetType);

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
				if (resolvedTargetType is not null &&
						IsIncompatibleCast (typeMap, ref reference, resolvedTargetType)) {
					return null;
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

		return base.CreatePeer (ref reference, transfer, targetType);
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

	/// <summary>
	/// Returns true when <paramref name="targetType"/>'s Java class is not assignable from
	/// <paramref name="reference"/>. Throws when <paramref name="targetType"/> has no usable mapping.
	/// </summary>
	static bool IsIncompatibleCast (
			TrimmableTypeMap typeMap,
			ref JniObjectReference reference,
			Type targetType)
	{
		if (!typeMap.TryGetJniNameForManagedType (targetType, out var targetJniName)) {
			throw new ArgumentException (
				$"Could not determine Java type corresponding to '{targetType.AssemblyQualifiedName}'.",
				nameof (targetType));
		}

		var instanceClass = JniEnvironment.Types.GetObjectClass (reference);
		JniObjectReference targetClass = default;
		try {
			try {
				targetClass = JniEnvironment.Types.FindClass (targetJniName);
			} catch (Java.Lang.ClassNotFoundException e) {
				throw new ArgumentException (
					$"Could not find Java class '{targetJniName}'.",
					nameof (targetType), e);
			}

			if (!JniEnvironment.Types.IsAssignableFrom (instanceClass, targetClass)) {
				// Bad cast: callers translate null to the expected result.
				return true;
			}
		} finally {
			JniObjectReference.Dispose (ref instanceClass);
			JniObjectReference.Dispose (ref targetClass);
		}

		// Compatible classes mean a proxy/activation gap.
		return false;
	}

	protected override bool TryConstructPeer (
			IJavaPeerable self,
			ref JniObjectReference reference,
			JniObjectReferenceOptions options,
			[DynamicallyAccessedMembers (Constructors)]
			Type type)
	{
		var c = type.GetConstructor (ActivationConstructorBindingFlags, null, XAConstructorSignature, null);
		if (c != null) {
			var args = new object[] {
				reference.Handle,
				JniHandleOwnership.DoNotTransfer,
			};
			c.Invoke (self, args);
			JniObjectReference.Dispose (ref reference, options);
			return true;
		}
		return base.TryConstructPeer (self, ref reference, options, type);
	}

	protected override bool TryUnboxPeerObject (IJavaPeerable value, [NotNullWhen (true)] out object? result)
	{
		var proxy = value as JavaProxyThrowable;
		if (proxy != null) {
			result = proxy.InnerException;
			return true;
		}
		return base.TryUnboxPeerObject (value, out result);
	}
}
