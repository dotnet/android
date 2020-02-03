using System;
using System.Collections.Generic;

namespace Xamarin.Android.Prepare
{
	abstract partial class LinuxUbuntuCommon : LinuxDebianCommon
	{
		static readonly List<DebianLinuxProgram> libtoolPackages = new List<DebianLinuxProgram> {
			new DebianLinuxProgram ("libtool-bin", "libtool"),
		};

		protected virtual bool NeedLibtool { get; } = false;

		protected LinuxUbuntuCommon (Context context)
			: base (context)
		{}

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
