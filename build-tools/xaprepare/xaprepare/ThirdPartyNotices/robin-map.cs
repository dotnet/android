using System;
using System.IO;

namespace Xamarin.Android.Prepare
{
	[TPN]
	class tessil_robin_map_TPN : ThirdPartyNotice
	{
		static readonly Uri url = new Uri ("https://github.com/Tessil/robin-map");
		static readonly string licenseFile = Path.Combine (Configurables.Paths.ExternalDir, "robin-map", "LICENSE");

		public override string LicenseFile => licenseFile;
		public override string Name => "tessil/robin-map";
		public override Uri SourceUrl => url;
		public override string LicenseText => String.Empty;

		public override bool Include (bool includeExternalDeps, bool includeBuildDeps) => includeBuildDeps;
	}
}
