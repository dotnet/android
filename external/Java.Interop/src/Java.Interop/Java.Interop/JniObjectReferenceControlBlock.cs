using System;
using System.Runtime.InteropServices;

namespace Java.Interop;

[StructLayout (LayoutKind.Sequential)]
internal struct JniObjectReferenceControlBlock {
	public	IntPtr  handle;
	public  int     handle_type;
	public  int     refs_added;

	public  static  readonly    int Size    = Marshal.SizeOf<JniObjectReferenceControlBlock>();

	/// <remarks>The returned zeroed native allocation belongs to the peer and must be released with Free.</remarks>
	public static unsafe JniObjectReferenceControlBlock* Alloc (JniObjectReference reference)
	{
		unsafe {
			// SAFETY: Allocate exactly one complete block before initializing its scalar fields; no caller buffer is read.
			var value = (JniObjectReferenceControlBlock*) NativeMemory.AllocZeroed (1, (uint) Size);
			value->handle       = reference.Handle;
			value->handle_type  = (int) reference.Type;
			return value;
		}
	}

	/// <safety>
	/// A non-null value must be an unreleased allocation from Alloc, exclusively owned by this slot.
	/// Detach reference-tracking contexts and prevent all concurrent reads, writes and releases through aliases.
	/// No alias may access the block after this call; the owner slot is cleared but other aliases are not.
	/// </safety>
	public static unsafe void Free (ref JniObjectReferenceControlBlock* value)
	{
		unsafe {
			// SAFETY: The caller supplies the exclusively owned NativeMemory allocation; null is not dereferenced.
			if (value == null) {
				return;
			}
			NativeMemory.Free (value);
			value   = null;
		}
	}

	/// <safety>
	/// A non-null value must address a live allocation from Alloc owned by this slot.
	/// Detach tracking contexts and exclude concurrent access or release through every alias for this call.
	/// If its handle is zero, no alias may access the block after it is freed; a valid block is retained.
	/// </safety>
	public static unsafe void FreeIfInvalid (ref JniObjectReferenceControlBlock* value)
	{
		unsafe {
			// SAFETY: The caller keeps the owned block readable for the check and permits release only when invalid.
			// Reference-tracking contexts must already be detached. Preserve a
			// valid reference reconstructed by the finalization callback.
			if (value != null && value->handle == IntPtr.Zero) {
				Free (ref value);
			}
		}
	}
}
