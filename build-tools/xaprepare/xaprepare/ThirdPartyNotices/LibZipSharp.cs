using System;
using System.Collections.Generic;
using System.IO;

namespace Xamarin.Android.Prepare
{
	[TPN]
	class LibZipSharp_grendello_LibZipSharp_TPN : ThirdPartyNotice
	{
		static readonly Uri    url         = new Uri ("https://github.com/grendello/LibZipSharp/");
		static readonly string licenseFile = Path.Combine ("external", "LibZipSharp", "LICENSE");

		public override string LicenseFile => licenseFile;
		public override string Name        => "grendello/LibZipSharp";
		public override Uri    SourceUrl   => url;
		public override string LicenseText => null;

		public override bool   Include (bool includeExternalDeps, bool includeBuildDeps) => includeExternalDeps;
	}
}
