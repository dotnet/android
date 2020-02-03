using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Xamarin.Android.Tools
{
	class Program
	{
		static string sdkPath;
		static string adbPath;
		static string avdManagerPath;
		static string emulatorPath;
		static string sdkManagerPath;
		static string deviceName;
		static int executionTimes = 1;
		static bool showVerbose;

		static int Main (string [] args)
		{
			var rootCommand = new RootCommand
			{
				new Option ("--verbose", "Show verbose output")
				{
					Argument = new Argument<bool> ()
				},
				new Option("--executiontimes", "Number of times emulator should run.")
				{
					    Argument = new Argument<int>(),
				},
				new Option("--sdkpath", "Android sdk location.")
				{
					    Argument = new Argument<string>(),
				},
				new Option("--devicename", "Emulator Virtual Device Name, If not provided a new one will be created.")
				{
					    Argument = new Argument<string>(),
				},
			};

			rootCommand.Name = "CheckBootTimes";
			rootCommand.Description = "Collect Android Emulator boot times.";
			rootCommand.Handler = System.CommandLine.Invocation.CommandHandler.Create(async (bool verbose, int? executiontimes, string sdkpath, string devicename) => {
				showVerbose = verbose;
				executionTimes = executiontimes.HasValue ? executiontimes.Value : 1;
				sdkPath = sdkpath;
				deviceName = devicename;

				Console.WriteLine ($"Testing emulator startup times for {executionTimes} execution(s). This may take several minutes.");

				var result = await Run ();

				Console.WriteLine ($"Check-Boot-Times Done.");

				return result ? 0 : 1;
			});


			try {
				return rootCommand.InvokeAsync (args).Result;
			} catch (Exception e) {
				Console.ForegroundColor = ConsoleColor.Red;
				Console.Write (e.Message);
				Console.ForegroundColor = ConsoleColor.White;
				return 1;
			}
		}

		static async Task<bool> Run ()
		{
			if (!ToolsExist ()) {
				return false;
			}

			if (!await CheckAccelerationType ()) {
				return false;
			}

			await PrintEmulatorVersion ();

			if (string.IsNullOrWhiteSpace (deviceName) && !await CheckVirtualDeviceExistsAndTryToCreateIfNeeded (true /* use skin */, true /* use Pixel device */)) {
				return false;
			}

			if (await GetBootTime (true /*cold boot */) < 0) {
				return false;
			}

			if (await GetBootTime (false /* cold boot */) < 0) {
				return false;
			}

			return true;
		}

		static async Task<bool> PrintEmulatorVersion ()
		{
			var output = new List<string> ();
			if (!await RunProcess (emulatorPath, $"-version", 5000, (data, mre) => {
				output.Add (data);
				if (!string.IsNullOrWhiteSpace (data) && data.IndexOf ("Android emulator version", StringComparison.InvariantCultureIgnoreCase) != -1) {
					Console.WriteLine (data);
					mre.Set ();
				}

				return true;
			})) {
				Console.WriteLine ("Unable to get emulator version. see logs below:");
				foreach (var o in output) {
					Console.WriteLine ($"Log output: {o}");
				}

				return false;
			}

			return true;
		}

		static async Task<double> GetBootTime (bool coldBoot)
		{
			var times = new List<long> ();
			int errors = 0;

			Stopwatch sw = new Stopwatch ();
			var token = coldBoot ? "emulator: INFO: boot time" : "onGuestSendCommand";

			for (int i = 0; i < executionTimes; i++) {
				bool validation (string data, ManualResetEvent mre)
				{
					PrintVerbose (data);

					if (!string.IsNullOrWhiteSpace (data) && data.IndexOf ("This application failed to start", StringComparison.OrdinalIgnoreCase) != -1) {
						return false;
					}

					if (sw.IsRunning && !string.IsNullOrWhiteSpace (data) && data.IndexOf (token, StringComparison.OrdinalIgnoreCase) != -1) {
						sw.Stop ();
						times.Add (sw.ElapsedMilliseconds);
						mre.Set ();
					}

					return true;
				}

				var bootOptions = string.Empty;
				if (coldBoot) {
					bootOptions = "-no-snapshot-load -wipe-data";
				}

				bool hasTimedOut = false;
				sw.Reset ();
				sw.Start ();
				if (!await RunProcess (emulatorPath, $"-avd {deviceName} {bootOptions} -verbose", 300000, validation, async () => {
					hasTimedOut = true;
					await ForceKillEmulator ();
				})) {
					errors++;
				}

				sw.Stop ();

				if (hasTimedOut) {
					continue;
				}

				var activity = i % 2 == 0 ? "com.google.android.apps.photos/.home.HomeActivity" : "com.android.settings/.wifi.WifiStatusTest";
				await RunProcess (adbPath, $"-e shell am start -n '{activity}'", 5000, (data, mre) => {
					if (!string.IsNullOrWhiteSpace (data) && data.IndexOf ("com.android") != -1) {
						mre.Set ();
					}
					return true;
				});

				await CloseEmulator ();
				await Task.Delay (1000);
			}

			if (errors > 0) {
				Console.WriteLine ($"Unable to boot emulator {errors} time(s)");
			}

			if (times.Count == 0) {
				return -1;
			}

			var time = times.Average ();
			Console.WriteLine ($"Average {(coldBoot ? "Cold" : "Hot")} Boot Time for {times.Count} run(s) out of {executionTimes} request(s) on Virtual device '{deviceName}': {time} ms");
			return time;
		}

		static async Task<bool> CloseEmulator ()
		{
			PrintVerbose ("CloseEmulator");
			bool validation (string data, ManualResetEvent mre)
			{
				PrintVerbose (data);
				if (!string.IsNullOrWhiteSpace (data) && data.StartsWith ("OK: killing emulator, bye bye", StringComparison.OrdinalIgnoreCase)) {
					mre.Set ();
				}

				return true;
			}

			if (!await RunProcess (adbPath, "-s emulator-5554 emu kill", 30000, validation)) {
				Console.WriteLine ("unable to quit emulator.");
				return false;
			}

			for (int i = 0; i < 10; i++) {
				PrintVerbose ("Checking if emulator was closed.");
				try {
					var proc = GetProcess ("emulator");
					if (proc == null) {
						proc = GetProcess ("qemu");

						if (proc == null) {
							return true;
						}
					}

					PrintVerbose ("Emulator still running");
					await Task.Delay (5000);
				} catch (Exception e) {
					PrintVerbose (e.Message);
					return true;
				}
			}

			await ForceKillEmulator ();
			return false;
		}

		static async Task<bool> CheckAccelerationType ()
		{
			var accel = new List<string> ();
			bool validation (string data, ManualResetEvent mre)
			{
				if (!string.IsNullOrWhiteSpace (data)) {
					if (!data.StartsWith ("accel", StringComparison.OrdinalIgnoreCase)) {
						accel.Add (data);
					}

					if (data.IndexOf ("This user doesn't have permissions to use", StringComparison.OrdinalIgnoreCase) != -1) {
						Console.WriteLine (data);
						return false;
					}

					if (data.EndsWith ("accel", StringComparison.OrdinalIgnoreCase)) {
						mre.Set ();
					}
				}

				return true;
			}

			if (!await RunProcess (emulatorPath, "-accel-check", 1000, validation)) {
				Console.WriteLine ("unable to detect acceleration type.");
				return false;
			}

			Console.WriteLine ($"Acceleration type: {string.Join (", ", accel)}");
			return true;
		}

		static async Task<bool> CheckVirtualDeviceExistsAndTryToCreateIfNeeded (bool useSkin, bool usePixelDevice)
		{
			deviceName = "XamarinPerfTest" + (usePixelDevice ? "Pixel" : string.Empty) + (useSkin ? "WithSkin" : string.Empty);

			if (await CheckVirtualDeviceExists ()) {
				return true;
			} else {
				Console.WriteLine ($"{deviceName} virtual device not found.");
				return await CreateVirtualDevice (useSkin, usePixelDevice);
			}
		}

		static async Task<bool> CheckVirtualDeviceExists ()
		{
			bool validation (string data, ManualResetEvent mre)
			{
				if (!string.IsNullOrWhiteSpace (data) && data == deviceName) {
					mre.Set ();
				}

				return true;
			}

			return await RunProcess (emulatorPath, "-list-avds", 10000, validation);
		}

		static async Task<bool> CreateVirtualDevice (bool useSkin, bool usePixelDevice)
		{
			Console.WriteLine ($"Creating {deviceName} virtual device.");

			await InstallSdk ();

			var contents = new List<string> ();
			bool validation (string data, ManualResetEvent mre)
			{
				contents.Add (data);
				if (!string.IsNullOrWhiteSpace (data) &&
					(data.IndexOf ("Do you wish to create a custom hardware profile?", StringComparison.OrdinalIgnoreCase) != -1 ||
					data.IndexOf ("100% Fetch remote repository...", StringComparison.OrdinalIgnoreCase) != -1)) {
					mre.Set ();
				}

				return true;
			}

			var filename = RunningOnWindowsEnvironment ? "cmd" : "bash";

			string deviceType = string.Empty;
			if (usePixelDevice) {
				deviceType = "--device \"pixel\"";
			}

			var args = $"{(RunningOnWindowsEnvironment ? "/" : "-")}c \" echo no | {avdManagerPath} create avd --force --name {deviceName} --abi google_apis_playstore/x86 --package system-images;android-29;google_apis_playstore;x86 {deviceType} \"";
			await RunProcess (filename, args, 10000, validation);
			await Task.Delay (5000);
			if (!await CheckVirtualDeviceExists ()) {
				Console.WriteLine ($"unable to create {deviceName} virtual device.");
				foreach (var data in contents) {
					Console.WriteLine (data);
				}

				return false;
			}

			if (useSkin) {
				if (!UpdateVirtualDevice ()) {
					Console.WriteLine ($"unable to update {deviceName} with skin.");
					return false;
				}
			}

			Console.WriteLine ($"{deviceName} virtual device created.");
			return true;
		}

		static bool UpdateVirtualDevice ()
		{
			var homePath = GetHomePath ();
			var vdPath = Path.Combine (homePath, $".android/avd/{deviceName}.avd/config.ini");

			if (!File.Exists (vdPath)) {
				return false;
			}

			var sdkPath = new FileInfo (emulatorPath).Directory.Parent.FullName;

			var content = "skin.name=pixel_2" + Environment.NewLine;
			content += "skin.dynamic=yes" + Environment.NewLine;
			content += $"skin.path={sdkPath}/skins/pixel_2" + Environment.NewLine;

			File.AppendAllText (vdPath, content);

			return true;
		}

		static async Task InstallSdk ()
		{
			bool validation (string data, ManualResetEvent mre)
			{
				if (!string.IsNullOrWhiteSpace (data) && data.IndexOf ("100%") != -1) {
					mre.Set ();
				}

				return true;
			}

			var filename = RunningOnWindowsEnvironment ? "cmd" : "bash";
			var args = $"{(RunningOnWindowsEnvironment ? "/" : "-")}c \" {sdkManagerPath} --install system-images;android-29;google_apis_playstore;x86 \"";
			await RunProcess (filename, args, 600000, validation);
		}

		static Task<bool> RunProcess (string filename, string arguments, int timeout, Func<string, ManualResetEvent, bool> validation, Action processTimeout = null)
		{
			return Task.Run (() => {
				using (var process = new Process ()) {

					process.StartInfo.FileName = filename;
					process.StartInfo.Arguments = arguments;
					process.StartInfo.UseShellExecute = false;
					process.StartInfo.CreateNoWindow = true;
					process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
					process.StartInfo.RedirectStandardOutput = true;
					process.StartInfo.RedirectStandardError = true;
					process.EnableRaisingEvents = true;

					var mre = new ManualResetEvent (false /* -> nonsignaled */);

					bool error = false;
					void dataReceived (object sender, DataReceivedEventArgs args)
					{
						if (!validation (args.Data, mre)) {
							error = true;
							mre.Set ();
						}
					}

					process.OutputDataReceived += dataReceived;
					process.ErrorDataReceived += dataReceived;

					process.Start ();

					process.BeginOutputReadLine ();
					process.BeginErrorReadLine ();

					if (!mre.WaitOne (timeout)) {
						processTimeout?.Invoke ();
						return false;
					}

					process.CancelOutputRead ();
					process.CancelErrorRead ();

					if (error) {
						return false;
					}
				}

				return true;
			});
		}

		static async Task ForceKillEmulator ()
		{
			PrintVerbose ("ForceKillEmulator");
			while (true) {
				try {
					var proc = GetProcess ("emulator");
					if (proc == null) {
						proc = GetProcess ("qemu");

						if (proc == null) {
							return;
						}
					}

					proc.Kill ();
					await Task.Delay (1000);
				} catch (Exception e) {
					PrintVerbose (e.Message);
					break;
				}
			}
		}

		static Process GetProcess (string processName)
		{
			return Process.GetProcesses ().FirstOrDefault (p => p.ProcessName.IndexOf (processName, StringComparison.OrdinalIgnoreCase) != -1);
		}

		static bool ToolsExist ()
		{
			if (string.IsNullOrWhiteSpace (adbPath)) {
				adbPath = GetProgramPath ("adb", "adb.exe", "adb.bat");
				if (string.IsNullOrWhiteSpace (adbPath)) {
					Console.WriteLine ("Unable to find adb.");
					return false;
				}
			}

			if (string.IsNullOrWhiteSpace (avdManagerPath)) {
				avdManagerPath = GetProgramPath ("avdmanager", "avdmanager.exe", "avdmanager.bat");
				if (string.IsNullOrWhiteSpace (avdManagerPath)) {
					Console.WriteLine ("Unable to find avdmanager.");
					return false;
				}
			}

			if (string.IsNullOrWhiteSpace (emulatorPath)) {
				emulatorPath = GetProgramPath ("emulator", "emulator.exe", "emulator.bat");
				if (string.IsNullOrWhiteSpace (emulatorPath)) {
					Console.WriteLine ("Unable to find emulator.");
					return false;
				}
			}

			if (string.IsNullOrWhiteSpace (sdkManagerPath)) {
				sdkManagerPath = GetProgramPath ("sdkmanager", "sdkmanager.exe", "sdkmanager.bat");
				if (string.IsNullOrWhiteSpace (sdkManagerPath)) {
					Console.WriteLine ("Unable to find sdkmanager.");
					return false;
				}
			}

			Console.WriteLine ($"Running adb from: '{adbPath}'");
			Console.WriteLine ($"Running avdmanager from: '{avdManagerPath}'");
			Console.WriteLine ($"Running emulator from: '{emulatorPath}'");
			Console.WriteLine ($"Running sdkmanager from: '{sdkManagerPath}'");

			return true;
		}

		static string GetProgramPath (params string [] filenames)
		{
			foreach (var filename in filenames) {
				var programPath = GetFullPath (filename);
				if (string.IsNullOrWhiteSpace (programPath)) {
					var homePath = GetHomePath ();

					var potentialLocations = new List<string> ();

					if (!string.IsNullOrWhiteSpace (sdkPath)) {
						potentialLocations.AddRange (new []{
							$"{sdkPath}/platform-tools",
							$"{sdkPath}/emulator",
							$"{sdkPath}/tools",
							$"{sdkPath}/tools/bin",
						});
					} else {
						potentialLocations.AddRange (new []{
							"AppData/Local/Android/Sdk/platform-tools",
							"AppData/Local/Android/Sdk/emulator",
							"AppData/Local/Android/Sdk/tools",
							"AppData/Local/Android/Sdk/tools/bin",
							"Library/Android/sdk/platform-tools",
							"Library/Android/sdk/emulator",
							"Library/Android/sdk/tools",
							"Library/Android/sdk/tools/bin",
							"android-toolchain/sdk/platform-tools",
							"android-toolchain/sdk/emulator",
							"android-toolchain/sdk/tools",
							"android-toolchain/sdk/tools/bin",
						});

						if (RunningOnWindowsEnvironment) {
							potentialLocations.AddRange (new []{
								"C:/Program Files (x86)/Android/android-sdk/platform-tools",
								"C:/Program Files (x86)/Android/android-sdk/emulator",
								"C:/Program Files (x86)/Android/android-sdk/tools",
								"C:/Program Files (x86)/Android/android-sdk/tools/bin",
							});
						}
					}

					foreach (var location in potentialLocations) {
						var programLocation = Path.Combine (homePath, location, filename);
						if (File.Exists (programLocation)) {
							return Path.GetFullPath (programLocation);
						}
					}
				} else {
					return programPath;
				}
			}

			return null;
		}

		static string GetHomePath ()
		{
			var homePath = RunningOnWindowsEnvironment
						? Environment.ExpandEnvironmentVariables ("%HOMEDRIVE%%HOMEPATH%")
						: Environment.GetEnvironmentVariable ("HOME");

			return homePath;
		}

		static string GetFullPath (string fileName)
		{
			if (File.Exists (fileName)) {
				return Path.GetFullPath (fileName);
			}

			return null;
		}

		static void PrintVerbose (string data)
		{
			if (showVerbose) {
				Console.WriteLine (data);
			}
		}

		static bool RunningOnWindowsEnvironment => !(Environment.OSVersion.Platform == PlatformID.Unix || Environment.OSVersion.Platform == PlatformID.MacOSX);
	}
}
