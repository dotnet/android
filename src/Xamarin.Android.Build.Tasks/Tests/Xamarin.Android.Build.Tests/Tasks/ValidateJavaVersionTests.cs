using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NUnit.Framework;
using Xamarin.Android.Tasks;
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
			var result = Validate (javaPath, Path.Combine (javaPath, "bin", javaExe), javacExe, "21.0", "25.0.99", useJavaExeVersion);
			Assert.AreEqual (succeeds, result.Success);
			Assert.AreEqual ("21.0", result.DefaultJdkVersion);
			Assert.AreEqual (expectedVersion ?? version, result.JdkVersion);
			Assert.IsEmpty (result.Warnings);
			Assert.AreEqual (succeeds ? 0 : 1, result.Errors.Count);
			if (!succeeds)
				Assert.AreEqual (errorCode, result.Errors [0].Code);
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

		[TestCase ("11.0.9", "XA0031")]
		[TestCase ("17.0.1", "XA0031")]
		[TestCase ("26.0.0", "XA0030")]
		public void JavacMustMeetConfiguredJavaVersionRange (string version, string errorCode)
		{
			var javaPath = CreateFauxJavaSdkDirectory (Path.Combine (path, "jdk"), "21.0.8", out string javaExe, out string javacExe);
			var otherJavaPath = CreateFauxJavaSdkDirectory (Path.Combine (path, "other-jdk"), version, out _, out string otherJavacExe);
			File.Copy (Path.Combine (otherJavaPath, "bin", otherJavacExe), Path.Combine (javaPath, "bin", javacExe), overwrite: true);
			var result = Validate (javaPath, Path.Combine (javaPath, "bin", javaExe), javacExe, "21.0", "25.0.99", useJavaExeVersion: true);
			Assert.IsFalse (result.Success);
			Assert.AreEqual ("21.0", result.DefaultJdkVersion);
			Assert.AreEqual (version, result.JdkVersion);
			Assert.AreEqual (1, result.Errors.Count);
			Assert.AreEqual (errorCode, result.Errors [0].Code);
		}

		[Test]
		public void ConfiguredMinimumJavaVersionIsRequired ()
		{
			var javaPath = CreateFauxJavaSdkDirectory (Path.Combine (path, "jdk"), "1.7.0", out string javaExe, out string javacExe);
			var result = Validate (javaPath, Path.Combine (javaPath, "bin", javaExe), javacExe, "1.8.0", "1.8.0", useJavaExeVersion: true);
			Assert.IsFalse (result.Success, "Validation should *not* succeed!");
			Assert.IsTrue (result.Errors.Any (e => e.Code == "XA0031"), "Should report the configured minimum Java version.");
		}

		[Test]
		public void ExtraJavacOutputIsIgnored ()
		{
			var extraPrefix = new[]{
				"Picked up JAVA_TOOL_OPTIONS: -Dcom.sun.jndi.ldap.object.trustURLCodebase=false -Dcom.sun.jndi.rmi.object.trustURLCodebase=false -Dcom.sun.jndi.cosnaming.object.trustURLCodebase=false -Dlog4j2.formatMsgNoLookups=true",
				"Picked up _JAVA_OPTIONS: -Dcom.sun.jndi.ldap.object.trustURLCodebase=false -Dcom.sun.jndi.rmi.object.trustURLCodebase=false -Dcom.sun.jndi.cosnaming.object.trustURLCodebase=false -Dlog4j2.formatMsgNoLookups=true",
			};
			var javaPath = CreateFauxJavaSdkDirectory (Path.Combine (path, "jdk"), "11.0.12", out string javaExe, out string javacExe, extraPrefix);
			var result = Validate (javaPath, Path.Combine (javaPath, "bin", javaExe), javacExe, "11.0.0", "11.99.0", useJavaExeVersion: true);
			Assert.IsTrue (result.Success, "Validation should succeed!");
			Assert.IsFalse (result.Warnings.Any (e => e.Code == "XA0033"),
				$"Want no XA0033 warnings! Got: ```{string.Join (Environment.NewLine, result.Warnings.Select (m => $"warning {m.Code}: {m.Message}"))}```");
		}

		[Test]
		public void UnparsableJavaOutputWarns ()
		{
			var javaPath = CreateFauxJavaSdkDirectory (Path.Combine (path, "jdk"), "21.0.8", out string javaExe, out string javacExe);
			File.Delete (Path.Combine (javaPath, "release"));
			File.WriteAllText (Path.Combine (javaPath, "bin", javaExe), IsWindows ? "@echo java version unknown" : "#!/bin/sh\necho 'java version unknown; O'\\''Brien'\n");
			var result = Validate (javaPath, javaExe, javacExe, "21.0", "25.0.99", useJavaExeVersion: false);
			Assert.IsTrue (result.Success);
			Assert.AreEqual ("", result.JdkVersion);
			var warning = result.Warnings.Single (w => w.Code == "XA0033");
			StringAssert.Contains ("java version unknown", warning.Message);
		}

		[TestCase (false)]
		[TestCase (true)]
		public void JavaPathsWithMSBuildSpecialCharacters (bool useJavaExeVersion)
		{
			var javaPath = CreateFauxJavaSdkDirectory (Path.Combine (path, "O'Brien;x=1", "jdk"), "21.0.8", out string javaExe, out string javacExe);
			var result = Validate (javaPath, javaExe, javacExe, "21.0", "25.0.99", useJavaExeVersion);
			Assert.IsTrue (result.Success, string.Join (Environment.NewLine, result.Errors.Select (e => e.Message)));
			Assert.AreEqual ("21.0.8", result.JdkVersion);
		}

		[TestCase (false)]
		[TestCase (true)]
		public void RelativeJavaPathsUseProjectDirectory (bool useJavaExeVersion)
		{
			var projectDirectory = Path.Combine (Root, path, "project");
			CreateFauxJavaSdkDirectory (Path.Combine (path, "project", "jdk"), "21.0.8", out string javaExe, out string javacExe);
			// Ignore a release file outside the selected JDK.
			File.WriteAllText (Path.Combine (Root, path, "release"), "JAVA_VERSION=\"17.0.1\"");
			var result = Validate ("jdk", javaExe, javacExe, "21.0", "25.0.99", useJavaExeVersion, projectDirectory);
			Assert.IsTrue (result.Success);
			Assert.AreEqual ("21.0.8", result.JdkVersion);
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

			var result = Validate (javaPath, Path.Combine (javaPath, "bin", javaExe), javacExe, "11.0.0", "17.99.0", useJavaExeVersion: false);
			Assert.IsTrue (result.Success, "Validation should succeed!");
			Assert.AreEqual ("11.0.19", result.JdkVersion);
			Assert.AreEqual ("11.0.0", result.DefaultJdkVersion);
		}

		[Test]
		public void ReleaseFile_Invalid ()
		{
			var javaPath = CreateFauxJavaSdkDirectory (Path.Combine (path, "jdk-1"), "11.0.9", out string javaExe, out string javacExe);
			File.WriteAllText (Path.Combine (javaPath, "release"), "Just some random text");

			// Falls back to `java -version`.
			var result = Validate (javaPath, Path.Combine (javaPath, "bin", javaExe), javacExe, "11.0.0", "17.99.0", useJavaExeVersion: false);
			Assert.IsTrue (result.Success, "Validation should succeed!");
			Assert.AreEqual ("11.0.9", result.JdkVersion);
		}

		[TestCase (false)]
		[TestCase (true)]
		public void MissingJavaSdkPathReportsXA5300 (bool useJavaExeVersion)
		{
			var result = Validate (null, "java", "javac", "11.0.0", "17.99.0", useJavaExeVersion);
			Assert.IsFalse (result.Success, "Validation should fail when JavaSdkPath is null.");
			Assert.AreEqual ("XA5300", result.Errors.Single ().Code);
		}

		[Test]
		public void AllowMissingSdkToolingDowngradesErrors ()
		{
			var javaPath = CreateFauxJavaSdkDirectory (Path.Combine (path, "jdk"), "17.0.1", out string javaExe, out string javacExe);
			var result = Validate (javaPath, javaExe, javacExe, "21.0", "25.0.99", useJavaExeVersion: false,
				properties: new Dictionary<string, string> { ["_AndroidAllowMissingSdkTooling"] = "true" });
			Assert.IsTrue (result.Success);
			Assert.IsEmpty (result.Errors);
			Assert.IsTrue (result.Warnings.Any (w => w.Code == "XA0031"));
		}

		(bool Success, string JdkVersion, string DefaultJdkVersion, List<BuildErrorEventArgs> Errors, List<BuildWarningEventArgs> Warnings) Validate (
			string javaSdkPath, string javaToolExe, string javacToolExe, string minimum, string latest, bool useJavaExeVersion,
			string projectDirectory = null, Dictionary<string, string> properties = null)
		{
			string targets = Path.Combine (XABuildPaths.TopDirectory, "src", "Xamarin.Android.Build.Tasks", "Microsoft.Android.Sdk", "targets", "Microsoft.Android.Sdk.Tooling.targets");
			string tasksAssembly = typeof (AndroidError).Assembly.Location;
			XNamespace ns = "http://schemas.microsoft.com/developer/msbuild/2003";
			var project = new XElement (ns + "Project",
				new XElement (ns + "PropertyGroup",
					new XElement (ns + "_XamarinAndroidBuildTasksAssembly", tasksAssembly),
					new XElement (ns + "_JavaSdkDirectory", javaSdkPath ?? ""),
					new XElement (ns + "JavaToolExe", javaToolExe ?? ""),
					new XElement (ns + "JavacToolExe", javacToolExe ?? ""),
					new XElement (ns + "MinimumSupportedJavaVersion", minimum),
					new XElement (ns + "LatestSupportedJavaVersion", latest),
					new XElement (ns + "_AndroidUseJavaExeVersion", useJavaExeVersion.ToString ()),
					new XElement (ns + "_AndroidAllowMissingSdkTooling", "False")),
				new XElement (ns + "UsingTask", new XAttribute ("TaskName", typeof (AndroidError).FullName), new XAttribute ("AssemblyFile", tasksAssembly)),
				new XElement (ns + "UsingTask", new XAttribute ("TaskName", typeof (AndroidWarning).FullName), new XAttribute ("AssemblyFile", tasksAssembly)),
				new XElement (ns + "Import", new XAttribute ("Project", targets)));
			foreach (var property in properties ?? []) {
				project.Element (ns + "PropertyGroup").Add (new XElement (ns + property.Key, property.Value));
			}

			using var reader = project.CreateReader ();
			using var collection = new ProjectCollection ();
			string toolsPath = Directory.EnumerateDirectories (Path.Combine (TestEnvironment.DotNetPreviewDirectory, "sdk"))
				.First (p => File.Exists (Path.Combine (p, "Microsoft.Common.tasks")));
			collection.AddToolset (new Toolset ("Current", toolsPath, collection, ""));
			var root = Microsoft.Build.Construction.ProjectRootElement.Create (reader, collection);
			projectDirectory ??= Path.Combine (Root, path);
			Directory.CreateDirectory (projectDirectory);
			root.FullPath = Path.Combine (projectDirectory, "Validate.proj");
			var instance = new ProjectInstance (root, globalProperties: null, toolsVersion: null, collection);
			var logger = new ValidateLogger ();
			bool success = instance.Build ("_ValidateJavaVersion", new [] { logger });
			return (success, instance.GetPropertyValue ("_JdkVersion"), instance.GetPropertyValue ("_DefaultJdkVersion"), logger.Errors, logger.Warnings);
		}

		sealed class ValidateLogger : Logger
		{
			public List<BuildWarningEventArgs> Warnings { get; } = [];
			public List<BuildErrorEventArgs> Errors { get; } = [];

			public override void Initialize (IEventSource eventSource)
			{
				eventSource.WarningRaised += (_, warning) => Warnings.Add (warning);
				eventSource.ErrorRaised += (_, error) => Errors.Add (error);
			}
		}
	}
}
