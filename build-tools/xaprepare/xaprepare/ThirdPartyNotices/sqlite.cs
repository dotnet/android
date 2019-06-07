using System;
using System.Collections.Generic;
using System.IO;

namespace Xamarin.Android.Prepare
{
	[TPN]
	class sqlite_xamarin_sqlite_TPN : ThirdPartyNotice
	{
		static readonly Uri    url         = new Uri ("https://github.com/xamarin/sqlite/");

		public override string LicenseFile => CommonLicenses.Apache20Path;
		public override string Name        => "xamarin/sqlite";
		public override Uri    SourceUrl   => url;
		public override string LicenseText => null;

		public override bool   Include (bool includeExternalDeps, bool includeBuildDeps) => includeExternalDeps;
	}

	[TPN]
	class sqlite_sqlite_sqlite_TPN : ThirdPartyNotice
	{
		static readonly Uri    url         = new Uri ("https://github.com/xamarin/sqlite/tree/master/dist/");
		static readonly string licenseFile = Path.Combine ("external", "sqlite", "dist", "NOTICE");

		public override string LicenseFile => licenseFile;
		public override string Name        => "sqlite/sqlite";
		public override Uri    SourceUrl   => url;
		public override string LicenseText => null;

		public override bool   Include (bool includeExternalDeps, bool includeBuildDeps) => includeExternalDeps;
	}
}
