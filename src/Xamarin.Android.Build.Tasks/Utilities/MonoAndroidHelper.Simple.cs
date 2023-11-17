using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Hashing;
using System.Text;

using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

partial class MonoAndroidHelper
{
	static readonly char[] ZipPathTrimmedChars = {'/', '\\'};

	static readonly Dictionary<string, string> ClangAbiMap = new Dictionary<string, string> (StringComparer.OrdinalIgnoreCase) {
		{"arm64-v8a",   "aarch64"},
		{"armeabi-v7a", "arm"},
		{"x86",         "i686"},
		{"x86_64",      "x86_64"}
	};

	public static AndroidTargetArch AbiToTargetArch (string abi)
	{
		return abi switch {
			"armeabi-v7a" => AndroidTargetArch.Arm,
			"arm64-v8a"   => AndroidTargetArch.Arm64,
			"x86_64"      => AndroidTargetArch.X86_64,
			"x86"         => AndroidTargetArch.X86,
			_             => throw new NotSupportedException ($"Internal error: unsupported ABI '{abi}'")
		};
	}

	public static string AbiToRid (string abi)
	{
		return abi switch {
			"armeabi-v7a" => "android-arm",
			"arm64-v8a"   => "android-arm64",
			"x86_64"      => "android-x64",
			"x86"         => "android-x86",
			_             => throw new NotSupportedException ($"Internal error: unsupported ABI '{abi}'")
		};
	}

	public static string ArchToRid (AndroidTargetArch arch)
	{
		return arch switch {
			AndroidTargetArch.Arm64  => "android-arm64",
			AndroidTargetArch.Arm    => "android-arm",
			AndroidTargetArch.X86    => "android-x86",
			AndroidTargetArch.X86_64 => "android-x64",
			_                        => throw new InvalidOperationException ($"Internal error: unsupported architecture '{arch}'")
		};
	}

	public static string ArchToAbi (AndroidTargetArch arch)
	{
		return arch switch {
			AndroidTargetArch.Arm64  => "arm64-v8a",
			AndroidTargetArch.Arm    => "armeabi-v7a",
			AndroidTargetArch.X86    => "x86",
			AndroidTargetArch.X86_64 => "x86_64",
			_                        => throw new InvalidOperationException ($"Internal error: unsupported architecture '{arch}'")
		};
	}

	public static string? CultureInvariantToString (object? obj)
	{
		if (obj == null) {
			return null;
		}

		return Convert.ToString (obj, CultureInfo.InvariantCulture);
	}

	public static string MapAndroidAbiToClang (string androidAbi)
	{
		if (ClangAbiMap.TryGetValue (androidAbi, out string clangAbi)) {
			return clangAbi;
		}
		return null;
	}

	public static string MakeZipArchivePath (string part1, params string[]? pathParts)
	{
		return MakeZipArchivePath (part1, (ICollection<string>?)pathParts);
	}

	public static string MakeZipArchivePath (string part1, ICollection<string>? pathParts)
	{
		var parts = new List<string> ();
		if (!String.IsNullOrEmpty (part1)) {
			parts.Add (part1.TrimEnd (ZipPathTrimmedChars));
		                                   };

		if (pathParts != null && pathParts.Count > 0) {
			foreach (string p in pathParts) {
				if (String.IsNullOrEmpty (p)) {
					continue;
				}
				parts.Add (p.TrimEnd (ZipPathTrimmedChars));
			}
		}

		if (parts.Count == 0) {
			return String.Empty;
		}

		return String.Join ("/", parts);
	}

	public static byte[] Utf8StringToBytes (string str) => Encoding.UTF8.GetBytes (str);

	public static ulong GetXxHash (string str, bool is64Bit) => GetXxHash (Utf8StringToBytes (str), is64Bit);

	public static ulong GetXxHash (byte[] stringBytes, bool is64Bit)
	{
		if (is64Bit) {
			return XxHash64.HashToUInt64 (stringBytes);
		}

		return (ulong)XxHash32.HashToUInt32 (stringBytes);
	}
}
