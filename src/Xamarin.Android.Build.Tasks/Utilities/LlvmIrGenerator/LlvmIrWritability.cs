using System;

namespace Xamarin.Android.Tasks.LLVMIR
{
	/// <summary>
	/// Address significance, see https://llvm.org/docs/LangRef.html#global-variables
	/// </summary>
	[Flags]
	enum LlvmIrWritability
	{
		Constant = 1 << 0,
		Writable = 1 << 1,
	}
}
