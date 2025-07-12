using System;

namespace Xamarin.Android.Tasks.LLVMIR
{
	/// <summary>
	/// Address significance, see https://llvm.org/docs/LangRef.html#global-variables
	/// </summary>
	[Flags]
	enum LlvmIrAddressSignificance
	{
		/// <summary>
		/// Default address significance.
		/// </summary>
		Default      = 1 << 0,
		/// <summary>
		/// Unnamed address significance - the address of the global is not significant.
		/// </summary>
		Unnamed      = 1 << 1,
		/// <summary>
		/// Local unnamed address significance - the address is not significant within the module.
		/// </summary>
		LocalUnnamed = 1 << 2,
	}
}
