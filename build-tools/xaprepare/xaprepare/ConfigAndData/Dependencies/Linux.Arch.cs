using System.Collections.Generic;

namespace Xamarin.Android.Prepare
{
	class LinuxArch : Linux
	{
		static readonly List<ArchLinuxProgram> packages = new List<ArchLinuxProgram> {
			new ArchLinuxProgram ("autoconf"),
			new ArchLinuxProgram ("automake"),
			new ArchLinuxProgram ("binutils"),
			new ArchLinuxProgram ("bison"),
			new ArchLinuxProgram ("curl"),
			new ArchLinuxProgram ("fakeroot"),
			new ArchLinuxProgram ("file"),
			new ArchLinuxProgram ("findutils"),
			new ArchLinuxProgram ("flex"),
			new ArchLinuxProgram ("gawk"),
			new ArchLinuxProgram ("gcc"),
			new ArchLinuxProgram ("gettext"),
			new ArchLinuxProgram ("git"),
			new ArchLinuxProgram ("grep"),
			new ArchLinuxProgram ("groff"),
			new ArchLinuxProgram ("gtk-sharp-2"),
			new ArchLinuxProgram ("gzip"),
			new ArchLinuxProgram ("jdk8-openjdk"),
			new ArchLinuxProgram ("libtool"),
			new ArchLinuxProgram ("libzip"),
			new ArchLinuxProgram ("m4"),
			new ArchLinuxProgram ("make"),
			new ArchLinuxProgram ("patch"),
			new ArchLinuxProgram ("pkg-config"),
			new ArchLinuxProgram ("referenceassemblies-pcl"),
			new ArchLinuxProgram ("sed"),
			new ArchLinuxProgram ("texinfo"),
			new ArchLinuxProgram ("unzip"),
			new ArchLinuxProgram ("which"),
			new ArchLinuxProgram ("zip"),
			new ArchLinuxProgram ("p7zip"),
		};

		public LinuxArch (Context context)
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
