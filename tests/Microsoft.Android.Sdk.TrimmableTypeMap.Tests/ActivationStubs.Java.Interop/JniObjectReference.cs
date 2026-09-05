using System;

namespace Java.Interop;

public struct JniObjectReference
{
	public IntPtr Handle;
}

public enum JniObjectReferenceOptions
{
	None,
	Copy,
	CopyAndDispose,
}
