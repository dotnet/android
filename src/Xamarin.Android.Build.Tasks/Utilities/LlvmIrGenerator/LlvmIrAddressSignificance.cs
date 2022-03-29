using System;

namespace Xamarin.Android.Tasks.LLVMIR
{
	/// <summary>
	/// Address significance, see https://llvm.org/docs/LangRef.html#global-variables
	/// </summary>
	[Flags]
	enum LlvmIrAddressSignificance
	{
		Default      = 1 << 0,
		Unnamed      = 1 << 1,
		LocalUnnamed = 1 << 2,
	}
}
