#if !NET9_0_OR_GREATER

using System;

namespace System.Diagnostics
{
	// This attribute was added in .NET 9, and we may not be targeting .NET 9 yet.
	public class DebuggerDisableUserUnhandledExceptionsAttribute : Attribute
	{
	}
}

#endif  // !NET9_0_OR_GREATER
