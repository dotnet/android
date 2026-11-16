using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	public class AndroidCreateDebugKeyTests : BaseTest
	{
		AndroidCreateDebugKey CreateTask (string keyStore, List<BuildErrorEventArgs> errors, List<BuildWarningEventArgs> warnings)
		{
			const string pass = "Cy(nBW~j.&@B-!R_aq7^/syzFR!S$4]7R%i6)R!";
			return new AndroidCreateDebugKey {
				BuildEngine = new MockBuildEngine (TestContext.Out, errors, warnings),
				KeyStore = keyStore,
				KeyAlias = "androiddebugkey",
				KeyPass = pass,
				StorePass = pass,
				ToolPath = Path.Combine (AndroidSdkResolver.GetJavaSdkPath (), "bin"),
			};
		}

		[Test]
		public void CreateDebugKey ()
		{
			string directory = Path.Combine (Root, "temp", TestName);
			if (Directory.Exists (directory))
				Directory.Delete (directory, recursive: true);
			// No shell runs keytool, so these characters must stay literal.
			string keyStore = Path.Combine (directory, "keys $PATH %PATH% it's", "debug.keystore");
			var errors = new List<BuildErrorEventArgs> ();
			var warnings = new List<BuildWarningEventArgs> ();

			Assert.IsTrue (CreateTask (keyStore, errors, warnings).Execute (), "First run should succeed.");
			FileAssert.Exists (keyStore);
			Assert.AreEqual (0, errors.Count, "First run should have no errors.");
			Assert.AreEqual (0, warnings.Count, "First run should have no warnings.");

			// A parallel build can create the key after the target's Exists() check.
			Assert.IsTrue (CreateTask (keyStore, errors, warnings).Execute (), "Second run should succeed.");
			Assert.AreEqual (0, errors.Count, "Second run should have no errors.");
			Assert.AreEqual (1, warnings.Count, "Second run should have one warning.");
			Assert.AreEqual ("ANDKT0000", warnings [0].Code);
			StringAssert.Contains ("alias <androiddebugkey> already exists", warnings [0].Message);
		}
	}
}