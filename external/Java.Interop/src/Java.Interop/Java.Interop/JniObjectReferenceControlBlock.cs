using System;
using System.Runtime.InteropServices;

namespace Java.Interop;

[StructLayout (LayoutKind.Sequential)]
internal struct JniObjectReferenceControlBlock {
	public	IntPtr  handle;
	public  int     handle_type;
	public  int     refs_added;

	public  static  readonly    int Size    = Marshal.SizeOf<JniObjectReferenceControlBlock>();

	public static unsafe JniObjectReferenceControlBlock* Alloc (JniObjectReference reference)
	{
		unsafe {
			var value = (JniObjectReferenceControlBlock*) NativeMemory.AllocZeroed (1, (uint) Size);
			value->handle       = reference.Handle;
			value->handle_type  = (int) reference.Type;
			return value;
		}
	}

	public static unsafe void Free (ref JniObjectReferenceControlBlock* value)
	{
		unsafe {
			if (value == null) {
				return;
			}
			NativeMemory.Free (value);
			value   = null;
		}
	}

	public static unsafe void FreeIfInvalid (ref JniObjectReferenceControlBlock* value)
	{
		unsafe {
			// Reference-tracking contexts must already be detached. Preserve a
			// valid reference reconstructed by the finalization callback.
			if (value != null && value->handle == IntPtr.Zero) {
				Free (ref value);
			}
		}
	}
}
