using System;
using System.Collections.Generic;
using System.IO;

namespace Xamarin.Android.Prepare
{
	[TPN]
	class dlfcn_TPN : ThirdPartyNotice
	{
		static readonly Uri url = new Uri ("https://github.com/dlfcn-win32/dlfcn-win32/");
		static readonly string licenseFile = Path.Combine (Configurables.Paths.ExternalDir, "dlfcn-win32", "COPYING");


		public override string LicenseFile => licenseFile;
		public override string Name        => "dlfcn-win32/dlfcn-win32";
		public override Uri    SourceUrl   => url;
		public override string LicenseText => "";

		public override bool Include (bool includeExternalDeps, bool includeBuildDeps) => includeExternalDeps;
	}
}
