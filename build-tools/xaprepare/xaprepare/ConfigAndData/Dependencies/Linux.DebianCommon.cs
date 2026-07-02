namespace Xamarin.Android.Prepare
{
	abstract class LinuxDebianCommon : Linux
	{
		protected override void InitializeDependencies ()
		{}

		protected LinuxDebianCommon (Context context)
			: base (context)
		{
			Flavor = "Debian";
		}
	};
}
