using NUnit.Framework;
using System.Diagnostics;
using System.IO;

namespace Xamarin.Android.Tools.Tests
{
	[TestFixture]
	class MonoDroidSdkTests
	{
		[TestFixtureSetUp]
		public void FixtureSetUp()
		{
			AndroidLogger.Info += OnInfo;
			AndroidLogger.Warning += OnWarning;
			AndroidLogger.Error += OnError;
		}

		[TestFixtureTearDown]
		public void FixtureTearDown()
		{
			AndroidLogger.Info -= OnInfo;
			AndroidLogger.Warning -= OnWarning;
			AndroidLogger.Error -= OnError;
		}

		void OnInfo(string task, string message)
		{
			Debug.WriteLine(task + ": " + message);
		}

		void OnWarning(string task, string message)
		{
			Assert.Fail(task + ": " + message);
		}

		void OnError(string task, string message)
		{
			Assert.Fail(task + ": " + message);
		}

		[Test]
		public void RefreshWithoutParameters()
		{
			//Just checking for exceptions, or AndroidLogger
			MonoDroidSdk.Refresh();
		}

		[Test]
		public void BinPathExists()
		{
			string path = MonoDroidSdk.BinPath;
			Assert.IsTrue(Directory.Exists(path), path + " does not exist!"); 
		}

		[Test]
		public void FrameworkPathExists()
		{
			string path = MonoDroidSdk.FrameworkPath;
			Assert.IsTrue(Directory.Exists(path), path + " does not exist!");
		}

		[Test]
		public void RuntimePathExists()
		{
			string path = MonoDroidSdk.RuntimePath;
			Assert.IsTrue(Directory.Exists(path), path + " does not exist!");
		}
	}
}
