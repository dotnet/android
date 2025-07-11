using System;

namespace Xamarin.Android.Tasks.LLVMIR
{
	/// <summary>
	/// Specifies whether a variable is writable or constant, see https://llvm.org/docs/LangRef.html#global-variables
	/// </summary>
	[Flags]
	enum LlvmIrWritability
	{
		/// <summary>
		/// Variable is constant and cannot be modified.
		/// </summary>
		Constant = 1 << 0,
		/// <summary>
		/// Variable is writable and can be modified.
		/// </summary>
		Writable = 1 << 1,
	}
}
