using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Java;
using Java.Interop;

namespace Microsoft.Android.Runtime;

[StructLayout (LayoutKind.Sequential)]
internal struct HandleContext
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
			unsafe {
				// SAFETY: the registered context/control block is retained by the reference-tracking handle; bridge argument graphs remain live for the native callback.
				if (controlBlock == IntPtr.Zero)
					throw new InvalidOperationException ("HandleContext control block is not initialized.");

				return ((JniObjectReferenceControlBlock*) controlBlock)->handle == IntPtr.Zero;
			}
		}
	}

	public JniObjectReference PeerReference
	{
		get
		{
			unsafe {
				// SAFETY: the registered context/control block is retained by the reference-tracking handle; bridge argument graphs remain live for the native callback.
				if (controlBlock == IntPtr.Zero)
					throw new InvalidOperationException ("HandleContext control block is not initialized.");

				var block = (JniObjectReferenceControlBlock*) controlBlock;
				return new JniObjectReference (block->handle, (JniObjectReferenceType) block->handle_type);
			}
		}
	}

	public int RefsAdded
	{
		get
		{
			unsafe {
				// SAFETY: the registered context/control block is retained by the reference-tracking handle; bridge argument graphs remain live for the native callback.
				if (controlBlock == IntPtr.Zero)
					throw new InvalidOperationException ("HandleContext control block is not initialized.");
				return ((JniObjectReferenceControlBlock*) controlBlock)->refs_added;
			}
		}
		set
		{
			unsafe {
				// SAFETY: the registered context/control block is retained by the reference-tracking handle; bridge argument graphs remain live for the native callback.
				if (controlBlock == IntPtr.Zero)
					throw new InvalidOperationException ("HandleContext control block is not initialized.");
				((JniObjectReferenceControlBlock*) controlBlock)->refs_added = value;
			}
		}
	}

	public void SetPeerReference (JniObjectReference reference)
	{
		unsafe {
			// SAFETY: the registered context/control block is retained by the reference-tracking handle; bridge argument graphs remain live for the native callback.
			if (controlBlock == IntPtr.Zero)
				throw new InvalidOperationException ("HandleContext control block is not initialized.");

			var block = (JniObjectReferenceControlBlock*) controlBlock;
			block->handle = reference.Handle;
			block->handle_type = (int) reference.Type;
		}
	}

	// This is an internal mirror of the Java.Interop.JniObjectReferenceControlBlock
	[StructLayout (LayoutKind.Sequential)]
	internal struct JniObjectReferenceControlBlock
	{
		public IntPtr handle;
		public int handle_type;
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

	/// <safety>
	/// <paramref name="mcr"/> must address the CLR bridge's live descriptor and correctly bounded
	/// component/context arrays for the entire callback. Checking registry membership does not validate native memory.
	/// </safety>
	public static unsafe void EnsureAllContextsAreOurs (MarkCrossReferencesArgs* mcr)
	{
		unsafe {
			// SAFETY: the registered context/control block is retained by the reference-tracking handle; bridge argument graphs remain live for the native callback.
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
		}
	}

	public static HandleContext* Alloc (IJavaPeerable peer)
	{
		unsafe {
			// SAFETY: the registered context/control block is retained by the reference-tracking handle; bridge argument graphs remain live for the native callback.
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
	}

	/// <safety>
	/// <paramref name="context"/> must be null or an unreleased context allocated by <see cref="Alloc"/>.
	/// The caller must own its release and prevent access through aliases after this call.
	/// </safety>
	public static unsafe void Free (ref HandleContext* context)
	{
		unsafe {
			// SAFETY: the registered context/control block is retained by the reference-tracking handle; bridge argument graphs remain live for the native callback.
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
}
