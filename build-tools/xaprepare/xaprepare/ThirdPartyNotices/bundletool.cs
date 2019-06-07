using System;
using System.Collections.Generic;
using System.IO;

namespace Xamarin.Android.Prepare
{
	[TPN]
	class bundletool_google_bundletool_TPN : ThirdPartyNotice
	{
		static readonly Uri    url         = new Uri ("https://github.com/google/bundletool");

		public override string LicenseFile => CommonLicenses.Apache20Path;
		public override string Name        => "google/bundletool";
		public override Uri    SourceUrl   => url;
		public override string LicenseText => null;

		public override bool   Include (bool includeExternalDeps, bool includeBuildDeps) => includeExternalDeps;
	}
}
