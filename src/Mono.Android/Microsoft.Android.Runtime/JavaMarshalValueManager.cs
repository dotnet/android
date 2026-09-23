using System;
using System.Collections.Concurrent;
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

	static readonly Type[] XAConstructorSignature = new Type [] { typeof (IntPtr), typeof (JniHandleOwnership) };
	static readonly Type[] JIConstructorSignature = new Type [] { typeof (JniObjectReference).MakeByRefType (), typeof (JniObjectReferenceOptions) };
	static readonly ConcurrentDictionary<Type, ActivationConstructor> ActivationConstructorCache = new ConcurrentDictionary<Type, ActivationConstructor> (1, 3);

	enum ActivationConstructorKind
	{
		Missing,
		XA,
		JI,
	}

	readonly record struct ActivationConstructor (ConstructorInfo? Constructor, ActivationConstructorKind Kind);

	// The GetOrAdd factory's key parameter cannot carry constructor-preservation annotations.
	readonly struct AnnotatedType
	{
		public AnnotatedType ([DynamicallyAccessedMembers (Constructors)] Type type)
		{
			Type = type;
		}

		[DynamicallyAccessedMembers (Constructors)]
		public Type Type { get; }
	}

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
		EnsureNotDisposed ();
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

	protected override object? CreateNonArrayListValue (
			ref JniObjectReference reference,
			JniObjectReferenceOptions options,
			Type targetType)
	{
		return JavaConvert.FromObjectReference (ref reference, options, targetType);
	}

	protected override bool TryConstructPeer (
			IJavaPeerable self,
			ref JniObjectReference reference,
			JniObjectReferenceOptions options,
			[DynamicallyAccessedMembers (Constructors)]
			Type type)
	{
		var activation = GetActivationConstructor (type);
		var c = activation.Constructor;
		if (c == null)
			return false;

		if (activation.Kind == ActivationConstructorKind.XA) {
			var args = new object[] {
				reference.Handle,
				JniHandleOwnership.DoNotTransfer,
			};
			c.Invoke (self, args);
			JniObjectReference.Dispose (ref reference, options);
			return true;
		}

		// Preserve ReflectionJniValueManager's JI fallback, including ref argument copy-back.
		var jiArgs = new object[] { reference, options };
		c.Invoke (self, jiArgs);
		reference = (JniObjectReference) jiArgs [0];
		JniObjectReference.Dispose (ref reference, options);
		return true;
	}

	static ActivationConstructor GetActivationConstructor ([DynamicallyAccessedMembers (Constructors)] Type type)
	{
		return ActivationConstructorCache.GetOrAdd (type,
				static (_, state) => {
					var constructor = state.Type.GetConstructor (ActivationConstructorBindingFlags, null, XAConstructorSignature, null);
					if (constructor != null)
						return new ActivationConstructor (constructor, ActivationConstructorKind.XA);

					constructor = state.Type.GetConstructor (ActivationConstructorBindingFlags, null, JIConstructorSignature, null);
					return new ActivationConstructor (
						constructor,
						constructor == null ? ActivationConstructorKind.Missing : ActivationConstructorKind.JI);
				}, new AnnotatedType (type));
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
