using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using NUnit.Framework;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	public class GetAndroidInstrumentationNameTests : BaseTest
	{
		string temp;
		string manifest;

		[SetUp]
		public void Setup ()
		{
			string tempDirectoryName = Path.Combine ("temp", TestName);
			temp = Path.Combine (Root, tempDirectoryName);
			Directory.CreateDirectory (temp);
			manifest = Path.Combine (temp, "AndroidManifest.xml");

			var references = CreateFauxReferencesDirectory (Path.Combine (tempDirectoryName, "references"), new [] {
				new ApiInfo { Id = "28", Level = 28, Name = "Pie", FrameworkVersion = "v9.0", Stable = true },
			});
			MonoAndroidHelper.RefreshSupportedVersions (new [] {
				Path.Combine (references, "MonoAndroid"),
			});
		}

		[TearDown]
		public void TearDown ()
		{
			Directory.Delete (temp, recursive: true);
		}

		void WriteManifest (string body)
		{
			File.WriteAllText (manifest,
				$"""
				<manifest xmlns:android="http://schemas.android.com/apk/res/android" package="com.mycompanyname.foo">
					<application />
				{body}
				</manifest>
				""");
		}

		GetAndroidInstrumentationName CreateTask (List<BuildErrorEventArgs> errors, bool noLaunchableActivity = false) =>
			new GetAndroidInstrumentationName {
				BuildEngine = new MockBuildEngine (TestContext.Out, errors),
				ManifestFile = manifest,
				NoLaunchableActivity = noLaunchableActivity,
			};

		[Test]
		public void InstrumentationFound ()
		{
			WriteManifest ("""	<instrumentation android:name="com.mycompanyname.foo.MyInstrumentation" android:targetPackage="com.mycompanyname.foo" />""");

			var errors = new List<BuildErrorEventArgs> ();
			var task = CreateTask (errors);
			Assert.IsTrue (task.Execute (), "Execute() should have succeeded.");
			Assert.AreEqual (0, errors.Count, "There should be no errors.");
			Assert.AreEqual ("com.mycompanyname.foo.MyInstrumentation", task.InstrumentationName);
		}

		[Test]
		public void MissingInstrumentationIsXA1048 ()
		{
			// `$(AndroidUseInstrumentation)` opted in, but there is nothing to run.
			WriteManifest ("");

			var errors = new List<BuildErrorEventArgs> ();
			var task = CreateTask (errors);
			Assert.IsFalse (task.Execute (), "Execute() should have failed.");
			Assert.AreEqual (1, errors.Count, "There should be one error.");
			Assert.AreEqual ("XA1048", errors [0].Code);
			Assert.IsNull (task.InstrumentationName);
		}

		[Test]
		public void MissingInstrumentationAndActivityIsXA1043 ()
		{
			// The fallback call site: no launchable `<activity/>` was found either.
			WriteManifest ("");

			var errors = new List<BuildErrorEventArgs> ();
			var task = CreateTask (errors, noLaunchableActivity: true);
			Assert.IsFalse (task.Execute (), "Execute() should have failed.");
			Assert.AreEqual (1, errors.Count, "There should be one error.");
			Assert.AreEqual ("XA1043", errors [0].Code);
			Assert.IsNull (task.InstrumentationName);
		}

		[Test]
		public void MissingAndroidNameIsXA1042 ()
		{
			WriteManifest ("""	<instrumentation android:targetPackage="com.mycompanyname.foo" />""");

			var errors = new List<BuildErrorEventArgs> ();
			var task = CreateTask (errors);
			Assert.IsFalse (task.Execute (), "Execute() should have failed.");
			Assert.AreEqual (1, errors.Count, "There should be one error.");
			Assert.AreEqual ("XA1042", errors [0].Code);
		}
	}
}
