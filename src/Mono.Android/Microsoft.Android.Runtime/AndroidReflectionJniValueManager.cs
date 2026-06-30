using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using Android.Runtime;
using Java.Interop;

namespace Microsoft.Android.Runtime;

[RequiresDynamicCode ("This value manager is reflection-backed and is not compatible with Native AOT.")]
[RequiresUnreferencedCode ("This value manager is reflection-backed and is not trimming-compatible.")]
abstract class AndroidReflectionJniValueManager : JniRuntime.ReflectionJniValueManager
{
	protected const DynamicallyAccessedMemberTypes Constructors = DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors;
	const BindingFlags ActivationConstructorBindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

	static readonly Type[] JIConstructorSignature  = [typeof (JniObjectReference).MakeByRefType (), typeof (JniObjectReferenceOptions)];
	static readonly Type[] XAConstructorSignature  = [typeof (IntPtr), typeof (JniHandleOwnership)];

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

		targetType = ResolvePeerType (targetType) ?? typeof (global::Java.Interop.JavaObject);

		if (!typeof (IJavaPeerable).IsAssignableFrom (targetType)) {
			throw new ArgumentException ($"targetType `{targetType.AssemblyQualifiedName}` must implement IJavaPeerable!", nameof (targetType));
		}

		var targetSig = Runtime.TypeManager.GetTypeSignature (targetType);
		if (!targetSig.IsValid || targetSig.SimpleReference == null) {
			throw new ArgumentException ($"Could not determine Java type corresponding to `{targetType.AssemblyQualifiedName}`.", nameof (targetType));
		}

		if (IsIncompatibleCast (targetSig.SimpleReference, ref reference, targetType)) {
			return null;
		}

		var refClass = JniEnvironment.Types.GetObjectClass (reference);
		try {
			var peer = CreatePeerInstance (ref refClass, targetType, ref reference, transfer);
			if (peer == null) {
				throw new NotSupportedException (string.Format (CultureInfo.InvariantCulture, "Could not find an appropriate constructable wrapper type for Java type '{0}', targetType='{1}'.",
						JniEnvironment.Types.GetJniTypeNameFromInstance (reference), targetType));
			}
			peer.SetJniManagedPeerState (peer.JniManagedPeerState | JniManagedPeerStates.Replaceable);
			return peer;
		} finally {
			JniObjectReference.Dispose (ref refClass);
		}
	}

	[return: DynamicallyAccessedMembers (Constructors)]
	protected static Type? ResolvePeerType ([DynamicallyAccessedMembers (Constructors)] Type? type)
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
	protected static bool IsIncompatibleCast (
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
}
