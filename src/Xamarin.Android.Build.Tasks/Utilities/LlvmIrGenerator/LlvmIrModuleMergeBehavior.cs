namespace Xamarin.Android.Tasks.LLVMIR
{
	/// <summary>
	/// Constants defining LLVM IR module merge behaviors for module flags metadata.
	/// See: https://llvm.org/docs/LangRef.html#module-flags-metadata
	/// </summary>
	static class LlvmIrModuleMergeBehavior
	{
		/// <summary>
		/// Error merge behavior - linking fails if flag values differ.
		/// </summary>
		public const int Error        = 1;
		/// <summary>
		/// Warning merge behavior - warning is emitted if flag values differ.
		/// </summary>
		public const int Warning      = 2;
		/// <summary>
		/// Require merge behavior - linking fails if the flag is not present in the other module.
		/// </summary>
		public const int Require      = 3;
		/// <summary>
		/// Override merge behavior - the flag value is overridden.
		/// </summary>
		public const int Override     = 4;
		/// <summary>
		/// Append merge behavior - the flag values are appended.
		/// </summary>
		public const int Append       = 5;
		/// <summary>
		/// Append unique merge behavior - the flag values are appended only if unique.
		/// </summary>
		public const int AppendUnique = 6;
		/// <summary>
		/// Max merge behavior - the maximum value is used.
		/// </summary>
		public const int Max          = 7;
	}
}
