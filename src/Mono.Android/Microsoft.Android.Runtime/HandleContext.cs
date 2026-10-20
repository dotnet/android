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
			if (controlBlock == IntPtr.Zero)
				throw new InvalidOperationException ("HandleContext control block is not initialized.");

			unsafe {
				// SAFETY: the registered peer retains controlBlock while this context is live; the null guard precedes the single handle read, without extending that native allocation's lifetime.
				return ((JniObjectReferenceControlBlock*) controlBlock)->handle == IntPtr.Zero;
			}
		}
	}

	public JniObjectReference PeerReference
	{
		get
		{
			if (controlBlock == IntPtr.Zero)
				throw new InvalidOperationException ("HandleContext control block is not initialized.");

			unsafe {
				// SAFETY: the registered peer retains controlBlock; handle and handle_type are read from the native mirror to reconstruct a borrowed reference, not to create new ownership.
				var block = (JniObjectReferenceControlBlock*) controlBlock;
				return new JniObjectReference (block->handle, (JniObjectReferenceType) block->handle_type);
			}
		}
	}

	public int RefsAdded
	{
		get
		{
			if (controlBlock == IntPtr.Zero)
				throw new InvalidOperationException ("HandleContext control block is not initialized.");
			unsafe {
				// SAFETY: the registered peer retains controlBlock during bridge processing; the null guard precedes this refs_added read.
				return ((JniObjectReferenceControlBlock*) controlBlock)->refs_added;
			}
		}
		set
		{
			if (controlBlock == IntPtr.Zero)
				throw new InvalidOperationException ("HandleContext control block is not initialized.");
			unsafe {
				// SAFETY: bridge processing owns updates to the retained control block; the null guard precedes the refs_added write.
				((JniObjectReferenceControlBlock*) controlBlock)->refs_added = value;
			}
		}
	}

	public void SetPeerReference (JniObjectReference reference)
	{
		if (controlBlock == IntPtr.Zero)
			throw new InvalidOperationException ("HandleContext control block is not initialized.");

		unsafe {
			// SAFETY: bridge processing retains controlBlock and owns its reference transition; the two writes publish an existing reference, without allocating or duplicating it.
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
			// SAFETY: the CLR callback supplies component/context arrays for their stated counts; the registry lock checks identity only, not native bounds or allocation lifetime.
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
			// SAFETY: AllocZeroed owns Size bytes for context; peer supplies the control block, whose lifetime is associated with the registered reference-tracking handle.
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
	/// Synchronize with bridge processing before freeing: the dictionary lock protects membership, not
	/// arbitrary concurrent readers of the context or its peer control block. A failed caller must not retry an already freed allocation.
	/// </safety>
	public static unsafe void Free (ref HandleContext* context)
	{
		unsafe {
			// SAFETY: the caller owns this Alloc-created context and excludes aliases during release; registry removal precedes the one native free and the caller's pointer is cleared.
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
