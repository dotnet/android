using System;
using System.Collections.Generic;
using System.IO;

namespace Xamarin.Android.Prepare
{
	[TPN]
	class mono_External_Dependencies_Group : ThirdPartyNoticeGroup
	{
		protected override bool ShouldInclude (bool includeExternalDeps, bool includeBuildDeps) => includeExternalDeps;

		public override List<ThirdPartyNotice> Notices => new List <ThirdPartyNotice> {
			new mono_mono_mono_TPN (),
			new mono_mono_cecil_TPN (),
			new mono_mono_ikdasm_TPN (),
			new mono_mono_linker_TPN (),
		};
	}

	class mono_mono_cecil_TPN : ThirdPartyNotice
	{
		static readonly Uri    url         = new Uri ("https://github.com/mono/cecil/");
		static readonly string licenseFile = String.Empty;

		public override string LicenseFile => licenseFile;
		public override string Name        => "mono/cecil";
		public override Uri    SourceUrl   => url;
		public override string LicenseText => Utilities.GetUrlContent (
			new Uri ("https://raw.githubusercontent.com/dotnet/cecil/acd8ad7bca1aa560576ef07875282e285cf20d53/LICENSE.txt"))
			.GetAwaiter ().GetResult ();
	}

	class mono_mono_mono_TPN : ThirdPartyNotice
	{
		static readonly Uri    url         = new Uri ("https://github.com/mono/");
		static readonly string licenseFile = String.Empty;

		public override string LicenseFile => licenseFile;
		public override string Name        => "mono/mono";
		public override Uri    SourceUrl   => url;
		public override string LicenseText => Utilities.GetUrlContent (
			new Uri ("https://raw.githubusercontent.com/mono/mono/12dd9040252e5b9c63b8df9ef53777eb01232405/LICENSE"))
			.GetAwaiter ().GetResult ();
	}

	class mono_mono_ikdasm_TPN : ThirdPartyNotice
	{
		static readonly Uri    url         = new Uri ("https://github.com/mono/ikdasm");

		public override string LicenseFile => String.Empty;
		public override string Name        => "mono/ikdasm";
		public override Uri    SourceUrl   => url;
		public override string LicenseText => @"
Copyright (C) 2012 Jeroen Frijters

This software is provided 'as-is', without any express or implied
warranty.  In no event will the authors be held liable for any damages
arising from the use of this software.

Permission is granted to anyone to use this software for any purpose,
including commercial applications, and to alter it and redistribute it
freely, subject to the following restrictions:

1. The origin of this software must not be misrepresented; you must not
   claim that you wrote the original software. If you use this software
   in a product, an acknowledgment in the product documentation would be
   appreciated but is not required.
2. Altered source versions must be plainly marked as such, and must not be
   misrepresented as being the original software.
3. This notice may not be removed or altered from any source distribution.

Jeroen Frijters
jeroen@frijters.net
";

		public override bool Include (bool includeExternalDeps, bool includeBuildDeps) => includeExternalDeps && includeBuildDeps;
	}

	class mono_mono_linker_TPN : ThirdPartyNotice
	{
		static readonly Uri    url         = new Uri ("https://github.com/mono/linker/");
		static readonly string licenseFile = String.Empty;

		public override string LicenseFile => licenseFile;
		public override string Name        => "mono/linker";
		public override Uri    SourceUrl   => url;
		public override string LicenseText => Utilities.GetUrlContent (
			new Uri ("https://raw.githubusercontent.com/dotnet/linker/6b3a3050c70577bd1b3fd7611eef56679e22a4f1/LICENSE.txt"))
			.GetAwaiter ().GetResult ();
	}
}
