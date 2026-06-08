using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Android.Build.Tasks;
using Mono.AndroidTools;
using AndroidLogger = Mono.AndroidTools.AndroidLogger;

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
		public string AndroidPackage { get; set; }
		public string DevicePropertyCache { get; set; }
		public string [] RuntimeIdentifiers { get; set; }

		[Output]
		public bool FoundDevices { get; set; }
		[Output]
		public string ResultingAbi { get; set; }
		[Output]
		public string ToolsAbi { get; set; }
		[Output]
		public string RuntimeIdentifier { get; set; }
		[Output]
		public int SdkVersion { get; set; }

		AndroidDevice device;

		public override bool Execute ()
		{
			if (!string.IsNullOrEmpty (AdbTargetArchitecture)) {
				LogDebugMessage ($"Using $(AdbTargetArchitecture): {AdbTargetArchitecture}");
				ResultingAbi = AdbTargetArchitecture;
				RuntimeIdentifier = GetRuntimeIdentifier ();
				LogOutputs ();
				return true;
			}

			device = AndroidHelper.ParseTarget (AdbTarget, LogDebugMessage, LogCodedError, logErrors: false, engine4: BuildEngine4);
			if (device == null) {
				LogDebugMessage ($"No device found: {nameof (AdbTarget)}=\"{AdbTarget}\"");
				// don't stop the build if we don't have a device.
				return true;
			}

			LogDebugMessage ($"Found device: {device.ID}");
			FoundDevices = true;

			AndroidLogger.Error += DebugHandler;
			AndroidLogger.Warning += DebugHandler;
			AndroidLogger.Info += DebugHandler;
			AndroidLogger.Debug += DebugHandler;
			try {
				bool result = base.Execute ();
				return result;
			} finally {
				AndroidLogger.Error += DebugHandler;
				AndroidLogger.Warning += DebugHandler;
				AndroidLogger.Info += DebugHandler;
				AndroidLogger.Debug -= DebugHandler;
			}
		}

		public async override System.Threading.Tasks.Task RunTaskAsync ()
		{
			int sdkver = 0;
			
			XDocument doc = null;
			if (File.Exists (DevicePropertyCache)) {
				LogDebugMessage ($"Using cached properties: {DevicePropertyCache}");
				doc = XDocument.Load (DevicePropertyCache);
				if (DeviceCache.TryGet (doc, device.ID, device.LongOutput, out var cachedAbi, out var cachedToolsAbi, out var cachedSdkVersion, Log)) {
					ResultingAbi = cachedAbi;
					ToolsAbi = cachedToolsAbi;
					SdkVersion = cachedSdkVersion;
					RuntimeIdentifier = GetRuntimeIdentifier ();
					LogOutputs ();
					return;
				}
				LogDebugMessage ($"Cache miss or stale for device {device.ID}. Refreshing.");
			} else {
				LogDebugMessage ($"Cached properties did not exist: {DevicePropertyCache}");
			}

			try {
				await device.EnsureProperties (CancellationToken);
			} catch (Exception ex) {
				LogDebugMessage (ex.ToString ());
				return;
			}

			sdkver = device.Properties.BuildVersionSdk;
			if (sdkver <= 0) {
				LogDebugMessage ($"device.Properties.BuildVersionSdk is {sdkver}. Forcing PropertyRefresh.");
				await device.RefreshProperties (CancellationToken);
				sdkver = device.Properties.BuildVersionSdk;
			}

			if (sdkver >= 21) {
				string command = "getprop ro.product.cpu.abilist64";
				string commandResult = await device.RunShellCommand (command, CancellationToken);
				LogDebugMessage ($"{command} {commandResult}");
				string[] abis = commandResult.Split (new [] { ',' }, StringSplitOptions.RemoveEmptyEntries);
				if (abis.Length > 0) {
					ResultingAbi = abis [0].Trim ();
				}
			}

			if (string.IsNullOrEmpty (ResultingAbi)) {
				if (string.IsNullOrEmpty (device.Properties.ProductCpuAbi)) {
					LogDebugMessage ("device.Properties.ProductCpuAbi is null. Forcing PropertyRefresh.");
					await device.RefreshProperties (CancellationToken);
				}
				ResultingAbi = device.Properties.ProductCpuAbi ?? device.Properties.ProductCpuAbi2;
			}
			if (string.IsNullOrEmpty (ResultingAbi) && device.IsEmulator) {
				string command = "uname -m";
				string commandResult = await device.RunShellCommand (command, CancellationToken);
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
				ResultingAbi = await GetAbiFromPmDump (device);
			}
			RuntimeIdentifier = GetRuntimeIdentifier ();
			SdkVersion = sdkver;
			LogOutputs ();

			doc = DeviceCache.Update (doc, device.ID, ResultingAbi, ToolsAbi, SdkVersion, device.LongOutput);
			if (doc.SaveIfChanged (DevicePropertyCache)) {
				LogDebugMessage ($"Saving: {DevicePropertyCache}");
			}
		}
		void DebugHandler (string task, string message)
		{
			LogDebugMessage ($"DEBUG {task} {message}.");
		}

		void LogOutputs ()
		{
			LogDebugMessage ($"  {nameof (ResultingAbi)}: {ResultingAbi}");
			LogDebugMessage ($"  {nameof (ToolsAbi)}: {ToolsAbi}");
			LogDebugMessage ($"  {nameof (RuntimeIdentifier)}: {RuntimeIdentifier}");
			LogDebugMessage ($"  {nameof (SdkVersion)}: {SdkVersion}");
		}

		async System.Threading.Tasks.Task<string> GetAbiFromPmDump (AndroidDevice device)
		{
			var rex = new Regex ("primaryCpuAbi=(?<abi>([A-Za-z0-9_-])*)");
			string command = "pm dump packages | grep primaryCpuAbi | grep -v '=null' | sort | uniq -c";
			string result = await device.RunShellCommand (command, CancellationToken);
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
