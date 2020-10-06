using System;
using System.Collections.Generic;
using System.IO;

namespace Xamarin.Android.Prepare
{
	[TPN]
	class apksigner_google_TPN : ThirdPartyNotice
	{
		static readonly Uri    url         = new Uri ("https://android.googlesource.com/platform/tools/apksig/");

		public override string LicenseFile => CommonLicenses.Apache20Path;
		public override string Name        => "google/apksig";
		public override Uri    SourceUrl   => url;
		public override string LicenseText => String.Empty;

		public override bool   Include (bool includeExternalDeps, bool includeBuildDeps) => includeExternalDeps;
	}
}
