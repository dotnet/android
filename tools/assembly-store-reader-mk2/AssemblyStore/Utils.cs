using System;
using System.IO;
using System.Buffers;

using Xamarin.Tools.Zip;

namespace Xamarin.Android.AssemblyStore;

static class Utils
{
	static readonly string[] aabZipEntries = {
		"base/manifest/AndroidManifest.xml",
		"BundleConfig.pb",
	};

	static readonly string[] aabBaseZipEntries = {
		"manifest/AndroidManifest.xml",
	};

	static readonly string[] apkZipEntries = {
		"AndroidManifest.xml",
	};

	public const uint ZIP_MAGIC = 0x4034b50;
	public const uint ASSEMBLY_STORE_MAGIC = 0x41424158;

	public static readonly ArrayPool<byte> BytePool = ArrayPool<byte>.Shared;

	public static (FileFormat format, FileInfo? info) DetectFileFormat (string path)
	{
		if (String.IsNullOrEmpty (path)) {
			return (FileFormat.Unknown, null);
		}

		var info = new FileInfo (path);
		if (!info.Exists) {
			return (FileFormat.Unknown, null);
		}

		using var reader = new BinaryReader (info.OpenRead ());

		// ATM, all formats we recognize have 4-byte magic at the start
		FileFormat format = reader.ReadUInt32 () switch {
			Utils.ZIP_MAGIC            => FileFormat.Zip,
			Utils.ASSEMBLY_STORE_MAGIC => FileFormat.AssemblyStore,
			_                          => FileFormat.Unknown
		};

		if (format == FileFormat.Unknown || format != FileFormat.Zip) {
			return (format, info);
		}

		return (DetectAndroidArchive (info, format), info);
	}

	static FileFormat DetectAndroidArchive (FileInfo info, FileFormat defaultFormat)
	{
		using var zip = ZipArchive.Open (info.FullName, FileMode.Open);

		if (HasAllEntries (zip, aabZipEntries)) {
			return FileFormat.Aab;
		}

		if (HasAllEntries (zip, apkZipEntries)) {
			return FileFormat.Apk;
		}

		if (HasAllEntries (zip, aabBaseZipEntries)) {
			return FileFormat.AabBase;
		}

		return defaultFormat;
	}

	static bool HasAllEntries (ZipArchive zip, string[] entries)
	{
		foreach (string entry in entries) {
			if (!zip.ContainsEntry (entry, caseSensitive: true)) {
				return false;
			}
		}

		return true;
	}
}
