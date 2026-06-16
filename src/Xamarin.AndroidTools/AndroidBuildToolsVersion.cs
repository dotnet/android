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

			try {
				result = ParseInternal (input);
				return true;
			} catch (FormatException) {
				// Non-numeric version component
			} catch (OverflowException) {
				// Version component outside the range of Int32
			} catch (ArgumentException) {
				// Negative or otherwise invalid version component (Version constructor)
			}

			return false;
		}

		public int CompareTo (AndroidBuildToolsVersion other)
		{
			int result = Version.CompareTo (other.Version);
			if (result != 0)
				return result;

			return String.Compare (SpecialVersion, other.SpecialVersion, StringComparison.OrdinalIgnoreCase);
		}

		static AndroidBuildToolsVersion ParseInternal (string input)
		{
			string versionText = input.Trim ();
			string specialVersionText = "";

			int index = versionText.IndexOf (' ');
			if (index > 0) {
				specialVersionText = versionText.Substring (index + 1).Trim ();
				versionText = versionText.Substring (0, index);
			}

			Version version = MonoDroidSdk.ParseVersion (versionText);
			return new AndroidBuildToolsVersion (version, specialVersionText);
		}
	}
}
