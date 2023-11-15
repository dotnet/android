using System.Collections.Generic;

namespace Xamarin.Android.Prepare
{
	class LinuxFedora : Linux
	{
		static readonly List<FedoraLinuxProgram> packages = new List<FedoraLinuxProgram> {
			new FedoraLinuxProgram ("autoconf"),
			new FedoraLinuxProgram ("automake"),
			new FedoraLinuxProgram ("binutils"),
			new FedoraLinuxProgram ("bison"),
			new FedoraLinuxProgram ("curl"),
			new FedoraLinuxProgram ("fakeroot"),
			new FedoraLinuxProgram ("file"),
			new FedoraLinuxProgram ("findutils"),
			new FedoraLinuxProgram ("flex"),
			new FedoraLinuxProgram ("gawk"),
			new FedoraLinuxProgram ("gcc"),
			new FedoraLinuxProgram ("gettext"),
			new FedoraLinuxProgram ("git"),
			new FedoraLinuxProgram ("grep"),
			new FedoraLinuxProgram ("groff"),
			new FedoraLinuxProgram ("gtk-sharp2"),
			new FedoraLinuxProgram ("gzip"),
			new FedoraLinuxProgram ("java-1.8.0-openjdk"),
			new FedoraLinuxProgram ("libtool"),
			new FedoraLinuxProgram ("libzip"),
			new FedoraLinuxProgram ("m4"),
			new FedoraLinuxProgram ("make"),
			new FedoraLinuxProgram ("patch"),
			new FedoraLinuxProgram ("pkgconf"),
			new FedoraLinuxProgram ("referenceassemblies-pcl"),
			new FedoraLinuxProgram ("sed"),
			new FedoraLinuxProgram ("texinfo"),
			new FedoraLinuxProgram ("unzip"),
			new FedoraLinuxProgram ("which"),
			new FedoraLinuxProgram ("zip"),
			new FedoraLinuxProgram ("p7zip"),
		};

		public LinuxFedora (Context context)
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
	}
}
