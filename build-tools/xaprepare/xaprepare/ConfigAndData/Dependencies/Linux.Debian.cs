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

		static readonly List<DebianLinuxProgram> packagesPreTrixie = new List<DebianLinuxProgram> {
			new DebianLinuxProgram ("libncurses5-dev"),
		};

		static readonly List<DebianLinuxProgram> packagesTrixieAndLater = new List<DebianLinuxProgram> {
			new DebianLinuxProgram ("libncurses-dev"),
		};

		// zulu-8 does NOT exist as official Debian package! We need it for our bots, but we have to figure out what to
		// do with Debian 10+ in general, as it does not contain OpenJDK 8 anymore and we require it to work.
		static readonly List<DebianLinuxProgram> packages10AndNewerBuildBots = new List<DebianLinuxProgram> {
			new DebianLinuxProgram ("zulu-8"),
		};

		static readonly Dictionary<string, string> DebianUnstableVersionMap = new Dictionary<string, string> (StringComparer.OrdinalIgnoreCase) {
			{ "bookworm", "12" },
			{ "bookworm/sid", "12" },
			{ "trixie", "13" },
			{ "trixie/sid", "13" },
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
				if (DebianRelease.Major >= 13 || (String.Compare ("SparkyLinux", Name, StringComparison.OrdinalIgnoreCase) == 0 && DebianRelease.Major >= 7)) {
					Dependencies.AddRange (packagesTrixieAndLater);
				} else {
					Dependencies.AddRange (packagesPreTrixie);
				}
			} else {
				Dependencies.AddRange (packagesPre10);
				Dependencies.AddRange (packagesPreTrixie);
			}
		}

		static bool IsDebian13OrNewer (string? version)
		{
			if (String.IsNullOrEmpty (version)) {
				return false;
			}

			return
				version.IndexOf ("trixie", StringComparison.OrdinalIgnoreCase) >= 0 ||
				version.IndexOf ("sid", StringComparison.OrdinalIgnoreCase) >= 0;
		}

		static bool IsDebian10OrNewer (string? version)
		{
			if (String.IsNullOrEmpty (version)) {
				return false;
			}

			return
				version!.IndexOf ("bullseye", StringComparison.OrdinalIgnoreCase) >= 0 ||
				version.IndexOf ("bookworm", StringComparison.OrdinalIgnoreCase) >= 0 ||
				version.IndexOf ("trixie", StringComparison.OrdinalIgnoreCase) >= 0 ||
				version.IndexOf ("sid", StringComparison.OrdinalIgnoreCase) >= 0;
		}

		static string? ReadDebianVersion ()
		{
			if (!File.Exists (DebianVersionPath)) {
				return null;
			}

			string[] lines = File.ReadAllLines (DebianVersionPath);
			return lines[0].Trim ();
		}

		static bool IsBookwormSidOrNewer (string? debian_version)
		{
			if (String.IsNullOrEmpty (debian_version)) {
				return false;
			}

			return debian_version!.IndexOf ("bookworm", StringComparison.OrdinalIgnoreCase) >= 0 ||
			       debian_version!.IndexOf ("trixie", StringComparison.OrdinalIgnoreCase) >= 0;
		}

		protected override bool EnsureVersionInformation (Context context)
		{
			string? debian_version = null;
			if (String.IsNullOrEmpty (Release)) {
				// Debian/unstable "bookworm" (to become Debian 12 eventually) removed
				// VERSION_ID and VERSION_CODENAME from /etc/os-release, so we need to
				// fake it
				debian_version = ReadDebianVersion ();
				if (IsBookwormSidOrNewer (debian_version) && DebianUnstableVersionMap.TryGetValue (debian_version!, out string? unstable_version) && unstable_version != null) {
					Release = unstable_version;
				};
			}

			if (!Version.TryParse (Release, out Version? debianRelease) || debianRelease == null) {
				if (Int32.TryParse (Release, out int singleNumberVersion)) {
					debianRelease = new Version (singleNumberVersion, 0);
				} else {
					if (String.Compare ("testing", Release, StringComparison.OrdinalIgnoreCase) != 0) {
						Log.ErrorLine ($"Unable to parse string '{Release}' as a valid Debian release version");
						return false;
					}

					IsTesting = true;
					debianRelease = new Version (12, 0); // Assume testing is newer than bullseye (11)
				}
			}

			if (debianRelease.Major < 10 && DerivativeDistro && File.Exists (DebianVersionPath)) {
				if (String.IsNullOrEmpty (debian_version)) {
					debian_version = ReadDebianVersion ();
				}

				if (IsDebian10OrNewer (debian_version))
				    debianRelease = new Version (10, 0); // faking it, but it's ok
			}

			DebianRelease = debianRelease;

			return true;
		}
	};
}
