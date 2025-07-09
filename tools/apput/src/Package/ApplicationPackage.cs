using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

using Xamarin.Android.Tasks;
using Xamarin.Android.Tools;

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

	readonly static HashSet<string> KnownSignatureEntries = new (StringComparer.Ordinal) {
		"META-INF/BNDLTOOL.RSA",
		"META-INF/ANDROIDD.RSA",
	};

	public static string AspectName { get; } = "Application package";

	public abstract string PackageFormat { get; }
	protected abstract string NativeLibDirBase { get; }
	protected abstract string AndroidManifestPath { get; }

	protected ZipArchive Zip { get; }
	public string? Description { get; }

	public bool Signed { get; protected set; }
	public bool ValidAndroidPackage { get; protected set; }
	public bool Debuggable { get; protected set; }
	public ApplicationRuntime Runtime { get; protected set; } = ApplicationRuntime.Unknown;
	public string PackageName { get; protected set; } = "";
	public string MainActivity { get; protected set; } = "";
	public List<AssemblyStore>? AssemblyStores { get; protected set; }
	public List<AndroidTargetArch> Architectures { get; protected set; } = new ();

	protected ApplicationPackage (ZipArchive zip, string? description)
	{
		Zip = zip;
		Description = description;
	}

	public static IAspect LoadAspect (Stream stream, IAspectState state, string? description)
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

		// TODO: for all of the below, add support for detection of older XA apps (just to warn that this version doesn't support
		//       and that people should use older tools)
		ret.TryDetectArchitectures (); // This must be called first, some further steps depend on it
		ret.TryDetectRuntime ();
		ret.TryDetectWhetherIsSigned ();
		ret.TryLoadAssemblyStores ();
		ret.TryLoadAndroidManifest ();

		return ret;
	}

	void TryDetectArchitectures ()
	{
		foreach (AndroidTargetArch arch in Enum.GetValues <AndroidTargetArch> ()) {
			if (!MonoAndroidHelper.SupportedTargetArchitectures.Contains (arch)) {
				continue;
			}

			// We can't simply test for presence of the libDir below, because it's possible
			// that a separate entry for the "directory" (they are only a naming convention
			// in the ZIP archive, not a separate entity) won't exist. Instead, we look for
			// any entry starting with the path.
			if (!HasEntryStartingWith (Zip, GetNativeLibDir (arch))) {
				continue;
			}
			Architectures.Add (arch);
			Log.Debug ($"Detected architecture: {arch}");
		}
	}

	void TryDetectRuntime ()
	{
		ApplicationRuntime runtime = ApplicationRuntime.Unknown;
		string runtimePath;
		foreach (AndroidTargetArch arch in Architectures) {
			runtimePath = GetNativeLibFile (arch, "libcoreclr.so");
			if (HasEntry (Zip, runtimePath)) {
				runtime = ApplicationRuntime.CoreCLR;
				break;
			}

			runtimePath = GetNativeLibFile (arch, "libmonosgen-2.0.so");
			if (HasEntry (Zip, runtimePath)) {
				runtime = ApplicationRuntime.MonoVM;
				break;
			}
		}

		if (runtime != ApplicationRuntime.Unknown || Architectures.Count == 0) {
			Log.Debug ($"Detected runtime: {runtime}");
			return;
		}

		runtimePath = GetNativeLibFile (Architectures[0], "libmonodroid.so");
		if (!HasEntry (Zip, runtimePath)) {
			return;
		}

		// TODO: it might be statically linked CoreCLR runtime. Need to check for presence of
		//       some public symbols to verify that.
	}

	void TryDetectWhetherIsSigned ()
	{
		Signed = HasAnyEntries (Zip, KnownSignatureEntries);
		Log.Debug ($"Signature detected: {Signed}");
	}

	void TryLoadAssemblyStores ()
	{
		foreach (AndroidTargetArch arch in Architectures) {
			string storePath = GetNativeLibFile (arch, $"libassemblies.{MonoAndroidHelper.ArchToAbi (arch)}.blob.so");
			Log.Debug ($"Trying assembly store: {storePath}");
			if (!HasEntry (Zip, storePath)) {
				Log.Debug ($"Assembly store '{storePath}' not found");
				continue;
			}

			Log.Debug ($"Found assembly store entry for architecture {arch}");
			AssemblyStore? store = TryLoadAssemblyStore (storePath);
			if (store == null) {
				continue;
			}
		}
	}

	AssemblyStore? TryLoadAssemblyStore (string storePath)
	{
		// AssemblyStore class owns the stream, don't dispose it here
		FileStream? storeStream = TryGetEntryStream (storePath);
		if (storeStream == null) {
			return null;
		}

		string fullStorePath = $"{Description}@!{storePath}";
		try {
			IAspectState state = AssemblyStore.ProbeAspect (storeStream, fullStorePath);
			if (!state.Success) {
				Log.Debug ($"Assembly store '{storePath}' is not in a supported format");
				storeStream.Close ();
				return null;
			}

			return (AssemblyStore)AssemblyStore.LoadAspect (storeStream, state, fullStorePath);
		} catch (Exception ex) {
			Log.Debug ($"Failed to load assembly store '{storePath}'. Exception thrown:", ex);
			return null;
		}
	}

	void TryLoadAndroidManifest ()
	{
		ValidAndroidPackage = HasEntry (Zip, AndroidManifestPath);
		if (!ValidAndroidPackage) {
			Log.Debug ($"Package is missing manifest entry '{AndroidManifestPath}'");
			return;
		}

		Log.Debug ($"Found Android manifest '{AndroidManifestPath}'");
		using Stream? manifestStream = TryGetEntryStream (AndroidManifestPath);
		// TODO: parse
	}

	string GetNativeLibDir (AndroidTargetArch arch) => $"{NativeLibDirBase}/{MonoAndroidHelper.ArchToAbi (arch)}/";
	string GetNativeLibFile (AndroidTargetArch arch, string fileName) => $"{GetNativeLibDir (arch)}{fileName}";

	FileStream? TryGetEntryStream (string path)
	{
		try {
			ZipArchiveEntry? entry = Zip.GetEntry (path);
			if (entry == null) {
				Log.Debug ($"ZIP entry '{path}' could not be loaded.");
				return null;
			}

			string tempFile = Path.GetTempFileName ();
			TempFileManager.RegisterFile (tempFile);

			Log.Debug ($"Extracting entry '{path}' to '{tempFile}'");
			entry.ExtractToFile (tempFile, overwrite: true);
			return File.OpenRead (tempFile);
		} catch (Exception ex) {
			Log.Debug ($"Failed to load entry '{path}' from the archive.", ex);

			// TODO: remove temp file (using a helper method, which doesn't exist yet)
			return null;
		}
	}

	public static IAspectState ProbeAspect (Stream stream, string? description)
	{
		Log.Debug ($"ApplicationPackage: checking if stream ('{description}') is a ZIP archive");
		using ZipArchive? zip = TryOpenAsZip (stream);
		if (zip == null) {
			return new BasicAspectState (false);
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
			return new BasicAspectState (false);
		}

		Log.Debug ($"ApplicationPackage: archive is {kind}");
		return new BasicAspectState (true);
	}

	static bool IsAPK (ZipArchive zip) => HasAllEntries (zip, KnownApkEntries);
	static bool IsAAB (ZipArchive zip) => HasAllEntries (zip, KnownAabEntries);
	static bool IsBase (ZipArchive zip) => HasAllEntries (zip, KnownBaseEntries);

	static bool HasAnyEntries (ZipArchive zip, HashSet<string> knownEntries)
	{
		return zip.Entries.Where ((ZipArchiveEntry entry) => knownEntries.Contains (entry.FullName)).Any ();
	}

	static bool HasAllEntries (ZipArchive zip, HashSet<string> knownEntries)
	{
		return zip.Entries.Where ((ZipArchiveEntry entry) => knownEntries.Contains (entry.FullName)).Count () == knownEntries.Count;
	}

	static bool HasEntry (ZipArchive zip, string path)
	{
		return zip.Entries.Where ((ZipArchiveEntry entry) => entry.FullName == path).Any ();
	}

	static bool HasEntryStartingWith (ZipArchive zip, string path)
	{
		return zip.Entries.Where ((ZipArchiveEntry entry) => entry.FullName.StartsWith (path, StringComparison.Ordinal)).Any ();
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
