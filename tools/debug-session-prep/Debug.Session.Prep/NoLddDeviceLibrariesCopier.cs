using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Xamarin.Android.Utilities;
using Xamarin.Android.Tasks;

namespace Xamarin.Debug.Session.Prep;

class NoLddDeviceLibraryCopier : DeviceLibraryCopier
{
	const string LdConfigPath = "/system/etc/ld.config.txt";

	// To make things interesting, it turns out that API29 devices have **both** API 28 and 29 (they report 28) and for that reason they have TWO config files for ld...
	const string LdConfigPath28 = "/etc/ld.config.28.txt";
	const string LdConfigPath29 = "/etc/ld.config.29.txt";

	// TODO: We probably need a "provider" for the list of paths, since on ARM devices, /system/lib{64} directories contain x86/x64 binaries, and the ARM binaries are found in
	// /system/lib{64]/arm{64} (but not on all devices, of course... e.g. Pixel 6 Pro doesn't have these)
	//
	// List of directory paths to use when the device has neither ldd nor /system/etc/ld.config.txt
	static readonly string[] FallbackLibraryDirectories = {
		"/system/@LIB@",
		"/system/@LIB@/drm",
		"/system/@LIB@/egl",
		"/system/@LIB@/hw",
		"/system/@LIB@/soundfx",
		"/system/@LIB@/ssl",
		"/system/@LIB@/ssl/engines",

		// /system/vendor is a symlink to /vendor on some Android versions, we'll skip the latter then
		"/system/vendor/@LIB@",
		"/system/vendor/@LIB@/egl",
		"/system/vendor/@LIB@/mediadrm",
	};


	public NoLddDeviceLibraryCopier (XamarinLoggingHelper log, AdbRunner adb, bool appIs64Bit, string localDestinationDir, int deviceApiLevel)
		: base (log, adb, appIs64Bit, localDestinationDir, deviceApiLevel)
	{}

	public override bool Copy ()
	{
		string? zygotePath = FetchZygote ();
		if (String.IsNullOrEmpty (zygotePath)) {
			Log.ErrorLine ("Unable to determine path of the zygote process on device");
			return false;
		}

		throw new NotImplementedException();
	}

	List<string> GetLibraryDirectories ()
	{
		if (DeviceApiLevel == 21) {
			// API21 devices (at least emulators) don't return adb error codes, so to avoid awkward error message parsing, we're going to just skip detection since we
			// know what API21 has and doesn't have
			return GetFallbackDirs ();
		}

		string localLdConfigPath = Path.Combine (LocalDestinationDir, ToLocalPathFormat (LdConfigPath));
		Utilities.MakeFileDirectory (localLdConfigPath);

		string deviceLdConfigPath;

		if (DeviceApiLevel == 28) {
			deviceLdConfigPath = LdConfigPath28;
		} else if (DeviceApiLevel == 29) {
			deviceLdConfigPath = LdConfigPath29;
		} else {
			deviceLdConfigPath = LdConfigPath;
		}

		if (!Adb.Pull (deviceLdConfigPath, localLdConfigPath).Result) {
			Log.DebugLine ($"Device doesn't have {LdConfigPath}");
			return GetFallbackDirs ();
		}

		var ret = new List<string> ();

		// TODO: parse ldconfig
		// TODO: must check if device has APEX mountpoints nad include dirs from them as well
		return ret;

		List<string> GetFallbackDirs ()
		{
			string lib = AppIs64Bit ? "lib64" : "lib";

			Log.DebugLine ("Using fallback library directories for this device");
			return FallbackLibraryDirectories.Select (l => l.Replace ("@LIB@", lib)).ToList ();
		}
	}
}
