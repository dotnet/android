using System;
using System.Collections.Generic;
using System.IO;

namespace Xamarin.Android.Prepare
{
	[TPN]
	class opentk_mono_opentk_TPN : ThirdPartyNotice
	{
		static readonly Uri    url         = new Uri ("https://github.com/mono/opentk/");
		static readonly string licenseFile = Path.Combine ("external", "opentk", "Documentation", "License.txt");

		public override string LicenseFile => licenseFile;
		public override string Name        => "mono/opentk";
		public override Uri    SourceUrl   => url;
		public override string LicenseText => null;

		public override bool   Include (bool includeExternalDeps, bool includeBuildDeps) => includeExternalDeps;
	}
}
