#nullable enable
using Microsoft.Build.Utilities;
using Xamarin.Android.Tasks.LLVMIR;

namespace Xamarin.Android.Tasks;

// The format marker remains in the app DSO until the remaining native linkage is retired.
class ApplicationFormatTagNativeAssemblyGenerator : LlvmIrComposer
{
	const ulong FORMAT_TAG = 0x00045E6972616D58;

	public ApplicationFormatTagNativeAssemblyGenerator (TaskLoggingHelper log) : base (log)
	{
	}

	protected override void Construct (LlvmIrModule module)
	{
		module.AddGlobalVariable ("format_tag", FORMAT_TAG, comment: $" 0x{FORMAT_TAG:x}");
	}
}
