using System;
using System.Collections.Generic;
using System.IO;

namespace Xamarin.Android.Prepare
{
	[TPN]
	class lz4_lz4_TPN : ThirdPartyNotice
	{
		static readonly Uri url = new Uri ("https://github.com/lz4/lz4/");
		static readonly string licenseFile = Path.Combine (Configurables.Paths.ExternalDir, "lz4", "lib", "LICENSE");

		public override string LicenseFile => licenseFile;
		public override string Name => "lz4/lz4";
		public override Uri SourceUrl => url;
		public override string LicenseText => String.Empty;

		public override bool Include (bool includeExternalDeps, bool includeBuildDeps) => includeExternalDeps;
	}
}
