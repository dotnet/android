using System;

namespace Xamarin.Android.Tasks.LLVMIR
{
	/// <summary>
	/// Visibility style, see https://llvm.org/docs/BitCodeFormat.html#visibility
	/// </summary>
	[Flags]
	enum LlvmIrVisibility
	{
		Default   = 1 << 0,
		Hidden    = 1 << 1,
		Protected = 1 << 2,
	}
}
