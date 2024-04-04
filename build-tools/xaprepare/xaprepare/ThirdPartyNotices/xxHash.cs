using System;
using System.Collections.Generic;
using System.IO;

namespace Xamarin.Android.Prepare
{
	[TPN]
	class constexpr_xxh3_TPN : ThirdPartyNotice
	{
		static readonly Uri url = new Uri ("https://github.com/chys87/constexpr-xxh3/");
		static readonly string licenseFile = Path.Combine (Configurables.Paths.ExternalDir, "constexpr-xxh3", "LICENSE");

		public override string LicenseFile => licenseFile;
		public override string Name => "chys87/constexpr-xxh3";
		public override Uri SourceUrl => url;
		public override string LicenseText => String.Empty;

		public override bool Include (bool includeExternalDeps, bool includeBuildDeps) => includeBuildDeps;
	}

	[TPN]
	class xxHash_TPN : ThirdPartyNotice
	{
		static readonly Uri url = new Uri ("https://github.com/Cyan4973/xxHash/");
		static readonly string licenseFile = Path.Combine (Configurables.Paths.ExternalDir, "xxHash", "LICENSE");

		public override string LicenseFile => licenseFile;
		public override string Name => "Cyan4973/xxHash";
		public override Uri SourceUrl => url;
		public override string LicenseText => String.Empty;

		public override bool Include (bool includeExternalDeps, bool includeBuildDeps) => includeBuildDeps;
	}
}
