using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using Android.Runtime;
using Java.Interop;

namespace Microsoft.Android.Runtime;

[RequiresDynamicCode ("This value manager is reflection-backed and is not compatible with Native AOT.")]
[RequiresUnreferencedCode ("This value manager is reflection-backed and is not trimming-compatible.")]
sealed class CoreClrJavaMarshalValueManager : JniRuntime.ReflectionJniValueManager
{
	const DynamicallyAccessedMemberTypes Constructors = DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors;
	const BindingFlags ActivationConstructorBindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

	static readonly Type[] JIConstructorSignature  = [typeof (JniObjectReference).MakeByRefType (), typeof (JniObjectReferenceOptions)];
	static readonly Type[] XAConstructorSignature  = [typeof (IntPtr), typeof (JniHandleOwnership)];

	static JniMethodInfo? s_classGetInterfacesMethod;

	public CoreClrJavaMarshalValueManager ()
	{
		JavaMarshalRegisteredPeers.InitializeIfNeeded ();
	}

	protected override void Dispose (bool disposing)
	{
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

		targetType = JavaMarshalValueManagerHelper.ResolvePeerType (targetType) ?? typeof (global::Java.Interop.JavaObject);

		if (!typeof (IJavaPeerable).IsAssignableFrom (targetType)) {
			throw new ArgumentException ($"targetType `{targetType.AssemblyQualifiedName}` must implement IJavaPeerable!", nameof (targetType));
		}

		var targetSig = Runtime.TypeManager.GetTypeSignature (targetType);
		if (!targetSig.IsValid || targetSig.SimpleReference == null) {
			throw new ArgumentException ($"Could not determine Java type corresponding to `{targetType.AssemblyQualifiedName}`.", nameof (targetType));
		}

		if (JavaMarshalValueManagerHelper.IsIncompatibleCast (targetSig.SimpleReference, ref reference, targetType)) {
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

			// The superclass walk above never inspects the Java interfaces a class
			// implements. When the requested targetType is itself an interface, the
			// concrete Java class (e.g. an anonymous class returned through a base
			// interface signature) may only advertise a more-derived interface, so we
			// must enumerate the class's interfaces to find the most-derived peer.
			if (type == null && targetType.IsInterface) {
				type = GetInterfaceTypeAssignableTo (klass, targetType);
			}

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

		// Recursively walks the Java interfaces declared on `klass` (and their
		// super-interfaces) looking for a registered .NET type assignable to
		// `targetType`. `Class.getInterfaces ()` only returns directly declared
		// interfaces, so we must recurse to cover transitive ones. Directly
		// declared interfaces are checked before their super-interfaces, so the
		// most-derived match is preferred. The `klass` reference is owned by the
		// caller and is not disposed here.
		Type? GetInterfaceTypeAssignableTo (JniObjectReference klass, Type targetType)
		{
			var interfaces = JniEnvironment.InstanceMethods.CallObjectMethod (klass, GetClassGetInterfacesMethod ());
			try {
				if (!interfaces.IsValid) {
					return null;
				}

				int count = JniEnvironment.Arrays.GetArrayLength (interfaces);
				for (int i = 0; i < count; i++) {
					var iface = JniEnvironment.Arrays.GetObjectArrayElement (interfaces, i);
					try {
						var ifaceName = JniEnvironment.Types.GetJniTypeNameFromClass (iface);
						if (ifaceName != null && JniTypeSignature.TryParse (ifaceName, out var ifaceSig)) {
							var type = GetTypeAssignableTo (ifaceSig, targetType);
							if (type != null) {
								return type;
							}
						}

						var result = GetInterfaceTypeAssignableTo (iface, targetType);
						if (result != null) {
							return result;
						}
					} finally {
						JniObjectReference.Dispose (ref iface);
					}
				}
			} finally {
				JniObjectReference.Dispose (ref interfaces);
			}

			return null;
		}
	}

	static JniMethodInfo GetClassGetInterfacesMethod ()
	{
		var method = s_classGetInterfacesMethod;
		if (method != null) {
			return method;
		}

		var classClass = JniEnvironment.Types.FindClass ("java/lang/Class");
		try {
			method = JniEnvironment.InstanceMethods.GetMethodID (classClass, "getInterfaces", "()[Ljava/lang/Class;");
		} finally {
			JniObjectReference.Dispose (ref classClass);
		}

		var previous = Interlocked.CompareExchange (ref s_classGetInterfacesMethod, method, null);
		return previous ?? method;
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
