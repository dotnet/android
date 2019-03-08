namespace Xamarin.Android.Prepare
{
	partial class Context
	{
		void InitOS ()
		{
			OS = Linux.DetectAndCreate (this);
		}
	}
}
