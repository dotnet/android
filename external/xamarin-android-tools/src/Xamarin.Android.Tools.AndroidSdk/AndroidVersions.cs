using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Xamarin.Android.Tools
{
	public class AndroidVersions
	{
		List<AndroidVersion>                installedVersions = new List<AndroidVersion> ();

		public  IReadOnlyList<string>       FrameworkDirectories            { get; }
		public  AndroidVersion?             MaxStableVersion                { get; private set; }
		public  AndroidVersion?             MinStableVersion                { get; private set; }

		public  IReadOnlyList<AndroidVersion>       InstalledBindingVersions    { get; private set; }

		public AndroidVersions (IEnumerable<string> frameworkDirectories)
		{
			if (frameworkDirectories == null)
				throw new ArgumentNullException (nameof (frameworkDirectories));

			var dirs    = new List<string> ();

			foreach (var d in frameworkDirectories) {
				if (!Directory.Exists (d))
					throw new ArgumentException ($"`{d}` must be a directory!", nameof (frameworkDirectories));

				var dp  = d.TrimEnd (Path.DirectorySeparatorChar);
				var dn  = Path.GetFileName (dp);
				// In "normal" use, `dp` will contain e.g. `...\MonoAndroid\v1.0`.
				// We want the `MonoAndroid` dir, not the versioned dir.
				var p   = dn.StartsWith ("v", StringComparison.Ordinal) ? (Path.GetDirectoryName (dp) ?? "") : dp;
				dirs.Add (Path.GetFullPath (p));
			}

			dirs    = dirs.Distinct (StringComparer.OrdinalIgnoreCase)
				.ToList ();

			FrameworkDirectories    = new ReadOnlyCollection<string> (dirs);

			var versions = dirs.SelectMany (d => Directory.EnumerateFiles (d, "AndroidApiInfo.xml", SearchOption.AllDirectories))
				.Select (file => AndroidVersion.Load (file));

			LoadVersions (versions);

			InstalledBindingVersions    = new ReadOnlyCollection<AndroidVersion> (installedVersions);
		}

		public AndroidVersions (IEnumerable<AndroidVersion> versions)
		{
			if (versions == null)
				throw new ArgumentNullException (nameof (versions));

			FrameworkDirectories    = new ReadOnlyCollection<string> (new string [0]);

			LoadVersions (versions);

			InstalledBindingVersions    = new ReadOnlyCollection<AndroidVersion> (installedVersions);
		}

		void LoadVersions (IEnumerable<AndroidVersion> versions)
		{
			foreach (var version in versions) {
				installedVersions.Add (version);
				if (!version.Stable)
					continue;
				if (MaxStableVersion == null || (MaxStableVersion.TargetFrameworkVersion < version.TargetFrameworkVersion)) {
					MaxStableVersion    = version;
				}
				if (MinStableVersion == null || (MinStableVersion.TargetFrameworkVersion > version.TargetFrameworkVersion)) {
					MinStableVersion = version;
				}
			}
		}

		public int? GetApiLevelFromFrameworkVersion (string frameworkVersion)
		{
			return installedVersions.FirstOrDefault (v => MatchesFrameworkVersion (v, frameworkVersion))?.ApiLevel ??
				KnownVersions.FirstOrDefault (v => MatchesFrameworkVersion (v, frameworkVersion))?.ApiLevel;
		}

		static bool MatchesFrameworkVersion (AndroidVersion version, string frameworkVersion)
		{
			return version.FrameworkVersion == frameworkVersion ||
				version.OSVersion == frameworkVersion;
		}

		public int? GetApiLevelFromId (string id)
		{
			return installedVersions.FirstOrDefault (v => MatchesId (v, id))?.ApiLevel ??
				KnownVersions.FirstOrDefault (v => MatchesId (v, id))?.ApiLevel ??
				(int.TryParse (id, out int apiLevel) ? apiLevel : default (int?));
		}

		static bool MatchesId (AndroidVersion version, string id)
		{
			return version.Id == id ||
				(version.AlternateIds?.Contains (id) ?? false) ||
				(version.ApiLevel.ToString () == id);
		}

		public string? GetIdFromApiLevel (int apiLevel)
		{
			return installedVersions.FirstOrDefault (v => v.ApiLevel == apiLevel)?.Id ??
				KnownVersions.FirstOrDefault (v => v.ApiLevel == apiLevel)?.Id ??
				apiLevel.ToString ();
		}

		// Sometimes, e.g. when new API levels are introduced, the "API level" is a letter, not a number,
		// e.g. 'API-H' for API-11, 'API-O' for API-26, etc.
		public string? GetIdFromApiLevel (string apiLevel)
		{
			if (int.TryParse (apiLevel, out var platform))
				return GetIdFromApiLevel (platform);
			return installedVersions.FirstOrDefault (v => MatchesId (v, apiLevel))?.Id ??
				KnownVersions.FirstOrDefault (v => MatchesId (v, apiLevel))?.Id ??
				apiLevel;
		}

		public string? GetIdFromFrameworkVersion (string frameworkVersion)
		{
			return installedVersions.FirstOrDefault (v => MatchesFrameworkVersion (v, frameworkVersion))?.Id ??
				KnownVersions.FirstOrDefault (v => MatchesFrameworkVersion (v, frameworkVersion))?.Id;
		}

		public string? GetFrameworkVersionFromApiLevel (int apiLevel)
		{
			return installedVersions.FirstOrDefault (v => v.ApiLevel == apiLevel)?.FrameworkVersion ??
				KnownVersions.FirstOrDefault (v => v.ApiLevel == apiLevel)?.FrameworkVersion;
		}

		public string? GetFrameworkVersionFromId (string id)
		{
			return installedVersions.FirstOrDefault (v => MatchesId (v, id))?.FrameworkVersion ??
				KnownVersions.FirstOrDefault (v => MatchesId (v, id))?.FrameworkVersion;
		}

		public static readonly AndroidVersion [] KnownVersions = new [] {
			new AndroidVersion (4,  "1.6",   "Donut"),
			new AndroidVersion (5,  "2.0",   "Eclair"),
			new AndroidVersion (6,  "2.0.1", "Eclair"),
			new AndroidVersion (7,  "2.1",   "Eclair"),
			new AndroidVersion (8,  "2.2",   "Froyo"),
			new AndroidVersion (10, "2.3",   "Gingerbread"),
			new AndroidVersion (11, "3.0",   "Honeycomb") {
				AlternateIds = new[]{ "H" },
			},
			new AndroidVersion (12, "3.1",   "Honeycomb"),
			new AndroidVersion (13, "3.2",   "Honeycomb"),
			new AndroidVersion (14, "4.0",   "Ice Cream Sandwich"),
			new AndroidVersion (15, "4.0.3", "Ice Cream Sandwich"),
			new AndroidVersion (16, "4.1",   "Jelly Bean"),
			new AndroidVersion (17, "4.2",   "Jelly Bean"),
			new AndroidVersion (18, "4.3",   "Jelly Bean"),
			new AndroidVersion (19, "4.4",   "Kit Kat"),
			new AndroidVersion (20, "4.4.87", "Kit Kat + Wear support"),
			new AndroidVersion (21, "5.0",   "Lollipop") {
				AlternateIds = new[]{ "L" },
			},
			new AndroidVersion (22, "5.1",   "Lollipop"),
			new AndroidVersion (23, "6.0",   "Marshmallow") {
				AlternateIds = new[]{ "M" },
			},
			new AndroidVersion (24, "7.0",   "Nougat") {
				AlternateIds = new[]{ "N" },
			},
			new AndroidVersion (25, "7.1",   "Nougat"),
			new AndroidVersion (26, "8.0",   "Oreo") {
				AlternateIds = new[]{ "O" },
			},
			new AndroidVersion (27, "8.1",   "Oreo"),
			new AndroidVersion (28, "9.0",   "Pie") {
				AlternateIds = new[]{ "P" },
			},
			new AndroidVersion (29, "10.0",  "Android10") {
				AlternateIds = new[]{ "Q" },
			},
			new AndroidVersion (30, "11.0") {
				AlternateIds = new[]{ "R" },
			},
			new AndroidVersion (31, "12.0") {
				AlternateIds = new[]{ "S" },
			},
			new AndroidVersion (32, "12.1") {
				AlternateIds = new[]{ "Sv2" },
			},
			new AndroidVersion (33, "13.0") {
				AlternateIds = new[]{ "T" },
			},
		};
	}
}
