using System;
using Microsoft.Build.Framework;
using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xamarin.Android.Tasks;
using ValidateJavaVersion = Xamarin.Android.Tasks.Legacy.ValidateJavaVersion;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	public class ValidateJavaVersionTests : BaseTest
	{
		string path;
		List<BuildErrorEventArgs> errors;
		List<BuildWarningEventArgs> warnings;
		List<BuildMessageEventArgs> messages;
		MockBuildEngine engine;

		[SetUp]
		public void Setup ()
		{
			path = Path.Combine ("temp", TestName);
			engine = new MockBuildEngine (TestContext.Out,
				errors: errors = new List<BuildErrorEventArgs> (),
				warnings: warnings = new List<BuildWarningEventArgs> (),
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
			Assert.IsTrue (
					errors.Any (e => e.Message.StartsWith ($"Java SDK 1.8 or above is required when using $(TargetFrameworkVersion) {validateJavaVersion.TargetFrameworkVersion}.", StringComparison.OrdinalIgnoreCase)),
					"Should get error about TargetFrameworkVersion=v8.1");
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
			Assert.IsTrue (
					errors.Any (e => e.Message.StartsWith ($"Java SDK 1.8 or above is required when using Android SDK Build-Tools {validateJavaVersion.AndroidSdkBuildToolsVersion}.", StringComparison.OrdinalIgnoreCase)),
					"Should get error about build-tools=27.0.0");
		}

		[Test]
		public void ExtraJavacOutputIsIgnored ()
		{
			var extraPrefix = new[]{
				"Picked up JAVA_TOOL_OPTIONS: -Dcom.sun.jndi.ldap.object.trustURLCodebase=false -Dcom.sun.jndi.rmi.object.trustURLCodebase=false -Dcom.sun.jndi.cosnaming.object.trustURLCodebase=false -Dlog4j2.formatMsgNoLookups=true",
				"Picked up _JAVA_OPTIONS: -Dcom.sun.jndi.ldap.object.trustURLCodebase=false -Dcom.sun.jndi.rmi.object.trustURLCodebase=false -Dcom.sun.jndi.cosnaming.object.trustURLCodebase=false -Dlog4j2.formatMsgNoLookups=true",
			};
			var javaPath = CreateFauxJavaSdkDirectory (Path.Combine (path, "jdk"), "11.0.12", out string javaExe, out string javacExe, extraPrefix);
			var validateJavaVersion = new ValidateJavaVersion {
				BuildEngine                 = engine,
				AndroidSdkBuildToolsVersion = "27.0.0",
				JavaSdkPath                 = javaPath,
				JavaToolExe                 = javaExe,
				JavacToolExe                = javacExe,
				LatestSupportedJavaVersion  = "11.99.0",
				MinimumSupportedJavaVersion = "11.0.0",
			};
			Assert.IsTrue (validateJavaVersion.Execute (), "Execute should succeed!");
			Assert.IsFalse (
					warnings.Any (e => e.Code == "XA0033"),
					$"Want no XA0033 warnings! Got: ```{string.Join (Environment.NewLine, warnings.Select (m => $"warning {m.Code}: {m.Message}"))}```");
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
			Assert.IsFalse (messages.Any (m => m.Message.StartsWith ("Using cached value for", StringComparison.Ordinal)), "`java -version` should *not* be cached!");
		}

		[Test]
		public void ReleaseFile_Valid ()
		{
			var javaPath = CreateFauxJavaSdkDirectory (Path.Combine (path, "jdk-1"), "11.0.9", out string javaExe, out string javacExe);
			var releasePath = Path.Combine (Path.GetDirectoryName (javaExe), "..", "release");
			File.WriteAllText (releasePath,
@"IMPLEMENTOR=""Microsoft""
IMPLEMENTOR_VERSION=""Microsoft-7621296""
JAVA_VERSION=""11.0.19""
JAVA_VERSION_DATE=""2023-04-18""
LIBC=""default""
MODULES=""java.base java.compiler java.datatransfer java.xml java.prefs java.desktop java.instrument java.logging java.management java.security.sasl java.naming java.rmi java.management.rmi java.net.http java.scripting java.security.jgss java.transaction.xa java.sql java.sql.rowset java.xml.crypto java.se java.smartcardio jdk.accessibility jdk.internal.vm.ci jdk.management jdk.unsupported jdk.internal.vm.compiler jdk.aot jdk.internal.jvmstat jdk.attach jdk.charsets jdk.compiler jdk.crypto.ec jdk.crypto.cryptoki jdk.crypto.mscapi jdk.dynalink jdk.internal.ed jdk.editpad jdk.hotspot.agent jdk.httpserver jdk.internal.le jdk.internal.opt jdk.internal.vm.compiler.management jdk.jartool jdk.javadoc jdk.jcmd jdk.management.agent jdk.jconsole jdk.jdeps jdk.jdwp.agent jdk.jdi jdk.jfr jdk.jlink jdk.jshell jdk.jsobject jdk.jstatd jdk.localedata jdk.management.jfr jdk.naming.dns jdk.naming.ldap jdk.naming.rmi jdk.net jdk.pack jdk.rmic jdk.scripting.nashorn jdk.scripting.nashorn.shell jdk.sctp jdk.security.auth jdk.security.jgss jdk.unsupported.desktop jdk.xml.dom jdk.zipfs""
OS_ARCH=""x86_64""
OS_NAME=""Windows""
SOURCE="".:git:6171c19d2c18""
");

			var validateJavaVersion = new Android.Tasks.ValidateJavaVersion {
				BuildEngine = engine,
				JavaSdkPath = javaPath,
				JavaToolExe = javaExe,
				JavacToolExe = javacExe,
				LatestSupportedJavaVersion = "17.99.0",
				MinimumSupportedJavaVersion = "11.0.0",
				UseJavaExeVersion = false,
			};
			Assert.IsTrue (validateJavaVersion.Execute (), "Execute should succeed!");
			Assert.IsTrue (messages.Any (m => m.Message.Contains ("contains JAVA_VERSION")), "contains JAVA_VERSION should be found");
		}

		[Test]
		public void ReleaseFile_Invalid ()
		{
			var javaPath = CreateFauxJavaSdkDirectory (Path.Combine (path, "jdk-1"), "11.0.9", out string javaExe, out string javacExe);
			var releasePath = Path.Combine (Path.GetDirectoryName (javaExe), "..", "release");
			File.WriteAllText (releasePath, "Just some random text");

			var validateJavaVersion = new Android.Tasks.ValidateJavaVersion {
				BuildEngine = engine,
				JavaSdkPath = javaPath,
				JavaToolExe = javaExe,
				JavacToolExe = javacExe,
				LatestSupportedJavaVersion = "17.99.0",
				MinimumSupportedJavaVersion = "11.0.0",
				UseJavaExeVersion = false,
			};

			// Will succeed due to fallback
			Assert.IsTrue (validateJavaVersion.Execute (), "Execute should succeed!");
			Assert.IsTrue (messages.Any (m => m.Message.Contains ("did not contain a valid JAVA_VERSION")), "valid JAVA_VERSION should *not* be found");
		}
	}
}
