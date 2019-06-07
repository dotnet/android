using System;
using System.Collections.Generic;
using System.IO;

namespace Xamarin.Android.Prepare
{
	[TPN]
	class JavaInterop_External_Dependencies_Group : ThirdPartyNoticeGroup
	{
		protected override bool ShouldInclude (bool includeExternalDeps, bool includeBuildDeps) => includeExternalDeps;

		public override List<ThirdPartyNotice> Notices => new List <ThirdPartyNotice> {
			new JavaInterop_xamarin_Java_Interop_TPN (),
			new JavaInterop_xamarin_mono_cecil_TPN (),
			new JavaInterop_jonpryor_mono_linq_expressions_TPN (),
			new JavaInterop_mono_csharp_TPN (),
			new JavaInterop_mono_LineEditor_TPN (),
			new JavaInterop_mono_Options_TPN (),
			new JavaInterop_zzzprojects_HtmlAgilityPack_TPN (),
		};
	}

	class JavaInterop_xamarin_Java_Interop_TPN : ThirdPartyNotice
	{
		static readonly Uri    url         = new Uri ("https://github.com/xamarin/Java.Interop/");
		static readonly string licenseFile = Path.Combine (Configurables.Paths.ExternalJavaInteropDir, "LICENSE");

		public override string LicenseFile => licenseFile;
		public override string Name        => "xamarin/Java.Interop";
		public override Uri    SourceUrl   => url;
		public override string LicenseText => null;
	}

	// git submodules of Java.Interop
	class JavaInterop_xamarin_mono_cecil_TPN : ThirdPartyNotice
	{
		static readonly Uri    url         = new Uri ("https://github.com/mono/cecil/");
		static readonly string licenseFile = Path.Combine (Configurables.Paths.ExternalJavaInteropDir, "external", "cecil", "LICENSE.txt");

		public override string LicenseFile => licenseFile;
		public override string Name        => "mono/cecil";
		public override Uri    SourceUrl   => url;
		public override string LicenseText => null;
	}

	class JavaInterop_jonpryor_mono_linq_expressions_TPN : ThirdPartyNotice
	{
		static readonly Uri    url         = new Uri ("https://github.com/jonpryor/mono.linq.expressions/");

		public override string LicenseFile => null;
		public override string Name        => "jonpryor/mono.linq.expressions";
		public override Uri    SourceUrl   => url;

		public override string LicenseText => @"
        Copyright (C) 2012 Jb Evain

        Permission is hereby granted, free of charge, to any person obtaining
        a copy of this software and associated documentation files (the
        ""Software""), to deal in the Software without restriction, including
        without limitation the rights to use, copy, modify, merge, publish,
        distribute, sublicense, and/or sell copies of the Software, and to
        permit persons to whom the Software is furnished to do so, subject to
        the following conditions:

        The above copyright notice and this permission notice shall be
        included in all copies or substantial portions of the Software.

        THE SOFTWARE IS PROVIDED ""AS IS"", WITHOUT WARRANTY OF ANY KIND,
        EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
        MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
        NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
        LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
        OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
        WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
";

		public override bool   Include(bool includeExternalDeps, bool includeBuildDeps) => includeBuildDeps;
	}

	// from various packages.config files in Java.Interop
	class JavaInterop_mono_csharp_TPN : ThirdPartyNotice
	{
		static readonly Uri    url         = new Uri ("https://github.com/mono/mono/tree/master/mcs/class/Mono.CSharp");

		public override string LicenseFile => CommonLicenses.MonoMITPath;
		public override string Name        => "mono/csharp";
		public override Uri    SourceUrl   => url;

		public override string LicenseText => null;
	}

	class JavaInterop_mono_LineEditor_TPN : ThirdPartyNotice
	{
		static readonly Uri    url         = new Uri ("https://github.com/mono/LineEditor/blob/master/LICENSE");

		public override string LicenseFile => CommonLicenses.MonoMITPath;
		public override string Name        => "mono/LineEditor";
		public override Uri    SourceUrl   => url;
		public override string LicenseText => null;
	}

	class JavaInterop_mono_Options_TPN : ThirdPartyNotice
	{
		static readonly Uri    url         = new Uri ("https://github.com/mono/mono/tree/master/mcs/class/Mono.Options");

		public override string LicenseFile => CommonLicenses.MonoMITPath;
		public override string Name        => "mono/Options";
		public override Uri    SourceUrl   => url;
		public override string LicenseText => null;
	}

	class JavaInterop_zzzprojects_HtmlAgilityPack_TPN : ThirdPartyNotice
	{
		static readonly Uri    url         = new Uri ("https://github.com/zzzprojects/html-agility-pack");

		public override string LicenseFile => null;
		public override string Name        => "zzzprojects/HtmlAgilityPack";
		public override Uri    SourceUrl   => url;

		public override string LicenseText => @"
        The MIT License (MIT)
        Permission is hereby granted, free of charge, to any person obtaining a copy
        of this software and associated documentation files (the ""Software""), to deal
        in the Software without restriction, including without limitation the rights
        to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
        copies of the Software, and to permit persons to whom the Software is
        furnished to do so, subject to the following conditions:

        The above copyright notice and this permission notice shall be included in all
        copies or substantial portions of the Software.

        THE SOFTWARE IS PROVIDED ""AS IS"", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
        IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
        FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
        AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
        LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
        OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
        SOFTWARE.
";
	}
}
