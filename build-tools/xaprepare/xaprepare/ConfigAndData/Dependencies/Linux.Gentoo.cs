using System.Collections.Generic;

namespace Xamarin.Android.Prepare
{
	class LinuxGentoo : Linux
	{
		static readonly List<GentooLinuxProgram> packages = new List<GentooLinuxProgram> {
			new GentooLinuxProgram ("sys-devel/autoconf"),
			new GentooLinuxProgram ("sys-devel/automake"),
			new GentooLinuxProgram ("sys-devel/binutils"),
			new GentooLinuxProgram ("sys-devel/bison"),
			new GentooLinuxProgram ("net-misc/curl"),
			new GentooLinuxProgram ("sys-apps/fakeroot"),
			new GentooLinuxProgram ("sys-apps/file"),
			new GentooLinuxProgram ("sys-apps/findutils"),
			new GentooLinuxProgram ("sys-devel/flex"),
			new GentooLinuxProgram ("sys-apps/gawk"),
			new GentooLinuxProgram ("sys-devel/gcc"),
			new GentooLinuxProgram ("sys-devel/gettext"),
			new GentooLinuxProgram ("dev-vcs/git"),
			new GentooLinuxProgram ("sys-apps/grep"),
			new GentooLinuxProgram ("sys-apps/groff"),
			//new GentooLinuxProgram ("gtk-sharp-2"),
			new GentooLinuxProgram ("app-arch/gzip"),
			new GentooLinuxProgram ("dev-java/openjdk-bin:8"),
			new GentooLinuxProgram ("sys-devel/libtool"),
			new GentooLinuxProgram ("dev-libs/libzip"),
			new GentooLinuxProgram ("sys-devel/m4"),
			new GentooLinuxProgram ("sys-devel/make"),
			new GentooLinuxProgram ("sys-devel/patch"),
			new GentooLinuxProgram ("dev-util/pkgconf"),
			//new GentooLinuxProgram ("referenceassemblies-pcl"),
			new GentooLinuxProgram ("sys-apps/sed"),
			new GentooLinuxProgram ("sys-apps/texinfo"),
			new GentooLinuxProgram ("app-arch/unzip"),
			new GentooLinuxProgram ("sys-apps/which"),
			new GentooLinuxProgram ("app-arch/zip"),
			new GentooLinuxProgram ("app-arch/p7zip"),
		};

		public LinuxGentoo (Context context)
			: base (context)
		{
			Dependencies.AddRange (packages);
		}

		protected override void InitializeDependencies ()
		{}

		protected override bool InitOS ()
		{
			if (!base.InitOS ())
				return false;

			return true;
		}
	};
}
