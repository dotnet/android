using System;
using System.ComponentModel;

namespace Java.Interop {

	[AttributeUsage (AttributeTargets.Method)]
	[EditorBrowsable (EditorBrowsableState.Never)]
	public sealed class JavaInterfaceDefaultMethodAttribute : Attribute {
	}
}
