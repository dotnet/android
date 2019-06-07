using System;
using System.Collections.Generic;
using System.IO;

namespace Xamarin.Android.Prepare
{
	// External dependencies from `Xamarin.Android.Tools.JavadocImporter/packages.config`

    [TPN]
	class XamarinAndroidToolsJavadocImporter_lovettchris_SgmlReader_TPN : ThirdPartyNotice
	{
		static readonly Uri    url         = new Uri ("https://github.com/lovettchris/SgmlReader/");

		public override string LicenseFile => CommonLicenses.Apache20Path;
		public override string Name        => "lovettchris/SgmlReader";
		public override Uri    SourceUrl   => url;
		public override string LicenseText => null;

		public override bool   Include(bool includeExternalDeps, bool includeBuildDeps) => includeExternalDeps;
	}
}
