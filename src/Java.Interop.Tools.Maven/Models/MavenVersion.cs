using System;
using Java.Interop.Tools.Maven.Extensions;

namespace Java.Interop.Tools.Maven.Models;

// https://docs.oracle.com/middleware/1212/core/MAVEN/maven_version.htm#MAVEN8855
public class MavenVersion : IComparable, IComparable<MavenVersion>, IEquatable<MavenVersion>
{
	public string? Major { get; private set; }
	public string? Minor { get; private set; }
	public string? Patch { get; private set; }
	public string RawVersion { get; private set; }
	public bool IsValid { get; private set; } = true;

	MavenVersion (string rawVersion) => RawVersion = rawVersion;

	public static MavenVersion Parse (string version)
	{
		var mv = new MavenVersion (version);

		if (!version.HasValue ()) {
			mv.IsValid = false;
			return mv;
		}

		// We're going to parse through this assuming it's a valid Maven version
		mv.Major = version.FirstSubset ('.');
		version = version.ChompFirst ('.');

		if (!TryParsePart (mv.Major, out var _, out var _))
			mv.IsValid = false;

		if (!version.HasValue ())
			return mv;

		mv.Minor = version.FirstSubset ('.');
		version = version.ChompFirst ('.');

		if (!TryParsePart (mv.Minor, out var _, out var _))
			mv.IsValid = false;

		if (!version.HasValue ())
			return mv;

		mv.Patch = version.FirstSubset ('.');
		version = version.ChompFirst ('.');

		if (!TryParsePart (mv.Patch, out var _, out var _))
			mv.IsValid = false;

		if (!version.HasValue ())
			return mv;

		// If there's something left, this is a nonstandard Maven version and all bets are off
		mv.IsValid = false;

		return mv;
	}

	public int CompareTo (object obj)
	{
		return CompareTo (obj as MavenVersion);
	}

	public int CompareTo (MavenVersion? other)
	{
		if (other is null)
			return 1;

		// If either instance is nonstandard, Maven does a simple string compare
		if (!IsValid || !other.IsValid)
			return string.Compare (RawVersion, other.RawVersion);

		var major_compare = ComparePart (Major ?? "0", other.Major ?? "0");

		if (major_compare != 0)
			return major_compare;

		var minor_compare = ComparePart (Minor ?? "0", other.Minor ?? "0");

		if (minor_compare != 0)
			return minor_compare;

		return ComparePart (Patch ?? "0", other.Patch ?? "0");
	}

	public bool Equals (MavenVersion other)
	{
		return CompareTo (other) == 0;
	}

	int ComparePart (string a, string b)
	{
		// Check if they're the same string
		if (a == b)
			return 0;

		// Don't need to check the return because this shouldn't be called if IsValid = false
		TryParsePart (a, out var a_version, out var a_qualifier);
		TryParsePart (b, out var b_version, out var b_qualifier);

		// If neither have a qualifier, treat them like numbers
		if (a_qualifier is null && b_qualifier is null)
			return a_version.CompareTo (b_version);

		// If the numeric versions are different, just use those
		if (a_version != b_version)
			return a_version.CompareTo (b_version);

		// Identical versions with different qualifier fields are compared by using basic string comparison.
		if (a_qualifier is not null && b_qualifier is not null)
			return a_qualifier.CompareTo (b_qualifier);

		// All versions with a qualifier are older than the same version without a qualifier (release version).
		if (a_qualifier is not null)
			return -1;

		return 1;
	}

	static bool TryParsePart (string part, out int version, out string? qualifier)
	{
		// These can look like:
		// 1
		// 1-anything
		var version_string = part.FirstSubset ('-');
		qualifier = null;

		// The first piece must be a number
		if (!int.TryParse (version_string, out version))
			return false;

		part = part.ChompFirst ('-');

		if (part.HasValue ())
			qualifier = part;

		return true;
	}
}
