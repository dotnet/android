using System;
using System.Runtime.InteropServices;

namespace Java.Interop;

internal struct JniObjectReferenceControlBlock {
	public	IntPtr  handle;
	public  int     handle_type;
	public  IntPtr  weak_handle;
	public  int     refs_added;

	public  static  readonly    int Size    = Marshal.SizeOf<JniObjectReferenceControlBlock>();

	public static unsafe JniObjectReferenceControlBlock* Alloc (JniObjectReference reference)
	{
		var value = (JniObjectReferenceControlBlock*) NativeMemory.AllocZeroed (1, (uint) Size);
		value->handle       = reference.Handle;
		value->handle_type  = (int) reference.Type;
		return value;
	}

	public static unsafe void Free (ref JniObjectReferenceControlBlock* value)
	{
		if (value == null) {
			return;
		}
		NativeMemory.Free (value);
		value   = null;
	}
}
