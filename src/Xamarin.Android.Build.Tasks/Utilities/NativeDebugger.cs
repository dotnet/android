using System;
using System.Collections.Generic;
using System.IO;

using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Utilities;
using Xamarin.Android.Tools;

using TPL = System.Threading.Tasks;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// Interface to lldb, the NDK native code debugger.
	/// </summary>
	class NativeDebugger
	{
		const ConsoleColor ErrorColor   = ConsoleColor.Red;
                const ConsoleColor DebugColor   = ConsoleColor.DarkGray;
                const ConsoleColor InfoColor    = ConsoleColor.Green;
                const ConsoleColor MessageColor = ConsoleColor.Gray;
                const ConsoleColor WarningColor = ConsoleColor.Yellow;
		const ConsoleColor StatusLabel  = ConsoleColor.Cyan;
		const ConsoleColor StatusText   = ConsoleColor.White;

		enum LogLevel
		{
			Error,
			Warning,
			Info,
			Message,
			Debug
		}

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
			public uint applicationPID;
			public List<string>? nativeLibraries;
		}

		// We want the shell/batch scripts first, since they set up Python environment for the debugger
		static readonly string[] lldbNames = {
			"lldb.sh",
			"lldb",
			"lldb.cmd",
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

		static HashSet<string> xaLibraries = new HashSet<string> (StringComparer.Ordinal) {
			"libmonodroid.so",
			"libxamarin-app.so",
			"libxamarin-debug-app-helper.so",
		};

		static readonly object consoleLock = new object ();

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

			if (!FindTools (ndkRootPath, supportedAbis)) {
				throw new InvalidOperationException ("Failed to find all the required tools and utilities");
			}
		}

		/// <summary>
		/// Detect PID of the running application and attach the debugger to it
		/// </summary>
		public bool Attach ()
		{
			Context? context = Init ();

			return context != null;
		}

		/// <summary>
		/// Launch the application under control of the debugger.
		/// </summary>
		public bool Launch (string activityName)
		{
			if (String.IsNullOrEmpty (activityName)) {
				throw new ArgumentException ("must not be null or empty", nameof (activityName));
			}

			Context? context = Init ();
			if (context == null) {
				return false;
			}

			// Start the app, tell it to wait for debugger to attach and to kill any running instance
			// We tell `am` to wait ('-W') for the app to start, so that `pidof` then can find the process
			string launchName = $"{packageName}/{activityName}";
			LogLine ();
			LogStatusLine ("Launching activity", launchName);
			Log ("Waiting for the activity to start...");
			(bool success, string output) = context.adb.Shell ("am", "start", "-D", "-S", "-W", launchName).Result;
			if (!success) {
				LogErrorLine ("Failed to launch the activity");
				return false;
			}

			(success, output) = context.adb.Shell ("pidof", packageName).Result;
			if (!success) {
				LogErrorLine ("Failed to obtain PID of the running application");
				LogErrorLine (output);
				return false;
			}

			output = output.Trim ();
			if (!UInt32.TryParse (output, out context.applicationPID)) {
				LogErrorLine ($"Unable to parse string '{output}' as the package's PID");
				return false;
			}

			LogStatusLine ("Application PID", output);

			TPL.Task<bool> debugServerTask = StartDebugServer (context);
			return true;
		}

		TPL.Task<bool> StartDebugServer (Context context)
		{
			return null;
		}

		Context? Init ()
		{
			LogLine ();

			var context = new Context {
				adb = new AdbRunner (log, adbPath)
			};

			(bool success, string output) = context.adb.GetPropertyValue ("ro.build.version.sdk").Result;
			if (!success || String.IsNullOrEmpty (output) || !Int32.TryParse (output, out int apiLevel)) {
				LogErrorLine ("Unable to determine connected device's API level");
				return null;
			}
			context.apiLevel = apiLevel;

			// Warn on old Pixel C firmware (b/29381985). Newer devices may have Yama
			// enabled but still work with ndk-gdb (b/19277529).
			(success, output) = context.adb.Shell ("cat", "/proc/sys/kernel/yama/ptrace_scope", "2>/dev/null").Result;
			if (success &&
			    YamaOK (output.Trim ()) &&
			    PropertyIsEqualTo (context.adb.GetPropertyValue ("ro.build.product").Result, "dragon") &&
			    PropertyIsEqualTo (context.adb.GetPropertyValue ("ro.product.name").Result, "ryu")
			) {
				LogWarningLine ("WARNING: The device uses Yama ptrace_scope to restrict debugging. ndk-gdb will");
				LogWarningLine ("    likely be unable to attach to a process. With root access, the restriction");
				LogWarningLine ("    can be lifted by writing 0 to /proc/sys/kernel/yama/ptrace_scope. Consider");
				LogWarningLine ("    upgrading your Pixel C to MXC89L or newer, where Yama is disabled.");
				LogLine ();
			}

			if (!DetermineArchitectureAndABI (context)) {
				return null;
			}

			if (!DetermineAppDataDirectory (context)) {
				return null;
			}

			if (!PushDebugServer (context)) {
				return null;
			}

			CopyLibraries (context);

			return context;

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

		bool EnsureSharedLibraryHasSymboles (string libraryPath, DotnetSymbolRunner? dotnetSymbol)
		{
			bool tryToFetchSymbols = false;
			bool hasSymbols = ELFHelper.HasDebugSymbols (log, libraryPath, out bool usesDebugLink);
			string libName = Path.GetFileName (libraryPath);

			if (!xaLibraries.Contains (libName)) {
				if (ELFHelper.IsAOTLibrary (log, libraryPath)) {
					return true; // We don't are about symbols, AOT libraries are only data
				}

				// It might be a framework shared library, we'll try to fetch symbols if necessary and possible
				tryToFetchSymbols = !hasSymbols && usesDebugLink;
			}

			if (tryToFetchSymbols && dotnetSymbol != null) {
				LogInfoLine ($"      Attempting to download debug symbols from symbol server");
				if (!dotnetSymbol.Fetch (libraryPath).Result) {
					LogWarningLine ($"      Warning: failed to download debug symbols for {libraryPath}");
				} else {
					LogLine ();
				}
			}

			hasSymbols = ELFHelper.HasDebugSymbols (log, libraryPath);
			return hasSymbols;
		}

		DotnetSymbolRunner? GetDotnetSymbolRunner ()
		{
			string dotnetSymbolPath = Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.UserProfile), ".dotnet", "tools", "dotnet-symbol");
			if (OS.IsWindows) {
				dotnetSymbolPath = $"{dotnetSymbolPath}.exe";
			}

			if (!File.Exists (dotnetSymbolPath)) {
				return null;
			}

			return new DotnetSymbolRunner (log, dotnetSymbolPath);
		}

		void CopyLibraries (Context context)
		{
			LogInfoLine ("Populating local native library cache");
			context.appLibrariesDir = Path.Combine (context.outputDir, "app", "lib");
			if (!Directory.Exists (context.appLibrariesDir)) {
				Directory.CreateDirectory (context.appLibrariesDir);
			}

			if (context.nativeLibraries != null) {
				LogInfoLine ("  Copying application native libraries");
				bool haveLibsWithoutSymbols = false;
				DotnetSymbolRunner? dotnetSymbol = GetDotnetSymbolRunner ();

				foreach (string library in context.nativeLibraries) {
					LogLine ($"    {library}");

					string fileName = Path.GetFileName (library);
					if (fileName.StartsWith ("libmono-android.")) {
						fileName = "libmonodroid.so";
					}
					string destPath = Path.Combine (context.appLibrariesDir, fileName);
					File.Copy (library, destPath, true);

					if (!EnsureSharedLibraryHasSymboles (destPath, dotnetSymbol)) {
						haveLibsWithoutSymbols = true;
					}
				}

				if (haveLibsWithoutSymbols) {
					LogWarningLine ($"One or more native libraries have no debug symbols.");
					if (dotnetSymbol == null) {
						LogWarningLine ($"The dotnet-symbol tool was not found. It can be installed using: dotnet tool install -g dotnet-symbol");
					}
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

			LogLine ();
			LogInfoLine ("  Copying binaries from device");
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
				LogWarningLine ("    Failed to copy 32-bit app_process");
			} else {
				LogLine ($"    From '{source}' to '{destination}' ");
			}

			string ToLocalPathFormat (string path) => isWindows ? path.Replace ("/", "\\") : path;
		}

		bool PushDebugServer (Context context)
		{
			if (!hostLldbServerPaths.TryGetValue (context.abi, out string debugServerPath)) {
				LogErrorLine ($"Debug server for abi '{context.abi}' not found.");
				return false;
			}

			string serverName = $"{context.arch}-{Path.GetFileName (debugServerPath)}";
			string deviceServerPath = Path.Combine (context.appDataDir, serverName);

			// Always push the server binary, as we don't know what version might already be there
			LogDebugLine ($"Uploading {debugServerPath} to device");

			// First upload to temporary path, as it's writable for everyone
			string remotePath = $"/data/local/tmp/{serverName}";
			if (!context.adb.Push (debugServerPath, remotePath).Result) {
				LogErrorLine ($"Failed to upload debug server {debugServerPath} to device path {remotePath}");
				return false;
			}

			// Next, copy it to the app dir, with run-as
			(bool success, string output) = context.adb.Shell (
				"cat", remotePath, "|",
				"run-as", packageName,
				"sh", "-c", $"'cat > {deviceServerPath}'"
			).Result;

			if (!success) {
				LogErrorLine ($"Failed to copy debug server on device, from {remotePath} to {deviceServerPath}");
				return false;
			}

			(success, output) = context.adb.RunAs (packageName, "chmod", "700", deviceServerPath).Result;
			if (!success) {
				LogErrorLine ($"Failed to make debug server executable on device, at {deviceServerPath}");
				return false;
			}

			context.debugServerPath = deviceServerPath;
			LogStatusLine ("Debug server path on device", context.debugServerPath);
			LogLine ();

			return true;
		}

		bool DetermineAppDataDirectory (Context context)
		{
			(bool success, string output) = context.adb.GetAppDataDirectory (packageName).Result;
			if (!success) {
				LogErrorLine ($"Unable to determine data directory for package '{packageName}'");
				return false;
			}

			context.appDataDir = output.Trim ();
			LogStatusLine ($"Application data directory on device", context.appDataDir);
			LogLine ();

			// Applications with minSdkVersion >= 24 will have their data directories
			// created with rwx------ permissions, preventing adbd from forwarding to
			// the gdbserver socket. To be safe, if we're on a device >= 24, always
			// chmod the directory.
			if (context.apiLevel >= 24) {
				(success, output) = context.adb.RunAs (packageName, "/system/bin/chmod", "a+x", context.appDataDir).Result;
				if (!success) {
					LogErrorLine ("Failed to make application data directory world executable");
					return false;
				}
			}

			return true;
		}

		bool DetermineArchitectureAndABI (Context context)
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
				LogErrorLine ("Unable to determine device ABI");
				return false;
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

						LogStatusLine ($"    Selected ABI", $"{context.abi} (architecture: {context.arch})");

						context.appIs64Bit = context.abi.IndexOf ("64", StringComparison.Ordinal) >= 0;
						context.outputDir = Path.Combine (outputDir, context.abi);
						if (NativeLibrariesPerABI != null && NativeLibrariesPerABI.TryGetValue (context.abi, out List<string> abiLibraries)) {
							context.nativeLibraries = abiLibraries;
						}
						return true;
					}
				}
			}

			LogErrorLine ($"Application cannot run on the selected device: no matching ABI found");
			return false;

			void LogABIs (string which, string[] abis)
			{
				LogStatusLine ($"{which} ABIs", String.Join (", ", abis));
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

		bool FindTools (string ndkRootPath, string[] supportedAbis)
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
				LogErrorLine ($"Unable to locate lldb executable in '{toolchainBinDir}'");
				return false;
			}
			lldbPath = path;

			hostLldbServerPaths = new Dictionary<string, string> (StringComparer.OrdinalIgnoreCase);
			string llvmVersion = GetLlvmVersion (toolchainDir);
			foreach (string abi in supportedAbis) {
				string llvmAbi = NdkHelper.TranslateAbiToLLVM (abi);
				path = Path.Combine (ndkRootPath, NdkHelper.RelativeToolchainDir, "lib64", "clang", llvmVersion, "lib", "linux", llvmAbi, "lldb-server");
				if (!File.Exists (path)) {
					LogErrorLine ($"LLVM lldb server component for ABI '{abi}' not found at '{path}'");
					return false;
				}

				hostLldbServerPaths.Add (abi, path);
			}

			if (hostLldbServerPaths.Count == 0) {
				LogErrorLine ("Unable to find any lldb-server executables, debugging not possible");
				return false;
			}

			return true;
		}

		void Log (string? message)
		{
			Log (LogLevel.Message, message);
		}

		void LogLine (string? message = null)
		{
			Log ($"{message}{Environment.NewLine}");
		}

		void LogWarning (string? message)
		{
			Log (LogLevel.Warning, message);
		}

		void LogWarningLine (string? message)
		{
			LogWarning ($"{message}{Environment.NewLine}");
		}

		void LogError (string? message)
		{
			Log (LogLevel.Error, message);
		}

		void LogErrorLine (string? message)
		{
			LogError ($"{message}{Environment.NewLine}");
		}

		void LogInfo (string? message)
		{
			Log (LogLevel.Info, message);
		}

		void LogInfoLine (string? message)
		{
			LogInfo ($"{message}{Environment.NewLine}");
		}

		void LogDebug (string? message)
		{
			Log (LogLevel.Debug, message);
		}

		void LogDebugLine (string? message)
		{
			LogDebug ($"{message}{Environment.NewLine}");
		}

		void LogStatusLine (string label, string text)
		{
			Log (LogLevel.Info, $"{label}: ", StatusLabel);
			Log (LogLevel.Info, $"{text}{Environment.NewLine}", StatusText);
		}

		void Log (LogLevel level, string? message)
		{
			Log (level, message, ForegroundColor (level));
		}

		void Log (LogLevel level, string? message, ConsoleColor color)
		{
			TextWriter writer = level == LogLevel.Error ? Console.Error : Console.Out;
			message = message ?? String.Empty;

			ConsoleColor fg = ConsoleColor.Gray;
			try {
				lock (consoleLock) {
					fg = Console.ForegroundColor;
					Console.ForegroundColor = color;
				}

				writer.Write (message);
			} finally {
				Console.ForegroundColor = fg;
			}

			if (!String.IsNullOrEmpty (message)) {
				switch (level) {
					case LogLevel.Error:
						log.LogError (message);
						break;

					case LogLevel.Warning:
						log.LogWarning (message);
						break;

					default:
					case LogLevel.Message:
					case LogLevel.Info:
						log.LogMessage (message);
						break;

					case LogLevel.Debug:
						log.LogDebugMessage (message);
						break;
				}
			}
		}

		ConsoleColor ForegroundColor (LogLevel level) => level switch {
			LogLevel.Error => ErrorColor,
			LogLevel.Warning => WarningColor,
			LogLevel.Info => InfoColor,
			LogLevel.Debug => DebugColor,
			LogLevel.Message => MessageColor,
			_ => MessageColor,
		};
	}
}
