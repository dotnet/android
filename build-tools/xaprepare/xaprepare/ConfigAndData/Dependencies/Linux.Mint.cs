namespace Xamarin.Android.Prepare
{
	class LinuxMint : LinuxUbuntu
	{
		protected override bool NeedLibtool => UbuntuRelease.Major == 19;

		public LinuxMint (Context context) : base (context)
		{}
	};
}
