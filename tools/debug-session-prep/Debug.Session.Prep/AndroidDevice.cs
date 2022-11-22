using System;

using Xamarin.Android.Tasks;
using Xamarin.Android.Utilities;

namespace Xamarin.Debug.Session.Prep;

class AndroidDevice
{
	static readonly string[] abiProperties = {
		// new properties
		"ro.product.cpu.abilist",

		// old properties
		"ro.product.cpu.abi",
		"ro.product.cpu.abi2",
	};

	static readonly string[] serialNumberProperties = {
		"ro.serialno",
		"ro.boot.serialno",
	};

	string packageName;
	string adbPath;
	string[] supportedAbis;
	int apiLevel = -1;
	string? appDataDir;
	string? appLldbBaseDir;
	string? appLldbBinDir;
	string? appLldbLogDir;
	string? abi;
	string? arch;
	string? serialNumber;
	bool appIs64Bit;

	XamarinLoggingHelper log;
	AdbRunner adb;

	public AndroidDevice (XamarinLoggingHelper log, string adbPath, string packageName, string[] supportedAbis, string? adbTargetDevice = null)
	{
		this.adbPath = adbPath;
		this.log = log;
		this.packageName = packageName;
		this.supportedAbis = supportedAbis;

		adb = new AdbRunner (log, adbPath, adbTargetDevice);
	}

	public bool GatherInfo ()
	{
		(bool success, string output) = adb.GetPropertyValue ("ro.build.version.sdk").Result;
		if (!success || String.IsNullOrEmpty (output) || !Int32.TryParse (output, out apiLevel)) {
			log.ErrorLine ("Unable to determine connected device's API level");
			return false;
		}

		// Warn on old Pixel C firmware (b/29381985). Newer devices may have Yama
		// enabled but still work with ndk-gdb (b/19277529).
		(success, output) = adb.Shell ("cat", "/proc/sys/kernel/yama/ptrace_scope", "2>/dev/null").Result;
		if (success &&
		    YamaOK (output.Trim ()) &&
		    PropertyIsEqualTo (adb.GetPropertyValue ("ro.build.product").Result, "dragon") &&
		    PropertyIsEqualTo (adb.GetPropertyValue ("ro.product.name").Result, "ryu")
		) {
			log.WarningLine ("WARNING: The device uses Yama ptrace_scope to restrict debugging. ndk-gdb will");
			log.WarningLine ("    likely be unable to attach to a process. With root access, the restriction");
			log.WarningLine ("    can be lifted by writing 0 to /proc/sys/kernel/yama/ptrace_scope. Consider");
			log.WarningLine ("    upgrading your Pixel C to MXC89L or newer, where Yama is disabled.");
			log.WarningLine ();
		}

		if (!DetermineArchitectureAndABI ()) {
			return false;
		}

		if (!DetermineAppDataDirectory ()) {
			return false;
		}

		serialNumber = GetFirstFoundPropertyValue (serialNumberProperties);
		if (String.IsNullOrEmpty (serialNumber)) {
			log.WarningLine ("Unable to determine device serial number");
		} else {
			log.StatusLine ($"Device serial number", serialNumber);
		}

		return true;

		bool YamaOK (string output)
		{
			return !String.IsNullOrEmpty (output) && String.Compare ("0", output, StringComparison.Ordinal) != 0;
		}

		bool PropertyIsEqualTo ((bool haveProperty, string value) result, string expected)
		{
			return
				result.haveProperty &&
				!String.IsNullOrEmpty (result.value) &&
				String.Compare (result.value, expected, StringComparison.Ordinal) == 0;
		}
	}

	bool DetermineAppDataDirectory ()
	{
		(bool success, string output) = adb.GetAppDataDirectory (packageName).Result;
		if (!success) {
			log.ErrorLine ($"Unable to determine data directory for package '{packageName}'");
			return false;
		}

		appDataDir = output.Trim ();
		log.StatusLine ($"Application data directory on device", appDataDir);

		appLldbBaseDir = $"{appDataDir}/lldb";
		appLldbBinDir = $"{appLldbBaseDir}/bin";
		appLldbLogDir = $"{appLldbBaseDir}/log";

		// Applications with minSdkVersion >= 24 will have their data directories
		// created with rwx------ permissions, preventing adbd from forwarding to
		// the gdbserver socket. To be safe, if we're on a device >= 24, always
		// chmod the directory.
		if (apiLevel >= 24) {
			(success, output) = adb.RunAs (packageName, "/system/bin/chmod", "a+x", appDataDir).Result;
			if (!success) {
				log.ErrorLine ("Failed to make application data directory world executable");
				return false;
			}
		}

		return true;
	}

	bool DetermineArchitectureAndABI ()
	{
		string? propValue = GetFirstFoundPropertyValue (abiProperties);
		string[]? deviceABIs = propValue?.Split (',');

		if (deviceABIs == null || deviceABIs.Length == 0) {
			log.ErrorLine ("Unable to determine device ABI");
			return false;
		}

		LogABIs ("Application", supportedAbis);
		LogABIs ("     Device", deviceABIs);

		foreach (string deviceABI in deviceABIs) {
			foreach (string appABI in supportedAbis) {
				if (String.Compare (appABI, deviceABI, StringComparison.OrdinalIgnoreCase) == 0) {
					abi = deviceABI;
					arch = abi switch {
						"armeabi" => "arm",
						"armeabi-v7a" => "arm",
						"arm64-v8a" => "arm64",
						_ => abi,
					};

					log.StatusLine ($"    Selected ABI", $"{abi} (architecture: {arch})");

					appIs64Bit = abi.IndexOf ("64", StringComparison.Ordinal) >= 0;
					return true;
				}
			}
		}

		log.ErrorLine ($"Application cannot run on the selected device: no matching ABI found");
		return false;

		void LogABIs (string which, string[] abis)
		{
			log.StatusLine ($"{which} ABIs", String.Join (", ", abis));
		}
	}

	string? GetFirstFoundPropertyValue (string[] propertyNames)
	{
		foreach (string prop in propertyNames) {
			(bool success, string value) = adb.GetPropertyValue (prop).Result;
			if (!success) {
				continue;
			}

			return value;
		}

		return null;
	}
}
