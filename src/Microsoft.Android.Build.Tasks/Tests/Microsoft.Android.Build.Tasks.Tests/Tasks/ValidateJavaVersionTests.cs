using System;
using System.Diagnostics;
using Microsoft.Build.Framework;
using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Android.Tasks;

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

			Directory.CreateDirectory (Path.Combine (Root, path));
		}

		[TearDown]
		public void TearDown ()
		{
			Directory.Delete (Path.Combine (Root, path), recursive: true);
		}

		[TestCase ("11.0.9", false, "XA0031", false)]
		[TestCase ("11.0.9", false, "XA0031", true)]
		[TestCase ("17.0.1", false, "XA0031", false)]
		[TestCase ("17.0.1", false, "XA0031", true)]
		[TestCase ("21.0", true, "", false)]
		[TestCase ("21.0", true, "", true)]
		[TestCase ("21.0.8", true, "", false)]
		[TestCase ("21.0.8", true, "", true)]
		[TestCase ("21.0.13.0.2", true, "", false, "21.0.13.0")]
		[TestCase ("21.0.13.0.2", true, "", true, "21.0.13.0")]
		[TestCase ("25.0.1", true, "", false)]
		[TestCase ("25.0.1", true, "", true)]
		[TestCase ("25.0.99", true, "", false)]
		[TestCase ("25.0.99", true, "", true)]
		[TestCase ("25.0.100", false, "XA0030", false)]
		[TestCase ("25.0.100", false, "XA0030", true)]
		[TestCase ("26.0.0", false, "XA0030", false)]
		[TestCase ("26.0.0", false, "XA0030", true)]
		public void CurrentSdkUsesConfiguredJavaVersionRange (string version, bool succeeds, string errorCode, bool useJavaExeVersion, string expectedVersion = null)
		{
			var javaPath = CreateFauxJavaSdkDirectory (Path.Combine (path, "jdk"), version, out string javaExe, out string javacExe);
			if (useJavaExeVersion)
				File.WriteAllText (Path.Combine (javaPath, "release"), "JAVA_VERSION=\"99.0.0\"");
			var validateJavaVersion = new ValidateJavaVersion {
				BuildEngine = engine,
				JavaSdkPath = javaPath,
				JavaToolExe = Path.Combine (javaPath, "bin", javaExe),
				JavacToolExe = javacExe,
				LatestSupportedJavaVersion = "25.0.99",
				MinimumSupportedJavaVersion = "21.0",
				UseJavaExeVersion = useJavaExeVersion,
			};
			Assert.AreEqual (succeeds, validateJavaVersion.Execute ());
			Assert.AreEqual ("21.0", validateJavaVersion.MinimumRequiredJdkVersion);
			Assert.AreEqual (expectedVersion ?? version, validateJavaVersion.JdkVersion);
			Assert.AreEqual (!useJavaExeVersion, messages.Any (m => m.Message.Contains ("contains JAVA_VERSION")));
			Assert.IsEmpty (warnings);
			Assert.AreEqual (succeeds ? 0 : 1, errors.Count);
			if (!succeeds)
				Assert.AreEqual (errorCode, errors [0].Code);
		}

		[Test]
		public void ShortAndQualifiedTaskNamesResolve ()
		{
			var javaPath = CreateFauxJavaSdkDirectory (Path.Combine (path, "jdk"), "21.0.8", out string javaExe, out string javacExe);
			string targets = Path.Combine (NativeAotBootstrapTestTools.FindRepositoryRoot (), "src", "Xamarin.Android.Build.Tasks", "Microsoft.Android.Sdk", "targets", "Microsoft.Android.Sdk.Tooling.targets");
			XNamespace ns = "http://schemas.microsoft.com/developer/msbuild/2003";
			var usingTasks = XDocument.Load (targets).Root.Elements (ns + "UsingTask")
				.Where (e => (string) e.Attribute ("TaskName") == "Microsoft.Android.Tasks.ValidateJavaVersion")
				.Select (e => new XElement ("UsingTask", e.Attributes ()))
				.ToList ();
			Assert.AreEqual (2, usingTasks.Count, "Expected the Full/Core UsingTask pair.");

			XElement Invoke (string taskName) => new XElement (taskName,
				new XAttribute ("JavaSdkPath", javaPath),
				new XAttribute ("JavaToolExe", javaExe),
				new XAttribute ("JavacToolExe", javacExe),
				new XAttribute ("MinimumSupportedJavaVersion", "21.0"),
				new XAttribute ("LatestSupportedJavaVersion", "25.0.99"));
			string project = Path.Combine (Root, path, "validate.proj");
			new XDocument (
				new XElement ("Project",
					new XElement ("PropertyGroup",
						new XElement ("_MicrosoftAndroidBuildTasksAssembly", typeof (ValidateJavaVersion).Assembly.Location)),
					usingTasks,
					new XElement ("Target", new XAttribute ("Name", "Build"),
						Invoke ("ValidateJavaVersion"),
						Invoke ("Microsoft.Android.Tasks.ValidateJavaVersion"))))
				.Save (project);

			string dotnet = Environment.GetEnvironmentVariable ("DOTNET_HOST_PATH") ?? "dotnet";
			var result = NativeAotBootstrapTestTools.Run (dotnet, "msbuild", project, "-v:normal", "-nr:false");
			string output = result.Output + result.Error;
			Assert.AreEqual (0, result.ExitCode, output);
			Assert.AreEqual (4, output.Split ("Found Java SDK version 21.0.8.").Length - 1, output);
		}

		[TestCase ("11.0.9", "XA0031")]
		[TestCase ("17.0.1", "XA0031")]
		[TestCase ("26.0.0", "XA0030")]
		public void JavacMustMeetConfiguredJavaVersionRange (string version, string errorCode)
		{
			var javaPath = CreateFauxJavaSdkDirectory (Path.Combine (path, "jdk"), "21.0.8", out string javaExe, out string javacExe);
			var otherJavaPath = CreateFauxJavaSdkDirectory (Path.Combine (path, "other-jdk"), version, out _, out string otherJavacExe);
			File.Copy (Path.Combine (otherJavaPath, "bin", otherJavacExe), Path.Combine (javaPath, "bin", javacExe), overwrite: true);
			var validateJavaVersion = new ValidateJavaVersion {
				BuildEngine = engine,
				JavaSdkPath = javaPath,
				JavaToolExe = Path.Combine (javaPath, "bin", javaExe),
				JavacToolExe = javacExe,
				LatestSupportedJavaVersion = "25.0.99",
				MinimumSupportedJavaVersion = "21.0",
				UseJavaExeVersion = true,
			};
			Assert.IsFalse (validateJavaVersion.Execute ());
			Assert.AreEqual ("21.0", validateJavaVersion.MinimumRequiredJdkVersion);
			Assert.AreEqual (version, validateJavaVersion.JdkVersion);
			Assert.AreEqual (1, errors.Count);
			Assert.AreEqual (errorCode, errors [0].Code);
		}

		[Test]
		public void ConfiguredMinimumJavaVersionIsRequired ()
		{
			var javaPath = CreateFauxJavaSdkDirectory (Path.Combine (path, "jdk"), "1.7.0", out string javaExe, out string javacExe);
			var validateJavaVersion = new ValidateJavaVersion {
				BuildEngine = engine,
				JavaSdkPath = javaPath,
				JavaToolExe = Path.Combine (javaPath, "bin", javaExe),
				JavacToolExe = javacExe,
				LatestSupportedJavaVersion = "1.8.0",
				MinimumSupportedJavaVersion = "1.8.0",
			};
			Assert.False (validateJavaVersion.Execute (), "Execute should *not* succeed!");
			Assert.IsTrue (
					errors.Any (e => e.Code == "XA0031"),
					"Should report the configured minimum Java version.");
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
				JavaSdkPath                 = javaPath,
				JavaToolExe                 = Path.Combine (javaPath, "bin", javaExe),
				JavacToolExe                = javacExe,
				LatestSupportedJavaVersion  = "11.99.0",
				MinimumSupportedJavaVersion = "11.0.0",
			};
			Assert.IsTrue (validateJavaVersion.Execute (), "Execute should succeed!");
			Assert.IsFalse (
					warnings.Any (e => e.Code == "XA0033"),
					$"Want no XA0033 warnings! Got: ```{string.Join (Environment.NewLine, warnings.Select (m => $"warning {m.Code}: {m.Message}"))}```");
		}

		[TestCase (false)]
		[TestCase (true)]
		public void RelativeJavaPathsUseProjectDirectory (bool useJavaExeVersion)
		{
			var projectDirectory = Path.Combine (Root, path, "project");
			CreateFauxJavaSdkDirectory (Path.Combine (path, "project", "jdk"), "21.0.8", out string javaExe, out string javacExe);
			// Ignore a release file outside the selected JDK.
			File.WriteAllText (Path.Combine (Root, path, "release"), "JAVA_VERSION=\"17.0.1\"");
			var task = new ValidateJavaVersion {
				BuildEngine = engine,
				TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment (projectDirectory, new Dictionary<string, string> ()),
				JavaSdkPath = "jdk",
				JavaToolExe = javaExe,
				JavacToolExe = javacExe,
				MinimumSupportedJavaVersion = "21.0",
				LatestSupportedJavaVersion = "25.0.99",
				UseJavaExeVersion = useJavaExeVersion,
			};

			Assert.IsTrue (task.Execute ());
			Assert.AreEqual ("21.0.8", task.JdkVersion);
		}

		[Test]
		public void JavaProcessUsesProjectEnvironment ()
		{
			var projectDirectory = Path.Combine (Root, path);
			var javaPath = CreateFauxJavaSdkDirectory (Path.Combine (path, "jdk"), "21.0.8", out string javaExe, out string javacExe);
			File.WriteAllText (Path.Combine (javaPath, "bin", javaExe), IsWindows ?
				"@echo java version \"%JAVA_TOOL_OPTIONS%\"" :
				"""
				#!/bin/sh
				echo "java version \"$JAVA_TOOL_OPTIONS\""
				""");
			var processOptions = Environment.GetEnvironmentVariable ("JAVA_TOOL_OPTIONS");

			var task = new ValidateJavaVersion {
				BuildEngine = engine,
				TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment (projectDirectory,
					new Dictionary<string, string> { ["JAVA_TOOL_OPTIONS"] = "21.0.8" }),
				JavaSdkPath = javaPath,
				JavaToolExe = javaExe,
				JavacToolExe = javacExe,
				MinimumSupportedJavaVersion = "21.0",
				LatestSupportedJavaVersion = "25.0.99",
			};

			Assert.IsTrue (task.Execute ());
			Assert.AreEqual ("21.0.8", task.JdkVersion);

			task.TaskEnvironment.SetEnvironmentVariable ("JAVA_TOOL_OPTIONS", "17.0.1");
			Assert.IsFalse (task.Execute ());
			Assert.AreEqual ("17.0.1", task.JdkVersion);
			Assert.AreEqual (processOptions, Environment.GetEnvironmentVariable ("JAVA_TOOL_OPTIONS"));
		}

		[Test]
		public void CacheWorks ()
		{
			var javaPath = CreateFauxJavaSdkDirectory (Path.Combine (path, "jdk"), "1.8.0", out string javaExe, out string javacExe);
			var validateJavaVersion = new ValidateJavaVersion {
				BuildEngine = engine,
				JavaSdkPath = javaPath,
				JavaToolExe = Path.Combine (javaPath, "bin", javaExe),
				JavacToolExe = javacExe,
				LatestSupportedJavaVersion = "1.8.0",
				MinimumSupportedJavaVersion = "1.8.0",
			};
			Assert.IsTrue (validateJavaVersion.Execute (), "first Execute should succeed!");

			validateJavaVersion.JavaToolExe = Path.Combine (javaPath, "bin", "..", "bin", javaExe);
			validateJavaVersion.JavacToolExe = Path.Combine ("..", "bin", javacExe);
			messages.Clear ();

			Assert.IsTrue (validateJavaVersion.Execute (), "second Execute should succeed!");
			Assert.AreEqual ("1.8.0", validateJavaVersion.JdkVersion);
			Assert.AreEqual ("1.8.0", validateJavaVersion.MinimumRequiredJdkVersion);
			Assert.IsFalse (messages.Any (m => m.Message == "ValidateJavaVersion Outputs:" ||
				m.Message.StartsWith ("  JdkVersion:", StringComparison.Ordinal) ||
				m.Message.StartsWith ("  MinimumRequiredJdkVersion:", StringComparison.Ordinal)));
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
				JavaToolExe = Path.Combine (javaPath, "bin", javaExe),
				JavacToolExe = javacExe,
				LatestSupportedJavaVersion = "1.8.0",
				MinimumSupportedJavaVersion = "1.7.0",
			};
			Assert.IsTrue (validateJavaVersion.Execute (), "first Execute should succeed!");

			messages.Clear ();

			javaPath = CreateFauxJavaSdkDirectory (Path.Combine (path, "jdk-2"), "1.8.0", out javaExe, out javacExe);
			validateJavaVersion.JavaSdkPath = javaPath;
			validateJavaVersion.JavaToolExe = Path.Combine (javaPath, "bin", javaExe);

			Assert.IsTrue (validateJavaVersion.Execute (), "second Execute should succeed!");
			Assert.IsFalse (messages.Any (m => m.Message.StartsWith ("Using cached value for", StringComparison.Ordinal)), "`java -version` should *not* be cached!");
		}

		[Test]
		public void ReleaseFile_Valid ()
		{
			var javaPath = CreateFauxJavaSdkDirectory (Path.Combine (path, "jdk-1"), "11.0.9", out string javaExe, out string javacExe);
			var releasePath = Path.Combine (javaPath, "release");
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

			var validateJavaVersion = new ValidateJavaVersion {
				BuildEngine = engine,
				JavaSdkPath = javaPath,
				JavaToolExe = Path.Combine (javaPath, "bin", javaExe),
				JavacToolExe = javacExe,
				LatestSupportedJavaVersion = "17.99.0",
				MinimumSupportedJavaVersion = "11.0.0",
				UseJavaExeVersion = false,
			};
			Assert.IsTrue (validateJavaVersion.Execute (), "Execute should succeed!");
			Assert.AreEqual ("11.0.19", validateJavaVersion.JdkVersion);
			Assert.AreEqual ("11.0.0", validateJavaVersion.MinimumRequiredJdkVersion);
			Assert.IsFalse (messages.Any (m => m.Message == "ValidateJavaVersion Outputs:" ||
				m.Message.StartsWith ("  JdkVersion:", StringComparison.Ordinal) ||
				m.Message.StartsWith ("  MinimumRequiredJdkVersion:", StringComparison.Ordinal)));
			Assert.IsTrue (messages.Any (m => m.Message.Contains ("contains JAVA_VERSION")), "contains JAVA_VERSION should be found");
		}

		[Test]
		public void ReleaseFile_Invalid ()
		{
			var javaPath = CreateFauxJavaSdkDirectory (Path.Combine (path, "jdk-1"), "11.0.9", out string javaExe, out string javacExe);
			var releasePath = Path.Combine (javaPath, "release");
			File.WriteAllText (releasePath, "Just some random text");

			var validateJavaVersion = new ValidateJavaVersion {
				BuildEngine = engine,
				JavaSdkPath = javaPath,
				JavaToolExe = Path.Combine (javaPath, "bin", javaExe),
				JavacToolExe = javacExe,
				LatestSupportedJavaVersion = "17.99.0",
				MinimumSupportedJavaVersion = "11.0.0",
				UseJavaExeVersion = false,
			};

			// Will succeed due to fallback
			Assert.IsTrue (validateJavaVersion.Execute (), "Execute should succeed!");
			Assert.IsTrue (messages.Any (m => m.Message.Contains ("did not contain a valid JAVA_VERSION")), "valid JAVA_VERSION should *not* be found");
		}

		[Test]
		public void NullJavaSdkPathDoesNotThrow ()
		{
			var validateJavaVersion = new ValidateJavaVersion {
				BuildEngine = engine,
				JavaSdkPath = null,
				JavaToolExe = "java",
				JavacToolExe = "javac",
				LatestSupportedJavaVersion = "17.99.0",
				MinimumSupportedJavaVersion = "11.0.0",
				UseJavaExeVersion = true,
			};

			Assert.IsFalse (validateJavaVersion.Execute (), "Execute should fail when JavaSdkPath is null.");
			Assert.IsFalse (errors.Any (e => e.Code == "XAVJV7001"), "ValidateJavaVersion should not throw when JavaSdkPath is null.");
			Assert.IsTrue (messages.Any (m => m.Message.Contains ("JavaSdkPath is not set")), "Should log debug message about missing JavaSdkPath.");
		}

		[Test]
		public void NullJavaSdkPathWithReleaseFileDoesNotThrow ()
		{
			var validateJavaVersion = new ValidateJavaVersion {
				BuildEngine = engine,
				JavaSdkPath = null,
				JavaToolExe = "java",
				JavacToolExe = "javac",
				LatestSupportedJavaVersion = "17.99.0",
				MinimumSupportedJavaVersion = "11.0.0",
				UseJavaExeVersion = false,
			};

			Assert.IsFalse (validateJavaVersion.Execute (), "Execute should fail when JavaSdkPath is null.");
			Assert.IsFalse (errors.Any (e => e.Code == "XAVJV7001"), "ValidateJavaVersion should not throw when JavaSdkPath is null (release file path).");
		}

		static bool IsWindows => OperatingSystem.IsWindows ();

		string CreateFauxJavaSdkDirectory (string path, string javaVersion, out string javaExe, out string javacExe, string[] extraPrefix = null)
		{
			javaExe = IsWindows ? "java.cmd" : "java";
			javacExe  = IsWindows ? "javac.cmd" : "javac";

			var javaPath = Path.Combine (Root, path);

			CreateFauxJdk (javaPath, javaVersion, javaVersion, javaVersion, extraPrefix);

			var jarSigner = IsWindows ? "jarsigner.exe" : "jarsigner";
			var javaBinPath = Path.Combine (javaPath, "bin");
			File.WriteAllText (Path.Combine (javaBinPath, jarSigner), "");

			return javaPath;
		}

		// https://github.com/dotnet/android-tools/blob/683f37508b56c76c24b3287a5687743438625341/tests/Xamarin.Android.Tools.AndroidSdk-Tests/JdkInfoTests.cs#L60-L100
		void CreateFauxJdk (string dir, string releaseVersion, string releaseBuildNumber, string javaVersion, string[] extraPrefix)
		{
			Directory.CreateDirectory (dir);

			using (var release = new StreamWriter (Path.Combine (dir, "release"))) {
				release.WriteLine ($"JAVA_VERSION=\"{releaseVersion}\"");
				release.WriteLine ($"BUILD_NUMBER={releaseBuildNumber}");
				release.WriteLine ($"JUST_A_KEY");
			}

			var bin = Path.Combine (dir, "bin");
			var inc = Path.Combine (dir, "include");
			var jre = Path.Combine (dir, "jre");
			var jli = Path.Combine (jre, "lib", "jli");

			Directory.CreateDirectory (bin);
			Directory.CreateDirectory (inc);
			Directory.CreateDirectory (jli);
			Directory.CreateDirectory (jre);

			string prefix = extraPrefix == null
				? ""
				: string.Join ("",
					extraPrefix.Select (e => "echo " + e + Environment.NewLine));

			string quote = IsWindows ? "" : "\"";
			string java = IsWindows
				? prefix + $"echo java version \"{javaVersion}\"{Environment.NewLine}"
				: prefix + $"echo java version '\"{javaVersion}\"'{Environment.NewLine}";
			java = java +
				$"echo Property settings:{Environment.NewLine}" +
				$"echo {quote}    java.home = {dir}{quote}{Environment.NewLine}" +
				$"echo {quote}    java.vendor = .NET for Android Unit Tests{quote}{Environment.NewLine}" +
				$"echo {quote}    java.version = {javaVersion}{quote}{Environment.NewLine}" +
				$"echo {quote}    xamarin.multi-line = line the first{quote}{Environment.NewLine}" +
				$"echo {quote}        line the second{quote}{Environment.NewLine}" +
				$"echo {quote}        .{quote}{Environment.NewLine}";

			string javac =
				prefix +
				$"echo javac {javaVersion}{Environment.NewLine}";

			CreateShellScript (Path.Combine (bin, "jar"), "");
			CreateShellScript (Path.Combine (bin, "java"), java);
			CreateShellScript (Path.Combine (bin, "javac"), javac);
			CreateShellScript (Path.Combine (jli, "libjli.dylib"), "");
			CreateShellScript (Path.Combine (jre, "libjvm.so"), "");
			CreateShellScript (Path.Combine (jre, "jvm.dll"), "");
		}

		// https://github.com/dotnet/android-tools/blob/683f37508b56c76c24b3287a5687743438625341/tests/Xamarin.Android.Tools.AndroidSdk-Tests/JdkInfoTests.cs#L108-L132
		void CreateShellScript (string path, string contents)
		{
			if (IsWindows && string.Compare (Path.GetExtension (path), ".dll", StringComparison.OrdinalIgnoreCase) != 0)
				path += ".cmd";
			using (var script = new StreamWriter (path)) {
				if (IsWindows) {
					script.WriteLine ("@echo off");
				}
				else {
					script.WriteLine ("#!/bin/sh");
				}
				script.WriteLine (contents);
			}
			if (IsWindows)
				return;
			var chmod = new ProcessStartInfo {
				FileName                    = "chmod",
				Arguments                   = $"+x \"{path}\"",
				UseShellExecute             = false,
				RedirectStandardInput       = false,
				RedirectStandardOutput      = true,
				RedirectStandardError       = true,
				CreateNoWindow              = true,
				WindowStyle                 = ProcessWindowStyle.Hidden,
			};
			var p = Process.Start (chmod);
			p.WaitForExit ();
		}
	}
}
