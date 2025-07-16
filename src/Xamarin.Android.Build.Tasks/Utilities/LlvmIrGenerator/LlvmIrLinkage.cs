using System;

namespace Xamarin.Android.Tasks.LLVMIR
{
	/// <summary>
	/// Variable linkage, see https://llvm.org/docs/LangRef.html#linkage
	/// </summary>
	[Flags]
	enum LlvmIrLinkage
	{
		/// <summary>
		/// Default linkage (external).
		/// </summary>
		Default             = 0 << 0,
		/// <summary>
		/// Private linkage - symbol is not accessible from outside the module.
		/// </summary>
		Private             = 1 << 0,
		/// <summary>
		/// Internal linkage - symbol is accessible within the current module only.
		/// </summary>
		Internal            = 1 << 1,
		/// <summary>
		/// Available externally linkage - symbol is available for inspection but not emission.
		/// </summary>
		AvailableExternally = 1 << 2,
		/// <summary>
		/// Link once linkage - symbol may be defined in multiple modules but only one will be chosen.
		/// </summary>
		LinkOnce            = 1 << 3,
		/// <summary>
		/// Weak linkage - symbol may be overridden by other definitions.
		/// </summary>
		Weak                = 1 << 4,
		/// <summary>
		/// Common linkage - for uninitialized global variables.
		/// </summary>
		Common              = 1 << 5,
		/// <summary>
		/// Appending linkage - for arrays that should be concatenated with arrays from other modules.
		/// </summary>
		Appending           = 1 << 6,
		/// <summary>
		/// Extern weak linkage - weak reference to external symbol.
		/// </summary>
		ExternWeak          = 1 << 7,
		/// <summary>
		/// Link once ODR linkage - link once with One Definition Rule enforcement.
		/// </summary>
		LinkOnceODR         = 1 << 8,
		/// <summary>
		/// External linkage - symbol is accessible from outside the module.
		/// </summary>
		External            = 1 << 9,
	}
}
