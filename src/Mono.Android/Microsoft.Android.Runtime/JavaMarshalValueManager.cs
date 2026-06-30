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

	static readonly Type[] XAConstructorSignature = new Type [] { typeof (IntPtr), typeof (JniHandleOwnership) };

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

	public override void ActivatePeer (JniObjectReference reference, Type type, ConstructorInfo cinfo, object?[]? argumentValues)
	{
		if (RuntimeFeature.TrimmableTypeMap)
			throw new PlatformNotSupportedException ("Activating Java peers is not supported when TrimmableTypeMap is enabled.");

		base.ActivatePeer (reference, type, cinfo, argumentValues);
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

		return base.CreatePeer (ref reference, transfer, targetType);
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
