using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace ApplicationUtility;

public abstract class ApplicationPackage : IAspect
{
	readonly static HashSet<string> KnownApkEntries = new (StringComparer.Ordinal) {
		"AndroidManifest.xml",
		"classes.dex",
	};

	readonly static HashSet<string> KnownAabEntries = new (StringComparer.Ordinal) {
		"BundleConfig.pb",
		"base/manifest/AndroidManifest.xml",
		"base/dex/classes.dex",
	};

	readonly static HashSet<string> KnownBaseEntries = new (StringComparer.Ordinal) {
		"manifest/AndroidManifest.xml",
		"dex/classes.dex",
	};

	public static string AspectName { get; } = "Application package";

	public abstract string PackageFormat { get; }

	protected ZipArchive Zip { get; }
	public string? Description { get; }

	public bool Signed { get; protected set; }
	public ApplicationRuntime Runtime { get; protected set; } = ApplicationRuntime.Unknown;
	public string PackageName { get; protected set; } = "";
	public List<AssemblyStore>? AssemblyStores { get; protected set; }
	public NativeArchitecture Architectures { get; protected set; }

	protected ApplicationPackage (ZipArchive zip, string? description)
	{
		Zip = zip;
		Description = description;
	}

	public static IAspect LoadAspect (Stream stream, string? description)
	{
		Log.Debug ($"ApplicationPackage: opening stream ('{description}') as a ZIP archive");
		ZipArchive? zip = TryOpenAsZip (stream);
		if (zip == null) {
			throw new InvalidOperationException ("Stream is not a ZIP archive. Call ProbeAspect first.");
		}

		ApplicationPackage ret;
		if (IsAPK (zip)) {
			ret = new PackageAPK (zip, description);
		} else if (IsAAB (zip)) {
			ret = new PackageAAB (zip, description);
		} else if (IsBase (zip)) {
			ret = new PackageBase (zip, description);
		} else {
			throw new InvalidOperationException ("Stream is not a supported Android ZIP package. Call ProbeAspect first.");
		}

		Log.Debug ($"ApplicationPackage: stream ('{description}') is: {ret.PackageFormat}");
		return ret;
	}

	public static bool ProbeAspect (Stream stream, string? description)
	{
		Log.Debug ($"ApplicationPackage: checking if stream ('{description}') is a ZIP archive");
		using ZipArchive? zip = TryOpenAsZip (stream);
		if (zip == null) {
			return false;
		}

		Log.Debug ($"ApplicationPackage: checking if stream ('{description}') is a supported Android ZIP package");
		// OK, it's a ZIP. Find out if it's what we support
		string? kind = null;
		if (IsAPK (zip)) {
			kind = "APK";
		} else if (IsAAB (zip)) {
			kind = "AAB";
		} else if (IsBase (zip)) {
			kind = "Base";
		} else {
			return false;
		}

		Log.Debug ($"ApplicationPackage: archive is {kind}");
		return true;
	}

	static bool IsAPK (ZipArchive zip) => HasEntries (zip, KnownApkEntries);
	static bool IsAAB (ZipArchive zip) => HasEntries (zip, KnownAabEntries);
	static bool IsBase (ZipArchive zip) => HasEntries (zip, KnownBaseEntries);

	static bool HasEntries (ZipArchive zip, HashSet<string> knownEntries)
	{
		return zip.Entries.Where ((ZipArchiveEntry entry) => knownEntries.Contains (entry.FullName)).Any ();
	}

	static ZipArchive? TryOpenAsZip (Stream stream)
	{
		stream.Seek (0, SeekOrigin.Begin);
		try {
			return new ZipArchive (stream, ZipArchiveMode.Read, leaveOpen: true);
		} catch (InvalidDataException) {
			return null;
		}
	}
}
