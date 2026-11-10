using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using NUnit.Framework;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests
{
	public partial class FastDeployTests
	{
		// Xamarin.Android.Build.Debugging.Tasks.dll (GetPrimaryCpuAbi) publishes the selected serial under this key.
		static object SharedDeviceKey (string target, string adbPath) =>
			("AndroidHelper_AndroidDeviceSerial", (object) Tuple.Create ("AndroidHelper_AndroidDevice", target, adbPath));

		[Test]
		public void FastDeployDoesNotExportAndroidHelper ()
		{
			Assert.IsFalse (typeof (AndroidHelper).IsPublic);
		}

		[Test]
		public void FastDeployReusesDeviceSelectedByDebuggingTasks ()
		{
			string adbToolPath = Path.Combine (Path.GetTempPath (), "missing-" + Guid.NewGuid ().ToString ("N"));
			IBuildEngine4 engine = new MockBuildEngine (TestContext.Out);
			engine.RegisterTaskObject (SharedDeviceKey ("-s emulator-5554", Path.Combine (adbToolPath, "adb")), "emulator-5554", RegisteredTaskObjectLifetime.Build, false);
			var messages = new List<string> ();
			var errors = new List<string> ();

			var device = AndroidHelper.ParseTarget ("-s emulator-5554", messages.Add, (code, message) => errors.Add (message),
				logErrors: true, engine4: engine, adbToolPath: adbToolPath, adbToolExe: "adb");

			Assert.AreEqual ("emulator-5554", device?.Serial);
			Assert.IsEmpty (errors, "adb must not run when another task assembly already selected the device.");
			CollectionAssert.Contains (messages, "Using cached value from RegisterTaskObject");
		}

		[Test]
		public void FastDeployReusesMissingDeviceSelectedByDebuggingTasks ()
		{
			string adbToolPath = Path.Combine (Path.GetTempPath (), "missing-" + Guid.NewGuid ().ToString ("N"));
			IBuildEngine4 engine = new MockBuildEngine (TestContext.Out);
			engine.RegisterTaskObject (SharedDeviceKey ("", Path.Combine (adbToolPath, "adb")), "", RegisteredTaskObjectLifetime.Build, false);
			var errors = new List<string> ();

			var device = AndroidHelper.ParseTarget ("", _ => { }, (code, message) => errors.Add ($"{code}: {message}"),
				logErrors: true, engine4: engine, adbToolPath: adbToolPath, adbToolExe: "adb");

			Assert.IsNull (device);
			CollectionAssert.AreEqual (new [] { $"XA0010: {Xamarin.Android.Build.Debugging.Tasks.Properties.Resources.XA0010_NoDevice}" }, errors);
		}
	}
}
