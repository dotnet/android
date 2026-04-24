using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

using Xamarin.Android.Tasks;
using Xamarin.Android.Tools;

namespace ApplicationUtility;

/// <summary>
/// Abstract base for Android application packages (APK, AAB, base module). Handles ZIP-based
/// container access, architecture detection, assembly store loading, shared library collection,
/// runtime detection, and manifest parsing.
/// </summary>
public abstract class ApplicationPackage : BaseAspect
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

	AndroidManifest? manifest;

	public override string AspectName { get; } = "Application package";

	protected abstract string AndroidManifestPath { get; }
	protected abstract string NativeLibDirBase { get; }
	public abstract string PackageFormat { get; }

	public AndroidManifest? AndroidManifest                => manifest;
	public List<NativeArchitecture> Architectures          { get; protected set; } = new ();
	public List<AssemblyStore>? AssemblyStores             { get; protected set; }
	public List<ApplicationAssembly>? StandaloneAssemblies { get; protected set; }
	public List<AssemblyPdb>? StandalonePdbs               { get; protected set; }
	public bool Debuggable                                 => manifest?.Debuggable ?? false;
	public string? Description                             { get; }
	public string? MainActivity                            => manifest?.MainActivity;
	public string? MinSdkVersion                           => manifest?.MinSdkVersion;
	public List<NativeAppInfo> NativeAppInfos              { get; protected set; } = new ();
	public string? PackageName                             => manifest?.PackageName;
	public List<string>? Permissions                       => manifest?.Permissions;
	public ApplicationRuntime Runtime                      { get; protected set; } = ApplicationRuntime.Unknown;
	public List<SharedLibrary> SharedLibraries             { get; protected set; } = new ();
	public bool Signed                                     { get; protected set; }
	public string? TargetSdkVersion                        => manifest?.TargetSdkVersion;
	public bool ValidAndroidPackage                        { get; protected set; }
	protected ZipArchive Zip                               { get; }

	protected ApplicationPackage (Stream stream, ZipArchive zip, string? description)
		: base (stream)
	{
		Zip = zip;
		Description = description;
	}

	protected override void Dispose (bool disposing)
	{
		if (Disposed || !disposing) {
			base.Dispose (disposing);
			return;
		}

		// Dispose all the owned aspects, they might be using substreams of our stream
		AndroidManifest?.Dispose ();

		// Then the assembly stores, they might be in shared libraries so they need to go before them
		if (AssemblyStores != null) {
			foreach (AssemblyStore store in AssemblyStores) {
				try {
					store.Dispose ();
				} catch (Exception ex) {
					Log.Debug ("Failed to dispose of an assembly store", ex);
				}
			}
			AssemblyStores.Clear ();
		}

		// Shared libraries are last
		foreach (SharedLibrary lib in SharedLibraries) {
			try {
				lib.Dispose ();
			} catch (Exception ex) {
				Log.Debug ("Failed to dispose of a shared library", ex);
			}
		}
		SharedLibraries.Clear ();

		base.Dispose (disposing);
	}

	protected static IAspect LoadAspect (Type aspectType, Stream stream, IAspectState state, string? description)
	{
		LogLoadAspectStart (aspectType);
		try {
			Log.Debug ($"ApplicationPackage: opening stream ('{description}') as a ZIP archive");
			ZipArchive? zip = TryOpenAsZip (stream);
			if (zip == null) {
				throw new InvalidOperationException ("Stream is not a ZIP archive. Call ProbeAspect first.");
			}

			ApplicationPackage ret;
			if (IsAPK (zip)) {
				ret = new PackageAPK (stream, zip, description);
			} else if (IsAAB (zip)) {
				ret = new PackageAAB (stream, zip, description);
			} else if (IsBase (zip)) {
				ret = new PackageBase (stream, zip, description);
			} else {
				throw new InvalidOperationException ("Stream is not a supported Android ZIP package. Call ProbeAspect first.");
			}
			Log.Debug ($"ApplicationPackage: stream ('{description}') is: {ret.PackageFormat}");

			// TODO: for all of the below, add support for detection of older XA apps (just to warn that this version doesn't support
			//       and that people should use older tools)
			ret.TryDetectArchitectures (); // This must be called first, some further steps depend on it
			ret.CollectSharedLibraries (); // This must be called second, some further steps need the collection of shared libraries
			ret.TryDetectRuntime ();
			ret.TryDetectWhetherIsSigned ();
			ret.TryLoadAssemblyStores ();
			ret.TryLoadStandaloneAssemblies ();
			ret.TryLoadStandalonePdbs ();
			ret.TryLoadAndroidManifest ();
			ret.TryLoadXamarinAppLibraries ();

			return ret;
		} finally {
			LogLoadAspectEnd ();
		}
	}

	void CollectSharedLibraries ()
	{
		Log.Debug ("Collecting shared libraries");
		foreach (NativeArchitecture arch in Architectures) {
			string libDir = GetNativeLibDir (arch);

			foreach (ZipArchiveEntry? entry in Zip.Entries) {
				if (entry == null) {
					continue;
				}

				// See if it's in the right directory...
				if (!entry.FullName.StartsWith (libDir, StringComparison.Ordinal)) {
					continue;
				}

				// ...and that it's a shared library...
				if (!entry.FullName.EndsWith (".so", StringComparison.Ordinal)) {
					continue;
				}

				Stream? stream = TryGetEntryStream (entry);
				SharedLibrary? lib = Detector.FindSharedLibraryAspect (stream, entry.FullName);
				if (lib != null) {
					SharedLibraries.Add (lib);
				}
			}
		}
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
			Architectures.Add (Utilities.AndroidTargetArchToNative (arch));
			Log.Debug ($"Detected architecture: {arch}");
		}
	}

	void TryDetectRuntime ()
	{
		try {
			Runtime = GetRuntimeMaybe ();
		} catch (Exception ex) {
			Log.Debug ("Exception caught while detecting runtime.", ex);
			Runtime = ApplicationRuntime.Unknown;
		}

		Log.Debug ($"Detected runtime: {Runtime}");
	}

	ApplicationRuntime GetRuntimeMaybe ()
	{
		Log.Debug ("Trying to detect runtime");
		ApplicationRuntime runtime = ApplicationRuntime.Unknown;
		string runtimePath;
		foreach (NativeArchitecture arch in Architectures) {
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
			return runtime;
		}

		runtimePath = GetNativeLibFile (Architectures[0], "libmonodroid.so");
		if (HasEntry (Zip, runtimePath)) {
			Log.Debug ("Unknown runtime. libmonodroid.so present but no CoreCLR or MonoVM libraries found.");
			return ApplicationRuntime.Unknown;
		}

		// TODO: it might be statically linked CoreCLR runtime or a NativeAOT application.
		// Need to check for presence of some public symbols to verify that.
		if (TryDetectNativeAotRuntime ()) {
			return ApplicationRuntime.NativeAOT;
		}

		return ApplicationRuntime.Unknown;
	}

	bool TryDetectNativeAotRuntime ()
	{
		Log.Debug ("Probing for NativeAOT runtime");
		foreach (SharedLibrary lib in SharedLibraries) {
			var naotLib = lib as NativeAotSharedLibrary;
			if (naotLib != null) {
				Log.Debug ("Found NativeAOT shared library");
				return true;
			}
		}

		return false;
	}

	void TryLoadXamarinAppLibraries ()
	{
		foreach (NativeArchitecture arch in Architectures) {
			string libPath = GetNativeLibFile (arch, "libxamarin-app.so");
			XamarinAppSharedLibrary? lib = TryLoadLibXamarinApp (libPath);
			if (lib == null) {
				continue;
			}
			NativeAppInfos.Add (new NativeAppInfo (lib));
		}
	}

	XamarinAppSharedLibrary? TryLoadLibXamarinApp (string libPath)
	{
		Stream? libStream = TryGetEntryStream (libPath);
		if (libStream == null) {
			return null;
		}

		string fullLibPath = $"{Description}@!{libPath}";
		try {
			IAspectState state = XamarinAppSharedLibrary.ProbeAspect (libStream, fullLibPath);
			if (!state.Success) {
				Log.Debug ($"Assembly store '{libPath}' is not in a supported format");
				libStream.Close ();
				return null;
			}

			return (XamarinAppSharedLibrary)XamarinAppSharedLibrary.LoadAspect (libStream, state, fullLibPath);
		} catch (Exception ex) {
			Log.Debug ($"Failed to load Xamarin app library '{libPath}'. Exception thrown:", ex);
			return null;
		}
	}

	void TryDetectWhetherIsSigned ()
	{
		Signed = HasAnyEntries (Zip, KnownSignatureEntries);
		Log.Debug ($"Signature detected: {Signed}");
	}

	void TryLoadAssemblyStores ()
	{
		var stores = new List<AssemblyStore> ();
		foreach (SharedLibrary dso in SharedLibraries) {
			var storeLib = dso as AssemblyStoreSharedLibrary;
			if (storeLib == null) {
				continue;
			}

			Log.Debug ($"Assembly store shared library '{storeLib.Name}' found.");
			stores.Add (storeLib.AssemblyStore);
		}

		foreach (NativeArchitecture arch in Architectures) {
			string storePath = GetNativeLibFile (arch, $"libassemblies.{MonoAndroidHelper.ArchToAbi (Utilities.NativeArchToAndroidTarget (arch))}.blob.so");
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

			stores.Add (store);
		}

		if (stores.Count > 0) {
			AssemblyStores = stores;
		}
	}

	AssemblyStore? TryLoadAssemblyStore (string storePath)
	{
		// AssemblyStore class owns the stream, don't dispose it here
		Stream? storeStream = TryGetEntryStream (storePath);
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

	void TryLoadStandaloneAssemblies ()
	{
		var assemblies = new List<ApplicationAssembly> ();
		foreach (SharedLibrary dso in SharedLibraries) {
			var assemblyLib = dso as AssemblySharedLibrary;
			if (assemblyLib == null) {
				continue;
			}

			Log.Debug ($"Assembly shared library '{assemblyLib.Name}' found (assembly name '{assemblyLib.Assembly.Name}')");
			assemblyLib.Assembly.Container = ApplicationAssemblyContainer.ApplicationPackage;
			assemblies.Add (assemblyLib.Assembly);
		}

		if (assemblies.Count > 0) {
			StandaloneAssemblies = assemblies;
		}
	}

	void TryLoadStandalonePdbs ()
	{
		var pdbs = new List<AssemblyPdb> ();
		foreach (SharedLibrary dso in SharedLibraries) {
			var pdbLib = dso as AssemblyPdbSharedLibrary;
			if (pdbLib == null) {
				continue;
			}

			Log.Debug ($"PDB shared library '{pdbLib.Name}' found");
			pdbs.Add (pdbLib.PDB);
		}

		if (pdbs.Count > 0) {
			StandalonePdbs = pdbs;
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

		try {
			Stream? manifestStream = TryGetEntryStream (AndroidManifestPath, extractToMemory: true);
			if (manifestStream == null) {
				Log.Error ("Failed to read android manifest from the application package.");
				return;
			}
			IAspectState manifestState = AndroidManifest.ProbeAspect (manifestStream, AndroidManifestPath);
			if (!manifestState.Success) {
				Log.Debug ($"Failed to detect '{AndroidManifestPath}' package entry as supported Android manifest data.");
				manifestStream.Dispose ();
				return;
			}
			manifest = (AndroidManifest)AndroidManifest.LoadAspect (manifestStream, manifestState, AndroidManifestPath);
		} catch (Exception ex) {
			Log.Debug ($"Failed to load android manifest '{AndroidManifestPath}' from the archive.", ex);
		}
	}

	string GetNativeLibDir (AndroidTargetArch arch) => $"{NativeLibDirBase}/{MonoAndroidHelper.ArchToAbi (arch)}/";
	string GetNativeLibDir (NativeArchitecture arch) => GetNativeLibDir (Utilities.NativeArchToAndroidTarget (arch));
	string GetNativeLibFile (NativeArchitecture arch, string fileName) => $"{GetNativeLibDir (arch)}{fileName}";

	Stream? TryGetEntryStream (string path, bool extractToMemory = false)
	{
		try {
			ZipArchiveEntry? entry = Zip.GetEntry (path);
			if (entry == null) {
				Log.Debug ($"ZIP entry '{path}' could not be loaded.");
				return null;
			}

			return TryGetEntryStream (entry, extractToMemory);
		} catch (Exception ex) {
			Log.Debug ($"Failed to load entry '{path}' from the archive.", ex);
			return null;
		}
	}

	Stream? TryGetEntryStream (ZipArchiveEntry entry, bool extractToMemory = false)
	{
		try {
			if (extractToMemory) {
				Log.Debug ($"Extracting entry '{entry.FullName}' to a memory stream");
				using var inputStream = entry.Open ();
				var outputStream = new MemoryStream ();
				inputStream.CopyTo (outputStream);
				inputStream.Flush ();
				return outputStream;
			}

			string tempFile = Path.GetTempFileName ();
			TempFileManager.RegisterFile (tempFile);

			Log.Debug ($"Extracting entry '{entry.FullName}' to '{tempFile}'");
			entry.ExtractToFile (tempFile, overwrite: true);
			return File.OpenRead (tempFile);
		} catch (Exception ex) {
			Log.Debug ($"Failed to load entry '{entry.FullName}' from the archive.", ex);
			return null;
		}
	}

	protected static IAspectState ProbeAspect (Type aspectType, Stream stream, string? description)
	{
		LogProbeAspectStart (aspectType);
		try {
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
		} finally {
			LogProbeAspectEnd ();
		}
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
