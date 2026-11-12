using Microsoft.Build.Framework;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Xamarin.Android.Build;
using Xamarin.Android.Tasks;
using Xamarin.ProjectTools;
using TaskItem = Microsoft.Build.Utilities.TaskItem;

namespace Xamarin.Android.Build.Tests
{
	[TestCategory ("Node-2")]
	[TestClass, DoNotParallelize]
	public partial class DebuggingTasksTests : BaseTest
	{
		[TestMethod]
		[DataRow ("arm64-v8a", "arm64-v8a,armeabi-v7a,armeabi", "android-arm", "armeabi-v7a", "android-arm")]
		[DataRow ("arm64-v8a", "arm64-v8a,armeabi-v7a", "android-arm;android-arm64", "arm64-v8a", "android-arm64")]
		[DataRow ("arm64-v8a", "arm64-v8a,armeabi-v7a", "android-x64;android-arm", "armeabi-v7a", "android-arm")]
		[DataRow ("arm64-v8a", "arm64-v8a", "android-arm", "arm64-v8a", null)]
		[DataRow ("armeabi-v7a", "armeabi-v7a,armeabi", "android-arm", "armeabi-v7a", "android-arm")]
		[DataRow ("x86_64", "x86_64,x86", "android-x86", "x86", "android-x86")]
		[DataRow ("x86_64", "x86_64,x86", "android-x86;android-x64", "x86_64", "android-x64")]
		[DataRow ("x86_64", "x86_64,arm64-v8a,armeabi-v7a", "android-arm;android-arm64", "arm64-v8a", "android-arm64")]
		[DataRow ("arm64-v8a", "armeabi-v7a,arm64-v8a", "android-arm;android-arm64", "arm64-v8a", "android-arm64")]
		[DataRow ("arm64-v8a", "arm64-v8a, armeabi-v7a ", "android-arm", "armeabi-v7a", "android-arm")]
		[DataRow ("arm64-v8a", "", "android-arm64", "arm64-v8a", "android-arm64")]
		[DataRow ("arm64-v8a", "arm64-v8a,armeabi-v7a", null, "arm64-v8a", null)]
		[DataRow ("arm64-v8a", "arm64-v8a,armeabi-v7a", "", "arm64-v8a", null)]
		[DataRow (null, "", "invalid", null, null)]
		public void SelectRuntimeIdentifier (string deviceAbi, string supportedAbis, string runtimeIdentifiers, string expectedAbi, string expectedRid)
		{
			var task = new GetPrimaryCpuAbi {
				BuildEngine = new MockBuildEngine (Console.Out),
				ResultingAbi = deviceAbi,
				RuntimeIdentifiers = runtimeIdentifiers?.Split (';'),
			};

			task.SelectRuntimeIdentifier (supportedAbis.Split (','));

			Assert.AreEqual (expectedAbi, task.ResultingAbi);
			Assert.AreEqual (expectedRid, task.RuntimeIdentifier);
		}

		[TestMethod]
		[DataRow (null, "arm64-v8a", "armeabi-v7a", "arm64-v8a,armeabi-v7a")]
		[DataRow ("", "arm64-v8a", "armeabi-v7a", "arm64-v8a,armeabi-v7a")]
		[DataRow (" , ", "arm64-v8a", "armeabi-v7a", "arm64-v8a,armeabi-v7a")]
		[DataRow ("", null, "armeabi-v7a", "armeabi-v7a")]
		[DataRow ("", "arm64-v8a", null, "arm64-v8a")]
		[DataRow ("", null, null, "")]
		[DataRow ("", " ", "", "")]
		[DataRow ("arm64-v8a", "arm64-v8a", "armeabi-v7a", "arm64-v8a")]
		public void GetSupportedAbis (string reportedAbis, string primaryAbi, string secondaryAbi, string expectedAbis)
		{
			var supportedAbis = GetPrimaryCpuAbi.GetSupportedAbis (reportedAbis?.Split (',') ?? [], primaryAbi, secondaryAbi);

			CollectionAssert.AreEqual (expectedAbis.Split (',', StringSplitOptions.RemoveEmptyEntries), supportedAbis);
		}

		[TestMethod]
		[DataRow ("arm64-v8a", "android-arm", "armeabi-v7a")]
		[DataRow ("arm64-v8a", "android-arm64", "arm64-v8a")]
		[DataRow (null, "android-arm", "armeabi-v7a")]
		[DataRow ("", "android-arm64", "arm64-v8a")]
		public void SelectRuntimeIdentifierFromDeviceCache (string deviceAbi, string runtimeIdentifier, string expectedAbi)
		{
			var doc = DeviceCache.Update (null, "device", deviceAbi, 36, "model:TestDevice", ["arm64-v8a", "armeabi-v7a"]);
			doc = XDocument.Parse (doc.ToString ());
			Assert.IsTrue (DeviceCache.TryGet (doc, "device", "model:TestDevice", out var abi, out var sdkVersion, out var supportedAbis));
			Assert.AreEqual (deviceAbi, abi);
			Assert.AreEqual (36, sdkVersion);
			CollectionAssert.AreEqual (new [] { "arm64-v8a", "armeabi-v7a" }, supportedAbis);

			var task = new GetPrimaryCpuAbi {
				BuildEngine = new MockBuildEngine (Console.Out),
				ResultingAbi = abi,
				RuntimeIdentifiers = [runtimeIdentifier],
			};
			task.SelectRuntimeIdentifier (supportedAbis);

			Assert.AreEqual (expectedAbi, task.ResultingAbi);
			Assert.AreEqual (runtimeIdentifier, task.RuntimeIdentifier);
			Assert.AreEqual (deviceAbi, doc.Root?.Element ("Device")?.Element ("ResultingAbi")?.Value,
				"The cache must retain the device ABI, not the app ABI.");
		}

		[TestMethod]
		[DataRow (null, "")]
		[DataRow ("", " , ")]
		public void DeviceCacheWithoutAnyAbiIsRefreshed (string deviceAbi, string supportedAbis)
		{
			var doc = DeviceCache.Update (null, "device", deviceAbi, 36, "model:TestDevice", supportedAbis.Split (','));

			Assert.IsFalse (DeviceCache.TryGet (doc, "device", "model:TestDevice", out _, out _, out _));
		}

		[TestMethod]
		public void DeviceCacheWithoutSupportedAbisIsRefreshed ()
		{
			var doc = XDocument.Parse (
				"""
				<Devices>
				  <Device id="device">
				    <ResultingAbi>arm64-v8a</ResultingAbi>
				    <SdkVersion>36</SdkVersion>
				    <LongOutput>model:TestDevice</LongOutput>
				  </Device>
				</Devices>
				""");

			Assert.IsFalse (DeviceCache.TryGet (doc, "device", "model:TestDevice", out _, out _, out _));
		}

		[TestMethod]
		public void DeviceCacheWithNoAdditionalAbisIsValid ()
		{
			var doc = DeviceCache.Update (null, "device", "armeabi-v7a", 19, "model:TestDevice", []);

			Assert.IsTrue (DeviceCache.TryGet (doc, "device", "model:TestDevice", out var abi, out _, out var supportedAbis));
			Assert.AreEqual ("armeabi-v7a", abi);
			Assert.IsEmpty (supportedAbis);
		}

		[TestMethod]
		public void GetPrimaryCpuAbiHonorsAdbTargetArchitecture ()
		{
			var messages = new List<BuildMessageEventArgs> ();
			var task = new GetPrimaryCpuAbi {
				BuildEngine = new MockBuildEngine (Console.Out, messages: messages),
				AdbTargetArchitecture = "armeabi-v7a",
				RuntimeIdentifiers = ["android-arm64", "android-arm"],
			};

			Assert.IsTrue (task.Execute ());
			Assert.AreEqual ("armeabi-v7a", task.ResultingAbi);
			Assert.AreEqual ("android-arm", task.RuntimeIdentifier);
			Assert.IsTrue (messages.Any (m => m.Message == "Using $(AdbTargetArchitecture): armeabi-v7a"));
			Assert.IsFalse (messages.Any (m => m.Message.StartsWith ("  ResultingAbi:", StringComparison.Ordinal) ||
				m.Message.StartsWith ("  RuntimeIdentifier:", StringComparison.Ordinal) ||
				m.Message.StartsWith ("  SdkVersion:", StringComparison.Ordinal)));
		}

		[TestMethod]
		[DataRow (null, "physical")]
		[DataRow ("-s physical", "physical")]
		[DataRow ("-d", "physical")]
		[DataRow ("-e", "emulator-5554")]
		public void AndroidHelperSelectsDevice (string target, string expected)
		{
			var devices = new [] {
				new Xamarin.Android.Tools.AdbDeviceInfo { Serial = "physical", Type = Xamarin.Android.Tools.AdbDeviceType.Device },
				new Xamarin.Android.Tools.AdbDeviceInfo { Serial = "emulator-5554", Type = Xamarin.Android.Tools.AdbDeviceType.Emulator },
			};
			Assert.AreEqual (expected, AndroidHelper.SelectDevice (devices, target)?.Serial);
			Assert.IsNull (AndroidHelper.SelectDevice (devices, "-s missing"));
			Assert.IsNull (AndroidHelper.SelectDevice (devices, "invalid"));
		}

		[TestMethod]
		public void BuilderDoesNotWaitForInheritedRedirectedOutput ()
		{
			if (!IsWindows)
				Assert.Inconclusive ("This test reproduces a Windows child process inheriting redirected output.");

			var psi = new ProcessStartInfo (Environment.GetEnvironmentVariable ("ComSpec") ?? "cmd.exe",
				"/c start \"\" /b powershell -NoProfile -Command \"Start-Sleep -Seconds 8\"") {
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				CreateNoWindow = true,
			};
			using var process = new Process { StartInfo = psi };
			var outputDone = new TaskCompletionSource<bool> (TaskCreationOptions.RunContinuationsAsynchronously);
			var errorDone = new TaskCompletionSource<bool> (TaskCreationOptions.RunContinuationsAsynchronously);
			process.OutputDataReceived += (_, e) => {
				if (e.Data == null)
					outputDone.TrySetResult (true);
			};
			process.ErrorDataReceived += (_, e) => {
				if (e.Data == null)
					errorDone.TrySetResult (true);
			};
			Assert.IsTrue (process.Start ());
			process.BeginOutputReadLine ();
			process.BeginErrorReadLine ();
			Assert.IsTrue (process.WaitForExit (5000), "The parent process should exit promptly.");

			var stopwatch = Stopwatch.StartNew ();
			Assert.IsFalse (Builder.WaitForRedirectedOutput (outputDone.Task, errorDone.Task));
			Assert.IsLessThan (TimeSpan.FromSeconds (6), stopwatch.Elapsed, "Inherited output handles must not hold the test runner indefinitely.");
			Assert.IsTrue (outputDone.Task.Wait (TimeSpan.FromSeconds (10)));
			Assert.IsTrue (errorDone.Task.Wait (TimeSpan.FromSeconds (10)));
		}

	}

}
