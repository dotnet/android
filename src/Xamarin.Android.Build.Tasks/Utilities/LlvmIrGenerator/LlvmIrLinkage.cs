using System;

namespace Xamarin.Android.Tasks.LLVMIR
{
	/// <summary>
	/// Variable linkage, see https://llvm.org/docs/LangRef.html#linkage
	/// </summary>
	[Flags]
	enum LlvmIrLinkage
	{
		Default             = 0 << 0,
		Private             = 1 << 0,
		Internal            = 1 << 1,
		AvailableExternally = 1 << 2,
		LinkOnce            = 1 << 3,
		Weak                = 1 << 4,
		Common              = 1 << 5,
		Appending           = 1 << 6,
		ExternWeak          = 1 << 7,
		LinkOnceODR         = 1 << 8,
		External            = 1 << 9,
	}
}
