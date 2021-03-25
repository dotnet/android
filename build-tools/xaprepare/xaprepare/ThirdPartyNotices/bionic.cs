using System;

namespace Xamarin.Android.Prepare
{
	[TPN]
	class google_bionic_TPN : ThirdPartyNotice
	{
		static readonly Uri url = new Uri ("https://android.googlesource.com/platform/bionic/");

		public override string LicenseFile => String.Empty;
		public override string Name => "google/bionic";
		public override Uri SourceUrl => url;
		public override string LicenseText => @"
Copyright (C) 2006 The Android Open Source Project

Licensed under the Apache License, Version 2.0 (the ""License"");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

     http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an ""AS IS"" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
";

		public override bool Include (bool includeExternalDeps, bool includeBuildDeps) => includeExternalDeps;
	}
}
