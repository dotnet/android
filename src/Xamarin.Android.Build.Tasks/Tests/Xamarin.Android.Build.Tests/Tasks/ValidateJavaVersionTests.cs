using Microsoft.Build.Framework;
using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	public class ValidateJavaVersionTests : BaseTest
	{
		string path;
		List<BuildErrorEventArgs> errors;
		List<BuildMessageEventArgs> messages;
		MockBuildEngine engine;

		[SetUp]
		public void Setup ()
		{
			path = Path.Combine ("temp", TestName);
			engine = new MockBuildEngine (TestContext.Out,
				errors: errors = new List<BuildErrorEventArgs> (),
				messages: messages = new List<BuildMessageEventArgs> ());

			//Setup statics on MonoAndroidHelper
			var referencePath = CreateFauxReferencesDirectory (Path.Combine (path, "references"), new [] {
				new ApiInfo { Id = "27", Level = 27, Name = "Oreo", FrameworkVersion = "v8.1",  Stable = true },
			});
			MonoAndroidHelper.RefreshSupportedVersions (new [] {
				Path.Combine (referencePath, "MonoAndroid"),
			});
		}

		[TearDown]
		public void TearDown ()
		{
			Directory.Delete (Path.Combine (Root, path), recursive: true);
		}

		[Test]
		public void TargetFramework8_1_Requires_1_8_0 ()
		{
			var javaPath = CreateFauxJavaSdkDirectory (Path.Combine (path, "jdk"), "1.7.0", out string javaExe, out string javacExe);
			var validateJavaVersion = new ValidateJavaVersion {
				BuildEngine = engine,
				TargetFrameworkVersion = "v8.1",
				JavaSdkPath = javaPath,
				JavaToolExe = javaExe,
				JavacToolExe = javacExe,
				LatestSupportedJavaVersion = "1.8.0",
				MinimumSupportedJavaVersion = "1.7.0",
			};
			Assert.False (validateJavaVersion.Execute (), "Execute should *not* succeed!");
			Assert.IsTrue (errors.Any (e => e.Message == $"Java SDK 1.8 or above is required when targeting FrameworkVersion {validateJavaVersion.TargetFrameworkVersion}."), "Should get error about TargetFrameworkVersion=v8.1");
		}

		[Test]
		public void BuildTools27_Requires_1_8_0 ()
		{
			var javaPath = CreateFauxJavaSdkDirectory (Path.Combine (path, "jdk"), "1.7.0", out string javaExe, out string javacExe);
			var validateJavaVersion = new ValidateJavaVersion {
				BuildEngine = engine,
				AndroidSdkBuildToolsVersion = "27.0.0",
				JavaSdkPath = javaPath,
				JavaToolExe = javaExe,
				JavacToolExe = javacExe,
				LatestSupportedJavaVersion = "1.8.0",
				MinimumSupportedJavaVersion = "1.7.0",
			};
			Assert.False (validateJavaVersion.Execute (), "Execute should *not* succeed!");
			Assert.IsTrue (errors.Any (e => e.Message == $"Java SDK 1.8 or above is required when using build-tools {validateJavaVersion.AndroidSdkBuildToolsVersion}."), "Should get error about build-tools=27.0.0");
		}

		[Test]
		public void CacheWorks ()
		{
			var javaPath = CreateFauxJavaSdkDirectory (Path.Combine (path, "jdk"), "1.8.0", out string javaExe, out string javacExe);
			var validateJavaVersion = new ValidateJavaVersion {
				BuildEngine = engine,
				JavaSdkPath = javaPath,
				JavaToolExe = javaExe,
				JavacToolExe = javacExe,
				LatestSupportedJavaVersion = "1.8.0",
				MinimumSupportedJavaVersion = "1.7.0",
			};
			Assert.IsTrue (validateJavaVersion.Execute (), "first Execute should succeed!");

			messages.Clear ();

			Assert.IsTrue (validateJavaVersion.Execute (), "second Execute should succeed!");
			var javaFullPath = Path.Combine (javaPath, "bin", javaExe);
			var javacFullPath = Path.Combine (javaPath, "bin", javacExe);
			Assert.IsTrue (messages.Any (m => m.Message == $"Using cached value for `{javaFullPath} -version`: 1.8.0"), "`java -version` should be cached!");
			Assert.IsTrue (messages.Any (m => m.Message == $"Using cached value for `{javacFullPath} -version`: 1.8.0"), "`javac -version` should be cached!");
		}

		[Test]
		public void CacheInvalidates ()
		{
			var javaPath = CreateFauxJavaSdkDirectory (Path.Combine (path, "jdk-1"), "1.8.0", out string javaExe, out string javacExe);
			var validateJavaVersion = new ValidateJavaVersion {
				BuildEngine = engine,
				JavaSdkPath = javaPath,
				JavaToolExe = javaExe,
				JavacToolExe = javacExe,
				LatestSupportedJavaVersion = "1.8.0",
				MinimumSupportedJavaVersion = "1.7.0",
			};
			Assert.IsTrue (validateJavaVersion.Execute (), "first Execute should succeed!");

			messages.Clear ();

			javaPath = CreateFauxJavaSdkDirectory (Path.Combine (path, "jdk-2"), "1.8.0", out javaExe, out javacExe);
			validateJavaVersion.JavaSdkPath = javaPath;

			Assert.IsTrue (validateJavaVersion.Execute (), "second Execute should succeed!");
			Assert.IsFalse (messages.Any (m => m.Message.StartsWith ("Using cached value for")), "`java -version` should *not* be cached!");
		}
	}
}
