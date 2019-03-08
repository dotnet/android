using System;
using System.Collections.Generic;

namespace Xamarin.Android.Prepare
{
	class LinuxDebian : LinuxDebianCommon
	{
		static readonly List<DebianLinuxProgram> packages = new List<DebianLinuxProgram> {
			new DebianLinuxProgram ("zlib1g-dev"),
			new DebianLinuxProgram ("libtool-bin", "libtool"),
		};

		static readonly List<DebianLinuxProgram> packagesPre10 = new List<DebianLinuxProgram> {
			new DebianLinuxProgram ("openjdk-8-jdk"),
		};

		// zulu-8 does NOT exist as official Debian package! We need it for our bots, but we have to figure out what to
		// do with Debian 10+ in general, as it does not contain OpenJDK 8 anymore and we require it to work.
		static readonly List<DebianLinuxProgram> packages10AndNewer = new List<DebianLinuxProgram> {
			new DebianLinuxProgram ("zulu-8"),
		};

		protected Version DebianRelease { get; private set; } = new Version (0, 0);
		protected bool IsTesting { get; private set; }

		public LinuxDebian (Context context)
			: base (context)
		{
			Dependencies.AddRange (packages);
		}

		protected override void InitializeDependencies ()
		{
			base.InitializeDependencies ();

			if (DebianRelease.Major >= 10 || (IsTesting && String.Compare ("buster", CodeName, StringComparison.OrdinalIgnoreCase) == 0))
				Dependencies.AddRange (packages10AndNewer);
			else
				Dependencies.AddRange (packagesPre10);
		}

		protected override bool InitOS ()
		{
			if (!base.InitOS ())
				return false;

			Version debianRelease;
			if (!Version.TryParse (Release, out debianRelease)) {
				if (Int32.TryParse (Release, out int singleNumberVersion)) {
					debianRelease = new Version (singleNumberVersion, 0);
				} else {
					if (String.Compare ("testing", Release, StringComparison.OrdinalIgnoreCase) != 0) {
						Log.ErrorLine ($"Unable to parse string '{Release}' as a valid Debian release version");
						return false;
					}

					IsTesting = true;
					return true;
				}
			}

			DebianRelease = debianRelease;

			return true;
		}
	};
}
