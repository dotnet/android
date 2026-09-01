using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NUnit.Framework;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	public class R8Tests
	{
		[TestCase ("-keep class com.example.Foo { *; }", false, "")]
		[TestCase ("-dontwarn com.example.**",           false, "")]
		[TestCase ("# -printmapping comment",            false, "")]
		[TestCase ("",                                   false, "")]
		[TestCase ("-printmappingFoo foo.txt",           false, "")] // token-boundary: must not match -printmapping
		[TestCase ("-dumpsterfire",                      false, "")] // token-boundary: must not match -dump
		[TestCase ("-printmapping mapping.txt",          true,  "-printmapping")]
		[TestCase ("-printmapping",                      true,  "-printmapping")] // option with no argument
		[TestCase ("  -printmapping mapping.txt",        true,  "-printmapping")]
		[TestCase ("\t-printseeds seeds.txt",            true,  "-printseeds")]
		[TestCase ("-printusage usage.txt",              true,  "-printusage")]
		[TestCase ("-printconfiguration config.txt",     true,  "-printconfiguration")]
		[TestCase ("-dump dump.txt",                     true,  "-dump")]
		[TestCase ("-dontoptimize",                      true,  "-dontoptimize")]
		[TestCase ("-dontobfuscate",                     true,  "-dontobfuscate")]
		[TestCase ("-PrintMapping mapping.txt",          true,  "-printmapping")] // case-insensitive
		[TestCase ("-DUMP dump.txt",                     true,  "-dump")]
		[TestCase ("-DontOptimize",                      true,  "-dontoptimize")]
		public void TryGetDisallowedOption (string line, bool expected, string expectedOption)
		{
			var actual = R8.TryGetDisallowedOption (line, out var option);
			Assert.AreEqual (expected, actual);
			Assert.AreEqual (expectedOption, option);
		}

		[TestCase ("package com.example.app;\npublic class Foo {}",            "com.example.app")]
		[TestCase ("package com.example.app ;\npublic class Foo {}",           "com.example.app")] // space before ';'
		[TestCase ("// header\n/* license */\npackage com.example.app;\nclass Foo {}", "com.example.app")] // skip comments
		[TestCase ("public class Foo {}",                                     null)] // no package
		[TestCase ("import java.util.List;\npackage com.late;\nclass Foo {}", null)] // package after import is ignored
		[TestCase ("class Foo {\npackage com.late;\n}",                       null)] // package after type is ignored
		public void ReadJavaPackage (string content, string? expected)
		{
			var path = Path.GetTempFileName ();
			try {
				File.WriteAllText (path, content);
				Assert.AreEqual (expected, R8.ReadJavaPackage (path));
			} finally {
				File.Delete (path);
			}
		}

		[Test]
		public void GenerateSeedMappingAllowsAcwObfuscation ()
		{
			string path = Path.Combine (Path.GetTempPath (), Guid.NewGuid ().ToString ("N"));
			Directory.CreateDirectory (path);
			string responseFile = "";
			try {
				string acwMap = Path.Combine (path, "acw-map.txt");
				string applicationConfiguration = Path.Combine (path, "acw-keep.cfg");
				string commonConfiguration = Path.Combine (path, "xamarin.cfg");
				string customConfiguration = Path.Combine (path, "custom.cfg");
				File.WriteAllText (acwMap, "Managed.Peer;com.example.Peer");
				File.WriteAllText (customConfiguration, "-dontwarn com.example.**");

				var task = new R8TestTask {
					BuildEngine = new MockBuildEngine (TestContext.Out),
					JarPath = "r8.jar",
					JavaPlatformJarPath = "android.jar",
					OutputDirectory = path,
					AcwMapFile = acwMap,
					ProguardGeneratedApplicationConfiguration = applicationConfiguration,
					ProguardCommonXamarinConfiguration = commonConfiguration,
					ProguardMappingFileOutput = Path.Combine (path, "mapping.txt"),
					ProguardConfigurationFiles = new ITaskItem [] { new TaskItem (customConfiguration) },
					GenerateSeedMapping = true,
					EnableObfuscation = true,
					IgnoreWarnings = true,
				};

				task.TestGenerateCommandLineCommands ();
				responseFile = task.ResponseFilePath;
				string [] response = File.ReadAllLines (responseFile);
				var configurationFiles = response
					.Select ((argument, index) => (argument, index))
					.Where (entry => entry.argument == "--pg-conf")
					.Select (entry => response [entry.index + 1])
					.ToArray ();
				string configuration = string.Join (Environment.NewLine, configurationFiles.Select (File.ReadAllText));

				Assert.That (configurationFiles, Does.Contain (customConfiguration), "Seed R8 should honor user ProGuard rules.");
				Assert.That (configurationFiles, Does.Contain (commonConfiguration), "Seed R8 should honor runtime keep rules.");
				Assert.That (configurationFiles, Does.Not.Contain (applicationConfiguration), "Seed R8 must not pass the ACW keep configuration.");
				FileAssert.DoesNotExist (applicationConfiguration, "Seed R8 must not generate obfuscation-blocking ACW keep rules.");
				StringAssert.Contains ("-keep class mono.MonoRuntimeProvider", configuration);
				StringAssert.DoesNotContain ("-keep class com.example.Peer", configuration);
				StringAssert.DoesNotContain ("-dontobfuscate", configuration);
				Assert.That (response, Does.Not.Contain ("--no-minification"));

				foreach (string configurationFile in configurationFiles) {
					if (configurationFile != customConfiguration && configurationFile != commonConfiguration) {
						File.Delete (configurationFile);
					}
				}
			} finally {
				if (File.Exists (responseFile)) {
					File.Delete (responseFile);
				}
				Directory.Delete (path, recursive: true);
			}
		}

		[Test]
		public void ValidateAppliedMappingUsesXA4327 ()
		{
			string path = Path.Combine (Path.GetTempPath (), Guid.NewGuid ().ToString ("N"));
			Directory.CreateDirectory (path);
			try {
				string seedMapping = Path.Combine (path, "seed-mapping.txt");
				string finalMapping = Path.Combine (path, "final-mapping.txt");
				string rewriteManifest = Path.Combine (path, "rewrite-manifest.txt");
				string reachabilityManifest = Path.Combine (path, "reachability-manifest.txt");
				File.WriteAllText (seedMapping, "acme.orig.MyView -> a.b.C:\n");
				File.WriteAllText (finalMapping, "acme.orig.MyView -> a.b.D:\n");
				File.WriteAllText (rewriteManifest, "C\tacme/orig/MyView\n");
				File.WriteAllText (reachabilityManifest, "");

				var errors = new List<BuildErrorEventArgs> ();
				var task = new R8 {
					BuildEngine = new MockBuildEngine (TestContext.Out, errors),
					ProguardMappingFileInput = seedMapping,
					ProguardMappingFileOutput = finalMapping,
					ProguardMappingRequiredEntriesFile = rewriteManifest,
					ProguardMappingRequiredReachabilityEntriesFile = reachabilityManifest,
				};

				Assert.IsFalse (task.ValidateAppliedMapping (), "A conflicting final mapping should fail validation.");
				Assert.That (errors, Has.Count.EqualTo (1));
				Assert.AreEqual ("XA4327", errors [0].Code);
			} finally {
				Directory.Delete (path, recursive: true);
			}
		}

		internal class R8TestTask : R8
		{
			public string ResponseFilePath { get; private set; } = "";

			protected override string CreateResponseFile ()
			{
				ResponseFilePath = base.CreateResponseFile ();
				return ResponseFilePath;
			}

			public string TestGenerateCommandLineCommands ()
				=> GetCommandLineBuilder ().ToString ();
		}
	}
}
