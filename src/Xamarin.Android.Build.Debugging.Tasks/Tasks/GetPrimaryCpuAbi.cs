using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml.Linq;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Android.Build.Tasks;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	[MSBuildMultiThreadableTask]
	public class GetPrimaryCpuAbi : MultithreadedAndroidTask, ICancelableTask
	{
		readonly CancellationTokenSource cancellation = new CancellationTokenSource ();

		static readonly Dictionary<string, string> UnameAbiMapping = new Dictionary<string, string> {
			{ "x86", "x86" },
			{ "x86_64", "x86_64" },
			{ "armeabi-v7a", "armeabi-v7a" },
			{ "arm64-v8a", "arm64-v8a" },
			{ "i386", "x86" },
			{ "i686", "x86" },
			{ "aarch64", "arm64-v8a" },
			{ "arm", "armeabi-v7a" },
		};

		public override string TaskPrefix => "GCPU";

		public CancellationToken CancellationToken => cancellation.Token;

		public void Cancel () => cancellation.Cancel ();

		[Output]
		public string AdbTarget { get; set; }
		/// <summary>
		/// IDEs set $(AdbTargetArchitecture) when an emulator is closed, as a performance optimization.
		/// </summary>
		public string AdbTargetArchitecture { get; set; }
		public string AdbOptions { get ;set; }
		public string AdbToolPath { get; set; }
		public string AdbToolExe { get; set; }
		public string AndroidPackage { get; set; }
		public string DevicePropertyCache { get; set; }
		public string [] RuntimeIdentifiers { get; set; }

		[Output]
		public bool FoundDevices { get; set; }
		[Output]
		public string ResultingAbi { get; set; }
		[Output]
		public string RuntimeIdentifier { get; set; }
		[Output]
		public int SdkVersion { get; set; }

		public override bool RunTask ()
		{
			if (!string.IsNullOrEmpty (AdbTargetArchitecture)) {
				Log.LogDebugMessage ($"Using $(AdbTargetArchitecture): {AdbTargetArchitecture}");
				ResultingAbi = AdbTargetArchitecture;
				SelectRuntimeIdentifier ();
				return true;
			}

			AdbDeviceInfo device;
			try {
				device = AndroidHelper.ParseTarget (AdbTarget, Log, logErrors: false, engine4: BuildEngine4,
					adbToolPath: AdbToolPath, adbToolExe: AdbToolExe, taskEnvironment: TaskEnvironment, cancellationToken: CancellationToken);
			} catch (OperationCanceledException) when (CancellationToken.IsCancellationRequested) {
				// Cancellation is not a build error.
				return !Log.HasLoggedErrors;
			}
			if (device == null) {
				Log.LogDebugMessage ($"No device found: {nameof (AdbTarget)}=\"{AdbTarget}\"");
				// don't stop the build if we don't have a device.
				return true;
			}

			Log.LogDebugMessage ($"Found device: {device.Serial}");
			AdbTarget = $"-s {device.Serial}";
			FoundDevices = true;
			try {
				QueryDeviceProperties (device);
			} catch (OperationCanceledException) {
				// Cancellation is not a build error.
			}
			return !Log.HasLoggedErrors;
		}

		void QueryDeviceProperties (AdbDeviceInfo device)
		{
			int sdkver = 0;
			var adb = AndroidHelper.CreateAdbRunner (AdbToolPath, AdbToolExe, TaskEnvironment);
			var cachePath = TaskEnvironment.GetAbsolutePath (DevicePropertyCache);

			string GetProperty (string name) =>
				adb.GetShellPropertyAsync (device.Serial, name, CancellationToken).GetAwaiter ().GetResult ();

			string RunShellCommand (string command) =>
				adb.RunShellCommandAsync (device.Serial, command, CancellationToken).GetAwaiter ().GetResult () ?? "";

			XDocument doc = null;
			if (File.Exists (cachePath)) {
				Log.LogDebugMessage ($"Using cached properties: {DevicePropertyCache}");
				doc = XDocument.Load (cachePath);
				if (DeviceCache.TryGet (doc, device.Serial, device.LongOutput, out var cachedAbi, out var cachedSdkVersion, out var cachedSupportedAbis)) {
					ResultingAbi = cachedAbi;
					SdkVersion = cachedSdkVersion;
					SelectRuntimeIdentifier (cachedSupportedAbis);
					return;
				}
				Log.LogDebugMessage ($"Cache miss or stale for device {device.Serial}. Refreshing.");
			} else {
				Log.LogDebugMessage ($"Cached properties did not exist: {DevicePropertyCache}");
			}

			string sdk;
			try {
				sdk = GetProperty ("ro.build.version.sdk");
			} catch (Exception ex) when (ex is not OperationCanceledException) {
				Log.LogDebugMessage (ex.ToString ());
				return;
			}
			int.TryParse (sdk, out sdkver);
			if (sdkver <= 0) {
				Log.LogDebugMessage ($"ro.build.version.sdk is {sdkver}. Refreshing.");
				sdk = GetProperty ("ro.build.version.sdk");
				int.TryParse (sdk, out sdkver);
			}

			if (sdkver >= 21) {
				string command = "getprop ro.product.cpu.abilist64";
				string commandResult = RunShellCommand (command);
				Log.LogDebugMessage ($"{command} {commandResult}");
				string[] abis = commandResult.Split (new [] { ',' }, StringSplitOptions.RemoveEmptyEntries);
				if (abis.Length > 0) {
					ResultingAbi = abis [0].Trim ();
				}
			}

			if (string.IsNullOrEmpty (ResultingAbi)) {
				ResultingAbi = GetProperty ("ro.product.cpu.abi");
				if (string.IsNullOrEmpty (ResultingAbi)) {
					Log.LogDebugMessage ("ro.product.cpu.abi is null. Refreshing.");
					ResultingAbi = GetProperty ("ro.product.cpu.abi") ?? GetProperty ("ro.product.cpu.abi2");
				}
			}
			if (string.IsNullOrEmpty (ResultingAbi) && device.IsEmulator) {
				string command = "uname -m";
				string commandResult = RunShellCommand (command);
				Log.LogDebugMessage ($"{command} {commandResult}");
				if (!commandResult.Contains ("adb:")) {
					string abi = commandResult.Trim ();
					if (!UnameAbiMapping.ContainsKey (abi)) {
						Log.LogDebugMessage ($"Unexpected Abi returned from `uname -m` {abi}. Ignoring result.");
					} else {
						ResultingAbi = UnameAbiMapping [abi];
					}
				}
			}
			if (string.IsNullOrEmpty (ResultingAbi) && !string.IsNullOrEmpty (AndroidPackage)) {
				Log.LogDebugMessage ($"Falling back to pm dump {AndroidPackage}.");
				ResultingAbi = GetAbiFromPmDump (adb, device.Serial);
			}
			string supportedAbiList = GetProperty ("ro.product.cpu.abilist");
			string [] supportedAbis;
			if (string.IsNullOrWhiteSpace (supportedAbiList)) {
				string primaryAbi = GetProperty ("ro.product.cpu.abi");
				string secondaryAbi = GetProperty ("ro.product.cpu.abi2");
				supportedAbis = GetSupportedAbis ([], primaryAbi, secondaryAbi);
			} else {
				supportedAbis = GetSupportedAbis (supportedAbiList.Split (','), null, null);
			}
			// Cache device capabilities before selecting the ABI for this app.
			doc = DeviceCache.Update (doc, device.Serial, ResultingAbi, sdkver, device.LongOutput, supportedAbis);
			SelectRuntimeIdentifier (supportedAbis);
			SdkVersion = sdkver;

			if (doc.SaveIfChanged (cachePath, TaskEnvironment)) {
				Log.LogDebugMessage ($"Saving: {DevicePropertyCache}");
			}
		}

		string GetAbiFromPmDump (AdbRunner adb, string serial)
		{
			var rex = new Regex ("primaryCpuAbi=(?<abi>([A-Za-z0-9_-])*)");
			string command = "pm dump packages | grep primaryCpuAbi | grep -v '=null' | sort | uniq -c";
			string result = adb.RunShellCommandAsync (serial, command, CancellationToken).GetAwaiter ().GetResult () ?? "";
			result = result.Trim ();
			Log.LogDebugMessage ($"{command}: {result}");
			SortedDictionary<int, string> abis = new SortedDictionary<int, string> ();
			foreach (var line in result.Split ('\n')) {
				string[] items = line.Split (new char[] {' '}, StringSplitOptions.RemoveEmptyEntries);
				if (items.Length != 2)
					continue;
				int count;
				if (!int.TryParse (items[0], out count))
					continue;
				string abi = rex.Match (items[1]).Groups ["abi"].ToString ();
				if (string.IsNullOrEmpty (abi))
					continue;
				abis.Add (count, abi);
			}
			if (abis.Count == 0)
				return string.Empty;
			
			return abis.Last ().Value;
		}

		internal static string [] GetSupportedAbis (string [] supportedAbis, string primaryAbi, string secondaryAbi)
		{
			if (Array.Exists (supportedAbis, abi => !string.IsNullOrWhiteSpace (abi))) {
				return supportedAbis;
			}

			var abis = new List<string> (2);
			if (!string.IsNullOrWhiteSpace (primaryAbi)) {
				abis.Add (primaryAbi);
			}
			if (!string.IsNullOrWhiteSpace (secondaryAbi)) {
				abis.Add (secondaryAbi);
			}
			return abis.ToArray ();
		}

		internal void SelectRuntimeIdentifier (params string [] supportedAbis)
		{
			RuntimeIdentifier = null;
			if (RuntimeIdentifiers == null) {
				return;
			}
			string [] candidates = [ResultingAbi, .. supportedAbis];
			foreach (var candidate in candidates) {
				string abi = candidate?.Trim ();
				if (string.IsNullOrEmpty (abi)) {
					continue;
				}
				foreach (var rid in RuntimeIdentifiers) {
					if (AndroidRidAbiHelper.RuntimeIdentifierToAbi (rid) == abi) {
						ResultingAbi = abi;
						RuntimeIdentifier = rid;
						return;
					}
				}
			}
			Log.LogDebugMessage ($"No device ABI matches the requested runtime identifiers: {string.Join (", ", RuntimeIdentifiers)}");
		}
	}
}
