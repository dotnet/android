using Microsoft.Build.Framework;
using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	public class CopyResourceTests
	{
		static readonly Assembly ExecutingAssembly = typeof (CopyResource).Assembly;
		string tempFile;
		List<BuildErrorEventArgs> errors;
		List<BuildMessageEventArgs> messages;
		MockBuildEngine engine;

		[SetUp]
		public void Setup ()
		{
			tempFile = Path.GetTempFileName ();
			engine = new MockBuildEngine (TestContext.Out,
				errors: errors = new List<BuildErrorEventArgs> (),
				messages: messages = new List<BuildMessageEventArgs> ());
		}

		[TearDown]
		public void TearDown ()
		{
			File.Delete (tempFile);
		}

		// If we remove one of these, this test should fail
		static object [] EmbeddedResources = new object [] {
			new object[] { "machine.config" },
			new object[] { "MonoRuntimeProvider.Bundled.java" },
		};

		/// <summary>
		/// Verifies that net.android.init.gradle.kts uses Kotlin's safe call pattern
		/// to handle nullable String? return from projectProperties map access.
		/// This is required for compatibility with Gradle 9.x which has stricter
		/// Kotlin type checking where file() requires non-nullable Any parameter.
		/// See: https://github.com/dotnet/android/issues/9818
		/// </summary>
		[Test]
		public void NetAndroidInitGradleKts_UsesNullSafePattern ()
		{
			const string resourceName = "net.android.init.gradle.kts";
			using (var stream = ExecutingAssembly.GetManifestResourceStream (resourceName))
			using (var reader = new StreamReader (stream)) {
				var content = reader.ReadToEnd ();
				// The script should use ?.let {} pattern for null-safe access
				// This ensures compatibility with Gradle 9.x which rejects nullable types passed to file()
				StringAssert.Contains ("?.let {", content, "Script should use Kotlin's safe call pattern (?.let) for Gradle 9.x compatibility");
				// The script should NOT pass nullable directly to file() like: file(gradle.startParameter.projectProperties["..."])
				StringAssert.DoesNotContain ("file(gradle.startParameter.projectProperties[", content,
					"Script should not pass nullable projectProperties value directly to file() as this fails on Gradle 9.x");
			}
		}

		[Test]
		[TestCaseSource (nameof (EmbeddedResources))]
		public void FilesThatAreExpected (string resourceName)
		{
			var task = new CopyResource {
				BuildEngine = engine,
				ResourceName = resourceName,
				OutputPath = tempFile,
			};
			Assert.IsTrue (task.Execute (), "task should succeed!");
			FileAssert.Exists (tempFile);
			Assert.AreNotEqual (0, new FileInfo (tempFile).Length, "file should be non-empty!");
		}

		[Test]
		public void FileThatDoesNotExist ()
		{
			var resourceName = "thisdoesnotexist";
			var task = new CopyResource {
				BuildEngine = engine,
				ResourceName = resourceName,
				OutputPath = tempFile,
			};
			Assert.IsFalse (task.Execute (), "task should fail!");
			Assert.AreEqual (1, errors.Count);
			var error = errors [0];
			Assert.AreEqual ("XA0116", error.Code);
			StringAssert.Contains (resourceName, error.Message);
		}
	}
}
