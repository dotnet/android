using System;
using System.Collections.Generic;
using System.IO;

using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// Interface to lldb, the NDK native code debugger.
	/// </summary>
	class NativeDebugger
	{
		sealed class Context
		{
			public AdbRunner adb;
			public int apiLevel;
			public string abi;
			public string arch;
			public string appDataDir;
		}

		// We want the shell/batch scripts first, since they set up Python environment for the debugger
		static readonly string[] lldbNames = {
			"lldb.sh",
			"lldb.cmd",
			"lldb",
			"lldb.exe",
		};

		static readonly string[] abiProperties = {
			// new properties
			"ro.product.cpu.abilist",

			// old properties
			"ro.product.cpu.abi",
			"ro.product.cpu.abi2",
		};

		TaskLoggingHelper log;
		string packageName;
		string lldbPath;
		string adbPath;
		Dictionary<string, string> hostLldbServerPaths;
		string[] supportedAbis;

		public string? AdbDeviceTarget { get; set; }

		public NativeDebugger (TaskLoggingHelper logger, string adbPath, string ndkRootPath, string packageName, string[] supportedAbis)
		{
			this.log = logger;
			this.packageName = packageName;
			this.adbPath = adbPath;
			this.supportedAbis = supportedAbis;

			FindTools (ndkRootPath, supportedAbis);
		}

		/// <summary>
		/// Detect PID of the running application and attach the debugger to it
		/// </summary>
		public void Attach ()
		{
			Context context = Init ();
		}

		/// <summary>
		/// Launch the application under control of the debugger. If <paramref name="activityName"/> is provided,
		/// it will be launched instead of the default launcher activity.
		/// </summary>
		public void Launch (string? activityName)
		{
			Context context = Init ();
		}

		Context Init ()
		{
			var context = new Context {
				adb = new AdbRunner (log, adbPath)
			};

			(bool success, string output) = context.adb.GetPropertyValue ("ro.build.version.sdk").Result;
			if (!success || String.IsNullOrEmpty (output) || !Int32.TryParse (output, out int apiLevel)) {
				throw new InvalidOperationException ("Unable to determine connected device's API level");
			}
			context.apiLevel = apiLevel;

			// Warn on old Pixel C firmware (b/29381985). Newer devices may have Yama
			// enabled but still work with ndk-gdb (b/19277529).
			(success, output) = context.adb.Shell ("cat", "/proc/sys/kernel/yama/ptrace_scope", "2>/dev/null").Result;
			if (success &&
			    YamaOK (output.Trim ()) &&
			    PropertyHasValue (context.adb.GetPropertyValue ("ro.build.product").Result, "dragon") &&
			    PropertyHasValue (context.adb.GetPropertyValue ("ro.product.name").Result, "ryu")
			) {
				LogLine ("WARNING: The device uses Yama ptrace_scope to restrict debugging. ndk-gdb will");
				LogLine ("    likely be unable to attach to a process. With root access, the restriction");
				LogLine ("    can be lifted by writing 0 to /proc/sys/kernel/yama/ptrace_scope. Consider");
				LogLine ("    upgrading your Pixel C to MXC89L or newer, where Yama is disabled.");
				LogLine ();
			}

			DetermineABI (context);
			context.arch = context.abi switch {
				"armeabi" => "arm",
				"armeabi-v7a" => "arm",
				"arm64-v8a" => "arm64",
				_ => context.abi,
			};

			DetermineAppDataDirectory (context);
			LogLine ($"Application data directory: {context.appDataDir}");

			return context;

			bool YamaOK (string output)
			{
				return !String.IsNullOrEmpty (output) && String.Compare ("0", output, StringComparison.Ordinal) != 0;
			}

			bool PropertyHasValue ((bool haveProperty, string value) result, string expected)
			{
				return
					result.haveProperty &&
					!String.IsNullOrEmpty (result.value) &&
					String.Compare (result.value, expected, StringComparison.Ordinal) == 0;
			}
		}

		void DetermineAppDataDirectory (Context context)
		{
			(bool success, string output) = context.adb.GetAppDataDirectory (packageName).Result;
			if (!success) {
				throw new InvalidOperationException ($"Unable to determine data directory for package '{packageName}'");
			}

			context.appDataDir = output.Trim ();

			// Applications with minSdkVersion >= 24 will have their data directories
			// created with rwx------ permissions, preventing adbd from forwarding to
			// the gdbserver socket. To be safe, if we're on a device >= 24, always
			// chmod the directory.
			if (context.apiLevel >= 24) {
				(success, output) = context.adb.Shell ("/system/bin/chmod", "a+x", context.appDataDir).Result;
				if (!success) {
					throw new InvalidOperationException ("Failed to make application data directory world executable");
				}
			}
		}

		void DetermineABI (Context context)
		{
			string[]? deviceABIs = null;

			foreach (string prop in abiProperties) {
				(bool success, string value) = context.adb.GetPropertyValue (prop).Result;
				if (!success) {
					continue;
				}

				deviceABIs = value.Split (',');
			}

			if (deviceABIs == null || deviceABIs.Length == 0) {
				throw new InvalidOperationException ("Unable to determine device ABI");
			}

			LogLine ($"Application ABIs: {String.Join ("", "", supportedAbis)}");
			LogLine ($"Device ABIs: {String.Join ("", "", deviceABIs)}");
			foreach (string deviceABI in deviceABIs) {
				foreach (string appABI in supportedAbis) {
					if (String.Compare (appABI, deviceABI, StringComparison.OrdinalIgnoreCase) == 0) {
						context.abi = deviceABI;
						return;
					}
				}
			}

			throw new InvalidOperationException ($"Application cannot run on the selected device: no matching ABI found");
		}

		string GetLlvmVersion (string toolchainDir)
		{
			string path = Path.Combine (toolchainDir, "AndroidVersion.txt");
			if (!File.Exists (path)) {
				throw new InvalidOperationException ($"LLVM version file not found at '{path}'");
			}

			string[] lines = File.ReadAllLines (path);
			string? line = lines.Length >= 1 ? lines[0].Trim () : null;
			if (String.IsNullOrEmpty (line)) {
				throw new InvalidOperationException ($"Unable to read LLVM version from '{path}'");
			}

			return line;
		}

		void FindTools (string ndkRootPath, string[] supportedAbis)
		{
			string toolchainDir = Path.Combine (ndkRootPath, NdkHelper.RelativeToolchainDir);
			string toolchainBinDir = Path.Combine (toolchainDir, "bin");
			string? path = null;

			foreach (string lldb in lldbNames) {
				path = Path.Combine (toolchainBinDir, lldb);
				if (File.Exists (path)) {
					break;
				}
			}

			if (String.IsNullOrEmpty (path)) {
				throw new InvalidOperationException ($"Unable to locate lldb executable in '{toolchainBinDir}'");
			}
			lldbPath = path;

			hostLldbServerPaths = new Dictionary<string, string> (StringComparer.OrdinalIgnoreCase);
			string llvmVersion = GetLlvmVersion (toolchainDir);
			foreach (string abi in supportedAbis) {
				string llvmAbi = NdkHelper.TranslateAbiToLLVM (abi);
				path = Path.Combine (ndkRootPath, NdkHelper.RelativeToolchainDir, "lib64", "clang", llvmVersion, "lib", "linux", abi, "lldb-server");
				if (!File.Exists (path)) {
					throw new InvalidOperationException ($"LLVM lldb server component for ABI '{abi}' not found at '{path}'");
				}

				hostLldbServerPaths.Add (abi, path);
			}

			if (hostLldbServerPaths.Count == 0) {
				throw new InvalidOperationException ("Unable to find any lldb-server executables, debugging not possible");
			}
		}

		void LogLine (string? message = null, bool isError = false)
		{
			Log (message, isError);
			Log (Environment.NewLine, isError);
		}

		void Log (string? message = null, bool isError = false)
		{
			TextWriter writer = isError ? Console.Error : Console.Out;
			message = message ?? String.Empty;
			writer.Write (message);
			if (!String.IsNullOrEmpty (message)) {
				log.LogError (message);
			}
		}
	}
}
