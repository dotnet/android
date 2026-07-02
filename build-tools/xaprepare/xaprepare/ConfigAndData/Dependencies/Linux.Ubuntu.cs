using System;

namespace Xamarin.Android.Prepare
{
	class LinuxUbuntu : LinuxUbuntuCommon
	{
		protected Version UbuntuRelease { get; private set; } = new Version (0, 0);

		public LinuxUbuntu (Context context) : base (context)
		{}

		protected override bool EnsureVersionInformation (Context context)
		{
			if (!Version.TryParse (Release, out Version? ubuntuRelease) || ubuntuRelease == null) {
				if (Int32.TryParse (Release, out int singleNumberVersion)) {
					ubuntuRelease = new Version (singleNumberVersion, 0);
				} else {
					Log.ErrorLine ($"Unable to parse string '{Release}' as a valid {Name} release version");
					return false;
				}
			}
			UbuntuRelease = ubuntuRelease;

			return true;
		}
	};
}
