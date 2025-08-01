#nullable enable
using System;

namespace Xamarin.Android.Tasks.LLVMIR
{
	/// <summary>
	/// Runtime preemption specifiers, see https://llvm.org/docs/LangRef.html#runtime-preemption-model
	/// </summary>
	[Flags]
	enum LlvmIrRuntimePreemption
	{
		/// <summary>
		/// Default runtime preemption (dso_preemptable).
		/// </summary>
		Default        = 0 << 0,
		/// <summary>
		/// DSO preemptable - symbol may be preempted by symbols from other modules.
		/// </summary>
		DSOPreemptable = 1 << 0,
		/// <summary>
		/// DSO local - symbol is local to the current dynamic shared object.
		/// </summary>
		DSOLocal       = 1 << 1,
	}
}
