namespace Xamarin.Android.Prepare
{
	partial class Step_PrepareBundle
	{
		const string bundle404Message = "The Windows build depends on a cached mono bundle, that may not be available yet. Try going back a few commits (with git reset) to download a different bundle.";

		void InitOS ()
		{
			osSupportsMonoBuild = false;
		}
	}
}
