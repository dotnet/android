using System;
using System.Collections.Generic;
using System.IO;

namespace Xamarin.Android.Prepare
{
	[TPN]
	class LibZipSharp_grendello_LibZipSharp_TPN : ThirdPartyNotice
	{
		static readonly Uri url = new Uri ("https://github.com/xamarin/LibZipSharp/");
		internal static readonly string LibZipSharpVersion = "1.0.6";
		static readonly string licenseFile = Path.Combine (BuildPaths.XamarinAndroidSourceRoot,
			"packages", "xamarin.libzipsharp", LibZipSharpVersion,
			"Licences", "LICENSE");

		public override string LicenseFile => licenseFile;
		public override string Name => "xamarin/LibZipSharp";
		public override Uri SourceUrl => url;
		public override string LicenseText => null;

		public override bool Include (bool includeExternalDeps, bool includeBuildDeps) => includeExternalDeps;
	}
}
