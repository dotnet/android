// Originally from: https://github.com/dotnet/java-interop/blob/9b1d8781e8e322849d05efac32119c913b21c192/src/Java.Runtime.Environment/Java.Interop/ManagedValueManager.cs
using System;
using System.Collections.Concurrent;
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

sealed class JavaMarshalPeerManager : IDisposable
{
	readonly Dictionary<int, List<ReferenceTrackingHandle>> RegisteredInstances = new ();
	readonly ConcurrentQueue<IntPtr> CollectedContexts = new ();
	readonly string ownerName;

	bool disposed;

	public unsafe JavaMarshalPeerManager (string ownerName)
	{
		this.ownerName = ownerName;

		var javaMarshalPeerManagerHandle = new GCHandle<JavaMarshalPeerManager> (this);
		var mark_cross_references_ftn = RuntimeNativeMethods.clr_initialize_gc_bridge (
			GCHandle<JavaMarshalPeerManager>.ToIntPtr (javaMarshalPeerManagerHandle), &BridgeProcessingStarted, &BridgeProcessingFinished);
		JavaMarshal.Initialize (mark_cross_references_ftn);
	}

	public void Dispose ()
	{
		disposed = true;
	}

	void ThrowIfDisposed ()
	{
		if (disposed)
			throw new ObjectDisposedException (ownerName);
	}

	public void WaitForGCBridgeProcessing ()
	{
		// Intentionally empty. The Mono runtime's own implementation acknowledges this
		// pattern is fundamentally flawed (see FIXME in sgen-bridge.c): a thread that
		// passes the check can still race with bridge processing that starts immediately
		// after. The wait cannot prevent the race, only reduce its window. On CoreCLR,
		// JNI wrapper threads hold their own handle copies via JniObjectReference, so
		// they are not affected by the bridge swapping control_block handles.
	}

	public unsafe void CollectPeers ()
	{
		ThrowIfDisposed ();

		while (CollectedContexts.TryDequeue (out IntPtr contextPtr)) {
			Debug.Assert (contextPtr != IntPtr.Zero, "CollectedContexts should not contain null pointers.");
			HandleContext* context = (HandleContext*)contextPtr;
			
			lock (RegisteredInstances) {
				Remove (context);
			}

			HandleContext.Free (ref context);
		}

		void Remove (HandleContext* context)
		{
			int key = context->PeerIdentityHashCode;
			if (!RegisteredInstances.TryGetValue (key, out List<ReferenceTrackingHandle>? peers))
				return;

			for (int i = peers.Count - 1; i >= 0; i--) {
				var peer = peers [i];
				if (peer.BelongsToContext (context)) {
					peers.RemoveAt (i);
				}
			}

			if (peers.Count == 0) {
				RegisteredInstances.Remove (key);
			}
		}
	}

	public void AddPeer (IJavaPeerable value)
	{
		ThrowIfDisposed ();

		// Remove any collected contexts before adding a new peer.
		CollectPeers ();

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
				ReferenceTrackingHandle peer = peers [i];
				if (peer.Target is not IJavaPeerable target)
					continue;
				if (!JniEnvironment.Types.IsSameObject (target.PeerReference, value.PeerReference))
					continue;
				if (target.JniManagedPeerState.HasFlag (JniManagedPeerStates.Replaceable)) {
					peer.Dispose ();
					peers [i] = new ReferenceTrackingHandle (value);
				} else {
					WarnNotReplacing (key, value, target);
				}
				GC.KeepAlive (target);
				return;
			}

			peers.Add (new ReferenceTrackingHandle (value));
		}
	}

	void WarnNotReplacing (int key, IJavaPeerable ignoreValue, IJavaPeerable keepValue)
	{
		JniEnvironment.Runtime.ObjectReferenceManager.WriteGlobalReferenceLine (
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

	public IJavaPeerable? PeekPeer (JniObjectReference reference)
	{
		ThrowIfDisposed ();

		if (!reference.IsValid)
			return null;

		int key = JniEnvironment.References.GetIdentityHashCode (reference);

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

	public void RemovePeer (IJavaPeerable value)
	{
		ThrowIfDisposed ();

		// Remove any collected contexts before modifying RegisteredInstances
		CollectPeers ();

		if (value == null)
			throw new ArgumentNullException (nameof (value));

		lock (RegisteredInstances) {
			int key = value.JniIdentityHashCode;
			if (!RegisteredInstances.TryGetValue (key, out List<ReferenceTrackingHandle>? peers))
				return;

			for (int i = peers.Count - 1; i >= 0; i--) {
				ReferenceTrackingHandle peer = peers [i];
				IJavaPeerable? target = peer.Target;
				if (ReferenceEquals (value, target)) {
					peers.RemoveAt (i);
					peer.Dispose ();
				}
				GC.KeepAlive (target);
			}
			if (peers.Count == 0)
				RegisteredInstances.Remove (key);
		}
	}

	public void FinalizePeer (IJavaPeerable value)
	{
		var h = value.PeerReference;
		var o = JniEnvironment.Runtime.ObjectReferenceManager;
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

	public List<JniSurfacedPeerInfo> GetSurfacedPeers ()
	{
		ThrowIfDisposed ();

		// Remove any collected contexts before iterating over all the registered instances
		CollectPeers ();

		lock (RegisteredInstances) {
			var peers = new List<JniSurfacedPeerInfo> (RegisteredInstances.Count);
			foreach (var (identityHashCode, referenceTrackingHandles) in RegisteredInstances) {
				foreach (var peer in referenceTrackingHandles) {
					if (peer.Target is IJavaPeerable target) {
						peers.Add (new JniSurfacedPeerInfo (identityHashCode, new WeakReference<IJavaPeerable> (target)));
					}
				}
			}
			return peers;
		}
	}

	unsafe struct ReferenceTrackingHandle : IDisposable
	{
		WeakReference<IJavaPeerable?> _weakReference;
		HandleContext* _context;

		public bool BelongsToContext (HandleContext* context)
			=> _context == context;

		public ReferenceTrackingHandle (IJavaPeerable peer)
		{
			_context = HandleContext.Alloc (peer);
			_weakReference = new (peer);
		}

		public IJavaPeerable? Target
			=> _weakReference.TryGetTarget (out var target) ? target : null;

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

		int identityHashCode;
		IntPtr controlBlock;

		public int PeerIdentityHashCode => identityHashCode;
		public bool IsCollected
		{
			get
			{
				if (controlBlock == IntPtr.Zero)
					throw new InvalidOperationException ("HandleContext control block is not initialized.");

				return ((JniObjectReferenceControlBlock*) controlBlock)->handle == IntPtr.Zero;
			}
		}

		// This is an internal mirror of the Java.Interop.JniObjectReferenceControlBlock
		private struct JniObjectReferenceControlBlock
		{
			public IntPtr handle;
			public int handle_type;
			public IntPtr weak_handle;
			public int refs_added;
		}

		public static GCHandle GetAssociatedGCHandle (HandleContext* context)
		{
			lock (referenceTrackingHandles) {
				if (!referenceTrackingHandles.TryGetValue ((IntPtr) context, out GCHandle handle)) {
					throw new InvalidOperationException ("Unknown reference tracking handle.");
				}

				return handle;
			}
		}

		public static unsafe void EnsureAllContextsAreOurs (MarkCrossReferencesArgs* mcr)
		{
// This call site is reachable on all platforms. 'MarkCrossReferencesArgs.ComponentCount' is only supported on: 'android'.
// This call site is reachable on all platforms. 'MarkCrossReferencesArgs.Components' is only supported on: 'android'.
// This call site is reachable on all platforms. 'StronglyConnectedComponent.Count' is only supported on: 'android'.
// This call site is reachable on all platforms. 'StronglyConnectedComponent.Contexts' is only supported on: 'android'.
#pragma warning disable CA1416

			lock (referenceTrackingHandles) {
				for (nuint i = 0; i < mcr->ComponentCount; i++) {
					StronglyConnectedComponent component = mcr->Components [i];
					EnsureAllContextsInComponentAreOurs (component);
				}
			}

			static void EnsureAllContextsInComponentAreOurs (StronglyConnectedComponent component)
			{
				for (nuint i = 0; i < component.Count; i++) {
					EnsureContextIsOurs ((IntPtr)component.Contexts [i]);
				}
			}

			static void EnsureContextIsOurs (IntPtr context)
			{
				if (!referenceTrackingHandles.ContainsKey (context)) {
					throw new InvalidOperationException ("Unknown reference tracking handle.");
				}
			}

#pragma warning restore CA1416
		}

		public static HandleContext* Alloc (IJavaPeerable peer)
		{
			var context = (HandleContext*) NativeMemory.AllocZeroed (1, Size);
			if (context == null) {
				throw new OutOfMemoryException ("Failed to allocate memory for HandleContext.");
			}

			context->identityHashCode = peer.JniIdentityHashCode;
			context->controlBlock = peer.JniObjectReferenceControlBlock;

			GCHandle handle = JavaMarshal.CreateReferenceTrackingHandle (peer, context);
			lock (referenceTrackingHandles) {
				referenceTrackingHandles [(IntPtr) context] = handle;
			}

			return context;
		}

		public static void Free (ref HandleContext* context)
		{
			if (context == null) {
				return;
			}

			lock (referenceTrackingHandles) {
				referenceTrackingHandles.Remove ((IntPtr)context);
			}

			NativeMemory.Free (context);
			context = null;
		}
	}

	[UnmanagedCallersOnly]
	static unsafe void BridgeProcessingStarted (MarkCrossReferencesArgs* mcr)
	{
		if (mcr == null) {
			throw new ArgumentNullException (nameof (mcr), "MarkCrossReferencesArgs should never be null.");
		}

		HandleContext.EnsureAllContextsAreOurs (mcr);
	}

	[UnmanagedCallersOnly]
	static unsafe void BridgeProcessingFinished (IntPtr javaMarshalPeerManagerHandle, MarkCrossReferencesArgs* mcr)
	{
		if (mcr == null) {
			throw new ArgumentNullException (nameof (mcr), "MarkCrossReferencesArgs should never be null.");
		}

		JavaMarshalPeerManager instance = GCHandle<JavaMarshalPeerManager>.FromIntPtr (javaMarshalPeerManagerHandle).Target;

		ReadOnlySpan<GCHandle> handlesToFree = instance.ProcessCollectedContexts (mcr);


// This call site is reachable on all platforms. 'JavaMarshal.FinishCrossReferenceProcessing(MarkCrossReferencesArgs*, ReadOnlySpan<GCHandle>)' is only supported on: 'android'.
#pragma warning disable CA1416
		JavaMarshal.FinishCrossReferenceProcessing (mcr, handlesToFree);
#pragma warning restore CA1416
	}

	unsafe ReadOnlySpan<GCHandle> ProcessCollectedContexts (MarkCrossReferencesArgs* mcr)
	{
		List<GCHandle> handlesToFree = [];

// This call site is reachable on all platforms. 'MarkCrossReferencesArgs.ComponentCount' is only supported on: 'android'.
// This call site is reachable on all platforms. 'MarkCrossReferencesArgs.Components' is only supported on: 'android'.
// This call site is reachable on all platforms. 'StronglyConnectedComponent.Count' is only supported on: 'android'.
// This call site is reachable on all platforms. 'StronglyConnectedComponent.Contexts' is only supported on: 'android'.
#pragma warning disable CA1416

		for (int i = 0; (nuint)i < mcr->ComponentCount; i++) {
			StronglyConnectedComponent component = mcr->Components [i];
			for (int j = 0; (nuint)j < component.Count; j++) {
				ProcessContext ((HandleContext*)component.Contexts [j]);
			}
		}

#pragma warning restore CA1416

		void ProcessContext (HandleContext* context)
		{
			if (context == null) {
				throw new ArgumentNullException (nameof (context), "HandleContext should never be null.");
			}

			// Ignore contexts which were not collected
			if (!context->IsCollected) {
				return;
			}

			GCHandle handle = HandleContext.GetAssociatedGCHandle (context);

			// Note: modifying the RegisteredInstances dictionary while processing the collected contexts
			// is tricky and can lead to deadlocks, so we remember which contexts were collected and we will free
			// them later outside of the bridge processing loop.
			CollectedContexts.Enqueue ((IntPtr)context);

			// important: we must not free the handle before passing it to JavaMarshal.FinishCrossReferenceProcessing
			handlesToFree.Add (handle);
		}

		return CollectionsMarshal.AsSpan (handlesToFree);
	}

}

static class JavaMarshalValueManagerHelper
{
	const DynamicallyAccessedMemberTypes Constructors = DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors;

	[return: DynamicallyAccessedMembers (Constructors)]
	public static Type? ResolvePeerType ([DynamicallyAccessedMembers (Constructors)] Type? type)
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
	public static bool IsIncompatibleCast (
		string targetJniName,
		ref JniObjectReference reference,
		Type targetType)
	{
		var instanceClass = JniEnvironment.Types.GetObjectClass (reference);
		JniObjectReference targetClass = default;
		try {
			targetClass = JniEnvironment.Types.FindClass (targetJniName);

			if (!JniEnvironment.Types.IsAssignableFrom (instanceClass, targetClass)) {
				// TODO revisit this logging
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
}

[RequiresDynamicCode ("This value manager is reflection-backed and is not compatible with Native AOT.")]
[RequiresUnreferencedCode ("This value manager is reflection-backed and is not trimming-compatible.")]
sealed class CoreClrJavaMarshalValueManager : JniRuntime.ReflectionJniValueManager
{
	const DynamicallyAccessedMemberTypes Constructors = DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors;
	const BindingFlags ActivationConstructorBindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

	static readonly Type[] JIConstructorSignature  = [typeof (JniObjectReference).MakeByRefType (), typeof (JniObjectReferenceOptions)];
	static readonly Type[] XAConstructorSignature  = [typeof (IntPtr), typeof (JniHandleOwnership)];

	readonly JavaMarshalPeerManager peerManager = new (nameof (CoreClrJavaMarshalValueManager));

	protected override void Dispose (bool disposing)
	{
		peerManager.Dispose ();
		base.Dispose (disposing);
	}

	public override void WaitForGCBridgeProcessing ()
	{
		peerManager.WaitForGCBridgeProcessing ();
	}

	public override void CollectPeers ()
	{
		peerManager.CollectPeers ();
	}

	public override void AddPeer (IJavaPeerable value)
	{
		peerManager.AddPeer (value);
	}

	public override IJavaPeerable? PeekPeer (JniObjectReference reference)
	{
		return peerManager.PeekPeer (reference);
	}

	public override void RemovePeer (IJavaPeerable value)
	{
		peerManager.RemovePeer (value);
	}

	public override void FinalizePeer (IJavaPeerable value)
	{
		peerManager.FinalizePeer (value);
	}

	public override List<JniSurfacedPeerInfo> GetSurfacedPeers ()
	{
		return peerManager.GetSurfacedPeers ();
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

sealed class TrimmableTypeMapValueManager : JniRuntime.JniValueManager
{
	const DynamicallyAccessedMemberTypes Constructors = DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors;
	const JniObjectReferenceOptions DoNotRegisterTarget = (JniObjectReferenceOptions)(1 << 2);

	readonly JavaMarshalPeerManager peerManager = new (nameof (TrimmableTypeMapValueManager));

	protected override void Dispose (bool disposing)
	{
		peerManager.Dispose ();
		base.Dispose (disposing);
	}

	public override void WaitForGCBridgeProcessing ()
	{
		peerManager.WaitForGCBridgeProcessing ();
	}

	public override void CollectPeers ()
	{
		peerManager.CollectPeers ();
	}

	public override void AddPeer (IJavaPeerable value)
	{
		peerManager.AddPeer (value);
	}

	public override IJavaPeerable? PeekPeer (JniObjectReference reference)
	{
		return peerManager.PeekPeer (reference);
	}

	public override void RemovePeer (IJavaPeerable value)
	{
		peerManager.RemovePeer (value);
	}

	public override void FinalizePeer (IJavaPeerable value)
	{
		peerManager.FinalizePeer (value);
	}

	public override List<JniSurfacedPeerInfo> GetSurfacedPeers ()
	{
		return peerManager.GetSurfacedPeers ();
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
#pragma warning disable 8653
			return default (T);
#pragma warning restore 8653
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
#pragma warning disable 8653
			return default (T);
#pragma warning restore 8653
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

		if (targetType != null && typeof (IJavaPeerable).IsAssignableFrom (targetType)) {
			return CreatePeer (ref reference, options, targetType);
		}

		var transfer = ToJniHandleOwnership (reference, options);
		var value = JavaConvert.FromJniHandle (reference.Handle, transfer, targetType);
		if (transfer != JniHandleOwnership.DoNotTransfer) {
			reference = default;
		}
		return value;
	}

	protected override JniValueMarshaler GetValueMarshalerCore (Type type)
	{
		EnsureNotDisposed ();
		if (type == null) {
			throw new ArgumentNullException (nameof (type));
		}
		if (type.ContainsGenericParameters) {
			throw new ArgumentException ("Generic type definitions are not supported.", nameof (type));
		}

		if (type == typeof (bool))
			return TrimmableValueMarshaler<bool>.Instance;
		if (type == typeof (bool?))
			return TrimmableValueMarshaler<bool?>.Instance;
		if (type == typeof (byte))
			return TrimmableValueMarshaler<byte>.Instance;
		if (type == typeof (sbyte))
			return TrimmableValueMarshaler<sbyte>.Instance;
		if (type == typeof (sbyte?))
			return TrimmableValueMarshaler<sbyte?>.Instance;
		if (type == typeof (char))
			return TrimmableValueMarshaler<char>.Instance;
		if (type == typeof (char?))
			return TrimmableValueMarshaler<char?>.Instance;
		if (type == typeof (short))
			return TrimmableValueMarshaler<short>.Instance;
		if (type == typeof (short?))
			return TrimmableValueMarshaler<short?>.Instance;
		if (type == typeof (int))
			return TrimmableValueMarshaler<int>.Instance;
		if (type == typeof (int?))
			return TrimmableValueMarshaler<int?>.Instance;
		if (type == typeof (long))
			return TrimmableValueMarshaler<long>.Instance;
		if (type == typeof (long?))
			return TrimmableValueMarshaler<long?>.Instance;
		if (type == typeof (float))
			return TrimmableValueMarshaler<float>.Instance;
		if (type == typeof (float?))
			return TrimmableValueMarshaler<float?>.Instance;
		if (type == typeof (double))
			return TrimmableValueMarshaler<double>.Instance;
		if (type == typeof (double?))
			return TrimmableValueMarshaler<double?>.Instance;
		if (type == typeof (string))
			return TrimmableValueMarshaler<string>.Instance;
		if (type == typeof (object))
			return ObjectValueMarshaler;
		if (type == typeof (int[]))
			return TrimmableValueMarshaler<int[]>.Instance;
		if (type == typeof (IList<int>))
			return TrimmableValueMarshaler<IList<int>>.Instance;
		if (type == typeof (global::Java.Interop.JavaArray<int>))
			return TrimmableValueMarshaler<global::Java.Interop.JavaArray<int>>.Instance;
		if (type == typeof (JavaPrimitiveArray<int>))
			return TrimmableValueMarshaler<JavaPrimitiveArray<int>>.Instance;
		if (type == typeof (JavaInt32Array))
			return TrimmableValueMarshaler<JavaInt32Array>.Instance;
		if (type == typeof (IJavaPeerable) || typeof (IJavaPeerable).IsAssignableFrom (type))
			return PeerableValueMarshaler;

		return ObjectValueMarshaler;
	}

	protected override JniValueMarshaler<T> GetValueMarshalerCore<[DynamicallyAccessedMembers (Constructors)] T> ()
	{
		EnsureNotDisposed ();
		if (typeof (T) == typeof (object)) {
			return (JniValueMarshaler<T>)(object) ObjectValueMarshaler;
		}
		if (typeof (T) == typeof (IJavaPeerable)) {
			return (JniValueMarshaler<T>)(object) PeerableValueMarshaler;
		}
		return TrimmableValueMarshaler<T>.Instance;
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

	sealed class TrimmableValueMarshaler<[DynamicallyAccessedMembers (Constructors)] T> : JniValueMarshaler<T>
	{
		public static readonly TrimmableValueMarshaler<T> Instance = new ();

		public override bool IsJniValueType => IsPrimitiveJniValueType (typeof (T));

		public override Type MarshalType => IsJniValueType ? typeof (T) : base.MarshalType;

		[return: MaybeNull]
		public override T CreateGenericValue (
			ref JniObjectReference reference,
			JniObjectReferenceOptions options,
			[DynamicallyAccessedMembers (Constructors)]
			Type? targetType)
		{
			return JniEnvironment.Runtime.ValueManager.GetValue<T> (ref reference, options, targetType);
		}

		public override JniValueMarshalerState CreateGenericArgumentState ([MaybeNull] T value, ParameterAttributes synchronize = ParameterAttributes.In)
		{
			if (IsJniValueType) {
				return new JniValueMarshalerState (CreatePrimitiveArgumentValue (value));
			}
			return CreateGenericObjectReferenceArgumentState (value, synchronize);
		}

		public override JniValueMarshalerState CreateGenericObjectReferenceArgumentState ([MaybeNull] T value, ParameterAttributes synchronize)
		{
			if (value == null) {
				return new JniValueMarshalerState ();
			}
			if (TryCreateInt32ArrayArgumentState (value, synchronize, out var state)) {
				return state;
			}

			var handle = JavaConvert.ToLocalJniHandle (value);
			return handle == IntPtr.Zero
				? new JniValueMarshalerState ()
				: new JniValueMarshalerState (new JniObjectReference (handle, JniObjectReferenceType.Local));
		}

		public override void DestroyGenericArgumentState ([AllowNull] T value, ref JniValueMarshalerState state, ParameterAttributes synchronize)
		{
			if (TryDestroyInt32ArrayArgumentState (value, ref state, synchronize)) {
				return;
			}
			DisposeReferenceState (ref state);
		}

		static bool TryCreateInt32ArrayArgumentState ([MaybeNull] T value, ParameterAttributes synchronize, out JniValueMarshalerState state)
		{
			state = new JniValueMarshalerState ();

			if (value is not IList<int> list) {
				return false;
			}

			synchronize = GetCopyDirection (synchronize);
			var copyToJava = (synchronize & ParameterAttributes.In) == ParameterAttributes.In;
			var array = copyToJava
				? new JavaInt32Array (list)
				: new JavaInt32Array (list.Count);
			state = new JniValueMarshalerState (array);
			return true;
		}

		static bool TryDestroyInt32ArrayArgumentState ([AllowNull] T value, ref JniValueMarshalerState state, ParameterAttributes synchronize)
		{
			if (state.PeerableValue is not JavaInt32Array array) {
				return false;
			}

			synchronize = GetCopyDirection (synchronize);
			if ((synchronize & ParameterAttributes.Out) == ParameterAttributes.Out && value is IList<int> list) {
				if (value is int[] targetArray) {
					array.CopyTo (targetArray, 0);
				} else {
					int count = Math.Min (array.Length, list.Count);
					for (int i = 0; i < count; i++) {
						list [i] = array [i];
					}
				}
			}

			array.Dispose ();
			state = new JniValueMarshalerState ();
			return true;
		}

		static ParameterAttributes GetCopyDirection (ParameterAttributes value)
		{
			const ParameterAttributes inout = ParameterAttributes.In | ParameterAttributes.Out;
			if ((value & inout) != 0) {
				return value & inout;
			}
			return inout;
		}

		static bool IsPrimitiveJniValueType (Type type)
		{
			return type == typeof (bool) ||
				type == typeof (byte) ||
				type == typeof (sbyte) ||
				type == typeof (char) ||
				type == typeof (short) ||
				type == typeof (ushort) ||
				type == typeof (int) ||
				type == typeof (uint) ||
				type == typeof (long) ||
				type == typeof (ulong) ||
				type == typeof (float) ||
				type == typeof (double);
		}

		static JniArgumentValue CreatePrimitiveArgumentValue ([MaybeNull] T value)
		{
			return value switch {
				null => throw new ArgumentNullException (nameof (value), "Value cannot be null for primitive JNI value types."),
				bool v => new JniArgumentValue (v),
				byte v => new JniArgumentValue (v),
				sbyte v => new JniArgumentValue (v),
				char v => new JniArgumentValue (v),
				short v => new JniArgumentValue (v),
				ushort v => new JniArgumentValue (v),
				int v => new JniArgumentValue (v),
				uint v => new JniArgumentValue (v),
				long v => new JniArgumentValue (v),
				ulong v => new JniArgumentValue (v),
				float v => new JniArgumentValue (v),
				double v => new JniArgumentValue (v),
				_ => throw new NotSupportedException ($"Type '{typeof (T).AssemblyQualifiedName}' is not a JNI primitive value type."),
			};
		}
	}

	static void DisposeReferenceState (ref JniValueMarshalerState state)
	{
		var r = state.ReferenceValue;
		JniObjectReference.Dispose (ref r);
		state = new JniValueMarshalerState ();
	}
}
