using System;
using System.Collections.Generic;
using System.IO;

using Microsoft.Build.Utilities;
using Xamarin.Android.Tools;

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
			public bool appIs64Bit;
			public string appDataDir;
			public string debugServerPath;
			public string outputDir;
			public string appLibrariesDir;
			public List<string>? nativeLibraries;
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

		static readonly string[] deviceLibraries = {
			"libc.so",
			"libm.so",
			"libdl.so",
		};

		TaskLoggingHelper log;
		string packageName;
		string lldbPath;
		string adbPath;
		string outputDir;
		Dictionary<string, string> hostLldbServerPaths;
		string[] supportedAbis;

		public string? AdbDeviceTarget { get; set; }
		public IDictionary<string, List<string>>? NativeLibrariesPerABI { get; set; }

		public NativeDebugger (TaskLoggingHelper logger, string adbPath, string ndkRootPath, string outputDirRoot, string packageName, string[] supportedAbis)
		{
			this.log = logger;
			this.packageName = packageName;
			this.adbPath = adbPath;
			this.supportedAbis = supportedAbis;
			outputDir = Path.Combine (outputDirRoot, "native-debug");

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
		/// Launch the application under control of the debugger.
		/// </summary>
		public void Launch (string activityName)
		{
			if (String.IsNullOrEmpty (activityName)) {
				throw new ArgumentException ("must not be null or empty", nameof (activityName));
			}

			Context context = Init ();
		}

		Context Init ()
		{
			LogLine ();

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

			DetermineArchitectureAndABI (context);
			DetermineAppDataDirectory (context);
			PushDebugServer (context);
			CopyLibraries (context);

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

		void CopyLibraries (Context context)
		{
			LogLine ("Populating local native library cache");
			context.appLibrariesDir = Path.Combine (context.outputDir, "app", "lib");
			if (!Directory.Exists (context.appLibrariesDir)) {
				Directory.CreateDirectory (context.appLibrariesDir);
			}

			if (context.nativeLibraries != null) {
				LogLine ("  Copying application native libraries");
				foreach (string library in context.nativeLibraries) {
					LogLine ($"    {library}");

					string fileName = Path.GetFileName (library);
					if (fileName.StartsWith ("libmono-android.")) {
						fileName = "libmonodroid.so";
					}
					File.Copy (library, Path.Combine (context.appLibrariesDir, fileName), true);
				}
			}

			var requiredFiles = new List<string> ();
			var libraries = new List<string> ();
			string libraryPath;

			if (context.appIs64Bit) {
				libraryPath = "/system/lib64";
				requiredFiles.Add ("/system/bin/app_process64");
				requiredFiles.Add ("/system/bin/linker64");
			} else {
				libraryPath = "/system/lib";
				requiredFiles.Add ("/system/bin/linker");
			}

			foreach (string lib in deviceLibraries) {
				requiredFiles.Add ($"{libraryPath}/{lib}");
			}

			LogLine ("  Copying binaries from device");
			bool isWindows = OS.IsWindows;
			foreach (string file in requiredFiles) {
				string filePath = ToLocalPathFormat (file);
				string localPath = $"{context.outputDir}{filePath}";
				string localDir = Path.GetDirectoryName (localPath);

				if (!Directory.Exists (localDir)) {
					Directory.CreateDirectory (localDir);
				}

				Log ($"    From '{file}' to '{localPath}' ");
				if (!context.adb.Pull (file, localPath).Result) {
					LogLine ("[FAILED]");
				} else {
					LogLine ("[SUCCESS]");
				}
			}

			if (context.appIs64Bit) {
				return;
			}

			// /system/bin/app_process is 32-bit on 32-bit devices, but a symlink to
			// # app_process64 on 64-bit. If we need the 32-bit version, try to pull
			// # app_process32, and if that fails, pull app_process.
			string destination = $"{context.outputDir}{ToLocalPathFormat ("/system/bin/app_process")}";
			string? source = "/system/bin/app_process32";

			if (!context.adb.Pull (source, destination).Result) {
				source = "/system/bin/app_process";
				if (!context.adb.Pull (source, destination).Result) {
					source = null;
				}
			}

			if (String.IsNullOrEmpty (source)) {
				LogLine ("    Failed to copy 32-bit app_process");
			} else {
				Log ($"    From '{source}' to '{destination}' ");
			}

			string ToLocalPathFormat (string path) => isWindows ? path.Replace ("/", "\\") : path;
		}

		void PushDebugServer (Context context)
		{
			if (!hostLldbServerPaths.TryGetValue (context.abi, out string debugServerPath)) {
				throw new InvalidOperationException ($"Debug server for abi '{context.abi}' not found.");
			}

			string serverName = $"{context.arch}-{Path.GetFileName (debugServerPath)}";
			string deviceServerPath = Path.Combine (context.appDataDir, serverName);

			// Always push the server binary, as we don't know what version might already be there
			LogLine ($"Uploading {debugServerPath} to device");

			// First upload to temporary path, as it's writable for everyone
			string remotePath = $"/data/local/tmp/{serverName}";
			if (!context.adb.Push (debugServerPath, remotePath).Result) {
				throw new InvalidOperationException ($"Failed to upload debug server {debugServerPath} to device path {remotePath}");
			}

			// Next, copy it to the app dir, with run-as
			(bool success, string output) = context.adb.Shell (
				"cat", remotePath, "|",
				"run-as", packageName,
				"sh", "-c", $"'cat > {deviceServerPath}'"
			).Result;

			if (!success) {
				throw new InvalidOperationException ($"Failed to copy debug server on device, from {remotePath} to {deviceServerPath}");
			}

			(success, output) = context.adb.RunAs (packageName, "chmod", "700", deviceServerPath).Result;
			if (!success) {
				throw new InvalidOperationException ($"Failed to make debug server executable on device, at {deviceServerPath}");
			}

			context.debugServerPath = deviceServerPath;
			LogLine ($"Debug server path on device: {context.debugServerPath}");
		}

		void DetermineAppDataDirectory (Context context)
		{
			(bool success, string output) = context.adb.GetAppDataDirectory (packageName).Result;
			if (!success) {
				throw new InvalidOperationException ($"Unable to determine data directory for package '{packageName}'");
			}

			context.appDataDir = output.Trim ();
			LogLine ($"Application data directory on device: {context.appDataDir}");

			// Applications with minSdkVersion >= 24 will have their data directories
			// created with rwx------ permissions, preventing adbd from forwarding to
			// the gdbserver socket. To be safe, if we're on a device >= 24, always
			// chmod the directory.
			if (context.apiLevel >= 24) {
				(success, output) = context.adb.RunAs (packageName, "/system/bin/chmod", "a+x", context.appDataDir).Result;
				if (!success) {
					throw new InvalidOperationException ("Failed to make application data directory world executable");
				}
			}
		}

		void DetermineArchitectureAndABI (Context context)
		{
			string[]? deviceABIs = null;

			foreach (string prop in abiProperties) {
				(bool success, string value) = context.adb.GetPropertyValue (prop).Result;
				if (!success) {
					continue;
				}

				deviceABIs = value.Split (',');
				break;
			}

			if (deviceABIs == null || deviceABIs.Length == 0) {
				throw new InvalidOperationException ("Unable to determine device ABI");
			}

			LogABIs ("Application", supportedAbis);
			LogABIs ("     Device", deviceABIs);

			foreach (string deviceABI in deviceABIs) {
				foreach (string appABI in supportedAbis) {
					if (String.Compare (appABI, deviceABI, StringComparison.OrdinalIgnoreCase) == 0) {
						context.abi = deviceABI;
						context.arch = context.abi switch {
							"armeabi" => "arm",
							"armeabi-v7a" => "arm",
							"arm64-v8a" => "arm64",
							_ => context.abi,
						};

						LogLine ($"    Selected ABI: {context.abi} (architecture: {context.arch})");

						context.appIs64Bit = context.abi.IndexOf ("64", StringComparison.Ordinal) >= 0;
						context.outputDir = Path.Combine (outputDir, context.abi);
						if (NativeLibrariesPerABI != null && NativeLibrariesPerABI.TryGetValue (context.abi, out List<string> abiLibraries)) {
							context.nativeLibraries = abiLibraries;
						}
						return;
					}
				}
			}

			throw new InvalidOperationException ($"Application cannot run on the selected device: no matching ABI found");

			void LogABIs (string which, string[] abis)
			{
				string list = String.Join (", ", abis);
				LogLine ($"{which} ABIs: {list}");
			}
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
				path = Path.Combine (ndkRootPath, NdkHelper.RelativeToolchainDir, "lib64", "clang", llvmVersion, "lib", "linux", llvmAbi, "lldb-server");
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
				if (isError) {
					log.LogError (message);
				} else {
					log.LogMessage (message);
				}
			}
		}
	}
}
