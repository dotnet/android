using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Android.Runtime;
using Java.Interop;

namespace Microsoft.Android.Runtime;

[RequiresDynamicCode ("This value manager is reflection-backed and can break in AOT scenarios.")]
[RequiresUnreferencedCode ("This value manager is reflection-backed and relies on custom trimming rules.")]
sealed class JavaMarshalValueManager : JniRuntime.ReflectionJniValueManager
{
	const DynamicallyAccessedMemberTypes Constructors = DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors;
	const BindingFlags ActivationConstructorBindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

	static readonly Type[] JIConstructorSignature = [typeof (JniObjectReference).MakeByRefType (), typeof (JniObjectReferenceOptions)];
	static readonly Type[] XAConstructorSignature = [typeof (IntPtr), typeof (JniHandleOwnership)];

	public JavaMarshalValueManager ()
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

		if (RuntimeFeature.TrimmableTypeMap) {
			try {
				var resolvedTargetType = GetPeerType (targetType);
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

		targetType = targetType ?? typeof (global::Java.Interop.JavaObject);
		targetType = GetPeerType (targetType);

		if (!typeof (IJavaPeerable).IsAssignableFrom (targetType)) {
			throw new ArgumentException ($"targetType `{targetType.AssemblyQualifiedName}` must implement IJavaPeerable!", nameof (targetType));
		}

		var targetSig = Runtime.TypeManager.GetTypeSignature (targetType);
		if (!targetSig.IsValid || targetSig.SimpleReference == null) {
			throw new ArgumentException ($"Could not determine Java type corresponding to `{targetType.AssemblyQualifiedName}`.", nameof (targetType));
		}

		var refClass = JniEnvironment.Types.GetObjectClass (reference);
		JniObjectReference targetClass;
		try {
			targetClass = JniEnvironment.Types.FindClass (targetSig.SimpleReference);
		} catch (Exception e) {
			JniObjectReference.Dispose (ref refClass);
			throw new ArgumentException ($"Could not find Java class `{targetSig.SimpleReference}`.",
					nameof (targetType),
					e);
		}

		if (!JniEnvironment.Types.IsAssignableFrom (refClass, targetClass)) {
			JniObjectReference.Dispose (ref refClass);
			JniObjectReference.Dispose (ref targetClass);
			return null;
		}

		JniObjectReference.Dispose (ref targetClass);

		var peer = CreatePeerInstance (ref refClass, targetType, ref reference, transfer);
		if (peer == null) {
			throw new NotSupportedException (string.Format (CultureInfo.InvariantCulture, "Could not find an appropriate constructable wrapper type for Java type '{0}', targetType='{1}'.",
					JniEnvironment.Types.GetJniTypeNameFromInstance (reference), targetType));
		}
		peer.SetJniManagedPeerState (peer.JniManagedPeerState | JniManagedPeerStates.Replaceable);
		return peer;
	}

	[return: DynamicallyAccessedMembers (Constructors)]
	static Type? GetPeerType ([DynamicallyAccessedMembers (Constructors)] Type? type)
	{
		if (type == typeof (object)) {
			return typeof (global::Java.Interop.JavaObject);
		}
		if (type == typeof (IJavaPeerable)) {
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

	IJavaPeerable? CreatePeerInstance (
		ref JniObjectReference klass,
		[DynamicallyAccessedMembers (Constructors)]
		Type targetType,
		ref JniObjectReference reference,
		JniObjectReferenceOptions transfer)
	{
		var jniTypeName = JniEnvironment.Types.GetJniTypeNameFromClass (klass);

		while (jniTypeName != null) {
			JniTypeSignature sig;
			if (!JniTypeSignature.TryParse (jniTypeName, out sig))
				return null;

			Type? type = GetTypeAssignableTo (sig, targetType);
			if (type != null) {
				var peer = TryCreatePeerInstance (ref reference, transfer, type);

				if (peer != null) {
					JniObjectReference.Dispose (ref klass);
					return peer;
				}
			}

			var super = JniEnvironment.Types.GetSuperclass (klass);
			jniTypeName = super.IsValid
				? JniEnvironment.Types.GetJniTypeNameFromClass (super)
				: null;

			JniObjectReference.Dispose (ref klass, JniObjectReferenceOptions.CopyAndDispose);
			klass = super;
		}
		JniObjectReference.Dispose (ref klass, JniObjectReferenceOptions.CopyAndDispose);

		return TryCreatePeerInstance (ref reference, transfer, targetType);

		Type? GetTypeAssignableTo (JniTypeSignature sig, Type targetType)
		{
			foreach (var t in Runtime.TypeManager.GetTypes (sig)) {
				if (targetType.IsAssignableFrom (t)) {
					return t;
				}
			}
			return null;
		}
	}

	IJavaPeerable? TryCreatePeerInstance (
			ref JniObjectReference reference,
			JniObjectReferenceOptions options,
			[DynamicallyAccessedMembers (Constructors)]
			Type type)
	{
		type = Runtime.TypeManager.GetInvokerType (type) ?? type;

		var self = (IJavaPeerable) RuntimeHelpers.GetUninitializedObject (type);
		self.SetJniManagedPeerState (JniManagedPeerStates.Replaceable | JniManagedPeerStates.Activatable);

		var constructed = false;
		try {
			constructed = TryConstructPeer (self, ref reference, options, type);
		} finally {
			if (!constructed) {
				GC.SuppressFinalize (self);
				self = null;
			}
		}
		return self;
	}

	bool TryConstructPeer (
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

		c = type.GetConstructor (ActivationConstructorBindingFlags, null, JIConstructorSignature, null);
		if (c != null) {
			var args = new object[] {
				reference,
				options,
			};
			c.Invoke (self, args);
			reference = (JniObjectReference) args [0];
			return true;
		}		

		return false;
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
