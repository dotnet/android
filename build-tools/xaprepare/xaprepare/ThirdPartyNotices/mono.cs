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
			new mono_mono_boringssl_TPN (),
			new mono_mono_ikdasm_TPN (),
			new mono_mono_ikvm_fork_TPN (),
			new mono_mono_linker_TPN (),
			new mono_mono_NuGet_BuildTasks_TPN (),
			new mono_mono_NUnitLite_TPN (),
			new mono_mono_rx_net_TPN (),
			new mono_mono_Ix_net_TPN (),
			new mono_llvm_Group (),
		};
	}

	class mono_mono_mono_TPN : ThirdPartyNotice
	{
		static readonly Uri    url         = new Uri ("https://github.com/mono/");
		static readonly string licenseFile = Path.Combine (Configurables.Paths.MonoSourceFullPath, "LICENSE");

		public override string LicenseFile => licenseFile;
		public override string Name        => "mono/mono";
		public override Uri    SourceUrl   => url;
		public override string LicenseText => null;
	}

	class mono_mono_aspnetwebstack_TPN : ThirdPartyNotice
	{
		static readonly Uri    url         = new Uri ("https://github.com/mono/aspnetwebstack/");
		static readonly string licenseFile = Path.Combine (Configurables.Paths.MonoSourceFullPath, "aspnetwebstack", "License.txt");

		public override string LicenseFile => licenseFile;
		public override string Name        => "mono/aspnetwebstack";
		public override Uri    SourceUrl   => url;
		public override string LicenseText => null;

		public override bool Include (bool includeExternalDeps, bool includeBuildDeps) => includeBuildDeps;
	}

	class mono_mono_boringssl_TPN : ThirdPartyNotice
	{
		static readonly Uri    url         = new Uri ("https://github.com/mono/boringssl");
		static readonly string licenseFile = Path.Combine (Configurables.Paths.MonoExternalFullPath, "boringssl", "LICENSE");

		public override string LicenseFile => licenseFile;
		public override string Name        => "mono/boringssl";
		public override Uri    SourceUrl   => url;
		public override string LicenseText => null;

		public override bool Include (bool includeExternalDeps, bool includeBuildDeps) => includeBuildDeps;
	}

	class mono_mono_ikdasm_TPN : ThirdPartyNotice
	{
		static readonly Uri    url         = new Uri ("https://github.com/mono/ikdasm");

		public override string LicenseFile => null;
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

		public override bool Include (bool includeExternalDeps, bool includeBuildDeps) => includeBuildDeps;
	}

	class mono_mono_ikvm_fork_TPN : ThirdPartyNotice
	{
		static readonly Uri    url         = new Uri ("https://github.com/mono/ikvm-fork/");
		static readonly string licenseFile = Path.Combine (Configurables.Paths.MonoExternalFullPath, "ikvm", "LICENSE");

		public override string LicenseFile => licenseFile;
		public override string Name        => "mono/ikvm-fork";
		public override Uri    SourceUrl   => url;
		public override string LicenseText => null;

		public override bool Include (bool includeExternalDeps, bool includeBuildDeps) => includeBuildDeps;
	}

	class mono_mono_linker_TPN : ThirdPartyNotice
	{
		static readonly Uri    url         = new Uri ("https://github.com/mono/linker/");
		static readonly string licenseFile = Path.Combine (Configurables.Paths.MonoExternalFullPath, "linker", "LICENSE");

		public override string LicenseFile => licenseFile;
		public override string Name        => "mono/linker";
		public override Uri    SourceUrl   => url;
		public override string LicenseText => null;
	}

	class mono_mono_NuGet_BuildTasks_TPN : ThirdPartyNotice
	{
		static readonly Uri    url         = new Uri ("https://github.com/mono/NuGet.BuildTasks/");
		static readonly string licenseFile = Path.Combine (Configurables.Paths.MonoExternalFullPath, "nuget-buildtasks", "LICENSE.txt");

		public override string LicenseFile => licenseFile;
		public override string Name        => "mono/NuGet.BuildTasks";
		public override Uri    SourceUrl   => url;
		public override string LicenseText => null;

		public override bool Include (bool includeExternalDeps, bool includeBuildDeps) => includeBuildDeps;
	}

	class mono_mono_NUnitLite_TPN : ThirdPartyNotice
	{
		static readonly Uri    url         = new Uri ("https://github.com/mono/NUnitLite/");
		static readonly string licenseFile = Path.Combine (Configurables.Paths.MonoExternalFullPath, "nunit-lite", "NUnitLite-1.0.0", "LICENSE.txt");

		public override string LicenseFile => licenseFile;
		public override string Name        => "mono/NUnitLite";
		public override Uri    SourceUrl   => url;
		public override string LicenseText => null;

		public override bool Include (bool includeExternalDeps, bool includeBuildDeps) => includeBuildDeps;
	}

	class mono_mono_rx_net_TPN : ThirdPartyNotice
	{
		static readonly Uri    url         = new Uri ("https://github.com/mono/rx/");
		static readonly string licenseFile = Path.Combine (Configurables.Paths.MonoExternalFullPath, "rx", "Rx", "NET", "Source", "license.txt");

		public override string LicenseFile => licenseFile;
		public override string Name        => "mono/rx.net";
		public override Uri    SourceUrl   => url;
		public override string LicenseText => null;

		public override bool Include (bool includeExternalDeps, bool includeBuildDeps) => includeBuildDeps;
	}

	class mono_mono_Ix_net_TPN : ThirdPartyNotice
	{
		static readonly Uri    url         = new Uri ("https://github.com/mono/rx/");
		static readonly string licenseFile = Path.Combine (Configurables.Paths.MonoExternalFullPath, "rx", "Ix", "NET", "license.txt");

		public override string LicenseFile => licenseFile;
		public override string Name        => "mono/Ix.net";
		public override Uri    SourceUrl   => url;
		public override string LicenseText => null;

		public override bool Include (bool includeExternalDeps, bool includeBuildDeps) => includeBuildDeps;
	}

	class mono_llvm_Group : ThirdPartyNoticeGroup
	{
		protected override bool ShouldInclude (bool includeExternalDeps, bool includeBuildDeps) => includeExternalDeps && (includeBuildDeps || Context.Instance.TargetAotAbisEnabled);

		public override List<ThirdPartyNotice> Notices => new List<ThirdPartyNotice> {
			new mono_llvm_llvm_TPN (),
			new mono_llvm_google_test_TPN (),
			new mono_llvm_openbsd_regex_TPN (),
			new mono_llvm_pyyaml_tests_TPN (),
			new mono_llvm_arm_contributions_TPN (),
			new mono_llvm_md5_contributions_TPN (),
		};
	}

	class mono_llvm_llvm_TPN : ThirdPartyNotice
	{
		static readonly Uri    url         = new Uri ("https://github.com/mono/llvm/");
		static readonly string licenseFile = Path.Combine (Configurables.Paths.MonoExternalFullPath, "llvm", "LICENSE.TXT");

		public override string LicenseFile => licenseFile;
		public override string Name        => "mono/llvm";
		public override Uri    SourceUrl   => url;
		public override string LicenseText => null;
	}

	class mono_llvm_google_test_TPN : ThirdPartyNotice
	{
		static readonly Uri    url         = new Uri ("https://github.com/mono/llvm/tree/master/utils/unittest/googletest/");
		static readonly string licenseFile = Path.Combine (Configurables.Paths.MonoExternalFullPath, "llvm", "utils", "unittest", "googletest", "LICENSE.TXT");

		public override string LicenseFile => licenseFile;
		public override string Name        => "mono/llvm Google Test";
		public override Uri    SourceUrl   => url;
		public override string LicenseText => null;
	}

	class mono_llvm_openbsd_regex_TPN : ThirdPartyNotice
	{
		static readonly Uri    url         = new Uri ("https://github.com/mono/llvm/tree/master/lib/Support/");
		static readonly string licenseFile = Path.Combine (Configurables.Paths.MonoExternalFullPath, "llvm", "lib", "Support", "COPYRIGHT.regex");

		public override string LicenseFile => licenseFile;
		public override string Name        => "mono/llvm OpenBSD Regex";
		public override Uri    SourceUrl   => url;
		public override string LicenseText => null;
	}

	class mono_llvm_pyyaml_tests_TPN : ThirdPartyNotice
	{
		static readonly Uri    url         = new Uri ("https://github.com/mono/llvm/tree/master/test/YAMLParser/");
		static readonly string licenseFile = Path.Combine (Configurables.Paths.MonoExternalFullPath, "llvm", "test", "YAMLParser", "LICENSE.txt");

		public override string LicenseFile => licenseFile;
		public override string Name        => "mono/llvm pyyaml tests";
		public override Uri    SourceUrl   => url;
		public override string LicenseText => null;
	}

	class mono_llvm_arm_contributions_TPN : ThirdPartyNotice
	{
		static readonly Uri    url         = new Uri ("https://github.com/mono/llvm/tree/master/lib/Target/ARM/");
		static readonly string licenseFile = Path.Combine (Configurables.Paths.MonoExternalFullPath, "llvm", "lib", "Target", "ARM", "LICENSE.TXT");

		public override string LicenseFile => licenseFile;
		public override string Name        => "mono/llvm ARM contributions";
		public override Uri    SourceUrl   => url;
		public override string LicenseText => null;
	}

	class mono_llvm_md5_contributions_TPN : ThirdPartyNotice
	{
		static readonly Uri    url         = new Uri ("https://github.com/mono/llvm/blob/master/lib/Support/MD5.cpp");

		public override string LicenseFile => null;
		public override string Name        => "mono/llvm md5 contributions";
		public override Uri    SourceUrl   => url;

		public override string LicenseText => @"
        This software was written by Alexander Peslyak in 2001.  No copyright is
        claimed, and the software is hereby placed in the public domain.
        In case this attempt to disclaim copyright and place the software in the
        public domain is deemed null and void, then the software is
        Copyright (c) 2001 Alexander Peslyak and it is hereby released to the
        general public under the following terms:

        Redistribution and use in source and binary forms, with or without
        modification, are permitted.

        There's ABSOLUTELY NO WARRANTY, express or implied.

        (This is a heavily cut-down ""BSD license"".)
";
	}
}
