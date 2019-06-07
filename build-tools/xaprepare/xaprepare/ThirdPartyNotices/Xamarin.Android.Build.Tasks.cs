using System;
using System.Collections.Generic;
using System.IO;

namespace Xamarin.Android.Prepare
{
	// The contents of the following files are originally from
	// 	https://github.com/bazelbuild/bazel/tree/master/src/tools/android/java/com/google/devtools/build/android/incrementaldeployment
	// 	* Xamarin.Android.Build.Tasks/Resources/IncrementalClassLoader.java
	// 	* Xamarin.Android.Build.Tasks/Resources/Placeholder.java
	// 	* Xamarin.Android.Build.Tasks/Resources/MonkeyPatcher.java
	// 	* Xamarin.Android.Build.Tasks/Resources/ResourceLoader.java

	[TPN]
	class XamarinAndroidBuildTasks_bazelbuild_bazel_TPN : ThirdPartyNotice
	{
		static readonly Uri    url         = new Uri ("https://github.com/bazelbuild/bazel/");

		public override string LicenseText => null;
		public override string LicenseFile => CommonLicenses.Apache20Path;
		public override string Name        => "bazelbuild/bazel";
		public override Uri    SourceUrl   => url;
	}

	[TPN]
	class XamarinAndroidBuildTasks_google_desugar_TPN : ThirdPartyNotice
	{
		static readonly Uri    url         = new Uri ("https://android.googlesource.com/platform/external/desugar/+/master/");

		public override string LicenseText => null;
		public override string LicenseFile => CommonLicenses.Apache20Path;
		public override string Name        => "google/desugar";
		public override Uri SourceUrl      => url;
	}

	[TPN]
	class XamarinAndroidBuildTasks_External_Dependencies_Group : ThirdPartyNoticeGroup
	{
		protected override bool ShouldInclude (bool includeExternalDeps, bool includeBuildDeps) => includeExternalDeps;

		public override List<ThirdPartyNotice> Notices => new List <ThirdPartyNotice> {
			new XamarinAndroidBuildTasks_fsharp_fsharp_TPN (),
			new XamarinAndroidBuildTasks_fsprojects_FSharpCompilerCodeDom_TPN (),
			new XamarinAndroidBuildTasks_IronyProject_Irony_TPN (),
			new XamarinAndroidBuildTasks_JamesNK_NewtonsoftJson_TPN (),
			new XamarinAndroidBuildTasks_NuGet_NuGetClient_TPN (),
		};
	}

	class XamarinAndroidBuildTasks_fsharp_fsharp_TPN : ThirdPartyNotice
	{
		static readonly Uri    url         = new Uri ("https://github.com/fsharp/fsharp/");

		public override string LicenseText => null;
		public override string LicenseFile => CommonLicenses.Apache20Path;
		public override string Name        => "fsharp/fsharp";
		public override Uri    SourceUrl   => url;
	}

	class XamarinAndroidBuildTasks_fsprojects_FSharpCompilerCodeDom_TPN : ThirdPartyNotice
	{
		static readonly Uri    url         = new Uri ("https://github.com/fsprojects/FSharp.Compiler.CodeDom/");

		public override string LicenseFile => null;
		public override string Name        => "fsprojects/FSharp.Compiler.CodeDom";
		public override Uri    SourceUrl   => url;

		public override string LicenseText => @"
        This is free and unencumbered software released into the public domain.

        Anyone is free to copy, modify, publish, use, compile, sell, or
        distribute this software, either in source code form or as a compiled
        binary, for any purpose, commercial or non-commercial, and by any
        means.

        In jurisdictions that recognize copyright laws, the author or authors
        of this software dedicate any and all copyright interest in the
        software to the public domain. We make this dedication for the benefit
        of the public at large and to the detriment of our heirs and
        successors. We intend this dedication to be an overt act of
        relinquishment in perpetuity of all present and future rights to this
        software under copyright law.

        THE SOFTWARE IS PROVIDED ""AS IS"", WITHOUT WARRANTY OF ANY KIND,
        EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
        MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
        IN NO EVENT SHALL THE AUTHORS BE LIABLE FOR ANY CLAIM, DAMAGES OR
        OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE,
        ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
        OTHER DEALINGS IN THE SOFTWARE.

        For more information, please refer to <http://unlicense.org>
";
	}

	class XamarinAndroidBuildTasks_IronyProject_Irony_TPN : ThirdPartyNotice
	{
		static readonly Uri    url         = new Uri ("https://github.com/IronyProject/Irony");

		public override string LicenseFile => null;
		public override string Name        => "IronyProject/Irony";
		public override Uri    SourceUrl   => url;

		public override string LicenseText => @"
        Copyright (c) 20012 Roman Ivantsov

        Permission is hereby granted, free of charge, to any person obtaining a copy of this software
        and associated documentation files (the ""Software""), to deal in the Software without restriction,
        including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense,
        and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so,
        subject to the following conditions:

        The above copyright notice and this permission notice shall be included in all copies or
        substantial portions of the Software.

        THE SOFTWARE IS PROVIDED ""AS IS"", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
        INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE
        AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM,
        DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
        OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
";
	}

	class XamarinAndroidBuildTasks_JamesNK_NewtonsoftJson_TPN : ThirdPartyNotice
	{
		static readonly Uri    url         = new Uri ("https://github.com/JamesNK/Newtonsoft.Json");

		public override string LicenseFile => null;
		public override string Name        => "JamesNK/Newtonsoft.Json";
		public override Uri    SourceUrl   => url;

		public override string LicenseText => @"
        The MIT License (MIT)

        Copyright (c) 2007 James Newton-King

        Permission is hereby granted, free of charge, to any person obtaining a copy of this software and
        associated documentation files (the ""Software""), to deal in the Software without restriction, including
        without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
        copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the
        following conditions:

        The above copyright notice and this permission notice shall be included in all copies or substantial
        portions of the Software.

        THE SOFTWARE IS PROVIDED ""AS IS"", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT
        LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
        IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
        WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE
        SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
";
	}

	class XamarinAndroidBuildTasks_NuGet_NuGetClient_TPN : ThirdPartyNotice
	{
		static readonly Uri    url         = new Uri ("https://github.com/NuGet/NuGet.Client");

		public override string LicenseFile => null;
		public override string Name => "NuGet/NuGet.Client";
		public override Uri    SourceUrl => url;

		public override string LicenseText => @"
        Copyright (c) .NET Foundation and Contributors.

        All rights reserved.

        Licensed under the Apache License, Version 2.0 (the ""License""); you may not use
        these files except in compliance with the License. You may obtain a copy of the
        License at

        http://www.apache.org/licenses/LICENSE-2.0

        Unless required by applicable law or agreed to in writing, software distributed
        under the License is distributed on an ""AS IS"" BASIS, WITHOUT WARRANTIES OR
        CONDITIONS OF ANY KIND, either express or implied. See the License for the
        specific language governing permissions and limitations under the License.
";
	}
}
