//
// AndroidBuildToolsVersion.cs
//
// Author:
//       Matt Ward <matt.ward@xamarin.com>
//
// Copyright (c) 2015 Xamarin Inc. (http://xamarin.com)
//

using System;

namespace Xamarin.AndroidTools
{
	class AndroidBuildToolsVersion : IComparable<AndroidBuildToolsVersion>
	{
		public AndroidBuildToolsVersion (int major, int minor, int build, string specialVersion = "")
			: this (new Version (major, minor, build), specialVersion)
		{
		}

		public AndroidBuildToolsVersion (Version version, string specialVersion = "")
		{
			Version = version;
			SpecialVersion = specialVersion;
		}

		public Version Version { get; private set; }
		public string SpecialVersion { get; private set; }

		public static AndroidBuildToolsVersion Parse (string input)
		{
			AndroidBuildToolsVersion buildToolsVersion = null;

			if (TryParse (input, out buildToolsVersion))
				return buildToolsVersion;

			throw new FormatException (String.Format ("Version string '{0}' is not valid.", input));
		}

		public static bool TryParse (string input, out AndroidBuildToolsVersion result)
		{
			result = null;

			if (String.IsNullOrEmpty (input))
				return false;

			string versionText = input.Trim ();
			string specialVersionText = "";

			int index = versionText.IndexOf (' ');
			if (index > 0) {
				specialVersionText = versionText.Substring (index + 1).Trim ();
				versionText = versionText.Substring (0, index);
			}

			if (!MonoDroidSdk.TryParseVersion (versionText, out Version version))
				return false;

			result = new AndroidBuildToolsVersion (version, specialVersionText);
			return true;
		}

		public int CompareTo (AndroidBuildToolsVersion other)
		{
			int result = Version.CompareTo (other.Version);
			if (result != 0)
				return result;

			return String.Compare (SpecialVersion, other.SpecialVersion, StringComparison.OrdinalIgnoreCase);
		}
	}
}
