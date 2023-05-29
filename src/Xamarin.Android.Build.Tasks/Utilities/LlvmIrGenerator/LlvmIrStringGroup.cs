using System.Collections.Generic;

namespace Xamarin.Android.Tasks.LLVM.IR;

sealed class LlvmIrStringGroup
{
	public ulong Count;
	public readonly string? Comment;
	public readonly List<LlvmIrStringVariable> Strings = new List<LlvmIrStringVariable> ();

	public LlvmIrStringGroup (string? comment = null)
	{
		Comment = comment;
		Count = 0;
	}
}
