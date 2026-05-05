using System;
using System.IO;

namespace Xamarin.Android.Prepare
{
	[TPN]
	class JavaInterop_xamarin_Java_Interop_TPN : ThirdPartyNotice
	{
		static readonly Uri    url         = new Uri ("https://github.com/dotnet/java-interop/");
		static readonly string licenseFile = Path.Combine (Configurables.Paths.ExternalJavaInteropDir, "LICENSE");

		public override string LicenseFile => licenseFile;
		public override string Name        => "dotnet/java-interop";
		public override Uri    SourceUrl   => url;
		public override string LicenseText => String.Empty;

		public override bool   Include (bool includeExternalDeps, bool includeBuildDeps) => includeExternalDeps;
	}
}
