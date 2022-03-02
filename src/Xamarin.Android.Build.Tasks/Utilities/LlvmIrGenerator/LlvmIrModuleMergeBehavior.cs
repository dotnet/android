namespace Xamarin.Android.Tasks
{
	// See: https://llvm.org/docs/LangRef.html#module-flags-metadata
	static class LlvmIrModuleMergeBehavior
	{
		public const int Error        = 1;
		public const int Warning      = 2;
		public const int Require      = 3;
		public const int Override     = 4;
		public const int Append       = 5;
		public const int AppendUnique = 6;
		public const int Max          = 7;
	}
}
