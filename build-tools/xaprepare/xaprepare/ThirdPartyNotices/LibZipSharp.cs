using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Xamarin.Android.Prepare
{
	[TPN]
	class LibZipSharp_grendello_LibZipSharp_TPN : ThirdPartyNotice
	{
		static readonly Uri url = new Uri ("https://github.com/xamarin/LibZipSharp/");
		internal static readonly string LibZipSharpVersion => {
			var doc = XDocument.Load (Path.Combine (BuildPaths.XamarinAndroidSourceRoot, "Configuration.props"));
			return doc.Descendants(XName.Get("LibZipSharpVersion", @"http://schemas.microsoft.com/developer/msbuild/2003")).First().Value;
		};
		static readonly string licenseFile = Path.Combine (BuildPaths.XamarinAndroidSourceRoot,
			"packages", "xamarin.libzipsharp", LibZipSharpVersion (),
			"Licences", "LICENSE");

		public override string LicenseFile => licenseFile;
		public override string Name => "xamarin/LibZipSharp";
		public override Uri SourceUrl => url;
		public override string LicenseText => null;

		public override bool Include (bool includeExternalDeps, bool includeBuildDeps) => includeExternalDeps;
	}
}
