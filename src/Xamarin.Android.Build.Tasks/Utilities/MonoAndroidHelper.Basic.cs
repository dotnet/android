using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Hashing;
using System.Text;

using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

partial class MonoAndroidHelper
{
	public static class AndroidAbi
	{
		public const string Arm32 = "armeabi-v7a";
		public const string Arm64 = "arm64-v8a";
		public const string X86	  = "x86";
		public const string X64	  = "x86_64";
	}

	public static class RuntimeIdentifier
	{
		public const string Arm32 = "android-arm";
		public const string Arm64 = "android-arm64";
		public const string X86	  = "android-x86";
		public const string X64	  = "android-x64";
	}

	public static readonly HashSet<AndroidTargetArch> SupportedTargetArchitectures = new HashSet<AndroidTargetArch> {
		AndroidTargetArch.Arm,
		AndroidTargetArch.Arm64,
		AndroidTargetArch.X86,
		AndroidTargetArch.X86_64,
	};

	static readonly char[] ZipPathTrimmedChars = {'/', '\\'};

	static readonly Dictionary<string, string> ClangAbiMap = new Dictionary<string, string> (StringComparer.OrdinalIgnoreCase) {
		{"arm64-v8a",	"aarch64"},
		{"armeabi-v7a", "arm"},
		{"x86",		"i686"},
		{"x86_64",	"x86_64"}
	};

	static readonly Dictionary<string, AndroidTargetArch> AbiToArchMap = new Dictionary<string, AndroidTargetArch> (StringComparer.OrdinalIgnoreCase) {
		{ AndroidAbi.Arm32, AndroidTargetArch.Arm },
		{ AndroidAbi.Arm64, AndroidTargetArch.Arm64 },
		{ AndroidAbi.X86,   AndroidTargetArch.X86 },
		{ AndroidAbi.X64,   AndroidTargetArch.X86_64 },
	};

	static readonly Dictionary<string, string> AbiToRidMap = new Dictionary<string, string> (StringComparer.OrdinalIgnoreCase) {
		{ AndroidAbi.Arm32, RuntimeIdentifier.Arm32 },
		{ AndroidAbi.Arm64, RuntimeIdentifier.Arm64 },
		{ AndroidAbi.X86,   RuntimeIdentifier.X86 },
		{ AndroidAbi.X64,   RuntimeIdentifier.X64 },
	};

	static readonly Dictionary<string, string> RidToAbiMap = new Dictionary<string, string> (StringComparer.OrdinalIgnoreCase) {
		{ RuntimeIdentifier.Arm32, AndroidAbi.Arm32 },
		{ RuntimeIdentifier.Arm64, AndroidAbi.Arm64 },
		{ RuntimeIdentifier.X86,   AndroidAbi.X86 },
		{ RuntimeIdentifier.X64,   AndroidAbi.X64 },
	};

	static readonly Dictionary<string, AndroidTargetArch> RidToArchMap = new Dictionary<string, AndroidTargetArch> (StringComparer.OrdinalIgnoreCase) {
		{ RuntimeIdentifier.Arm32, AndroidTargetArch.Arm },
		{ RuntimeIdentifier.Arm64, AndroidTargetArch.Arm64 },
		{ RuntimeIdentifier.X86,   AndroidTargetArch.X86 },
		{ RuntimeIdentifier.X64,   AndroidTargetArch.X86_64 },
	};

	static readonly Dictionary<AndroidTargetArch, string> ArchToRidMap = new Dictionary<AndroidTargetArch, string> {
		{ AndroidTargetArch.Arm,    RuntimeIdentifier.Arm32 },
		{ AndroidTargetArch.Arm64,  RuntimeIdentifier.Arm64 },
		{ AndroidTargetArch.X86,    RuntimeIdentifier.X86 },
		{ AndroidTargetArch.X86_64, RuntimeIdentifier.X64 },
	};

	static readonly Dictionary<AndroidTargetArch, string> ArchToAbiMap = new Dictionary<AndroidTargetArch, string> {
		{ AndroidTargetArch.Arm,    AndroidAbi.Arm32 },
		{ AndroidTargetArch.Arm64,  AndroidAbi.Arm64 },
		{ AndroidTargetArch.X86,    AndroidAbi.X86 },
		{ AndroidTargetArch.X86_64, AndroidAbi.X64 },
	};

	public static AndroidTargetArch AbiToTargetArch (string abi)
	{
		if (!AbiToArchMap.TryGetValue (abi, out AndroidTargetArch arch)) {
			return AndroidTargetArch.None;
		};

		return arch;
	}

	public static string AbiToRid (string abi)
	{
		if (!AbiToRidMap.TryGetValue (abi, out string rid)) {
			throw new NotSupportedException ($"Internal error: unsupported ABI '{abi}'");
		};

		return rid;
	}

	public static string RidToAbi (string rid)
	{
		if (!RidToAbiMap.TryGetValue (rid, out string abi)) {
			throw new NotSupportedException ($"Internal error: unsupported Runtime Identifier '{rid}'");
		};

		return abi;
	}

	public static AndroidTargetArch RidToArch (string rid)
	{
		if (!RidToArchMap.TryGetValue (rid, out AndroidTargetArch arch)) {
			throw new NotSupportedException ($"Internal error: unsupported Runtime Identifier '{rid}'");
		};

		return arch;
	}

	public static string ArchToRid (AndroidTargetArch arch)
	{
		if (!ArchToRidMap.TryGetValue (arch, out string rid)) {
			throw new InvalidOperationException ($"Internal error: unsupported architecture '{arch}'");
		};

		return rid;
	}

	public static string ArchToAbi (AndroidTargetArch arch)
	{
		if (!ArchToAbiMap.TryGetValue (arch, out string abi)) {
			throw new InvalidOperationException ($"Internal error: unsupported architecture '{arch}'");
		};

		return abi;
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

	public static bool IsValidAbi (string abi) => AbiToRidMap.ContainsKey (abi);

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
