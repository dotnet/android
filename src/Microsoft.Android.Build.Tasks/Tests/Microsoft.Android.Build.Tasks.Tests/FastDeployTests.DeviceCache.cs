using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using NUnit.Framework;
using Microsoft.Android.Tasks;
using Xamarin.Android.Tasks;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Build.Tests
{
	public partial class FastDeployTests
	{
		// Xamarin.Android.Build.Debugging.Tasks.dll (GetPrimaryCpuAbi) caches the selected device under this key.
		static object DeviceCacheKey (string target, string adbPath) =>
			Tuple.Create ("AndroidHelper_AndroidDevice", target, adbPath);

		[Test]
		public void FastDeployReusesDeviceSelectedByDebuggingTasks ()
		{
			string adbToolPath = Path.Combine (Path.GetTempPath (), "missing-" + Guid.NewGuid ().ToString ("N"));
			IBuildEngine4 engine = new MockBuildEngine (TestContext.Out);
			engine.RegisterTaskObject (DeviceCacheKey ("-s emulator-5554", Path.Combine (adbToolPath, "adb")), new object [] {
				"emulator-5554", "sdk_gphone64_arm64", (int) AdbDeviceType.Emulator, (int) AdbDeviceStatus.Online, null,
				"sdk_gphone64_arm64", "sdk_gphone64_arm64", "emu64a", "1", "emulator-5554 device transport_id:1",
			}, RegisteredTaskObjectLifetime.Build, false);
			var messages = new List<string> ();
			var errors = new List<string> ();

			var device = AndroidHelper.ParseTarget ("-s emulator-5554", messages.Add, (code, message) => errors.Add (message),
				logErrors: true, engine4: engine, adbToolPath: adbToolPath, adbToolExe: "adb");

			Assert.IsEmpty (errors, "adb must not run when another task assembly already selected the device.");
			CollectionAssert.Contains (messages, "Using cached value from RegisterTaskObject");
			Assert.IsNotNull (device);
			Assert.AreEqual ("emulator-5554", device.Serial);
			Assert.AreEqual ("sdk_gphone64_arm64", device.Description);
			Assert.IsTrue (device.IsEmulator);
			Assert.AreEqual (AdbDeviceStatus.Online, device.Status);
			Assert.IsNull (device.AvdName);
			Assert.AreEqual ("sdk_gphone64_arm64", device.Model);
			Assert.AreEqual ("sdk_gphone64_arm64", device.Product);
			Assert.AreEqual ("emu64a", device.Device);
			Assert.AreEqual ("1", device.TransportId);
			Assert.AreEqual ("emulator-5554 device transport_id:1", device.LongOutput);
		}

		[Test]
		public void FastDeployReusesMissingDeviceSelectedByDebuggingTasks ()
		{
			string adbToolPath = Path.Combine (Path.GetTempPath (), "missing-" + Guid.NewGuid ().ToString ("N"));
			IBuildEngine4 engine = new MockBuildEngine (TestContext.Out);
			engine.RegisterTaskObject (DeviceCacheKey ("", Path.Combine (adbToolPath, "adb")), new object [0], RegisteredTaskObjectLifetime.Build, false);
			var errors = new List<string> ();

			var device = AndroidHelper.ParseTarget ("", _ => { }, (code, message) => errors.Add ($"{code}: {message}"),
				logErrors: true, engine4: engine, adbToolPath: adbToolPath, adbToolExe: "adb");

			Assert.IsNull (device);
			CollectionAssert.AreEqual (new [] { $"XA0010: {Xamarin.Android.Build.Debugging.Tasks.Properties.Resources.XA0010_NoDevice}" }, errors);
		}
	}
}
