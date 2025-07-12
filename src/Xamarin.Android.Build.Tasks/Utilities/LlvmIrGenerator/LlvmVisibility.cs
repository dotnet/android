using System;

namespace Xamarin.Android.Tasks.LLVMIR
{
	/// <summary>
	/// Visibility style, see https://llvm.org/docs/BitCodeFormat.html#visibility
	/// </summary>
	[Flags]
	enum LlvmIrVisibility
	{
		/// <summary>
		/// Default visibility - symbol is visible to other modules.
		/// </summary>
		Default   = 1 << 0,
		/// <summary>
		/// Hidden visibility - symbol is not visible to other modules.
		/// </summary>
		Hidden    = 1 << 1,
		/// <summary>
		/// Protected visibility - symbol is visible but not overrideable.
		/// </summary>
		Protected = 1 << 2,
	}
}
