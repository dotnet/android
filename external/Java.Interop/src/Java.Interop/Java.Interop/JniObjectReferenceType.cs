#nullable enable

using System;

namespace Java.Interop
{
	public enum JniObjectReferenceType {
		Invalid     = 0,
		Local       = 1,
		Global      = 2,
		WeakGlobal  = 3,
	}
}

