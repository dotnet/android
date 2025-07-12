namespace Xamarin.Android.Tasks.LLVMIR
{
	/// <summary>
	/// Represents call markers for LLVM IR call instructions that control tail call optimization.
	/// </summary>
	enum LlvmIrCallMarker
	{
		/// <summary>
		/// No call marker specified.
		/// </summary>
		None,
		/// <summary>
		/// Tail call marker - suggests tail call optimization.
		/// </summary>
		Tail,
		/// <summary>
		/// Must tail call marker - requires tail call optimization.
		/// </summary>
		MustTail,
		/// <summary>
		/// No tail call marker - prevents tail call optimization.
		/// </summary>
		NoTail,
	}
}
