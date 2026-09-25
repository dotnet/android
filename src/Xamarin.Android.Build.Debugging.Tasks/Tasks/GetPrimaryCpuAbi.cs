using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Android.Build.Tasks;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	public class GetPrimaryCpuAbi : AsyncTask
	{
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

		AdbDeviceInfo device;

		public override bool Execute ()
		{
			if (!string.IsNullOrEmpty (AdbTargetArchitecture)) {
				LogDebugMessage ($"Using $(AdbTargetArchitecture): {AdbTargetArchitecture}");
				ResultingAbi = AdbTargetArchitecture;
				RuntimeIdentifier = GetRuntimeIdentifier ();
				LogOutputs ();
				return true;
			}

			device = AndroidHelper.ParseTarget (AdbTarget, LogDebugMessage, LogCodedError, logErrors: false, engine4: BuildEngine4, adbToolPath: AdbToolPath, adbToolExe: AdbToolExe);
			if (device == null) {
				LogDebugMessage ($"No device found: {nameof (AdbTarget)}=\"{AdbTarget}\"");
				// don't stop the build if we don't have a device.
				return true;
			}

			LogDebugMessage ($"Found device: {device.Serial}");
			FoundDevices = true;
			return base.Execute ();
		}

		public async override System.Threading.Tasks.Task RunTaskAsync ()
		{
			int sdkver = 0;
			var adb = AndroidHelper.CreateAdbRunner (AdbToolPath, AdbToolExe);
			
			XDocument doc = null;
			if (File.Exists (DevicePropertyCache)) {
				LogDebugMessage ($"Using cached properties: {DevicePropertyCache}");
				doc = XDocument.Load (DevicePropertyCache);
				if (DeviceCache.TryGet (doc, device.Serial, device.LongOutput, out var cachedAbi, out var cachedSdkVersion)) {
					ResultingAbi = cachedAbi;
					SdkVersion = cachedSdkVersion;
					RuntimeIdentifier = GetRuntimeIdentifier ();
					LogOutputs ();
					return;
				}
				LogDebugMessage ($"Cache miss or stale for device {device.Serial}. Refreshing.");
			} else {
				LogDebugMessage ($"Cached properties did not exist: {DevicePropertyCache}");
			}

			string sdk;
			try {
				sdk = await adb.GetShellPropertyAsync (device.Serial, "ro.build.version.sdk", CancellationToken);
			} catch (Exception ex) when (ex is not OperationCanceledException) {
				LogDebugMessage (ex.ToString ());
				return;
			}
			int.TryParse (sdk, out sdkver);
			if (sdkver <= 0) {
				LogDebugMessage ($"ro.build.version.sdk is {sdkver}. Refreshing.");
				sdk = await adb.GetShellPropertyAsync (device.Serial, "ro.build.version.sdk", CancellationToken);
				int.TryParse (sdk, out sdkver);
			}

			if (sdkver >= 21) {
				string command = "getprop ro.product.cpu.abilist64";
				string commandResult = await adb.RunShellCommandAsync (device.Serial, command, CancellationToken) ?? "";
				LogDebugMessage ($"{command} {commandResult}");
				string[] abis = commandResult.Split (new [] { ',' }, StringSplitOptions.RemoveEmptyEntries);
				if (abis.Length > 0) {
					ResultingAbi = abis [0].Trim ();
				}
			}

			if (string.IsNullOrEmpty (ResultingAbi)) {
				ResultingAbi = await adb.GetShellPropertyAsync (device.Serial, "ro.product.cpu.abi", CancellationToken);
				if (string.IsNullOrEmpty (ResultingAbi)) {
					LogDebugMessage ("ro.product.cpu.abi is null. Refreshing.");
					ResultingAbi = await adb.GetShellPropertyAsync (device.Serial, "ro.product.cpu.abi", CancellationToken)
						?? await adb.GetShellPropertyAsync (device.Serial, "ro.product.cpu.abi2", CancellationToken);
				}
			}
			if (string.IsNullOrEmpty (ResultingAbi) && device.IsEmulator) {
				string command = "uname -m";
				string commandResult = await adb.RunShellCommandAsync (device.Serial, command, CancellationToken) ?? "";
				LogDebugMessage ($"{command} {commandResult}");
				if (!commandResult.Contains ("adb:")) {
					string abi = commandResult.Trim ();
					if (!UnameAbiMapping.ContainsKey (abi)) {
						LogDebugMessage ($"Unexpected Abi returned from `uname -m` {abi}. Ignoring result.");
					} else {
						ResultingAbi = UnameAbiMapping [abi];
					}
				}
			}
			if (string.IsNullOrEmpty (ResultingAbi) && !string.IsNullOrEmpty (AndroidPackage)) {
				LogDebugMessage ($"Falling back to pm dump {AndroidPackage}.");
				ResultingAbi = await GetAbiFromPmDump (adb, device.Serial);
			}
			RuntimeIdentifier = GetRuntimeIdentifier ();
			SdkVersion = sdkver;
			LogOutputs ();

			doc = DeviceCache.Update (doc, device.Serial, ResultingAbi, SdkVersion, device.LongOutput);
			if (doc.SaveIfChanged (DevicePropertyCache)) {
				LogDebugMessage ($"Saving: {DevicePropertyCache}");
			}
		}
		void LogOutputs ()
		{
			LogDebugMessage ($"  {nameof (ResultingAbi)}: {ResultingAbi}");
			LogDebugMessage ($"  {nameof (RuntimeIdentifier)}: {RuntimeIdentifier}");
			LogDebugMessage ($"  {nameof (SdkVersion)}: {SdkVersion}");
		}

		async System.Threading.Tasks.Task<string> GetAbiFromPmDump (AdbRunner adb, string serial)
		{
			var rex = new Regex ("primaryCpuAbi=(?<abi>([A-Za-z0-9_-])*)");
			string command = "pm dump packages | grep primaryCpuAbi | grep -v '=null' | sort | uniq -c";
			string result = await adb.RunShellCommandAsync (serial, command, CancellationToken) ?? "";
			result = result.Trim ();
			LogDebugMessage ($"{command}: {result}");
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

		string GetRuntimeIdentifier ()
		{
			if (string.IsNullOrEmpty (ResultingAbi)) {
				return null;
			}
			if (RuntimeIdentifiers != null) {
				foreach (var rid in RuntimeIdentifiers) {
					if (AndroidRidAbiHelper.RuntimeIdentifierToAbi (rid) == ResultingAbi) {
						return rid;
					}
				}
			}
			return null;
		}
	}
}
