using System;
using System.Collections.Generic;
using System.IO;

namespace Xamarin.Android.Prepare
{
	[TPN]
	class libzip_nih_at_libzip_TPN : ThirdPartyNotice
	{
		static readonly Uri url = new Uri ("https://github.com/nih-at/libzip/");
		static readonly string licenseFile = Path.Combine (
			Context.Instance.Properties.GetRequiredValue (KnownProperties.PkgXamarin_LibZipSharp),
			"Licences", "libzip", "LICENSE"
		);

		public override string LicenseFile => licenseFile;
		public override string Name => "nih-at/libzip";
		public override Uri SourceUrl => url;
		public override string LicenseText => String.Empty;

		public override bool Include (bool includeExternalDeps, bool includeBuildDeps) => includeExternalDeps;
	}
}
