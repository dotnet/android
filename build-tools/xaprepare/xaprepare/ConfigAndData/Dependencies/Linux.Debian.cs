using System;
using System.Collections.Generic;
using System.IO;

namespace Xamarin.Android.Prepare
{
	class LinuxDebian : LinuxDebianCommon
	{
		const string DebianVersionPath = "/etc/debian_version";

		static readonly Dictionary<string, string> DebianUnstableVersionMap = new Dictionary<string, string> (StringComparer.OrdinalIgnoreCase) {
			{ "bookworm", "12" },
			{ "bookworm/sid", "12" },
			{ "trixie", "13" },
			{ "trixie/sid", "13" },
			{ "forky/sid", "14" },
		};

		protected Version DebianRelease { get; private set; } = new Version (0, 0);
		protected bool IsTesting { get; private set; }

		public LinuxDebian (Context context)
			: base (context)
		{}

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
			       debian_version!.IndexOf ("trixie", StringComparison.OrdinalIgnoreCase) >= 0 ||
			       debian_version!.IndexOf ("forky", StringComparison.OrdinalIgnoreCase) >= 0;
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
