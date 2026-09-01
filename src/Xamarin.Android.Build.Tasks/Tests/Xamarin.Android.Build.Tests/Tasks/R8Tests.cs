using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
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
	}
}
