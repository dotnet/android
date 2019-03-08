using System;
using System.Collections.Generic;
using System.IO;

namespace Xamarin.Android.Prepare
{
	[TPN]
	class aapt2_google_aapt2_TPN : ThirdPartyNotice
	{
		static readonly Uri    url         = new Uri ("https://mvnrepository.com/artifact/com.android.tools.build/aapt2");

		public override string LicenseFile => CommonLicenses.Apache20Path;
		public override string Name        => "google/aapt2";
		public override Uri    SourceUrl   => url;
		public override string LicenseText => null;

		public override bool   Include (bool includeExternalDeps, bool includeBuildDeps) => includeExternalDeps;
	}
}
