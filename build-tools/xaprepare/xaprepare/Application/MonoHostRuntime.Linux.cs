namespace Xamarin.Android.Prepare
{
	partial class MonoHostRuntime
	{
		partial void InitOS ()
		{
			if (Context.Is32BitMingwHostAbi (Name))
				CanStripNativeLibrary = false;
		}
	}
}
