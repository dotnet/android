using Microsoft.Build.Framework;
using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests {

	[TestFixture]
	[Category ("Node-2")]
	public class MakeBundleNativeCodeExternalTests : BaseTest {
		List<BuildErrorEventArgs> errors;
		List<BuildWarningEventArgs> warnings;
		List<BuildMessageEventArgs> messages;
		MockBuildEngine engine;
		string path;

		[SetUp]
		public void Setup ()
		{
			engine = new MockBuildEngine (TestContext.Out,
				errors: errors = new List<BuildErrorEventArgs> (),
				warnings: warnings = new List<BuildWarningEventArgs> (),
				messages: messages = new List<BuildMessageEventArgs> ());

			path = Path.Combine (Root, "temp", TestName);
			TestOutputDirectories [TestContext.CurrentContext.Test.ID] = path;
		}

		[TestCase (null)]
		[TestCase ("")]
		[TestCase ("DoesNotExist")]
		public void XA5104AndroidNdkNotFound (string androidNdkDirectory)
		{
			var task1 = new MakeBundleNativeCodeExternal {
				BuildEngine = engine,
				AndroidNdkDirectory = androidNdkDirectory,
				Assemblies = new ITaskItem [0],
				SupportedAbis = new [] { "armeabi-v7a" },
				TempOutputPath = path,
				ToolPath = "",
				BundleApiPath = ""
			};

			Assert.IsFalse (task1.Execute (), "Task should fail!");
			BuildErrorEventArgs error1 = errors [0];
			Assert.AreEqual ("XA5104", error1.Code);
			StringAssert.Contains (" NDK ", error1.Message);
			StringAssert.Contains ("AndroidNdkDirectory", error1.Message);
			StringAssert.Contains ("SDK Manager", error1.Message);

			var task2 = new Aot {
				BuildEngine = engine,
				AndroidNdkDirectory = androidNdkDirectory,
				AndroidAotMode = "normal",
				AndroidApiLevel = "28",
				EnableLLVM = true,
				ResolvedAssemblies = new ITaskItem [0],
				SupportedAbis = new [] { "armeabi-v7a" },
				AotOutputDirectory = path,
				IntermediateAssemblyDir = path
			};

			Assert.IsFalse (task2.Execute (), "Task should fail!");
			BuildErrorEventArgs error2 = errors [1];
			Assert.AreEqual (error1.Message, error2.Message, "Aot and MakeBundleNativeCodeExternal should produce the same error messages.");
		}
	}
}
