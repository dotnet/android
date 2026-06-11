using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using Java.Interop;

namespace Microsoft.Android.Runtime;

abstract class AndroidReflectionJniValueManager : JniRuntime.ReflectionJniValueManager
{
	const DynamicallyAccessedMemberTypes Constructors = DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors;

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
	static Type GetPeerType ([DynamicallyAccessedMembers (Constructors)] Type type)
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
			if (!JniTypeSignature.TryParse (jniTypeName, out sig)) {
				return null;
			}

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

		[return: DynamicallyAccessedMembers (Constructors)]
		Type? GetTypeAssignableTo (JniTypeSignature sig, Type targetType)
		{
			foreach (var t in Runtime.TypeManager.GetReflectionConstructibleTypes (sig)) {
				if (targetType.IsAssignableFrom (t.Type)) {
					return t.Type;
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

	static readonly Type ByRefJniObjectReference = typeof (JniObjectReference).MakeByRefType ();
	static readonly Type[] JIConstructorSignature = new Type [] { ByRefJniObjectReference, typeof (JniObjectReferenceOptions) };

	protected virtual bool TryConstructPeer (
			IJavaPeerable self,
			ref JniObjectReference reference,
			JniObjectReferenceOptions options,
			[DynamicallyAccessedMembers (Constructors)]
			Type type)
	{
		var c = type.GetConstructor (BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, JIConstructorSignature, null);
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
