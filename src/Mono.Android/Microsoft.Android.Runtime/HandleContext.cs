using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Java;
using Java.Interop;

namespace Microsoft.Android.Runtime;

[StructLayout (LayoutKind.Sequential)]
internal unsafe struct HandleContext
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

	public JniObjectReference PeerReference
	{
		get
		{
			if (controlBlock == IntPtr.Zero)
				throw new InvalidOperationException ("HandleContext control block is not initialized.");

			var block = (JniObjectReferenceControlBlock*) controlBlock;
			return new JniObjectReference (block->handle, (JniObjectReferenceType) block->handle_type);
		}
	}

	public int RefsAdded
	{
		get
		{
			if (controlBlock == IntPtr.Zero)
				throw new InvalidOperationException ("HandleContext control block is not initialized.");
			return ((JniObjectReferenceControlBlock*) controlBlock)->refs_added;
		}
		set
		{
			if (controlBlock == IntPtr.Zero)
				throw new InvalidOperationException ("HandleContext control block is not initialized.");
			((JniObjectReferenceControlBlock*) controlBlock)->refs_added = value;
		}
	}

	public void SetPeerReference (JniObjectReference reference)
	{
		if (controlBlock == IntPtr.Zero)
			throw new InvalidOperationException ("HandleContext control block is not initialized.");

		var block = (JniObjectReferenceControlBlock*) controlBlock;
		block->handle = reference.Handle;
		block->handle_type = (int) reference.Type;
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

	public static void EnsureAllContextsAreOurs (MarkCrossReferencesArgs* mcr)
	{
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
