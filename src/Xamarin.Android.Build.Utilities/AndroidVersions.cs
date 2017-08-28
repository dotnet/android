using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Xamarin.Android.Build.Utilities
{
	public class AndroidVersions
	{
		List<AndroidVersion>            installedVersions   = new List<AndroidVersion> ();

		public  IReadOnlyList<string>   FrameworkDirectories                { get; }
		public  AndroidVersion          MaxStableVersion                    { get; private set; }

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
				var p   = dn.StartsWith ("v", StringComparison.Ordinal) ? Path.GetDirectoryName (dp) : dp;
				dirs.Add (Path.GetFullPath (p));
			}

			dirs    = dirs.Distinct (StringComparer.OrdinalIgnoreCase)
				.ToList ();

			FrameworkDirectories    = new ReadOnlyCollection<string> (dirs);

			ReadAndroidVersions ();

			AndroidLogger.LogInfo (null, "Xamarin.Android Supported $(TargetFrameworkVersion)s: {0}", string.Join (", ", installedVersions));
		}

		void ReadAndroidVersions ()
		{
			foreach (var frameworkDirectory in FrameworkDirectories) {
				foreach (var file in Directory.EnumerateFiles (frameworkDirectory, "AndroidApiInfo.xml", SearchOption.AllDirectories)) {
					try {
						var v   = ToAndroidVersion (file);
						installedVersions.Add (v);
						if (MaxStableVersion == null || (v.Stable && MaxStableVersion.Version < v.Version)) {
							MaxStableVersion = v;
						}
					}
					catch (Exception e) {
						AndroidLogger.LogError (message: $"Could not create AndroidVersion information for `{file}`.", ex: e);
					}
				}
			}
		}

		AndroidVersion ToAndroidVersion (string file)
		{
			var info    = XDocument.Load (file);
			var id      = (string) info.Root.Element ("Id");
			var level   = (int) info.Root.Element ("Level");
			var name    = (string) info.Root.Element ("Name");
			var version = (string) info.Root.Element ("Version");
			var stable  = (bool) info.Root.Element ("Stable");
			var pver    = version.TrimStart ('v');
			var v       = new AndroidVersion (level, pver, name, Version.Parse (pver), version, stable) {
				Id      = id,
			};
			return v;
		}

		public int? GetApiLevelFromFrameworkVersion (string frameworkVersion)
		{
			return installedVersions.FirstOrDefault (v => v.FrameworkVersion == frameworkVersion)?.ApiLevel ??
				KnownVersions.FirstOrDefault (v => v.FrameworkVersion == frameworkVersion)?.ApiLevel;
		}

		public int? GetApiLevelFromId (string id)
		{
			return installedVersions.FirstOrDefault (v => MatchesId (v, id))?.ApiLevel ??
				KnownVersions.FirstOrDefault (v => MatchesId (v, id))?.ApiLevel;
		}

		static bool MatchesId (AndroidVersion version, string id)
		{
			return version.Id == id ||
				(version.AlternateIds?.Contains (id) ?? false) ||
				(version.ApiLevel.ToString () == id);
		}

		public string GetIdFromApiLevel (int apiLevel)
		{
			return installedVersions.FirstOrDefault (v => v.ApiLevel == apiLevel)?.Id ??
				KnownVersions.FirstOrDefault (v => v.ApiLevel == apiLevel)?.Id;
		}

		// Sometimes, e.g. when new API levels are introduced, the "API level" is a letter, not a number,
		// e.g. 'API-H' for API-11, 'API-O' for API-26, etc.
		public string GetIdFromApiLevel (string apiLevel)
		{
			if (int.TryParse (apiLevel, out var platform))
				return GetIdFromApiLevel (platform);
			return installedVersions.FirstOrDefault (v => MatchesId (v, apiLevel))?.Id ??
				KnownVersions.FirstOrDefault (v => MatchesId (v, apiLevel))?.Id;
		}

		public string GetIdFromFrameworkVersion (string frameworkVersion)
		{
			return installedVersions.FirstOrDefault (v => v.FrameworkVersion == frameworkVersion)?.Id ??
				KnownVersions.FirstOrDefault (v => v.FrameworkVersion == frameworkVersion)?.Id;
		}

		public string GetFrameworkVersionFromApiLevel (int apiLevel)
		{
			return installedVersions.FirstOrDefault (v => v.ApiLevel == apiLevel)?.FrameworkVersion ??
				KnownVersions.FirstOrDefault (v => v.ApiLevel == apiLevel)?.FrameworkVersion;
		}

		public string GetFrameworkVersionFromId (string id)
		{
			return installedVersions.FirstOrDefault (v => MatchesId (v, id))?.FrameworkVersion ??
				KnownVersions.FirstOrDefault (v => MatchesId (v, id))?.FrameworkVersion;
		}

		static readonly AndroidVersion [] KnownVersions = new [] {
			new AndroidVersion (4,  "1.6",   "Donut",                   new Version (1, 6)),
			new AndroidVersion (5,  "2.0",   "Eclair",                  new Version (2, 0)),
			new AndroidVersion (6,  "2.0.1", "Eclair",                  new Version (2, 0, 1)),
			new AndroidVersion (7,  "2.1",   "Eclair",                  new Version (2, 1)),
			new AndroidVersion (8,  "2.2",   "Froyo",                   new Version (2, 2)),
			new AndroidVersion (10, "2.3",   "Gingerbread",             new Version (2, 3)),
			new AndroidVersion (11, "3.0",   "Honeycomb",               new Version (3, 0)) {
				AlternateIds = new[]{ "H" },
			},
			new AndroidVersion (12, "3.1",   "Honeycomb",               new Version (3, 1)),
			new AndroidVersion (13, "3.2",   "Honeycomb",               new Version (3, 2)),
			new AndroidVersion (14, "4.0",   "Ice Cream Sandwich",      new Version (4, 0)),
			new AndroidVersion (15, "4.0.3", "Ice Cream Sandwich",      new Version (4, 0, 3)),
			new AndroidVersion (16, "4.1",   "Jelly Bean",              new Version (4, 1)),
			new AndroidVersion (17, "4.2",   "Jelly Bean",              new Version (4, 2)),
			new AndroidVersion (18, "4.3",   "Jelly Bean",              new Version (4, 3)),
			new AndroidVersion (19, "4.4",   "Kit Kat",                 new Version (4, 4)),
			new AndroidVersion (20, "4.4.87", "Kit Kat + Wear support", new Version (4, 4, 87)),
			new AndroidVersion (21, "5.0",   "Lollipop",                new Version (5, 0)) {
				AlternateIds = new[]{ "L" },
			},
			new AndroidVersion (22, "5.1",   "Lollipop",                new Version (5, 1)),
			new AndroidVersion (23, "6.0",   "Marshmallow",             new Version (6, 0)) {
				AlternateIds = new[]{ "M" },
			},
			new AndroidVersion (24, "7.0",   "Nougat",                  new Version (7, 0)) {
				AlternateIds = new[]{ "N" },
			},
			new AndroidVersion (25, "7.1",   "Nougat",                  new Version (7, 1)),
			new AndroidVersion (26, "8.0",   "Oreo",                    new Version (8, 0)) {
				AlternateIds = new[]{ "O" },
			},
		};
	}

	class EqualityComparer<T> : IEqualityComparer<T>
	{
		Func<T, T, bool>    equals;
		Func<T, int>        getHashCode;

		public EqualityComparer (Func<T, T, bool> equals, Func<T, int> getHashCode = null)
		{
			this.equals         = equals;
			this.getHashCode    = getHashCode ?? (v => v.GetHashCode ());
		}

		public bool Equals (T x, T y)
		{
			return equals (x, y);
		}

		public int GetHashCode (T obj)
		{
			return getHashCode (obj);
		}
	}
}
