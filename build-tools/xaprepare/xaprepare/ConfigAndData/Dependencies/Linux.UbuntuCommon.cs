using System;
using System.Collections.Generic;

namespace Xamarin.Android.Prepare
{
	abstract partial class LinuxUbuntuCommon : LinuxDebianCommon
	{
		static readonly List<DebianLinuxProgram> commonPackages64bit = new List<DebianLinuxProgram> {
			new DebianLinuxProgram ("linux-libc-dev:i386"),
			new DebianLinuxProgram ("zlib1g-dev:i386"),
		};

		static readonly List<DebianLinuxProgram> libtoolPackages = new List<DebianLinuxProgram> {
			new DebianLinuxProgram ("libtool-bin", "libtool"),
		};

		protected virtual bool NeedLibtool { get; } = false;

		protected LinuxUbuntuCommon (Context context)
			: base (context)
		{}

		protected override void InitializeDependencies ()
		{
			base.InitializeDependencies ();

			if (Is64Bit)
				Dependencies.AddRange (commonPackages64bit);
		}

		protected override bool InitOS ()
		{
			if (!base.InitOS ())
				return false;

			if (NeedLibtool)
				Dependencies.AddRange (libtoolPackages);

			return true;
		}
	};
}
