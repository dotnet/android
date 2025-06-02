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

	Dictionary<int, List<GCHandle>>?   RegisteredInstances = new ();

	internal unsafe ManagedValueManager ()
	{
		var mark_cross_references_ftn = RuntimeNativeMethods.clr_initialize_gc_bridge (
			&BridgeProcessingStarted,
			&CollectGCHandles,
			&BridgeProcessingFinished);
		JavaMarshal.Initialize (mark_cross_references_ftn);
	}

	public override void WaitForGCBridgeProcessing()
	{
		// AndroidRuntimeInternal.WaitForGCBridgeProcessing(); // TODO
	}

	public override void CollectPeers ()
	{
		if (RegisteredInstances == null)
			throw new ObjectDisposedException (nameof (ManagedValueManager));

		var peers = new List<GCHandle> ();

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
				if (peer.Target is IDisposable disposable)
					disposable.Dispose ();
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
			List<GCHandle>? peers;
			if (!RegisteredInstances.TryGetValue (key, out peers)) {
				peers = new List<GCHandle> () {
					CreateReferenceTrackingHandle (value)
				};
				RegisteredInstances.Add (key, peers);
				return;
			}

			for (int i = peers.Count - 1; i >= 0; i--) {
				var p   = peers [i];
				if (p.Target is not IJavaPeerable peer)
					continue;
				if (!JniEnvironment.Types.IsSameObject (peer.PeerReference, value.PeerReference))
					continue;
				if (Replaceable (p)) {
					peers [i] = CreateReferenceTrackingHandle (value);
				} else {
					WarnNotReplacing (key, value, peer);
				}
				return;
			}
			peers.Add (CreateReferenceTrackingHandle (value));
		}
	}

	static bool Replaceable (GCHandle handle)
	{
		if (handle.Target is not IJavaPeerable peer)
			return true;
		return peer.JniManagedPeerState.HasFlag (JniManagedPeerStates.Replaceable);
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
			List<GCHandle>? peers;
			if (!RegisteredInstances.TryGetValue (key, out peers))
				return null;

			for (int i = peers.Count - 1; i >= 0; i--) {
				var p = peers [i];
				if (p.Target is IJavaPeerable peer && JniEnvironment.Types.IsSameObject (reference, peer.PeerReference))
					return peer;
			}
			if (peers.Count == 0)
				RegisteredInstances.Remove (key);
		}
		return null;
	}

	private GCHandle PeekPeerHandle (JniObjectReference reference)
	{
		if (RegisteredInstances == null)
			throw new ObjectDisposedException (nameof (ManagedValueManager));

		if (!reference.IsValid)
			return default;

		int key = GetJniIdentityHashCode (reference);

		lock (RegisteredInstances) {
			List<GCHandle>? peers;
			if (!RegisteredInstances.TryGetValue (key, out peers))
				return default;

			for (int i = peers.Count - 1; i >= 0; i--) {
				var p = peers [i];
				if (p.Target is IJavaPeerable peer && JniEnvironment.Types.IsSameObject (reference, peer.PeerReference))
					return p;
			}
			if (peers.Count == 0)
				RegisteredInstances.Remove (key);
		}
		return default;
	}

	public override void RemovePeer (IJavaPeerable value)
	{
		if (RegisteredInstances == null)
			throw new ObjectDisposedException (nameof (ManagedValueManager));

		if (value == null)
			throw new ArgumentNullException (nameof (value));

		int key = value.JniIdentityHashCode;
		lock (RegisteredInstances) {
			List<GCHandle>? peers;
			if (!RegisteredInstances.TryGetValue (key, out peers))
				return;

			for (int i = peers.Count - 1; i >= 0; i--) {
				var p   = peers [i];
				if (object.ReferenceEquals (value, p.Target)) {
					peers.RemoveAt (i);
					FreeHandle (p);
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
					if (p.Target is not IJavaPeerable peer)
						continue;
					peers.Add (new JniSurfacedPeerInfo (e.Key, new WeakReference<IJavaPeerable> (peer)));
				}
			}
			return peers;
		}
	}

	// private unsafe struct HandleContext
	// {
	// 	public IntPtr ControlBlock;
	// 	public IntPtr Handle;

	// 	public static HandleContext* Alloc(IntPtr controlBlock)
	// 	{
	// 		var size = (uint)Marshal.SizeOf<HandleContext>();
	// 		var ctx = (HandleContext*)NativeMemory.AllocZeroed(1, size);
	// 		ctx->ControlBlock = controlBlock;
	// 		return ctx;
	// 	}

	// 	public static void Free(HandleContext* ctx)
	// 	{
	// 		if (ctx->ControlBlock != IntPtr.Zero) {
	// 			NativeMemory.Free((void*)ctx->ControlBlock);
	// 		}

	// 		NativeMemory.Free((void*)ctx);
	// 	}
	// }

	static unsafe GCHandle CreateReferenceTrackingHandle(IJavaPeerable value)
	{
		// JniObjectReferenceControlBlock* controlBlock = (JniObjectReferenceControlBlock*)value.JniObjectReferenceControlBlock;
		// Console.WriteLine($"Creating reference tracking handle for {value.GetType().FullName} with JniObjectReferenceControlBlock: {controlBlock->handle}, type: {controlBlock->handle_type}, weak_handle: {controlBlock->weak_handle}, refs_added: {controlBlock->refs_added}");

		return JavaMarshal.CreateReferenceTrackingHandle(value, value.JniObjectReferenceControlBlock);
	}

	static unsafe void FreeHandle(GCHandle handle)
	{
		Console.WriteLine($"Freeing handle");
		IntPtr context = JavaMarshal.GetContext(handle);
		if (context != IntPtr.Zero)
		{
			var ctx = (JniObjectReferenceControlBlock*)context;
			Console.WriteLine($"Freeing handle with JniObjectReferenceControlBlock: {ctx->handle}, type: {ctx->handle_type}, weak_handle: {ctx->weak_handle}, refs_added: {ctx->refs_added}");
			NativeMemory.Free((void*)context);
		}
	}

	[UnmanagedCallersOnly]
	internal static void BridgeProcessingStarted()
	{
		AndroidRuntimeInternal.BridgeProcessing = true;
	}

	[UnmanagedCallersOnly]
	internal static unsafe IntPtr CollectGCHandles(MarkCrossReferences* mcr)
	{
		Console.WriteLine($"CollectGCHandles (mcr.ComponentsLen={mcr->ComponentsLen})");

		List<GCHandle> handles = [];
		for (int i = 0; i < mcr->ComponentsLen; i++)
		{
			for (int j = 0; j < mcr->Components[i].Count; j++)
			{
				Console.WriteLine($"CollectGCHandles i={i} j={j}");

				var ctx = mcr->Components[i].Context[j];
				if (ctx == IntPtr.Zero)
				{
					Console.WriteLine($"CollectGCHandles: controlBlock->handle is zero, skipping");
					handles.Add(default);
				}
				else
				{
					var controlBlock = (JniObjectReferenceControlBlock*)ctx;
					var reference = new JniObjectReference(controlBlock->handle, (JniObjectReferenceType)controlBlock->handle_type);
					Console.WriteLine($"CollectGCHandles: controlBlock->handle={controlBlock->handle}, type={controlBlock->handle_type}, weak_handle={controlBlock->weak_handle}, refs_added={controlBlock->refs_added}, reference={reference}");
					GCHandle handle = ((ManagedValueManager)AndroidRuntime.CurrentRuntime.ValueManager).PeekPeerHandle(reference);
					Console.WriteLine($"CollectGCHandles: PeekPeerHandle returned {handle.IsAllocated} for reference {reference}");
					handles.Add(handle);
				}

			}
		}

		Console.WriteLine($"CollectGCHandles: collected {handles.Count} handles");

		return GCHandle.ToIntPtr(GCHandle.Alloc(handles));
	}

	[UnmanagedCallersOnly]
	internal static unsafe void BridgeProcessingFinished(MarkCrossReferences* mcr, IntPtr handles)
	{
		Console.WriteLine($"BridgeProcessingFinished (mcr.ComponentsLen={mcr->ComponentsLen})");
		List<GCHandle>? originalHandles = GCHandle.FromIntPtr(handles).Target as List<GCHandle>;
		if (originalHandles is null)
		{
			Console.WriteLine($"BridgeProcessingFinished: invalid handles {handles}, target={GCHandle.FromIntPtr(handles).Target}");
			throw new InvalidOperationException($"Invalid GCHandles collection");
		}

		List<GCHandle> handlesToFree = [];

		for (int i = 0; i < mcr->ComponentsLen; i++)
		{
			for (int j = 0; j < mcr->Components[i].Count; j++)
			{
				Console.WriteLine($"BridgeProcessingFinished i={i} j={j}");

				var controlBlock = (JniObjectReferenceControlBlock*)mcr->Components[i].Context[j];
				if (controlBlock->handle == IntPtr.Zero)
				{
					Console.WriteLine($"BridgeProcessingFinished: controlBlock->handle is zero, skipping");

					// TODO figure out how to get the GCHandle here
					// GCHandle handle = PeekGCHandle(new JniObjectReference(controlBlock->handle, controlBlock->handle_type));
					// if (handle.IsAllocated && handle.Target is IJavaPeerable peer)
					// {
					// 	Console.WriteLine($"BridgeProcessingFinished: handle for {peer.GetType().FullName} will be freed");
					// 	handlesToFree.Add(handle);
					// 	JniObjectReferenceControlBlock.Free(ref controlBlock);
					// }
				}
			}
		}

		Console.WriteLine($"BridgeProcessingFinished: freeing {handlesToFree.Count} handles");

		JavaMarshal.FinishCrossReferenceProcessing(mcr, CollectionsMarshal.AsSpan(handlesToFree));
		AndroidRuntimeInternal.BridgeProcessing = false;
	}

	const BindingFlags ActivationConstructorBindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

	static readonly Type[] XAConstructorSignature = new Type[] { typeof(IntPtr), typeof(JniHandleOwnership) };

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
		var proxy = value as JavaProxyThrowable;
		if (proxy != null) {
			result  = proxy.InnerException;
			return true;
		}
		return base.TryUnboxPeerObject (value, out result);
	}
}
