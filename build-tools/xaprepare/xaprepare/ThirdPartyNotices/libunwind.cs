using System;
using System.IO;

namespace Xamarin.Android.Prepare
{
	[TPN]
	class libunwind_libunwind_TPN : ThirdPartyNotice
	{
		static readonly Uri url = new Uri ("https://github.com/libunwind/libunwind");
		static readonly string licenseFile = Path.Combine (Configurables.Paths.ExternalDir, "libunwind", "LICENSE");

		public override string LicenseFile => licenseFile;
		public override string Name => "libunwind/libunwind";
		public override Uri SourceUrl => url;
		public override string LicenseText => String.Empty;

		public override bool Include (bool includeExternalDeps, bool includeBuildDeps) => includeExternalDeps;
	}
}
