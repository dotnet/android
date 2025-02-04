// Originally from: https://github.com/dotnet/java-interop/blob/9b1d8781e8e322849d05efac32119c913b21c192/src/Java.Runtime.Environment/Java.Interop/ManagedValueManager.cs
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Android.Runtime;
using Java.Interop;

namespace NativeAOT;

internal class NativeAotValueManager : JniRuntime.JniValueManager
{
	const DynamicallyAccessedMemberTypes Constructors = DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors;

	readonly JniRuntime.JniTypeManager TypeManager;
	Dictionary<int, List<IJavaPeerable>>?   RegisteredInstances = new Dictionary<int, List<IJavaPeerable>>();

	public NativeAotValueManager(JniRuntime.JniTypeManager typeManager) =>
		TypeManager = typeManager;

	public override void WaitForGCBridgeProcessing ()
	{
	}

	public override void CollectPeers ()
	{
		if (RegisteredInstances == null)
			throw new ObjectDisposedException (nameof (NativeAotValueManager));

		var peers = new List<IJavaPeerable> ();

		lock (RegisteredInstances) {
			foreach (var ps in RegisteredInstances.Values) {
				foreach (var p in ps) {
					peers.Add (p);
				}
			}
			RegisteredInstances.Clear ();
		}
		List<Exception>? exceptions = null;
		foreach (var peer in peers) {
			try {
				peer.Dispose ();
			}
			catch (Exception e) {
				exceptions = exceptions ?? new List<Exception> ();
				exceptions.Add (e);
			}
		}
		if (exceptions != null)
			throw new AggregateException ("Exceptions while collecting peers.", exceptions);
	}

	public override void AddPeer (IJavaPeerable value)
	{
		if (RegisteredInstances == null)
			throw new ObjectDisposedException (nameof (NativeAotValueManager));

		var r = value.PeerReference;
		if (!r.IsValid)
			throw new ObjectDisposedException (value.GetType ().FullName);
		var o = PeekPeer (value.PeerReference);
		if (o != null)
			return;

		if (r.Type != JniObjectReferenceType.Global) {
			value.SetPeerReference (r.NewGlobalRef ());
			JniObjectReference.Dispose (ref r, JniObjectReferenceOptions.CopyAndDispose);
		}
		int key = value.JniIdentityHashCode;
		lock (RegisteredInstances) {
			List<IJavaPeerable>? peers;
			if (!RegisteredInstances.TryGetValue (key, out peers)) {
				peers = new List<IJavaPeerable> () {
					value,
				};
				RegisteredInstances.Add (key, peers);
				return;
			}

			for (int i = peers.Count - 1; i >= 0; i--) {
				var p   = peers [i];
				if (!JniEnvironment.Types.IsSameObject (p.PeerReference, value.PeerReference))
					continue;
				if (Replaceable (p)) {
					peers [i] = value;
				} else {
					WarnNotReplacing (key, value, p);
				}
				return;
			}
			peers.Add (value);
		}
	}

	static bool Replaceable (IJavaPeerable peer)
	{
		if (peer == null)
			return true;
		return peer.JniManagedPeerState.HasFlag (JniManagedPeerStates.Replaceable);
	}

	void WarnNotReplacing (int key, IJavaPeerable ignoreValue, IJavaPeerable keepValue)
	{
		Runtime.ObjectReferenceManager.WriteGlobalReferenceLine (
				"Warning: Not registering PeerReference={0} IdentityHashCode=0x{1} Instance={2} Instance.Type={3} Java.Type={4}; " +
				"keeping previously registered PeerReference={5} Instance={6} Instance.Type={7} Java.Type={8}.",
				ignoreValue.PeerReference.ToString (),
				key.ToString ("x"),
				RuntimeHelpers.GetHashCode (ignoreValue).ToString ("x"),
				ignoreValue.GetType ().FullName,
				JniEnvironment.Types.GetJniTypeNameFromInstance (ignoreValue.PeerReference),
				keepValue.PeerReference.ToString (),
				RuntimeHelpers.GetHashCode (keepValue).ToString ("x"),
				keepValue.GetType ().FullName,
				JniEnvironment.Types.GetJniTypeNameFromInstance (keepValue.PeerReference));
	}

	public override IJavaPeerable? PeekPeer (JniObjectReference reference)
	{
		if (RegisteredInstances == null)
			throw new ObjectDisposedException (nameof (NativeAotValueManager));

		if (!reference.IsValid)
			return null;

		int key = GetJniIdentityHashCode (reference);

		lock (RegisteredInstances) {
			List<IJavaPeerable>? peers;
			if (!RegisteredInstances.TryGetValue (key, out peers))
				return null;

			for (int i = peers.Count - 1; i >= 0; i--) {
				var p = peers [i];
				if (JniEnvironment.Types.IsSameObject (reference, p.PeerReference))
					return p;
			}
			if (peers.Count == 0)
				RegisteredInstances.Remove (key);
		}
		return null;
	}

	public override void RemovePeer (IJavaPeerable value)
	{
		if (RegisteredInstances == null)
			throw new ObjectDisposedException (nameof (NativeAotValueManager));

		if (value == null)
			throw new ArgumentNullException (nameof (value));

		int key = value.JniIdentityHashCode;
		lock (RegisteredInstances) {
			List<IJavaPeerable>? peers;
			if (!RegisteredInstances.TryGetValue (key, out peers))
				return;

			for (int i = peers.Count - 1; i >= 0; i--) {
				var p   = peers [i];
				if (object.ReferenceEquals (value, p)) {
					peers.RemoveAt (i);
				}
			}
			if (peers.Count == 0)
				RegisteredInstances.Remove (key);
		}
	}

	public override void FinalizePeer (IJavaPeerable value)
	{
		var h = value.PeerReference;
		var o = Runtime.ObjectReferenceManager;
		// MUST NOT use SafeHandle.ReferenceType: local refs are tied to a JniEnvironment
		// and the JniEnvironment's corresponding thread; it's a thread-local value.
		// Accessing SafeHandle.ReferenceType won't kill anything (so far...), but
		// instead it always returns JniReferenceType.Invalid.
		if (!h.IsValid || h.Type == JniObjectReferenceType.Local) {
			if (o.LogGlobalReferenceMessages) {
				o.WriteGlobalReferenceLine ("Finalizing PeerReference={0} IdentityHashCode=0x{1} Instance=0x{2} Instance.Type={3}",
						h.ToString (),
						value.JniIdentityHashCode.ToString ("x"),
						RuntimeHelpers.GetHashCode (value).ToString ("x"),
						value.GetType ().ToString ());
			}
			RemovePeer (value);
			value.SetPeerReference (new JniObjectReference ());
			value.Finalized ();
			return;
		}

		RemovePeer (value);
		if (o.LogGlobalReferenceMessages) {
			o.WriteGlobalReferenceLine ("Finalizing PeerReference={0} IdentityHashCode=0x{1} Instance=0x{2} Instance.Type={3}",
					h.ToString (),
					value.JniIdentityHashCode.ToString ("x"),
					RuntimeHelpers.GetHashCode (value).ToString ("x"),
					value.GetType ().ToString ());
		}
		value.SetPeerReference (new JniObjectReference ());
		JniObjectReference.Dispose (ref h);
		value.Finalized ();
	}

	public override void ActivatePeer (IJavaPeerable? self, JniObjectReference reference, ConstructorInfo cinfo, object?[]? argumentValues)
	{
		try {
			ActivateViaReflection (reference, cinfo, argumentValues);
		} catch (Exception e) {
			var m = string.Format ("Could not activate {{ PeerReference={0} IdentityHashCode=0x{1} Java.Type={2} }} for managed type '{3}'.",
					reference,
					GetJniIdentityHashCode (reference).ToString ("x"),
					JniEnvironment.Types.GetJniTypeNameFromInstance (reference),
					cinfo.DeclaringType?.FullName);
			Debug.WriteLine (m);

			throw new NotSupportedException (m, e);
		}
	}

	void ActivateViaReflection (JniObjectReference reference, ConstructorInfo cinfo, object?[]? argumentValues)
	{
		var declType  = cinfo.DeclaringType ?? throw new NotSupportedException ("Do not know the type to create!");

#pragma warning disable IL2072
		var self      = (IJavaPeerable) System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject (declType);
#pragma warning restore IL2072
		self.SetPeerReference (reference);

		cinfo.Invoke (self, argumentValues);
	}

	public override List<JniSurfacedPeerInfo> GetSurfacedPeers ()
	{
		if (RegisteredInstances == null)
			throw new ObjectDisposedException (nameof (NativeAotValueManager));

		lock (RegisteredInstances) {
			var peers = new List<JniSurfacedPeerInfo> (RegisteredInstances.Count);
			foreach (var e in RegisteredInstances) {
				foreach (var p in e.Value) {
					peers.Add (new JniSurfacedPeerInfo (e.Key, new WeakReference<IJavaPeerable> (p)));
				}
			}
			return peers;
		}
	}

	const   BindingFlags    ActivationConstructorBindingFlags   = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

	static  readonly    Type[]  XAConstructorSignature  = new Type [] { typeof (IntPtr), typeof (JniHandleOwnership) };

	protected override IJavaPeerable? TryCreatePeer (ref JniObjectReference reference, JniObjectReferenceOptions options, Type type)
	{
		var c = type.GetConstructor (ActivationConstructorBindingFlags, null, XAConstructorSignature, null);
		if (c != null) {
			var args = new object[] {
				reference.Handle,
				JniHandleOwnership.DoNotTransfer,
			};
			var p       = (IJavaPeerable) c.Invoke (args);
			JniObjectReference.Dispose (ref reference, options);
			return p;
		}
		return base.TryCreatePeer (ref reference, options, type);
	}
}
