namespace Xamarin.Android.Prepare
{
	partial class Context
	{
		void InitOS ()
		{
			OS = new MacOS (this);
		}
	}
}
