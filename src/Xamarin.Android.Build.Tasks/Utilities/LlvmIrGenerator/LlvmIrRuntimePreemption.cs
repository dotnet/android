using System;

namespace Xamarin.Android.Tasks.LLVMIR
{
	/// <summary>
	/// Runtime preemption specifiers, see https://llvm.org/docs/LangRef.html#runtime-preemption-model
	/// </summary>
	[Flags]
	enum LlvmIrRuntimePreemption
	{
		Default        = 0 << 0,
		DSOPreemptable = 1 << 0,
		DSOLocal       = 1 << 1,
	}
}
