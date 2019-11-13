namespace Xamarin.Android.Prepare
{
	partial class Context
	{
		static bool isWindows = false;

		void InitOS ()
		{
			OS = new MacOS (this);
		}
	}
}
