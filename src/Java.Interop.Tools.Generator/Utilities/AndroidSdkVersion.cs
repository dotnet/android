
using System;
using System.Diagnostics.CodeAnalysis;

namespace Java.Interop.Tools.Generator;

public struct AndroidSdkVersion : IComparable, IComparable<AndroidSdkVersion>, IEquatable<AndroidSdkVersion>
{
	public  int     ApiLevel        { get; private set; }
	public  int     MinorRelease    { get; private set; }

	public AndroidSdkVersion (int major, int minor = 0)
	{
		ApiLevel   = major;
		MinorRelease   = minor;
	}

	int IComparable.CompareTo (object? value)
	{
		var other = value as AndroidSdkVersion?;
		if (other == null) {
			return 1;
		}
		return CompareTo (other.Value);
	}

	public int CompareTo (AndroidSdkVersion value)
	{
		int r = ApiLevel.CompareTo (value.ApiLevel);
		if (r == 0) {
			r = MinorRelease.CompareTo (value.MinorRelease);
		}
		return r;
	}

	public override int GetHashCode ()
		=> ApiLevel ^ MinorRelease;

	public override bool Equals (object? value)
	{
		var other = value as AndroidSdkVersion?;
		if (other == null) {
			return false;
		}
		return Equals (other.Value);
	}

	public bool Equals (AndroidSdkVersion value)
	{
		return value.ApiLevel == ApiLevel && value.MinorRelease == MinorRelease;
	}

	public override string ToString ()
		=> MinorRelease == 0
		? ApiLevel.ToString ()
		: $"{ApiLevel}.{MinorRelease}";

	//	public static implicit operator ApiLevel (int value)
	//		=> new ApiLevel (value);

	public static bool operator < (AndroidSdkVersion lhs, AndroidSdkVersion rhs)
		=> lhs.CompareTo (rhs) < 0;
	public static bool operator <= (AndroidSdkVersion lhs, AndroidSdkVersion rhs)
		=> lhs.CompareTo (rhs) <= 0;
	public static bool operator > (AndroidSdkVersion lhs, AndroidSdkVersion rhs)
		=> lhs.CompareTo (rhs) > 0;
	public static bool operator >= (AndroidSdkVersion lhs, AndroidSdkVersion rhs)
		=> lhs.CompareTo (rhs) >= 0;
	public static bool operator == (AndroidSdkVersion lhs, AndroidSdkVersion rhs)
		=> lhs.Equals (rhs);
	public static bool operator != (AndroidSdkVersion lhs, AndroidSdkVersion rhs)
		=> !lhs.Equals (rhs);

	public static bool operator < (AndroidSdkVersion lhs, int rhs)
		=> lhs.ApiLevel.CompareTo (rhs) < 0;
	public static bool operator <= (AndroidSdkVersion lhs, int rhs)
		=> lhs.ApiLevel.CompareTo (rhs) <= 0;
	public static bool operator > (AndroidSdkVersion lhs, int rhs)
		=> lhs.ApiLevel.CompareTo (rhs) > 0;
	public static bool operator >= (AndroidSdkVersion lhs, int rhs)
		=> lhs.ApiLevel.CompareTo (rhs) >= 0;
	public static bool operator == (AndroidSdkVersion lhs, int rhs)
		=> lhs.ApiLevel.Equals (rhs);
	public static bool operator != (AndroidSdkVersion lhs, int rhs)
		=> !lhs.ApiLevel.Equals (rhs);

	public static bool operator < (int lhs, AndroidSdkVersion rhs)
		=> lhs.CompareTo (rhs.ApiLevel) < 0;
	public static bool operator <= (int lhs, AndroidSdkVersion rhs)
		=> lhs.CompareTo (rhs.ApiLevel) <= 0;
	public static bool operator > (int lhs, AndroidSdkVersion rhs)
		=> lhs.CompareTo (rhs.ApiLevel) > 0;
	public static bool operator >= (int lhs, AndroidSdkVersion rhs)
		=> lhs.CompareTo (rhs.ApiLevel) >= 0;
	public static bool operator == (int lhs, AndroidSdkVersion rhs)
		=> lhs.Equals (rhs.ApiLevel);
	public static bool operator != (int lhs, AndroidSdkVersion rhs)
		=> !lhs.Equals (rhs.ApiLevel);

	public static bool TryParse (string? value, out AndroidSdkVersion apiLevel)
	{
		if (value == null) {
			apiLevel    = default;
			return false;
		}
		if (Version.TryParse (value, out var v)) {
			apiLevel    = new AndroidSdkVersion (v.Major, v.Minor);
			return true;
		}
		if (int.TryParse (value, out var major)) {
			apiLevel    = new AndroidSdkVersion (major);
			return true;
		}
		apiLevel    = default;
		return false;
	}

	public static AndroidSdkVersion Parse (string? value)
	{
		AndroidSdkVersion v;
		if (TryParse (value, out v)) {
			return v;
		}
		throw new NotSupportedException ($"Could not parse `{value}` as an ApiLevel.");
	}
}
