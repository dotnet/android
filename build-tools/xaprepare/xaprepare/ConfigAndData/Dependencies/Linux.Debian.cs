using System;
using System.IO;
using System.Collections.Generic;

namespace Xamarin.Android.Prepare
{
	class LinuxDebian : LinuxDebianCommon
	{
		const string DebianVersionPath = "/etc/debian_version";

		static readonly List<DebianLinuxProgram> packages = new List<DebianLinuxProgram> {
			new DebianLinuxProgram ("libtool-bin", "libtool"),
		};

		static readonly List<DebianLinuxProgram> packagesPre10 = new List<DebianLinuxProgram> {
			new DebianLinuxProgram ("openjdk-8-jdk"),
		};

		// zulu-8 does NOT exist as official Debian package! We need it for our bots, but we have to figure out what to
		// do with Debian 10+ in general, as it does not contain OpenJDK 8 anymore and we require it to work.
		static readonly List<DebianLinuxProgram> packages10AndNewerBuildBots = new List<DebianLinuxProgram> {
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

			if (DebianRelease.Major >= 10 || (IsTesting && String.Compare ("buster", CodeName, StringComparison.OrdinalIgnoreCase) == 0)) {
				if (Context.IsRunningOnHostedAzureAgent)
					Dependencies.AddRange (packages10AndNewerBuildBots);
			} else
				Dependencies.AddRange (packagesPre10);
		}

		static bool IsDebian10OrNewer (string[] lines)
		{
			if (lines == null || lines.Length < 1)
				return false;

			string version = lines[0].Trim ();
			if (String.IsNullOrEmpty (version))
				return false;

			return
				version.IndexOf ("bullseye", StringComparison.OrdinalIgnoreCase) >= 0 ||
				version.IndexOf ("sid", StringComparison.OrdinalIgnoreCase) >= 0;
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
				}
			}

			if (debianRelease.Major < 10 && DerivativeDistro && File.Exists (DebianVersionPath)) {
				if (IsDebian10OrNewer (File.ReadAllLines (DebianVersionPath)))
				    debianRelease = new Version (10, 0); // faking it, but it's ok
			}

			DebianRelease = debianRelease;

			return true;
		}
	};
}
