using NUnit.Framework;
using NUnit.Framework.Interfaces;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.XPath;

namespace Xamarin.Android.Build.Tests
{
	public class DeviceTest: BaseTest
	{
		[OneTimeSetUp]
		public void DeviceSetup ()
		{
			SetAdbLogcatBufferSize (64);
			RunAdbCommand ("logcat -c");
		}

		[TearDown]
		protected override void CleanupTest ()
		{
			if (HasDevices && TestContext.CurrentContext.Result.Outcome.Status == TestStatus.Failed &&
					TestOutputDirectories.TryGetValue (TestContext.CurrentContext.Test.ID, out string outputDir)) {
				string local = Path.Combine (outputDir, "screenshot.png");
				string deviceLog = Path.Combine (outputDir, "logcat-failed.log");
				string remote = "/data/local/tmp/screenshot.png";
				RunAdbCommand ($"shell screencap {remote}");
				RunAdbCommand ($"pull {remote} \"{local}\"");
				RunAdbCommand ($"shell rm {remote}");
				RunAdbCommand ($"logcat > {deviceLog}", timeout: 5);
				TestContext.AddTestAttachment (local);
			}

			base.CleanupTest ();
		}

		protected static void RunAdbInput (string command, params object [] args)
		{
			RunAdbCommand ($"shell {command} {string.Join (" ", args)}");
		}

		protected static string SetAdbLogcatBufferSize (int sizeInMeg)
		{
			return RunAdbCommand ($"logcat -G {sizeInMeg}M");
		}

		protected static string ClearAdbLogcat ()
		{
			return RunAdbCommand ("logcat -c");
		}

		protected static string ClearDebugProperty ()
		{
			return RunAdbCommand ("shell setprop debug.mono.extra \"\"");
		}

		protected static void AdbStartActivity (string activity)
		{
			RunAdbCommand ($"shell am start -S -n \"{activity}\"");
		}

		protected void WaitFor (TimeSpan timeSpan, Func<bool> func)
		{
			var pause = new ManualResetEvent (false);
			TimeSpan total = timeSpan;
			TimeSpan interval = TimeSpan.FromMilliseconds (10);
			while (total.TotalMilliseconds > 0) {
				pause.WaitOne (interval);
				total = total.Subtract (interval);
				if (func ()) {
					break;
				}
			}
		}

		protected static bool MonitorAdbLogcat (Func<string, bool> action, string logcatFilePath, int timeout = 15)
		{
			string ext = Environment.OSVersion.Platform != PlatformID.Unix ? ".exe" : "";
			string adb = Path.Combine (AndroidSdkPath, "platform-tools", "adb" + ext);
			var info = new ProcessStartInfo (adb, "logcat") {
				RedirectStandardOutput = true,
				UseShellExecute = false,
				CreateNoWindow = true,
				WindowStyle = ProcessWindowStyle.Hidden,
			};

			bool didActionSucceed = false;
			ManualResetEventSlim stdout_done = new ManualResetEventSlim ();
			using (var sw = File.CreateText (logcatFilePath)) {
				using (var proc = Process.Start (info)) {
					proc.OutputDataReceived += (sender, e) => {
						if (e.Data != null) {
							sw.WriteLine (e.Data);
							if (action (e.Data)) {
								didActionSucceed = true;
							}
						} else {
							stdout_done.Set ();
						}
					};
					proc.BeginOutputReadLine ();
					TimeSpan time = TimeSpan.FromSeconds (timeout);
					while (!stdout_done.IsSet && !didActionSucceed && time.TotalMilliseconds > 0) {
						proc.WaitForExit (100);
						time -= TimeSpan.FromMilliseconds (100);
					}
					proc.Kill ();
					proc.WaitForExit ();
					stdout_done.Wait ();
					sw.Flush ();
					return didActionSucceed;
				}
			}
		}

		protected static bool WaitForDebuggerToStart (string logcatFilePath, int timeout = 60)
		{
			bool result = MonitorAdbLogcat ((line) => {
				return line.IndexOf ("Trying to initialize the debugger with options:", StringComparison.OrdinalIgnoreCase) > 0;
			}, logcatFilePath, timeout);
			return result;
		}

		static Regex regex = new Regex (@"\s*(\++)(?<seconds>\d)s(?<milliseconds>\d+)ms", RegexOptions.Compiled | RegexOptions.ExplicitCapture);

		protected static bool WaitForPermissionActivity (string logcatFilePath, int timeout = 5)
		{
			string activityNamespace = "com.android.permissioncontroller";
			string activityName = "com.android.packageinstaller.permission.ui.ReviewPermissionsActivity";
			bool result = WaitForActivityToStart (activityNamespace, activityName, logcatFilePath, timeout);
			if (result)
				ClickButton ("", "com.android.permissioncontroller:id/continue_button", "CONTINUE");
			return result;
		}

		protected static bool WaitForActivityToStart (string activityNamespace, string activityName, string logcatFilePath, int timeout = 60)
		{
			return WaitForActivityToStart (activityNamespace, activityName, logcatFilePath, out TimeSpan time, timeout);
		}

		protected static bool WaitForActivityToStart (string activityNamespace, string activityName, string logcatFilePath, out TimeSpan startupTime, int timeout = 60)
		{
			startupTime = TimeSpan.Zero;
			string capturedLine = string.Empty;
			bool result = MonitorAdbLogcat ((line) => {
				capturedLine = line;
				var idx1 = line.IndexOf ("ActivityManager: Displayed", StringComparison.OrdinalIgnoreCase);
				var idx2 = idx1 > 0 ? 0 : line.IndexOf ("ActivityTaskManager: Displayed", StringComparison.OrdinalIgnoreCase);
				return (idx1 > 0 || idx2 > 0) && line.Contains (activityNamespace) && line.Contains (activityName);
			}, logcatFilePath, timeout);
			var match = regex.Match (capturedLine);
			if (match.Success) {
				startupTime = new TimeSpan (0, 0, 0, int.Parse (match.Groups ["seconds"].Value), int.Parse (match.Groups ["milliseconds"].Value));
			}
			return result;
		}

		protected static XDocument GetUI ()
		{
			var ui = RunAdbCommand ("exec-out uiautomator dump /dev/tty");
			while (ui.Contains ("ERROR:")) {
				ui = RunAdbCommand ("exec-out uiautomator dump /dev/tty");
				WaitFor (1);
			}
			ui = ui.Replace ("UI hierchary dumped to: /dev/tty", string.Empty).Trim ();
			try {
				return XDocument.Parse (ui);
			} catch {
				return XDocument.Parse ("<node />");
			}
		}

		protected static (int x, int y, int w, int h) GetControlBounds (string packageName, string uiElement, string text)
		{
			var regex = new Regex (@"[(0-9)]\d*", RegexOptions.Compiled);
			var result = (x: 0, y: 0, w: 0, h: 0);
			var uiDoc = GetUI ();
			var node = uiDoc.XPathSelectElement ($"//node[contains(@resource-id,'{uiElement}')]");
			if (node == null)
				node = uiDoc.XPathSelectElement ($"//node[contains(@content-desc,'{uiElement}')]");
			if (node == null)
				node = uiDoc.XPathSelectElement ($"//node[contains(@text,'{text}')]");
			if (node == null)
				return result;
			var bounds = node.Attribute ("bounds");
			var matches = regex.Matches (bounds.Value);
			int.TryParse (matches [0].Value, out int x);
			int.TryParse (matches [1].Value, out int y);
			int.TryParse (matches [2].Value, out int w);
			int.TryParse (matches [3].Value, out int h);
			return (x: x, y: y, w: w, h: h);
		}

		protected static void ClickButton (string packageName, string buttonName, string buttonText)
		{
			var bounds = GetControlBounds (packageName, buttonName, buttonText);
			RunAdbInput ("input tap", bounds.x + ((bounds.w - bounds.x) / 2), bounds.y + ((bounds.h - bounds.y) / 2));
		}

		/// <summary>
		/// Returns the first device listed via `adb devices`
		/// 
		/// Output is:
		/// > adb devices
		/// List of devices attached
		/// 89RY0AEFA device
		/// </summary>
		/// <returns></returns>
		protected static string GetAttachedDeviceSerial ()
		{
			var text = RunAdbCommand ("devices");
			var lines = text.Split ('\n');
			if (lines.Length < 2) {
				Assert.Fail ($"Unexpected `adb devices` output: {text}");
			}
			var serial = lines [1];
			var index = serial.IndexOf ('\t');
			if (index != -1) {
				serial = serial.Substring (0, index);
			}
			return serial.Trim ();
		}
	}
}
