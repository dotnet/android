using System;
using System.IO;
using NUnit.Framework;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	public class ValidateJavaVersionTests : BaseTest
	{
		string path;

		[SetUp]
		public void Setup ()
		{
			path = Path.Combine ("temp", TestName);
			Directory.CreateDirectory (Path.Combine (Root, path));
		}

		[TearDown]
		public void TearDown ()
		{
			Directory.Delete (Path.Combine (Root, path), recursive: true);
		}

		[TestCase (false)]
		[TestCase (true)]
		public void ToolingTargetRejectsOldJavaForModernAndroidApi (bool useJavaExeVersion)
		{
			var javaPath = CreateFauxJavaSdkDirectory (Path.Combine (path, "jdk"), "17.0.1", out string javaExe, out string javacExe);
			var proj = new XamarinAndroidApplicationProject ();
			proj.SetProperty ("TargetPlatformVersion", "37.0");
			proj.SetProperty ("MinimumSupportedJavaVersion", "21.0");
			proj.SetProperty ("LatestSupportedJavaVersion", "25.0.99");
			proj.SetProperty ("_JavaSdkDirectory", javaPath);
			proj.SetProperty ("JavaToolExe", Path.Combine (javaPath, "bin", javaExe));
			proj.SetProperty ("JavacToolExe", javacExe);
			proj.SetProperty ("_AndroidUseJavaExeVersion", useJavaExeVersion.ToString ());
			proj.SetProperty ("_AndroidAllowMissingSdkTooling", "false");

			using var builder = CreateApkBuilder (Path.Combine (path, "App"));
			builder.Target = "_ResolveAndroidTooling";
			builder.ThrowOnBuildFailure = false;
			Assert.IsFalse (builder.Build (proj), "The tooling target must reject JDK 17 even for Android API 31+.");
			StringAssertEx.Contains ("error XA0031: Java SDK 21.0 or above", builder.LastBuildOutput,
				string.Join (Environment.NewLine, builder.LastBuildOutput));
			StringAssertEx.Contains ("Found Java SDK version 17.0.1.", builder.LastBuildOutput);
		}
	}
}
