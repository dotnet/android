//
// AndroidDevice.cs
//
// Author:
//       Michael Hutchinson <mhutch@xamarin.com>
//
// Copyright (c) 2010-2011 Novell, Inc.
// Copyright (c) 2011 Xamarin Inc.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mono.AndroidTools.Internal;
using Mono.AndroidTools.Adb;
using System.Threading;
using Mono.AndroidTools.Util;
using System.Net;
using System.Net.Sockets;

namespace Mono.AndroidTools
{
	public class AndroidDevice : IAndroidDevice
	{
		static IPEndPoint TryGetTcpDeviceEndpoint (string deviceID)
		{
			var portIdx = deviceID.LastIndexOf (':');
			if (portIdx < 1)
				return null;
			short port;
			if (!short.TryParse (deviceID.Substring (portIdx + 1), out port))
				return null;
			IPAddress address;
			if (!IPAddress.TryParse (deviceID.Substring (0, portIdx), out address))
				return null;
			return new IPEndPoint (address, port);
		}

		AdbServer adb;

		public string ID { get; private set; }
		public string State { get; private set; }
		public string LongOutput { get; set; } = "";
		AndroidDeviceProperties properties;

		public AndroidDevice (string id, string state = "unknown", AdbServer adb = null)
		{
			this.ID = id;
			TcpEndpoint = TryGetTcpDeviceEndpoint (id);
			this.State = state;
			this.adb = adb ?? AdbServer.Default;
		}

		public static AndroidDevice Any {
			get {
				return new AndroidDevice ("any");
			}
		}

		public static AndroidDevice Usb {
			get {
				return new AndroidDevice ("usb");
			}
		}

		public static AndroidDevice Local {
			get {
				return new AndroidDevice ("local");
			}
		}

		public IPEndPoint TcpEndpoint {
			get; private set;
		}

		public bool IsPlayerEmulator {
			get {
				var prop = Properties;
				if (prop == null)
					return false;

				return prop.ProductManufacturer == "Xamarin";
			}
		}

		public bool IsGoogleEmulator {
			get {
				return ID.StartsWith ("emulator-", StringComparison.OrdinalIgnoreCase);
			}
		}

		public bool IsEmulator {
			get { return IsGoogleEmulator || IsPlayerEmulator; }
		}

		public bool IsTcpDevice {
			get { return TcpEndpoint != null; }
		}

		public int GetEmulatorConsolePort ()
		{
			return int.Parse (ID.Substring ("emulator-".Length));
		}

		public string AvdName {
			get {
				if (IsPlayerEmulator)
					return XapName;

				if (!IsEmulator)
					return null;

				var prop = Properties;
				if (prop == null)
					return null;
				return prop.Get ("monodroid.avdname");
			}
		}

		public string XapName {
			get {
				var prop = Properties;
				if (prop == null)
					return null;

				return prop.Get ("xapd.name");
			}
		}

		public int BuildVersionSdk {
			get {
				return Properties != null ? Properties.BuildVersionSdk : 0;
			}
		}

		public bool IsOnline {
			get { return State == "device"; }
		}

		// Manual fixups for android product names for names that don't work with the logic
		// based on the data from https://bugzilla.xamarin.com/show_bug.cgi?id=9823
		//
		// We fix up product codes to product names even when multiple names are used for one device
		// because the code won't mean anything to anyone, but a name will make sense to some users.
		//
		static Dictionary<string,string> nameFixes = new Dictionary<string, string> {
			{"ZT 1000et", "Elonex 1000ET"},
			{"samsung Galaxy Nexus", "Google Galaxy Nexus"}, //for branding consistency
			{"asus Nexus 7", "Google Nexus 7"},
			{"samsung GT-I9000", "Samsung Galaxy S"}, //could also be branded (captivate etc), can't tell them apart
			{"Amazon KFTT", "Amazon Kindle Fire HD 7\""},
			{"motorola DROID BIONIC", "Motorola Droid Bionic"},
			{"samsung GT-S5570", "Samsung Galaxy Mini"}, // has several other branded names - Dart/Move
			{"HTC ADR6400L", "HTC ThunderBolt"},
			{"samsung GT-I9100", "Samsung Galaxy S II"},
			{"Sony Ericsson SK17i", "Sony Ericsson Xperia mini pro"},
			{"Amazon KFJWI", "Amazon Kindle Fire HD 8.9\""},
			{"bn NookColor", "B&N NookColor"},
			{"samsung Nexus 7", "Google Nexus S"},
			{"samsung GT-I5800", "Samsung Galaxy 3"},
			{"htc Nexus 9", "Nexus 9" },
		};

		static Dictionary<string,string> manufacturerNameFixes = new Dictionary<string, string> {
			{ "asus", "Asus"},
			{ "google", "Google"},
			{ "samsung", "Samsung"},
			{ "motorola", "Motorola"},
			{ "kobo", "Kobo" },
			{ "bn", "B&N" },
		};

		/// <summary>
		/// Returns a nicer display name, if possible, else null
		/// </summary>
		public string GetDisplayName ()
		{
			var prop = Properties;
			if (prop == null)
				return null;

			if (IsEmulator) {
				//this is the property that MD sets to link running emulators with the AVD the were launched from
				var avdName = AvdName;
				if (!string.IsNullOrWhiteSpace (avdName)) {
					return avdName;
				}
				return null;
			}

			var model = prop.ProductModel;
			var manufacturer = prop.ProductManufacturer;

			//'model' is most usually correct, but fallback to 'name' for devices that don't have 'model'
			if (string.IsNullOrEmpty (model)) {
				model = prop.ProductName;
				if (string.IsNullOrEmpty (model)) {
					model = null;
				}
			}

			//'manufacturer' is most usually correct, but fallback to 'brand' for devices that don't have 'manufacturer'
			if (string.IsNullOrEmpty (manufacturer)) {
				manufacturer = prop.ProductBrand;
				if (string.IsNullOrEmpty (manufacturer)) {
					manufacturer = null;
				}
			}

			//many devices use the keys in nonstandard/inconsistent ways, so special-case them as we find them
			if (model != null && manufacturer != null) {
				string value;
				if (nameFixes.TryGetValue (manufacturer + " " + model, out value)) {
					return value;
				}
			}

			if (manufacturer != null) {
				//some model names are prefixed with the manufacturer name, remove it
				if (model != null && model.StartsWith (manufacturer, StringComparison.OrdinalIgnoreCase)) {
					model = model.Substring (manufacturer.Length).TrimStart (' ');
					if (model.Length == 0) {
						model = null;
					}
				}

				//some manufacturers can't spell/case their own name correctly
				string value;
				if (manufacturerNameFixes.TryGetValue (manufacturer, out value)) {
					manufacturer = value;
				}
			}

			if (manufacturer == "unknown") {
				// check for genymotion, some of their emulators don't show the correct name
				if (!string.IsNullOrEmpty(prop.Get("ro.genymotion.player.version"))) {
					manufacturer = "Genymotion";
				}
			}


			//construct a combined name from manufacturer/model, if present
			string combined;
			if (model != null && manufacturer != null) {
				combined = manufacturer + " " + model;
			} else {
				combined = model ?? manufacturer;
			}

			return combined ?? "Unknown";
		}

		public AndroidDeviceProperties Properties {
			get { return properties; }
		}

		public event Action<AndroidDevice> PropertiesChanged;

		internal void SetProperties (Dictionary<string, string> values)
		{
			properties = new AndroidDeviceProperties (values);
			PropertiesChanged?.Invoke (this);
		}

		public async Task RefreshProperties (CancellationToken token)
		{
			var newProps = await GetProperties (token).ConfigureAwait (false);
			SetProperties (newProps);
		}

		public override string ToString ()
		{
			return string.Format ("Device: {0} [{1}]", ID, State);
		}

		public override bool Equals (object obj)
		{
			if (obj == null)
				return false;
			if (ReferenceEquals (this, obj))
				return true;
			var other = obj as AndroidDevice;
			return other != null && ID == other.ID && State == other.State && IsEmulator == other.IsEmulator;
		}

		public override int GetHashCode ()
		{
			unchecked {
				return (ID != null ? ID.GetHashCode () : 0)
					^ (State != null ? State.GetHashCode () : 0)
					^ IsEmulator.GetHashCode ();
			}
		}

		/// <summary>
		/// Given a semicolon delimited list of architectures a package supports,
		/// returns true or false is this device can run the package.
		/// </summary>
		public bool CanRunPackageArchitecture (string archs)
		{
			return CanRunPackageArchitecture (archs.Split (';', ','));
		}

		public bool CanRunPackageArchitecture (string[] archs)
		{
			return archs.Contains (Properties.ProductCpuAbi)
				|| archs.Contains (Properties.ProductCpuAbi2)
				|| archs.Intersect (Properties.ProductCpuAbiList).Any ();
		}

		public string SupportedArchitecturesFormatted ()
		{
			if (string.IsNullOrWhiteSpace (Properties.ProductCpuAbi2))
				return Properties.ProductCpuAbi;

			return string.Format ("{0}/{1}", Properties.ProductCpuAbi, Properties.ProductCpuAbi2);
		}

		Task ConnectClientTransport (CancellationToken cancellationToken, out AdbClient client)
		{
			client = adb.CreateClient (cancellationToken);
			try {
				return client.ConnectTransportAsync (ID);
			} catch (ObjectDisposedException) {
				if (cancellationToken.IsCancellationRequested) {
					var tcs = new TaskCompletionSource<object> ();
					tcs.SetCanceled ();
					return tcs.Task;
				}
				throw;
			}
		}

		Task ConnectSyncClient (CancellationToken cancellationToken, out AdbSyncClient client)
		{
			client = adb.CreateSyncClient (cancellationToken);
			return client.ConnectSyncSessionAsync (ID);
		}

		public Task<string> RunShellCommand (params string[] commands)
		{
			return RunShellCommand (AdbShellCommand.JoinArguments (commands), CancellationToken.None);
		}

		public Task<string> RunShellCommand (CancellationToken token, params string[] commands)
		{
			return RunShellCommand (AdbShellCommand.JoinArguments (commands), token);
		}

		public Task<string> RunShellCommand (string command, CancellationToken cancellationToken)
		{
			var writer = new System.IO.StringWriter ();
			return RunShellCommand (command, writer.Write, cancellationToken)
				.ContinueWith (t => {
					if (t.IsFaulted)
						throw t.Exception;
					return StripLibDvmRelocationWarning (writer.ToString ());
				});
		}

		static string StripLibDvmRelocationWarning (string s)
		{
			int i = s.IndexOf (libDvmRelocationWarning, StringComparison.Ordinal);
			if (i < 0)
				return s;

			int len = libDvmRelocationWarning.Length;
			while (s.Length > i + len && (s[i + len] == '\n' || s[i + len] == '\r'))
				len++;

			return s.Substring (0, i) + s.Substring (i + len);
		}

		static readonly string libDvmRelocationWarning =
			"WARNING: linker: libdvm.so has text relocations. This is wasting memory and is a security risk. Please fix.";

		public Task RunShellCommand (string command, Action<string> output, CancellationToken cancellationToken)
		{
			AndroidLogger.LogDebug ("RunShellCommand", "{0} {1}", ID, command);
			AdbClient client;
			var transport = ConnectClientTransport (cancellationToken, out client);
			return transport.ContinueWith (t => {
					if (t.IsFaulted)
						throw t.Exception;
					return client.WriteCommandWithStatusAsync ("shell:" + command);
				}, cancellationToken)
				.Unwrap ()
				.ContinueWith (t => {
					if (t.IsFaulted)
						throw t.Exception;
					return client.ReadTextAsync (output);
				}, cancellationToken)
				.Unwrap ()
				.Cleanup (client, cancellationToken);
		}

		const string DefaultPackagesFile = "/data/system/packages.xml";
		const string BackupPackagesFile = "/dbdata/system/packages.xml";

		public Task<List<AndroidInstalledPackage>> GetPackages ()
		{
			return GetPackages (CancellationToken.None);
		}

		public Task<List<AndroidInstalledPackage>> GetPackages (CancellationToken cancellationToken)
		{
			return GetPackages (false, cancellationToken);
		}

		public Task<List<AndroidInstalledPackage>> GetPackages(bool requireVersions, CancellationToken cancellationToken)
		{
			PmListPackagesCommand pmListPackagesCommand = new PmListPackagesCommand () {
				RequireVersions = requireVersions,
				ApiLevel = Properties?.BuildVersionSdk ?? 0,
			};
			return GetPackages (pmListPackagesCommand, cancellationToken);
		}

		public async Task<List<AndroidInstalledPackage>> GetPackages (PmListPackagesCommand pmListPackagesCommand, CancellationToken cancellationToken)
		{
			bool requireVersions = pmListPackagesCommand.RequireVersions;
			try {
				if (Properties == null) {
					await RefreshProperties (cancellationToken);
				}
				if (!requireVersions || Properties?.BuildVersionSdk >= 26) {
					return await GetPmPackages (pmListPackagesCommand, cancellationToken);
				}
			} catch (Exception ex) {
				AndroidLogger.LogDebug ($"Exception in {nameof (GetPackages)}: {ex}");
			}
			try {
				return await GetPackages (DefaultPackagesFile, cancellationToken);
			} catch (Exception ex) {
				AndroidLogger.LogDebug ($"Exception in {nameof (GetPackages)}: {ex}");
			}
			try {
				return await GetPackages (BackupPackagesFile, cancellationToken);
			} catch (Exception ex) {
				// One final attempt using requireVersions=false
				if (requireVersions) {
					AndroidLogger.LogDebug ($"Exception in {nameof (GetPackages)}: {ex}");
					pmListPackagesCommand.RequireVersions = false;
					return await GetPmPackages (pmListPackagesCommand, cancellationToken: cancellationToken);
				}
				// Otherwise just throw
				throw;
			}
		}

		// We use this first for new devices going forward. API < 26 won't give
		// us the package version, but at least we can tell if a package is
		// installed, and should hopefully never fail.
		async Task<List<AndroidInstalledPackage>> GetPmPackages (PmListPackagesCommand pmListPackagesCommand, CancellationToken cancellationToken)
		{
			string command = pmListPackagesCommand.ToString();
			var log = new AndroidTaskLog (nameof (GetPmPackages), command);
			var output = await RunShellCommand (command, cancellationToken);
			AndroidLogger.LogTask (log.Complete (output));
			// I think this is if the system isn't fully booted.
			// It should (hopefully) never be hit.
			if (output.StartsWith ("Error:", StringComparison.Ordinal))
				throw new AdbException ($"Error retrieving package list:\n{output}");
			return AdbOutputParsing.ParsePmPackageList (output);
		}

		public Task<List<AndroidInstalledPackage>> GetPackages (string packageFile)
		{
			return GetPackages (packageFile, CancellationToken.None);
		}

		public Task<List<AndroidInstalledPackage>> GetPackages (string packageFile, CancellationToken cancellationToken)
		{
			var command = "cat " + packageFile;
			var log = new AndroidTaskLog ("GetPackages", command);

			return RunShellCommand (command, cancellationToken)
				.ContinueWith (t => {
					if (t.IsFaulted)
						throw t.Exception;
					AndroidLogger.LogTask (log.Complete (t.Result));
					return AdbOutputParsing.ParsePackageList (t.Result);
				}, cancellationToken);
		}

		public Task<Dictionary<string,string>> GetProperties ()
		{
			return GetProperties (CancellationToken.None);
		}

		public Task<Dictionary<string,string>> GetProperties (CancellationToken cancellationToken)
		{
			var command = "getprop";
			var log = new AndroidTaskLog ("GetProperties", command);

			var propertyTask = RunShellCommand (command, cancellationToken);

			return propertyTask
				.ContinueWith<Dictionary<string, string>> (t => {
					if (t.IsFaulted)
						throw t.Exception;
					AndroidLogger.LogTask (log.Complete (t.Result));
					return AdbOutputParsing.ParseGetprop (t.Result);
				}, cancellationToken);
		}

		public Task<AndroidDiskInformation> GetAvailableSpace ()
		{
			return GetAvailableSpace (CancellationToken.None);
		}

		public Task<AndroidDiskInformation> GetAvailableSpace (CancellationToken cancellationToken)
		{
			var command = "df";
			var log = new AndroidTaskLog ("GetAvailableSpace", command);

			return RunShellCommand (command, cancellationToken)
				.ContinueWith (t => {
					if (t.IsFaulted)
						throw t.Exception;
					AndroidLogger.LogTask (log.Complete (t.Result));
					return AndroidDiskInformation.FromDfOutput (t.Result);
				}, cancellationToken);
		}

		public Task<int> GetProcessId (string packageName)
		{
			return GetProcessId (packageName, CancellationToken.None);
		}

		//if pid is zero, process was not found
		public async Task<int> GetProcessId (string packageName, CancellationToken cancellationToken)
		{
			if (packageName == null)
				throw new ArgumentNullException ("packageName");

			var result = await GetProcessId (packageName, tryGrep: true, cancellationToken);

			// In case we fail with: `/system/bin/sh: grep: inaccessible or not found`
			if (result?.IndexOf ("grep:", StringComparison.OrdinalIgnoreCase) != -1) {
				result = await GetProcessId (packageName, tryGrep: false, cancellationToken);
			}

			return AdbOutputParsing.GetPackagePidFromPs (packageName, result);
		}

		Task<string> GetProcessId (string packageName, bool tryGrep, CancellationToken cancellationToken)
		{
			var builder = new StringBuilder ("ps");
			if (BuildVersionSdk >= 26)
				builder.Append (" -A");

			// Pipe `ps -A` output to `grep -w -E 'PID|com.companyname.mauiapp42'`
			// -w is "whole word" match
			// -E is extended Regex
			// This will exclude lines beside the header row & the package we need, example:
			// USER           PID  PPID     VSZ    RSS WCHAN            ADDR S NAME
			// u0_a993      12547  1340 14952808 216740 0                  0 S com.companyname.mauiapp42
			if (tryGrep && !string.IsNullOrEmpty (packageName)) {
				builder.Append (" | grep -w -E 'PID|");
				builder.Append (packageName);
				builder.Append ('\'');
			}

			var command = builder.ToString ();
			var log = new AndroidTaskLog ("GetProcessId", command);

			// Can't pass any info to 'ps', as it seems to have an irregular behaviour among versions.
			return RunShellCommand (command, cancellationToken)
				.ContinueWith (t => {
					AndroidLogger.LogTask (log.Complete (t.Result));
					return t.Result;
				}, cancellationToken);

		}

		public Task<string> Broadcast (string action, string category)
		{
			return Broadcast (action, category, CancellationToken.None);
		}

		public Task<string> Broadcast (string action, string category, CancellationToken cancellationToken)
		{
			var command = AdbShellCommand.Am ("broadcast", action, new[]{category});
			var log = new AndroidTaskLog ("Broadcast", command);

			return RunShellCommand (command, cancellationToken)
				.ContinueWith (t => {
					AndroidLogger.LogTask (log.Complete (t.Result));
					return AdbOutputParsing.TryBroadcastResult (t.Result);
				}, cancellationToken);
		}

		public Task ForceStop (string package)
		{
			return ForceStop (package, CancellationToken.None);
		}

		public Task ForceStop (string package, CancellationToken cancellationToken)
		{
			var command = AdbShellCommand.AmForceStop (package);
			var log = new AndroidTaskLog ("ForceStop", command);

			return RunShellCommand (command, cancellationToken)
				.ContinueWith (t => {
					var msg = t.Result;
					AndroidLogger.LogTask (log.Complete (msg));
					if (!string.IsNullOrEmpty (msg) && !msg.StartsWith ("WARNING:"))
						throw new AdbException (msg);
				}, cancellationToken);
		}

		public Task<string> Broadcast (string action, string[] categories, Dictionary<string, string> extras, string componentName, CancellationToken cancellationToken)
		{
			return Broadcast (action, categories, extras, componentName, null, cancellationToken);
		}

		public Task<string> Broadcast (string action, string[] categories, Dictionary<string, string> extras, string componentName, string intent, CancellationToken cancellationToken)
		{
			var objExtras = new Dictionary<string, object> (extras != null ? extras.Count : 0);
			if (extras != null)
				foreach (var e in extras)
					objExtras [e.Key] = e.Value;
			return Broadcast (new AmBroadcastCommand {
				Action = action,
				Categories = categories,
				Extras = objExtras,
				Component = componentName, // -n <COMPONENT>
				Intent = intent, // [<URI> | <PACKAGE> | <COMPONENT>]
			}, cancellationToken);
		}

		public Task<string> Broadcast (AmBroadcastCommand broadcastCommand, CancellationToken cancellationToken = default(CancellationToken))
		{
			// Only Android 3.0+ supports specifying a uri/package/component intent.
			if (Properties.BuildVersionSdk < 11)
				broadcastCommand.Intent = null;

			var command = broadcastCommand.ToString ();
			var log = new AndroidTaskLog ("Broadcast", command);

			return RunShellCommand (command, cancellationToken)
				.ContinueWith (t => {
					AndroidLogger.LogTask (log.Complete (t.Result));
					return AdbOutputParsing.TryBroadcastResult (t.Result);
				}, cancellationToken);
		}

		public Task StartActivity (string action, string package, string activity, bool wait = false)
		{
			return StartActivity (action, package, activity, wait, CancellationToken.None);
		}

		public Task StartActivity (string action, string package, string activity, bool wait,
		                           CancellationToken cancellationToken)
		{
			return StartActivity (action, null, package, activity, wait, cancellationToken);
		}

		public Task StartActivity (string action, string [] categories, string package, string activity, bool wait, CancellationToken cancellationToken)
		{
			return ExecuteIntentCommandAsync (new AmStartCommand (package, activity) {
				Action = action,
				Categories = categories,
				Wait = wait,
			}, null, cancellationToken);
		}

		[Obsolete ("Use ExecuteIntentCommandAsync")]
		public Task StartActivity (AmStartCommand startCommand, CancellationToken cancellationToken = default(CancellationToken))
		{
			return ExecuteIntentCommandAsync(startCommand, null, cancellationToken);
		}

		/// <summary>
		/// Executes the given intent command, if logWriter is not null passes the output of the command to logWriter
		/// </summary>
		public async Task ExecuteIntentCommandAsync(AmIntentCommand intentCommand, Action<string> logWiter, CancellationToken cancellationToken = default(CancellationToken))
		{
			var command = intentCommand.ToString();
			var log = new AndroidTaskLog("StartIntent", command);

			var shellTask = RunShellCommand(command, cancellationToken).ContinueWith(t => {
				if (t.IsFaulted) {
					AndroidLogger.LogError("Error executing intent", t.Exception);
					return t.Exception.Message;
				}

				AndroidLogger.LogTask(log.Complete(t.Result));
				return t.Result;
			});

			var result = string.Empty;

			// wait for the am command to complete, if it doesn't then it might be because
			// we are launching a receiver on device and we need to stop waiting and get the debugger to connect
			// then the broadcast command will complete
			if (await Task.WhenAny(shellTask, Task.Delay(5000)) == shellTask)
			{
				// task completed within timeout
				result = shellTask.Result;
			}
			else {
				// we timed out
			}

			if (logWiter != null) {
				logWiter(result);
			}

			AndroidLogger.LogTask(log.Complete(result));
			AdbOutputParsing.CheckStartResult(result, intentCommand.Component ?? intentCommand.Intent);
		}

		public Task<long> GetDate ()
		{
			return GetDate (CancellationToken.None);
		}

		public Task<long> GetDate (CancellationToken cancellationToken)
		{
			var command = "date +%s";
			var log = new AndroidTaskLog ("GetDate", command);

			return RunShellCommand (command, cancellationToken)
				.ContinueWith (t => {
					if (t.IsFaulted)
						throw t.Exception;
					AndroidLogger.LogTask (log.Complete (t.Result));
					return long.Parse (t.Result);
				}, cancellationToken);
		}

		public Task<string[]> ListFilesAsync (string remoteDirectory, CancellationToken cancellationToken)
		{
			var command = string.Format ("cd {0} || exit 1; for f in *; do echo $f; done;", remoteDirectory);
			var log = new AndroidTaskLog ("GetFiles", command);

			return RunShellCommand (command, cancellationToken)
				.ContinueWith (t => {
					if (t.IsFaulted)
						throw t.Exception;
					AndroidLogger.LogTask (log.Complete (t.Result));
					if (t.Result.StartsWith ("/system/bin/sh"))
						return new string [0];
					return t.Result.Split (new char[] { '\n' }).Select (x => x.Trim ()).ToArray ();
				}, cancellationToken);
		}

		public static string GetRemoteTempApkPath (string apkFile)
		{
			var name = System.IO.Path.GetFileName (apkFile);
			return "/data/local/tmp/" + name;
		}

		public Task InstallPackage (string remoteApkFile, bool reinstall = false, bool external = false)
		{
			return InstallPackage (remoteApkFile, reinstall, external, CancellationToken.None);
		}

		public Task InstallPackage (string remoteApkFile, bool reinstall, bool external,
			CancellationToken cancellationToken)
		{
			return InstallPackage (remoteApkFile, AdbShellCommand.ToInstallFlags (reinstall, external), cancellationToken);
		}

		public Task InstallPackage (string remoteApkFile, AdbInstallFlags flags, CancellationToken cancellationToken)
		{
			return InstallPackage (new PmInstallCommand {
				RemoteApkFile = remoteApkFile,
				Flags = flags,
			}, cancellationToken);
		}

		public Task InstallPackage (PmInstallCommand pmInstallCommand, CancellationToken cancellationToken) {
			var command = pmInstallCommand.ToString ();
			var log = new AndroidTaskLog ("InstallPackage", command);

			return RunShellCommand (command, cancellationToken)
				.ContinueWith (t => {
					AndroidLogger.LogTask (log.Complete (t.Result));
					AdbOutputParsing.CheckInstallSuccess (t.Result, pmInstallCommand.RemoteApkFile);
				}, cancellationToken);
		}

		public Task DeleteFile (string remotePath, bool ignoreError = false)
		{
			return DeleteFile (remotePath, ignoreError, CancellationToken.None);
		}

		public async Task DeleteFile (string remotePath, bool ignoreError, CancellationToken cancellationToken)
		{
			var command = AdbShellCommand.Rm (remotePath, recursive: false, ignoreError: ignoreError);
			var log = new AndroidTaskLog ("DeleteFile", command);
			var msg = await RunShellCommand (command, cancellationToken);
			AndroidLogger.LogTask (log.Complete (msg));
			if (!ignoreError && !string.IsNullOrEmpty (msg))
				throw new AdbException (msg);
		}

		public async Task DeleteDirectory (string remotePath, bool ignoreError, CancellationToken cancellationToken)
		{
			var command = AdbShellCommand.Rm (remotePath, recursive: true, ignoreError: ignoreError);
			var log = new AndroidTaskLog ("DeleteDirectory", command);
			var msg = await RunShellCommand (command, cancellationToken);
			AndroidLogger.LogTask (log.Complete (msg));
			if (!ignoreError && !string.IsNullOrEmpty (msg))
				throw new AdbException (msg);
		}

		public Task WaitUntilReady ()
		{
			return WaitUntilReady (CancellationToken.None);
		}

		public Task WaitUntilReady (CancellationToken token)
		{
			//unlike wait-for-device, this will actually wait until the device is properly booted
			//see http://code.google.com/p/android/issues/detail?id=842
			return Task.Run (() => {
				var command = AdbShellCommand.JoinArguments ("pm", "path", "android");
				var log = new AndroidTaskLog ("WaitUntilReady", command);
				while (true) {
					var task = RunShellCommand (command, token);
					//wait will end if the task is cancelled
					task.Wait ();
					if (task.IsCanceled) {
						AndroidLogger.LogTask (log.Complete ("timed out"));
						return;
					}
					using (var reader  = new StringReader (task.Result)) {
						string result;
						bool ready = false;
						while ((result = reader.ReadLine ()) != null) {
							ready = result.StartsWith ("package", StringComparison.OrdinalIgnoreCase);
							if (ready) {
								AndroidLogger.LogTask (log.Complete (result));
								return;
							}
						}
						AndroidLogger.LogDebug ("WaitUntilReady", "-- Device not ready yet, output:\n{0}", task.Result);
					}
				}
			}, token);
		}

		public Task SetProperty (string property, string value)
		{
			return SetProperty (property, value, CancellationToken.None);
		}

		public Task SetProperty (string property, string value, CancellationToken cancellationToken)
		{
			var command = AdbShellCommand.Setprop (property, value);
			var log = new AndroidTaskLog ("SetProperty", command);

			return RunShellCommand (command, cancellationToken).ContinueWith (t => {
				AndroidLogger.LogTask (log.Complete (t.Result));
			}, cancellationToken);
		}

		public async Task<bool> SetInternalPropertyFile (string packageName, string property, string value, CancellationToken cancellationToken)
		{
			if (property == null)
				throw new ArgumentNullException (nameof (property));
			if (string.IsNullOrEmpty (property))
				throw new ArgumentException ("Must contain a value.", nameof (property));
			if (property.Contains ('/') || property.Contains ('\\'))
				throw new ArgumentException ("Must not contain directory separators.", nameof (property));
			string tempPath = "/data/local/tmp/.";
			string overridePath = "files/.__override__/";
			if (string.IsNullOrEmpty (value)) {
				await RunShellCommand (cancellationToken, "run-as", packageName, "rm", "-f", $"{overridePath}{property}");
				return true;
			}
			using(var ms = new MemoryStream (System.Text.Encoding.UTF8.GetBytes (value))) {
				await Push (ms, $"{tempPath}{property}", cancellationToken: cancellationToken);
			}
			try {
				await RunShellCommand (cancellationToken, "run-as", packageName, "mkdir", "-p", overridePath);
				await RunShellCommand (cancellationToken, "run-as", packageName, "cp", $"{tempPath}{property}", $"{overridePath}{property}");
				return true;
			} finally {
 				await RunShellCommand (cancellationToken, "run-as", packageName, "rm", "-f", $"{tempPath}{property}");
			}
		}

		public Task SetPropertyFile (string destination, string value, CancellationToken cancellationToken)
		{
			if (string.IsNullOrEmpty (value))
				return DeleteFile (destination, true, cancellationToken);

			var destDir = new AdbSyncDirectory (Path.GetDirectoryName (destination));
			return PushSyncItems (destDir,
					Path.GetDirectoryName (destination).Replace ('\\', '/'),
					new AdbSyncClient.PushOptions () {
						DryRun = false,
						RemoveUnknown = false,
						NotifySync = (a) => AndroidLogger.LogDebug ("NotifySync", "{0} {1} {2} {3}", a.Kind, a.LocalPath, a.RemotePath, a.Size),
						NotifyPhase = (b) => AndroidLogger.LogDebug ("NotifyPhase", "{0}", b),
					}, cancellationToken).ContinueWith (r => {
						var log = new AndroidTaskLog ("SetPropertyFile", destination);
						return Push (new MemoryStream (System.Text.Encoding.UTF8.GetBytes (value)), destination, cancellationToken: cancellationToken)
							.ContinueWith (t => {
								AndroidLogger.LogTask (log.Complete(t.Result));
							}, cancellationToken);
				}, cancellationToken, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default).Unwrap ();
		}

		public Task ClearLogCat ()
		{
			return ClearLogCat (CancellationToken.None);
		}

		public Task ClearLogCat (CancellationToken cancellationToken)
		{
			var command = AdbShellCommand.JoinArguments ("logcat", "-c");
			var log = new AndroidTaskLog ("ClearLogCat", command);

			return RunShellCommand (command, cancellationToken).ContinueWith (t => {
				AndroidLogger.LogTask (log.Complete (t.Result));
			}, cancellationToken);
		}

		/// <summary>
		/// Reads logcat to the end and returns.
		/// </summary>
		public Task<List<AndroidLogCatEntry>> GetLogCat ()
		{
			return GetLogCat (CancellationToken.None);
		}

		/// <summary>
		/// Reads logcat to the end and returns.
		/// </summary>
		public Task<List<AndroidLogCatEntry>> GetLogCat (CancellationToken cancellationToken, string[] excludedLogTags = null)
		{
			var pb = new ProcessArgumentBuilder ();
			pb.Add ("-v", "time", "-d");
			if (excludedLogTags != null) {
				foreach (string tag in excludedLogTags)
					pb.AddQuoted (tag + ":S");
			}
			string command = "logcat " + pb.ToString ();

			var log = new AndroidTaskLog ("GetLogCat", command);

			return RunShellCommand (command, cancellationToken).ContinueWith (t => {
				AndroidLogger.LogTask (log.Complete (t.Result));
				return AdbOutputParsing.ParseLogCat (t.Result);
			}, cancellationToken);
		}

		public Task RunShellCommandAsync (string command, Action<string> outputLine, CancellationToken cancellationToken)
		{
			return Task.Run (() => {
				try {
					AndroidLogger.LogDebug ("RunShellCommand", "{0} {1}", ID, command);

					var client = adb.CreateClient (cancellationToken);

					try {
						client.ConnectTransport (ID);
						if (cancellationToken.IsCancellationRequested)
							return;

						client.WriteCommandWithStatus ("shell:" + command);
						if (cancellationToken.IsCancellationRequested)
							return;

						client.ReadLineWhile (
							output: (line) => { outputLine (line); },
							predicate: () => { return !cancellationToken.IsCancellationRequested; }
						);
					} catch (DeviceDisconnectedException) {
						AndroidLogger.LogWarning ("RunShellCommand", "{0} {1} end: device disconected", ID, command);
					} catch (AdbException adbEx) {
						var socketEx = adbEx.InnerException as SocketException;
						if (socketEx == null) {
							var ioEx = adbEx.InnerException as IOException;
							if(ioEx != null)
								socketEx = ioEx.InnerException as SocketException;
						}

						var wasDisconnected = socketEx != null && socketEx.SocketErrorCode == SocketError.ConnectionAborted;
						if (wasDisconnected)
							AndroidLogger.LogWarning ("RunShellCommand", "{0} {1} end: device disconnected: {2}", ID, command, adbEx);
						else
							AndroidLogger.LogError ("RunShellCommand", "{0} {1} failed: {2}", ID, command, adbEx);
					} catch (Exception ex) {
						AndroidLogger.LogError ("RunShellCommand", "{0} {1} failed: {2]", ID, command, ex);
					} finally {
						client.Dispose ();
					}
				} catch (Exception e) {
					AndroidLogger.LogError (e.Message, e);
				}
			}, cancellationToken);
		}


		/// <summary>
		/// Reads logcat continuously until cancelled.
		/// </summary>
		public Task GetLogCat (Action<AndroidLogCatEntry> callback, CancellationToken cancellationToken, string [] excludedLogTags = null)
		{
			var pb = new ProcessArgumentBuilder ();
			pb.Add ("-v", "time");
			if (excludedLogTags != null) {
				foreach (string tag in excludedLogTags)
					pb.AddQuoted (tag + ":S");
			}
			string command = "logcat " + pb.ToString ();

			AndroidLogger.LogDebug ("GetLogCat", command);

			return RunShellCommandAsync (
				command,
				logLine => {
					try {
						var entry = AdbOutputParsing.ParseLogCatEntry (logLine);
						if (entry != null) {
							try {
								callback (entry);
							} catch (Exception exC) {
								AndroidLogger.LogError ("logcat callback failed", exC);
							}
						}
					} catch (Exception ex) {
						AndroidLogger.LogError ("logcat output line parse failed", ex);
					}
				},
				cancellationToken
			);
		}

		public Task UninstallPackage (string package, bool preserveData)
		{
			return UninstallPackage (package, preserveData, CancellationToken.None);
		}

		public Task UninstallPackage (string package, bool preserveData, CancellationToken cancellationToken)
		{
			return UninstallPackage (new PmUninstallCommand {
				PackageName = package,
				PreserveData = preserveData,
			}, cancellationToken);
		}

		public Task UninstallPackage (PmUninstallCommand pmUninstallCommand, CancellationToken cancellationToken)
		{
			var command = pmUninstallCommand.ToString ();
			var log = new AndroidTaskLog ("UninstallPackage", command);
			Task cmdtask = DoUninstallPackage (log, command, cancellationToken);
			if (cmdtask.IsFaulted)
				return cmdtask; // Something bad happened, no need to try again
			return DoUninstallPackage (log, command, cancellationToken);
		}

		Task DoUninstallPackage (AndroidTaskLog log, string command, CancellationToken cancellationToken)
		{
			return RunShellCommand (command, cancellationToken).ContinueWith (t => {
				AndroidLogger.LogTask (log.Complete (t.Result));
			}, cancellationToken);
		}

		[Obsolete ("Use another overload with 'AdbSyncClient.PushOptions' parameter.")]
		public Task<long> PushSyncItems (
			AdbSyncDirectory rootItem,
			string remoteParentDir,
			bool dryRun=false,
			Action<AdbSyncNotification> notifySync=null,
			Action<string> notifyPhase=null,
			AdbProgressReporter notifyProgress=null,
			CancellationToken cancellationToken = new CancellationToken ())
		{
			return PushSyncItems (rootItem, remoteParentDir, new AdbSyncClient.PushOptions () {
				DryRun = dryRun,
				NotifySync = notifySync,
				NotifyPhase = notifyPhase,
				NotifyProgress = notifyProgress
				}, cancellationToken);
		}

		public Task<long> PushSyncItems (
			AdbSyncDirectory rootItem,
			string remoteParentDir,
			AdbSyncClient.PushOptions options,
			CancellationToken cancellationToken = new CancellationToken ())
		{
			var log = new AndroidTaskLog ("PushSyncItems", $"{rootItem.ToString ()} => {remoteParentDir}");
			AdbSyncClient client;
			Task connect = ConnectSyncClient (cancellationToken, out client);
			return connect.ContinueWith (t => {
				if (t.IsFaulted)
					throw t.Exception;
				return client.PushSyncItemsAsync (rootItem, remoteParentDir, options);
			}, cancellationToken)
			.Unwrap ()
			.Cleanup (client, log, cancellationToken);
		}

		public Task<long> Push (
			string localFilePath,
			string remoteFilePath,
			AdbProgressReporter notifyProgress=null,
			CancellationToken cancellationToken = new CancellationToken ())
		{
			AdbSyncClient client;
			var log = new AndroidTaskLog ("Push", localFilePath + " : " + remoteFilePath);

			Task connect = ConnectSyncClient (cancellationToken, out client);
			return connect.ContinueWith (t => {
				if (t.IsFaulted)
					throw t.Exception;
				return client.PushAsync (localFilePath, remoteFilePath, notifyProgress);
			}, cancellationToken)
			.Unwrap ()
			.Cleanup (client, log, cancellationToken);
		}

		public Task<long> Push (
			Stream contents,
			string remoteFilePath,
			AdbProgressReporter notifyProgress=null,
			CancellationToken cancellationToken = new CancellationToken ())
		{
			AdbSyncClient client;
			var log = new AndroidTaskLog ("Push", remoteFilePath);

			Task connect = ConnectSyncClient (cancellationToken, out client);
			return connect.ContinueWith (t => {
				if (t.IsFaulted)
					throw t.Exception;
				return client.PushAsync (contents, remoteFilePath, notifyProgress);
			}, cancellationToken)
				.Unwrap ()
				.Cleanup (client, log, cancellationToken);
		}

		public Task<long> Push (
			Stream contents,
			bool leaveOpen,
			string remoteFilePath,
			AdbProgressReporter notifyProgress=null,
			CancellationToken cancellationToken = new CancellationToken ())
		{
			AdbSyncClient client;
			var log = new AndroidTaskLog ("Push", remoteFilePath);

			Task connect = ConnectSyncClient (cancellationToken, out client);
			return connect.ContinueWith (t => {
				if (t.IsFaulted)
					throw t.Exception;
				return client.PushAsync (contents, leaveOpen, remoteFilePath, notifyProgress);
			}, cancellationToken)
				.Unwrap ()
				.Cleanup (client, log, cancellationToken);
		}

		public Task<long> PushDirectory (
			string localDirectoryPath,
			string remoteDirectoryPath,
			bool checkTimestamps,
			bool removeUnknown,
			bool dryRun,
			bool removeBeforeCopy,
			Action<AdbSyncNotification> notifySync=null,
			Action<string> notifyPhase=null,
			AdbProgressReporter notifyProgress=null,
			CancellationToken cancellationToken = new CancellationToken ())
		{
			AdbSyncClient client;
			var log = new AndroidTaskLog ("PushDirectory", localDirectoryPath + " : " + remoteDirectoryPath);

			Task connect = ConnectSyncClient (cancellationToken, out client);
			return connect.ContinueWith (t => {
				if (t.IsFaulted)
					throw t.Exception;
				return client.PushDirectoryAsync (new AdbSyncClient.PushOptions () {
					LocalDirectoryPath = localDirectoryPath,
					RemoteDirectoryPath = remoteDirectoryPath,
					CheckTimestamps = checkTimestamps,
					RemoveUnknown = removeUnknown,
					DryRun = dryRun,
					RemoveBeforeCopy = removeBeforeCopy,
					NotifySync = notifySync,
					NotifyPhase = notifyPhase,
					NotifyProgress = notifyProgress,
				}, cancellationToken);
			}, cancellationToken)
			.Unwrap ()
			.Cleanup (client, log, cancellationToken);
		}

		public Task<AdbFileInfo> GetRemoteFileInfo (string remoteFilePath, CancellationToken token)
		{
			AdbSyncClient client;
			var log = new AndroidTaskLog ("GetRemoteFileInfo", remoteFilePath);

			Task connect = ConnectSyncClient (token, out client);
			return connect.ContinueWith (t => {
				if (t.IsFaulted)
					throw t.Exception;
				return client.StatAsync (remoteFilePath);
			}, token)
			.Unwrap ()
			.Cleanup (client, log, token);
		}

		public async Task<string> RunShellCommandStream (
			string [] commands,
			Stream extraInput = null,
			CancellationToken cancellationToken = default (CancellationToken))
		{
			AdbClient client = await StartRunShellCommand (commands, cancellationToken);
			if (extraInput != null)
				await client.WriteStreamAsync (extraInput);

			return await client.ReadStringAsync (token: cancellationToken);
		}

		public async Task<string> RunShellCommandStream (
			string [] commands,
			Func<Stream, Task<bool>> extraInput = null,
			CancellationToken cancellationToken = default (CancellationToken))
		{
			AdbClient client = await StartRunShellCommand (commands, cancellationToken);
			if (extraInput != null) {
				while (await extraInput (client.Stream)) ;
			}

			return await client.ReadStringAsync (token: cancellationToken);
		}

		public async Task<byte[]> RunShellCommandStreamBytesOutput (
			string [] commands,
			Stream extraInput = null,
			CancellationToken cancellationToken = default (CancellationToken))
		{
			AdbClient client = await StartRunShellCommand (commands, cancellationToken);
			if (extraInput != null)
				await client.WriteStreamAsync (extraInput);

			return await client.ReadBytesAsync (token: cancellationToken);
		}

		public async Task<byte []> RunShellCommandStreamBytesOutput (
			string [] commands,
			Func<Stream, Task<bool>> extraInput = null,
			CancellationToken cancellationToken = default (CancellationToken))
		{
			AdbClient client = await StartRunShellCommand (commands, cancellationToken);
			if (extraInput != null) {
				while (await extraInput (client.Stream)) ;
			}

			return await client.ReadBytesAsync (token: cancellationToken);
		}

		async Task<AdbClient> StartRunShellCommand(string [] commands,
			CancellationToken cancellationToken = default (CancellationToken))
		{
			string JoinArguments (params string [] arguments)
			{
				var pb = new Mono.AndroidTools.Util.ProcessArgumentBuilder ();
				foreach (var a in arguments)
					pb.AddQuoted (a);
				return pb.ToString ();
			}

			var command = JoinArguments (commands);

			await ConnectClientTransport (cancellationToken, out AdbClient client);

			cancellationToken.ThrowIfCancellationRequested ();

			await client.WriteCommandWithStatusAsync ("shell:" + command);
			cancellationToken.ThrowIfCancellationRequested ();

			return client;
		}
	}
}
