namespace Xamarin.Android.Prepare
{
	partial class Context
	{
		static bool isWindows = false;

		void InitOS ()
		{
			OS = Linux.DetectAndCreate (this);
		}
	}
}
