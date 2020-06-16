﻿using Microsoft.Build.Framework;
using NUnit.Framework;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Xamarin.Android.Tasks;
using Xamarin.Android.Tools;
using Xamarin.ProjectTools;
using System.Linq;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	[Category ("Node-2")]
	public class KeyToolTests : BaseTest
	{
		List<BuildErrorEventArgs> errors;
		List<BuildWarningEventArgs> warnings;
		List<BuildMessageEventArgs> messages;
		MockBuildEngine engine;
		string temp;
		string keyToolPath;

		[SetUp]
		public void Setup()
		{
			engine = new MockBuildEngine (TestContext.Out, errors = new List<BuildErrorEventArgs> (), warnings = new List<BuildWarningEventArgs> (), messages = new List<BuildMessageEventArgs> ());
			temp = Path.GetTempFileName ();

			var androidSdk = new AndroidSdkInfo ((level, message) => {
				if (level == TraceLevel.Error)
					Assert.Fail (message);
			}, AndroidSdkPath, AndroidNdkPath);
			keyToolPath = Path.Combine (androidSdk.JavaSdkPath, "bin");
		}

		[TearDown]
		public void TearDown ()
		{
			File.Delete (temp);
		}

		void GetValidKeyStore ()
		{
			using (var keyStream = typeof (XamarinAndroidCommonProject).Assembly.GetManifestResourceStream ("Xamarin.ProjectTools.Resources.Base.test.keystore"))
			using (var fileStream = File.Create (temp)) {
				keyStream.CopyTo (fileStream);
			}
		}

		[Test]
		public void ListEmptyKey ()
		{
			var task = new KeyTool {
				BuildEngine = engine,
				KeyStore = temp,
				StorePass = "android",
				KeyAlias = "mykey",
				KeyPass = "android",
				Command = "-list",
				ToolPath = keyToolPath,
			};
			Assert.IsFalse (task.Execute (), "Task should have failed.");
			Assert.AreEqual (1, errors.Count, "Task should have one error.");
			Assert.AreEqual (0, warnings.Count);
			var error = errors [0];
			Assert.AreEqual ($"keytool error: java.lang.Exception: Keystore file exists, but is empty: {temp}", error.Message);
			Assert.AreEqual ("ANDKT0000", error.Code);
		}

		[Test]
		public void ListInvalidAlias ()
		{
			GetValidKeyStore ();

			var task = new KeyTool {
				BuildEngine = engine,
				KeyStore = temp,
				StorePass = "android",
				KeyAlias = "asdf",
				KeyPass = "android",
				Command = "-list",
				ToolPath = keyToolPath,
			};
			Assert.IsFalse (task.Execute (), "Task should have failed.");
			Assert.AreEqual (1, errors.Count, "Task should have one error.");
			Assert.AreEqual (0, warnings.Count, "Task should have no warnings.");
			var error = errors [0];
			Assert.AreEqual ($"keytool error: java.lang.Exception: Alias <{task.KeyAlias}> does not exist", error.Message);
			Assert.AreEqual ("ANDKT0000", error.Code);
		}

		[Test]
		public void ListSuccessWithPassword ()
		{
			GetValidKeyStore ();

			var task = new KeyTool {
				BuildEngine = engine,
				KeyStore = temp,
				StorePass = "android",
				KeyAlias = "mykey",
				KeyPass = "android",
				Command = "-list",
				ToolPath = keyToolPath,
			};
			Assert.IsTrue (task.Execute (), "Task should have succeeded.");
			Assert.AreEqual (0, errors.Count, "Task should have no errors.");
			Assert.AreEqual (0, warnings.Count, "Task should have no warnings.");
		}

		[Test]
		public void ListInvalidPassword ()
		{
			GetValidKeyStore ();

			var task = new KeyTool {
				BuildEngine = engine,
				KeyStore = temp,
				StorePass = "asdf",
				KeyAlias = "mykey",
				KeyPass = "asdf",
				Command = "-list",
				ToolPath = keyToolPath,
			};
			Assert.IsFalse (task.Execute (), "Task should have failed.");
			Assert.AreEqual (1, errors.Count, "Task should have one error.");
			Assert.AreEqual (0, warnings.Count, "Task should have no warnings.");
			var error = errors [0];
			Assert.AreEqual ("keytool error: java.io.IOException: Keystore was tampered with, or password was incorrect", error.Message);
			Assert.AreEqual ("ANDKT0000", error.Code);
		}

		[Test]
		public void CreateDebugKeyStore ()
		{
			string keyfile = Path.Combine (TestName, "debug.keystore");
			if (File.Exists (keyfile))
				File.Delete (keyfile);
			var task = new AndroidCreateDebugKey {
				BuildEngine = engine,
				KeyStore = keyfile,
				StorePass = "android",
				KeyAlias = "androiddebugkey",
				KeyPass = "android",
				KeyAlgorithm="RSA",
				Validity=10000,
				StoreType="pkcs12",
				Command="-genkeypair",
				ToolPath = keyToolPath,
			};
			Assert.IsTrue (task.Execute (), "Task should have succeeded.");
			Assert.AreEqual (0, errors.Count, "Task should have no errors.");
			Assert.AreEqual (0, warnings.Count, "Task should have no warnings.");

			var keyToolTask = new KeyTool {
				BuildEngine = engine,
				KeyStore = keyfile,
				StorePass = "android",
				KeyAlias = "androiddebugkey",
				KeyPass = "android",
				Command = "-list",
				ToolPath = keyToolPath,
			};

			Assert.IsTrue (keyToolTask.Execute (), "Task should have succeeded.");
		}

		[Test]
		public void CreateDebugKeyStoreWithStrongPassword ()
		{
			string keyfile = Path.Combine (TestName, "debug.keystore");
			string pass = "Cy(nBW~j.&@B-!R_aq7^/syzFR!S$4]7R%i6)R!";
			if (File.Exists (keyfile))
				File.Delete (keyfile);
			var task = new AndroidCreateDebugKey {
				BuildEngine = engine,
				KeyStore = keyfile,
				StorePass = pass,
				KeyAlias = "teststringkey",
				KeyPass = pass,
				KeyAlgorithm="RSA",
				Validity=10000,
				StoreType="pkcs12",
				Command="-genkeypair",
				ToolPath = keyToolPath,
			};
			Assert.IsTrue (task.Execute (), "Task should have succeeded.");
			Assert.AreEqual (0, errors.Count, "Task should have no errors.");
			Assert.AreEqual (0, warnings.Count, "Task should have no warnings.");
			Assert.AreEqual (0, task.ExitCode, "ExitCode should have been 0");

			messages.Clear ();
			var keyToolTask = new KeyTool {
				BuildEngine = engine,
				KeyStore = keyfile,
				StorePass = pass,
				KeyAlias = "teststringkey",
				KeyPass = pass,
				Command = "-list",
				ToolPath = keyToolPath,
			};

			Assert.IsTrue (keyToolTask.Execute (), "Task should have succeeded.");
			Assert.AreEqual (0, errors.Count, "Task should have no errors.");
			Assert.AreEqual (0, warnings.Count, "Task should have no warnings.");
			Assert.AreEqual (0, task.ExitCode, "ExitCode should have been 0");
			string output = string.Join (" ", messages.Select (x => x.Message));
			Assert.IsTrue (output.Contains ("Certificate fingerprint (SHA"), "Certificate SHA1 or SHA-256 should have been printed.");
		}
	}
}
