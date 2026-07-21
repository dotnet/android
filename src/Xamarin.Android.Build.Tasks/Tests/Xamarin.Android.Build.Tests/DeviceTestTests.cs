using System;
using System.IO;
using NUnit.Framework;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	public class DeviceTestTests
	{
		[Test]
		public void TryMatchLogcatOutputMatchesBufferedLine ()
		{
			var output = string.Join (Environment.NewLine, new [] {
				"ActivityManager: Displayed com.example/.MainActivity",
				"I/mono-stdout(11111): #TEST#",
			});

			using (var logcat = new StringWriter ()) {
				bool result = DeviceTest.TryMatchLogcatOutput (output, logcat, line => line.Contains ("#TEST#"));

				Assert.IsTrue (result, "Expected buffered logcat output to contain a matching line.");
				StringAssert.Contains ("ActivityManager: Displayed com.example/.MainActivity", logcat.ToString ());
				StringAssert.Contains ("I/mono-stdout(11111): #TEST#", logcat.ToString ());
			}
		}

		[Test]
		public void TryMatchLogcatOutputKeepsSearchingUntilFirstMatch ()
		{
			var output = string.Join (Environment.NewLine, new [] {
				"line-1",
				"line-2",
				"line-3",
			});
			int invocations = 0;

			using (var logcat = new StringWriter ()) {
				bool result = DeviceTest.TryMatchLogcatOutput (output, logcat, line => {
					invocations++;
					return line == "line-2";
				});

				Assert.IsTrue (result, "Expected a match in buffered logcat output.");
				Assert.AreEqual (2, invocations, "Line matcher should stop after the first match.");
			}
		}
	}
}
