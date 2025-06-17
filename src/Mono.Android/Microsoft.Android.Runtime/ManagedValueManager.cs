// Originally from: https://github.com/dotnet/java-interop/blob/9b1d8781e8e322849d05efac32119c913b21c192/src/Java.Runtime.Environment/Java.Interop/ManagedValueManager.cs
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Java;
using System.Threading;
using Android.Runtime;
using Java.Interop;

namespace Microsoft.Android.Runtime;

class ManagedValueManager : JniRuntime.JniValueManager
{
	const DynamicallyAccessedMemberTypes Constructors = DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors;

	Dictionary<int, List<ReferenceTrackingHandle>>? RegisteredInstances = new ();

	static Lazy<ManagedValueManager> s_instance = new (() => new ManagedValueManager ());
	public static ManagedValueManager GetOrCreateInstance () => s_instance.Value;

	unsafe ManagedValueManager ()
	{
		// There can only be one instance of ManagedValueManager because we can call JavaMarshal.Initialize only once.
		var mark_cross_references_ftn = RuntimeNativeMethods.clr_initialize_gc_bridge (&BridgeProcessingStarted, &BridgeProcessingFinished);
		JavaMarshal.Initialize (mark_cross_references_ftn);
	}

	public override void WaitForGCBridgeProcessing ()
	{
		AndroidRuntimeInternal.WaitForBridgeProcessing ();
	}

	public override void CollectPeers ()
	{
		GC.Collect ();
	}

	public override void AddPeer (IJavaPeerable value)
	{
		if (RegisteredInstances == null)
			throw new ObjectDisposedException (nameof (ManagedValueManager));

		var r = value.PeerReference;
		if (!r.IsValid)
			throw new ObjectDisposedException (value.GetType ().FullName);

		if (r.Type != JniObjectReferenceType.Global) {
			value.SetPeerReference (r.NewGlobalRef ());
			JniObjectReference.Dispose (ref r, JniObjectReferenceOptions.CopyAndDispose);
		}
		int key = value.JniIdentityHashCode;
		lock (RegisteredInstances) {
			List<ReferenceTrackingHandle>? peers;
			if (!RegisteredInstances.TryGetValue (key, out peers)) {
				peers = [new ReferenceTrackingHandle (value)];
				RegisteredInstances.Add (key, peers);
				return;
			}

			for (int i = peers.Count - 1; i >= 0; i--) {
				var p   = peers [i];
				if (p.Target is not IJavaPeerable peer)
					continue;
				if (!JniEnvironment.Types.IsSameObject (peer.PeerReference, value.PeerReference))
					continue;
				if (peer.JniManagedPeerState.HasFlag (JniManagedPeerStates.Replaceable)) {
					p.Dispose ();
					peers [i] = new ReferenceTrackingHandle (value);
					GC.KeepAlive (peer);
				} else {
					WarnNotReplacing (key, value, peer);
				}
				return;
			}

			peers.Add (new ReferenceTrackingHandle (value));
		}
	}

	void WarnNotReplacing (int key, IJavaPeerable ignoreValue, IJavaPeerable keepValue)
	{
		Runtime.ObjectReferenceManager.WriteGlobalReferenceLine (
				"Warning: Not registering PeerReference={0} IdentityHashCode=0x{1} Instance={2} Instance.Type={3} Java.Type={4}; " +
				"keeping previously registered PeerReference={5} Instance={6} Instance.Type={7} Java.Type={8}.",
				ignoreValue.PeerReference.ToString (),
				key.ToString ("x", CultureInfo.InvariantCulture),
				RuntimeHelpers.GetHashCode (ignoreValue).ToString ("x", CultureInfo.InvariantCulture),
				ignoreValue.GetType ().FullName,
				JniEnvironment.Types.GetJniTypeNameFromInstance (ignoreValue.PeerReference),
				keepValue.PeerReference.ToString (),
				RuntimeHelpers.GetHashCode (keepValue).ToString ("x", CultureInfo.InvariantCulture),
				keepValue.GetType ().FullName,
				JniEnvironment.Types.GetJniTypeNameFromInstance (keepValue.PeerReference));
	}

	public override IJavaPeerable? PeekPeer (JniObjectReference reference)
	{
		if (RegisteredInstances == null)
			throw new ObjectDisposedException (nameof (ManagedValueManager));

		if (!reference.IsValid)
			return null;

		int key = GetJniIdentityHashCode (reference);

		lock (RegisteredInstances) {
			if (!RegisteredInstances.TryGetValue (key, out List<ReferenceTrackingHandle>? peers))
				return null;

			for (int i = peers.Count - 1; i >= 0; i--) {
				if (peers [i].Target is IJavaPeerable peer
					&& JniEnvironment.Types.IsSameObject (reference, peer.PeerReference))
				{
					return peer;
				}
			}

			if (peers.Count == 0)
				RegisteredInstances.Remove (key);
		}
		return null;
	}

	public override void RemovePeer (IJavaPeerable value)
	{
		if (RegisteredInstances == null)
			throw new ObjectDisposedException (nameof (ManagedValueManager));

		if (value == null)
			throw new ArgumentNullException (nameof (value));

		lock (RegisteredInstances) {
			RemoveRegisteredInstance (value, disposeReferenceTrackingHandle: true);
		}
	}

	private void RemoveRegisteredInstance (IJavaPeerable target, bool disposeReferenceTrackingHandle)
	{
		int key = target.JniIdentityHashCode;
		if (!RegisteredInstances.TryGetValue (key, out List<ReferenceTrackingHandle>? peers))
			return;

		for (int i = peers.Count - 1; i >= 0; i--) {
			var peer = peers [i];
			if (object.ReferenceEquals (target, peer.Target)) {
				peers.RemoveAt (i);
				if (disposeReferenceTrackingHandle) {
					peer.Dispose ();
				}
			}
		}
		if (peers.Count == 0)
			RegisteredInstances.Remove (key);
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
						value.JniIdentityHashCode.ToString ("x", CultureInfo.InvariantCulture),
						RuntimeHelpers.GetHashCode (value).ToString ("x", CultureInfo.InvariantCulture),
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
					value.JniIdentityHashCode.ToString ("x", CultureInfo.InvariantCulture),
					RuntimeHelpers.GetHashCode (value).ToString ("x", CultureInfo.InvariantCulture),
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
			var m = string.Format (
					CultureInfo.InvariantCulture,
					"Could not activate {{ PeerReference={0} IdentityHashCode=0x{1} Java.Type={2} }} for managed type '{3}'.",
					reference,
					GetJniIdentityHashCode (reference).ToString ("x", CultureInfo.InvariantCulture),
					JniEnvironment.Types.GetJniTypeNameFromInstance (reference),
					cinfo.DeclaringType?.FullName);
			Debug.WriteLine (m);

			throw new NotSupportedException (m, e);
		}
	}

	void ActivateViaReflection (JniObjectReference reference, ConstructorInfo cinfo, object?[]? argumentValues)
	{
		var declType  = GetDeclaringType (cinfo);

#pragma warning disable IL2072
		var self      = (IJavaPeerable) System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject (declType);
#pragma warning restore IL2072
		self.SetPeerReference (reference);

		cinfo.Invoke (self, argumentValues);

		[UnconditionalSuppressMessage ("Trimming", "IL2073", Justification = "🤷‍♂️")]
		[return: DynamicallyAccessedMembers (Constructors)]
		Type GetDeclaringType (ConstructorInfo cinfo) =>
			cinfo.DeclaringType ?? throw new NotSupportedException ("Do not know the type to create!");
	}

	public override List<JniSurfacedPeerInfo> GetSurfacedPeers ()
	{
		if (RegisteredInstances == null)
			throw new ObjectDisposedException (nameof (ManagedValueManager));

		lock (RegisteredInstances) {
			var peers = new List<JniSurfacedPeerInfo> (RegisteredInstances.Count);
			foreach (var e in RegisteredInstances) {
				foreach (var p in e.Value) {
					if (p.Target is IJavaPeerable peer) {
						peers.Add (new JniSurfacedPeerInfo (e.Key, new WeakReference<IJavaPeerable> (peer)));
					}
				}
			}
			return peers;
		}
	}

	unsafe struct ReferenceTrackingHandle : IDisposable
	{
		private WeakReference<IJavaPeerable> _weakReference;
		private HandleContext* _context;

		public ReferenceTrackingHandle (IJavaPeerable peer)
		{
			_context = HandleContext.Alloc (peer);
			_weakReference = new WeakReference<IJavaPeerable> (peer);
		}

		public IJavaPeerable? Target => _weakReference.TryGetTarget (out var target) ? target : null;

		public void Dispose ()
		{
			if (_context == null)
				return;

			IJavaPeerable? target = Target;

			GCHandle handle = HandleContext.GetAssociatedGCHandle (_context);
			HandleContext.Free (ref _context);
			_weakReference.SetTarget (null);
			if (handle.IsAllocated) {
				handle.Free ();
			}

			// Make sure the target is not collected before we finish disposing
			GC.KeepAlive (target);
		}
	}

	[StructLayout (LayoutKind.Sequential)]
	unsafe struct HandleContext
	{
		static readonly nuint Size = (nuint)Marshal.SizeOf<HandleContext> ();
		static readonly Dictionary<IntPtr, GCHandle> referenceTrackingHandles = new ();

		IntPtr controlBlock;

		public bool IsCollected => controlBlock == IntPtr.Zero;

		public static GCHandle GetAssociatedGCHandle (HandleContext* context)
		{
			lock (referenceTrackingHandles)
			{
				if (!referenceTrackingHandles.TryGetValue ((IntPtr)context, out GCHandle handle)) {
					throw new InvalidOperationException ("Unknown reference tracking handle.");
				}

				return handle;
			}
		}

		public static HandleContext* Alloc (IJavaPeerable peer)
		{
			var context = (HandleContext*)NativeMemory.AllocZeroed (1, Size);
			if (context == null) {
				throw new OutOfMemoryException ("Failed to allocate memory for HandleContext.");
			}

			context->controlBlock = peer.JniObjectReferenceControlBlock;

			GCHandle handle = JavaMarshal.CreateReferenceTrackingHandle (peer, context);
			lock (referenceTrackingHandles) {
				referenceTrackingHandles [(IntPtr)context] = handle;
			}

			return context;
		}

		public static void Free (ref HandleContext* context)
		{
			if (context != null) {
				NativeMemory.Free (context);
				context = null;
			}
		}
	}

	[UnmanagedCallersOnly]
	static void BridgeProcessingStarted ()
	{
		AndroidRuntimeInternal.BridgeProcessing = true;
	}

	[UnmanagedCallersOnly]
	static unsafe void BridgeProcessingFinished (MarkCrossReferencesArgs* mcr)
	{
		Trace.Assert (mcr != null, "Bridge processing was not started before finishing it.");

		ReadOnlySpan<GCHandle> handlesToFree = ProcessCollectedContexts (mcr);
		JavaMarshal.FinishCrossReferenceProcessing (mcr, handlesToFree);

		AndroidRuntimeInternal.BridgeProcessing = false;
	}

	static unsafe ReadOnlySpan<GCHandle> ProcessCollectedContexts (MarkCrossReferencesArgs* mcr)
	{
		List<GCHandle> handlesToFree = [];
		ManagedValueManager instance = GetOrCreateInstance ();

		lock (instance.RegisteredInstances) {
			for (int i = 0; (nuint)i < mcr->ComponentCount; i++) {
				ProcessComponent (mcr->Components [i]);
			}
		}

		void ProcessComponent (StronglyConnectedComponent component)
		{
			for (int j = 0; (nuint)j < component.Count; j++) {
				ProcessContext ((HandleContext*)component.Contexts [j]);
			}
		}

		void ProcessContext (HandleContext* context)
		{
			Trace.Assert (context != null, "Context should never be null.");

			// ignore contexts which were not collected
			if (!context->IsCollected) {
				return;
			}

			// ignore contexts which were not allocated by the ManagedValueManager
			GCHandle handle = HandleContext.GetAssociatedGCHandle (context);
			IJavaPeerable peer = (IJavaPeerable)handle.Target;
			instance.RemoveRegisteredInstance (peer, disposeReferenceTrackingHandle: false);
			HandleContext.Free (ref context);
			handlesToFree.Add (handle);
		}

		return CollectionsMarshal.AsSpan (handlesToFree);
	}

	const BindingFlags ActivationConstructorBindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

	static readonly Type[] XAConstructorSignature = new Type[] { typeof (IntPtr), typeof (JniHandleOwnership) };

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

	protected override bool TryUnboxPeerObject (IJavaPeerable value, [NotNullWhen (true)]out object? result)
	{
		if (value is JavaProxyThrowable proxy) {
			result  = proxy.InnerException;
			return true;
		}
		return base.TryUnboxPeerObject (value, out result);
	}
}
