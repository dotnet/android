using System.Collections.Generic;

namespace Xamarin.Android.Prepare
{
	abstract class LinuxDebianCommon : Linux
	{
		static readonly List<DebianLinuxProgram> commonPackages = new List<DebianLinuxProgram> {
			new DebianLinuxProgram ("autoconf"),
			new DebianLinuxProgram ("autotools-dev"),
			new DebianLinuxProgram ("automake"),
			new DebianLinuxProgram ("ccache"),
			new DebianLinuxProgram ("cmake"),
			new DebianLinuxProgram ("build-essential"),
			new DebianLinuxProgram ("cli-common-dev"),
			new DebianLinuxProgram ("curl"),
			new DebianLinuxProgram ("devscripts"),
			new DebianLinuxProgram ("gcc"),
			new DebianLinuxProgram ("g++"),
			new DebianLinuxProgram ("git"),
			new DebianLinuxProgram ("libtool"),
			new DebianLinuxProgram ("linux-libc-dev"),
			new DebianLinuxProgram ("make"),
			new DebianLinuxProgram ("ninja-build", "ninja"),
			new DebianLinuxProgram ("p7zip-full", "7z"),
			new DebianLinuxProgram ("sqlite3"),
			new DebianLinuxProgram ("vim-common"),
			new DebianLinuxProgram ("zlib1g-dev"),
		};

		static readonly List<DebianLinuxProgram> commonPackages64bit = new List<DebianLinuxProgram> {
			new DebianLinuxProgram ("lib32stdc++6"),
			new DebianLinuxProgram ("lib32z1"),
		};

		protected override void InitializeDependencies ()
		{
			Dependencies.AddRange (commonPackages);
			if (!Is64Bit)
				Dependencies.AddRange (commonPackages64bit);
		}

		protected LinuxDebianCommon (Context context)
			: base (context)
		{
			Flavor = "Debian";
		}
	};
}
