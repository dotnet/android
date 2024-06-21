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
			new JavaInterop_gityf_crc_TPN (),
			new JavaInterop_javaparser_javaparser_TPN (),
			new JavaInterop_jbevain_mono_linq_expressions_TPN (),
			new JavaInterop_mono_csharp_TPN (),
			new JavaInterop_mono_LineEditor_TPN (),
			new JavaInterop_mono_Options_TPN (),
			new JavaInterop_zzzprojects_HtmlAgilityPack_TPN (),
		};
	}

	class JavaInterop_xamarin_Java_Interop_TPN : ThirdPartyNotice
	{
		static readonly Uri    url         = new Uri ("https://github.com/dotnet/java-interop/");
		static readonly string licenseFile = Path.Combine (Configurables.Paths.ExternalJavaInteropDir, "LICENSE");

		public override string LicenseFile => licenseFile;
		public override string Name        => "dotnet/java-interop";
		public override Uri    SourceUrl   => url;
		public override string LicenseText => String.Empty;
	}

	class JavaInterop_gityf_crc_TPN : ThirdPartyNotice
	{
		static readonly Uri    url         = new Uri ("https://github.com/gityf/crc");

		public override string LicenseFile => String.Empty;
		public override string Name        => "gityf/crc";
		public override Uri    SourceUrl   => url;

		public override string LicenseText => @"
Copyright (c) 2012, Salvatore Sanfilippo <antirez at gmail dot com>
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

  * Redistributions of source code must retain the above copyright notice,
    this list of conditions and the following disclaimer.
  * Redistributions in binary form must reproduce the above copyright
    notice, this list of conditions and the following disclaimer in the
    documentation and/or other materials provided with the distribution.
  * Neither the name of Redis nor the names of its contributors may be used
    to endorse or promote products derived from this software without
    specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS ""AS IS""
AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE
LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
POSSIBILITY OF SUCH DAMAGE.
";
	}

	// via: https://github.com/dotnet/java-interop/blob/b588ef502d8d3b4c32e0ad731115e1b71fd56b5c/tools/java-source-utils/build.gradle#L33-L34
	class JavaInterop_javaparser_javaparser_TPN : ThirdPartyNotice
	{
		static readonly Uri    url         = new Uri ("https://github.com/javaparser/javaparser/");
		static readonly string licenseFile = Path.Combine (Configurables.Paths.ExternalJavaInteropDir, "LICENSE");

		public override string LicenseFile => CommonLicenses.Apache20Path;
		public override string Name        => "javaparser/javaparser";
		public override Uri    SourceUrl   => url;
		public override string LicenseText => String.Empty;
	}

	// git submodules of Java.Interop
	class JavaInterop_jbevain_mono_linq_expressions_TPN : ThirdPartyNotice
	{
		static readonly Uri    url         = new Uri ("https://github.com/jbevain/mono.linq.expressions/");

		public override string LicenseFile => String.Empty;
		public override string Name        => "jbevain/mono.linq.expressions";
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

		public override bool   Include(bool includeExternalDeps, bool includeBuildDeps) => includeExternalDeps && includeBuildDeps;
	}

	// from various packages.config files in Java.Interop
	class JavaInterop_mono_csharp_TPN : ThirdPartyNotice
	{
		static readonly Uri    url         = new Uri ("https://github.com/mono/mono/tree/master/mcs/class/Mono.CSharp");

		public override string LicenseFile => CommonLicenses.MonoMITPath;
		public override string Name        => "mono/csharp";
		public override Uri    SourceUrl   => url;

		public override string LicenseText => String.Empty;
	}

	class JavaInterop_mono_LineEditor_TPN : ThirdPartyNotice
	{
		static readonly Uri    url         = new Uri ("https://github.com/mono/LineEditor/blob/master/LICENSE");

		public override string LicenseFile => CommonLicenses.MonoMITPath;
		public override string Name        => "mono/LineEditor";
		public override Uri    SourceUrl   => url;
		public override string LicenseText => String.Empty;
	}

	class JavaInterop_mono_Options_TPN : ThirdPartyNotice
	{
		static readonly Uri    url         = new Uri ("https://github.com/mono/mono/tree/master/mcs/class/Mono.Options");

		public override string LicenseFile => CommonLicenses.MonoMITPath;
		public override string Name        => "mono/Options";
		public override Uri    SourceUrl   => url;
		public override string LicenseText => String.Empty;
	}

	class JavaInterop_zzzprojects_HtmlAgilityPack_TPN : ThirdPartyNotice
	{
		static readonly Uri    url         = new Uri ("https://github.com/zzzprojects/html-agility-pack");

		public override string LicenseFile => String.Empty;
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
