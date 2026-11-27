using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using NUnit.Framework;
using Microsoft.Android.Tasks;
using Xamarin.Android.Tasks;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Build.Tests
{
	public partial class FastDeployTests
	{
		[Test]
		public void FastDeployReusesDeviceSelectedByDebuggingTasks ()
		{
			string adbToolPath = Path.Combine (Path.GetTempPath (), "missing-" + Guid.NewGuid ().ToString ("N"));
			IBuildEngine4 engine = new MockBuildEngine (TestContext.Out);
			engine.RegisterTaskObject (AndroidHelper.GetDeviceCacheKey ("-s emulator-5554", adbToolPath, "adb", TaskEnvironment.Fallback), new object [] {
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
			var errors = new List<BuildErrorEventArgs> ();
			var messages = new List<BuildMessageEventArgs> ();
			var engine = new MockBuildEngine (TestContext.Out, errors, messages: messages);
			// adb does not exist, so this caches "no device".
			((IBuildEngine4) engine).RegisterTaskObject (AndroidHelper.GetDeviceCacheKey ("", adbToolPath, "adb", TaskEnvironment.Fallback), new object [0], RegisteredTaskObjectLifetime.Build, false);

			var task = new FastDeploy {
				BuildEngine = engine,
				AdbToolPath = adbToolPath,
				AdbToolExe = "adb",
			};

			Assert.IsFalse (task.Execute ());
			Assert.IsTrue (messages.Any (m => m.Message == "Using cached value from RegisterTaskObject"));
			CollectionAssert.AreEqual (new [] { Xamarin.Android.Build.Debugging.Tasks.Properties.Resources.XA0010_NoDevice },
				errors.Select (e => e.Message));
		}
	}
}
