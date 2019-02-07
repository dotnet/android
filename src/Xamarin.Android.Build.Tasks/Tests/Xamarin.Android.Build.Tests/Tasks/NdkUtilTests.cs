using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Xamarin.Android.Tasks;
using Xamarin.Android.Tools;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests.Tasks {

	[TestFixture]
	public class NdkUtilTests : BaseTest {

		List<BuildErrorEventArgs> errors;
		List<BuildWarningEventArgs> warnings;
		MockBuildEngine engine;

		[SetUp]
		public void Setup ()
		{
			engine = new MockBuildEngine (TestContext.Out, errors = new List<BuildErrorEventArgs> (), warnings = new List<BuildWarningEventArgs> ());
		}

		[Test]
		public void TestNdkUtil ()
		{
			var log = new TaskLoggingHelper (engine, TestName);
			using (var builder = new Builder ()) {
				builder.ResolveSdks ();
				var ndkDir = builder.AndroidNdkDirectory;
				var sdkDir = builder.AndroidSdkDirectory;
				MonoAndroidHelper.AndroidSdk = new AndroidSdkInfo ((arg1, arg2) => { }, sdkDir, ndkDir);
				NdkUtil.Init (log, ndkDir);
				var platforms = NdkUtil.GetSupportedPlatforms (ndkDir);
				Assert.AreNotEqual (0, platforms.Count (), "No platforms found");
				var arch = AndroidTargetArch.X86;
				Assert.IsTrue (NdkUtil.ValidateNdkPlatform (log, ndkDir, arch, enableLLVM: false));
				Assert.AreEqual (0, errors.Count, "NdkUtil.ValidateNdkPlatform should not have returned false.");
				int level = NdkUtil.GetMinimumApiLevelFor (arch, ndkDir);
				int expected = 16;
				Assert.AreEqual (expected, level, $"Min Api Level for {arch} should be {expected}.");
				var compilerNoQuotes = NdkUtil.GetNdkTool (ndkDir, arch, "gcc", level);
				Assert.AreEqual (0, errors.Count, "NdkUtil.GetNdkTool should not have errored.");
				Assert.NotNull (compilerNoQuotes, "NdkUtil.GetNdkTool returned null.");
				var gas = NdkUtil.GetNdkTool (ndkDir, arch, "as", level);
				Assert.AreEqual (0, errors.Count, "NdkUtil.GetNdkTool should not have errored.");
				Assert.NotNull (gas, "NdkUtil.GetNdkTool returned null.");
				var inc = NdkUtil.GetNdkPlatformIncludePath (ndkDir, arch, level);
				Assert.NotNull (inc, " NdkUtil.GetNdkPlatformIncludePath should not return null");
				var libPath = NdkUtil.GetNdkPlatformLibPath (ndkDir, arch, level);
				Assert.NotNull (libPath, "NdkUtil.GetNdkPlatformLibPath  should not return null");
				string ld = NdkUtil.GetNdkTool (ndkDir, arch, "ld", level);
				Assert.AreEqual (0, errors.Count, "NdkUtil.GetNdkTool should not have errored.");
				Assert.NotNull (ld, "NdkUtil.GetNdkTool returned null.");
			}
		}
	}
}
