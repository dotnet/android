//
// AndroidVersion.cs
//
// Author:
//       Michael Hutchinson <mhutch@xamarin.com>
//
// Copyright (c) 2012 Xamarin Inc. (http://xamarin.com)

using System;
using System.Linq;
using System.Collections.Generic;

namespace Xamarin.AndroidTools
{
	[Obsolete ("Use Xamarin.Android.Tools.AndroidVersion, in Xamarin.Android.Tools.AndroidSdk.dll")]
	public class AndroidVersion : IEquatable<AndroidVersion>
	{
		public static readonly int MaxApiLevel = 26;

		public AndroidVersion (int apilevel, string osVersion)
		{
			this.ApiLevel = apilevel;
			this.OSVersion = osVersion;
		}

		internal AndroidVersion (int apilevel, string osVersion, string codeName, Version version)
		{
			this.ApiLevel = apilevel;
			// TODO: remove osVersion from parameter list and generate from version
			this.OSVersion = osVersion;
			this.CodeName = codeName;
			this.Version = version;
		}
		
		public int ApiLevel { get; private set; }
		public string OSVersion { get; private set; }
		public string CodeName { get; private set; }
		public Version Version { get; private set; }

		public static int OSVersionToApiLevel (string osVersion)
		{
			int ret = TryOSVersionToApiLevel (osVersion);
			if (ret == 0)
				throw new ArgumentOutOfRangeException ("OS version not recognized: " + osVersion);
			return ret;
		}

		public static int TryOSVersionToApiLevel (string frameworkVersion)
		{
			// Use MonoDroidSdk.GetApiLevelForFrameworkVersion because that will translate XA versions >= 5.xx to the correct api level
			var apiLevelText = MonoDroidSdk.GetApiLevelForFrameworkVersion (frameworkVersion);
			int apiLevel;
			int.TryParse (apiLevelText, out apiLevel);
			return apiLevel;
		}

		public static string ApiLevelToOSVersion (int apiLevel)
		{
			string ret = TryApiLevelToOSVersion (apiLevel);
			if (ret == null)
				throw new ArgumentOutOfRangeException ("API level not recognized: " + apiLevel);
			return ret;
		}

		public static string TryApiLevelToOSVersion (int apiLevel)
		{
			var osVersion = MonoDroidSdk.GetFrameworkVersionForApiLevel (apiLevel.ToString ());
			if (!string.IsNullOrEmpty (osVersion))
				return osVersion.TrimStart ('v');
			return null;
		}

		public static string TryOSVersionToCodeName (string frameworkVersion)
		{
			// First try to get the code name from InstalledBindingVersions
			if (MonoDroidSdk.AndroidVersions != null) {
				var id = MonoDroidSdk.AndroidVersions.GetIdFromFrameworkVersion(frameworkVersion);
				var bindingVersion = MonoDroidSdk.AndroidVersions.InstalledBindingVersions.FirstOrDefault(v => v.Id == id);
				if (bindingVersion != null) return bindingVersion.CodeName;
			}

			// If for some reason there's an error there then fall back to KnownVersions

			// match on API level, the framework version might not match what we have here (>= XA 5.x uses a different version scheme)
			var apiLevel = TryOSVersionToApiLevel (frameworkVersion);

			foreach (var version in KnownVersions)
				if (version.ApiLevel == apiLevel)
					return version.CodeName;
			return null;
		}

		public static string TryFrameworkVersionToOSVersion (string frameworkVersion)
		{
			// match on API level, the framework version might not match what we have here (>= XA 5.x uses a different version scheme)
			var apiLevel = TryOSVersionToApiLevel (frameworkVersion);

			foreach (AndroidVersion version in KnownVersions)
				if (version.ApiLevel == apiLevel)
					return version.OSVersion;
			return null;
		}

		[Obsolete("No longer used")]
		public static bool IsKnownVersion (string version)
		{
			return TryOSVersionToApiLevel (version) > 0;
		}

		public bool Equals (AndroidVersion other)
		{
			if (other == null)
				return false;

			return ApiLevel == other.ApiLevel &&
				OSVersion == other.OSVersion &&
				CodeName == other.CodeName;
		}

		public override bool Equals (object obj)
		{
			if (obj is null)
				return false;

			if (ReferenceEquals (this, obj))
				return true;

			return Equals (obj as AndroidVersion);
		}

		public static bool operator == (AndroidVersion left, AndroidVersion right)
		{
			if ((object) left == null || (object) right == null)
				return ReferenceEquals (left, right);

			return left.Equals (right);
		}

		public static bool operator != (AndroidVersion left, AndroidVersion right)
		{
			return !(left == right);
		}

		public override int GetHashCode ()
		{
			unchecked
			{
				int hash = 17;
				hash = hash * 23 + ApiLevel.GetHashCode ();
				hash = hash * 23 + OSVersion.GetHashCode ();
				hash = hash * 23 + CodeName.GetHashCode ();
				return hash;
			}
		}

		public static AndroidVersion[] KnownVersions = new[] {
			new AndroidVersion (4,  "1.6",   "Donut",                   new Version (1, 6)),
			new AndroidVersion (5,  "2.0",   "Eclair",                  new Version (2, 0)),
			new AndroidVersion (6,  "2.0.1", "Eclair",                  new Version (2, 0, 1)),
			new AndroidVersion (7,  "2.1",   "Eclair",                  new Version (2, 1)),
			new AndroidVersion (8,  "2.2",   "Froyo",                   new Version (2, 2)),
			new AndroidVersion (10, "2.3",   "Gingerbread",             new Version (2, 3)),
			new AndroidVersion (11, "3.0",   "Honeycomb",               new Version (3, 0)),
			new AndroidVersion (12, "3.1",   "Honeycomb",               new Version (3, 1)),
			new AndroidVersion (13, "3.2",   "Honeycomb",               new Version (3, 2)),
			new AndroidVersion (14, "4.0",   "Ice Cream Sandwich",      new Version (4, 0)),
			new AndroidVersion (15, "4.0.3", "Ice Cream Sandwich",      new Version (4, 0, 3)),
			new AndroidVersion (16, "4.1",   "Jelly Bean",              new Version (4, 1)),
			new AndroidVersion (17, "4.2",   "Jelly Bean",              new Version (4, 2)),
			new AndroidVersion (18, "4.3",   "Jelly Bean",              new Version (4, 3)),
			new AndroidVersion (19, "4.4",   "Kit Kat",                 new Version (4, 4)),
			new AndroidVersion (20, "4.4.87", "Kit Kat + Wear support", new Version (4, 4, 87)),
			new AndroidVersion (21, "5.0",   "Lollipop",                new Version (5, 0)),
			new AndroidVersion (22, "5.1",   "Lollipop",                new Version (5, 1)),
			new AndroidVersion (23, "6.0",   "Marshmallow",             new Version (6, 0)),
			new AndroidVersion (24, "7.0",   "Nougat",                  new Version (7, 0)),
			new AndroidVersion (25, "7.1",   "Nougat",                  new Version (7, 1)),
			new AndroidVersion (26, "8.0",	 "Oreo",                    new Version (8, 0)),
			new AndroidVersion (27, "8.1",	 "Oreo",                    new Version (8, 1)),
		};
	}

	public static class AndroidVersionExtensions
	{
#pragma warning disable CS0618 // Type or member is obsolete

		public static AndroidVersion ToLegacyVersion (this Xamarin.Android.Tools.AndroidVersion androidVersion)
		{
			if (!Version.TryParse (androidVersion.OSVersion, out Version version))
				return null;

			return new AndroidVersion (
				androidVersion.ApiLevel,
				androidVersion.OSVersion,
				androidVersion.CodeName,
				version
			);
		}

#pragma warning restore CS0618

	}
}
