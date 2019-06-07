using System;
using System.IO;

namespace Xamarin.Android.Prepare
{
	static class CommonLicenses
	{
		static readonly string LicenseDataDir      = Path.Combine ("build-tools", "license-data");

		public static readonly string Apache20Path = Path.Combine (LicenseDataDir, "Apache-2.0.txt");
		public static readonly string GPLv2Path    = Path.Combine (LicenseDataDir, "GPLv2.txt");
		public static readonly string MonoMITPath  = Path.Combine (LicenseDataDir, "Mono-MIT.txt");
	}
}
