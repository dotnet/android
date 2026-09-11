using System;
using System.IO;
using System.Linq;
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

		[TestCase ("disabled", true, false)]
		[TestCase ("private-members", false, false)]
		[TestCase ("runtime-remapping", false, false)]
		[TestCase ("disabled", true, true)]
		[TestCase ("private-members", false, true)]
		[TestCase ("runtime-remapping", false, true)]
		public void GenerateCommonXamarinConfiguration_RespectsObfuscationMode (string obfuscationMode, bool expectDontObfuscate, bool nativeAot)
		{
			var path = Path.GetTempFileName ();
			try {
				var task = new R8 {
					BuildEngine = new MockBuildEngine (TestContext.Out),
					ObfuscationMode = obfuscationMode,
					UseTrimmableNativeAotProguardConfiguration = nativeAot,
					ProguardCommonXamarinConfiguration = path,
				};
				task.GenerateCommonXamarinConfiguration ();

				var lines = File.ReadAllLines (path);
				Assert.AreEqual (expectDontObfuscate, lines.Any (l => l.Trim () == "-dontobfuscate"),
					"Only disabled mode should prevent all obfuscation.");
				Assert.IsTrue (lines.Any (l => l.Contains ("-keep class net.dot.jni.")),
					"Shared bootstrap rules must survive.");
				Assert.AreEqual (obfuscationMode == "private-members",
					lines.Contains ("-keep,allowshrinking,allowoptimization class **"));
				Assert.AreEqual (obfuscationMode == "runtime-remapping",
					lines.Contains ("-keepclassmembernames interface * { *; }"),
					"Remapping-specific rules must not change the existing modes.");
				CollectionAssert.Contains (lines, "-keep class net.dot.android.ApplicationRegistration { *; }");
				if (nativeAot) {
					CollectionAssert.Contains (lines, "-keep class mono.android.IGCUserPeer { *; }");
					if (obfuscationMode == "runtime-remapping") {
						CollectionAssert.Contains (lines, "-keep class mono.android.Runtime { *; }");
						CollectionAssert.Contains (lines, "-keep class mono.android.GCUserPeer { <init>(); }");
					}
				}
			} finally {
				File.Delete (path);
			}
		}

		[Test]
		public void GenerateCommonXamarinConfiguration_RejectsUnknownObfuscationMode ()
		{
			var path = Path.GetTempFileName ();
			var task = new R8 {
				BuildEngine = new MockBuildEngine (TestContext.Out),
				ObfuscationMode = "unknown",
				ProguardCommonXamarinConfiguration = path,
			};
			try {
				Assert.Throws<InvalidOperationException> (() => task.GenerateCommonXamarinConfiguration ());
			} finally {
				File.Delete (path);
			}
		}

		[Test]
		public void WritePrivateMemberObfuscationRules ()
		{
			using var writer = new StringWriter ();
			R8.WriteObfuscationRules (writer, "private-members");

			var expected = """
				-keep,allowshrinking,allowoptimization class **
				-keepclassmembers,allowshrinking,allowoptimization class ** {
				   public protected *;
				}
				-keep,allowoptimization interface ** {
				   public protected *;
				}
				-keep,allowshrinking class * implements **

				""";
			Assert.AreEqual (expected.ReplaceLineEndings (System.Environment.NewLine), writer.ToString ());
		}

		[Test]
		public void WriteDisabledObfuscationRules ()
		{
			using var writer = new StringWriter ();
			R8.WriteObfuscationRules (writer, "disabled");

			Assert.AreEqual ("-dontobfuscate" + System.Environment.NewLine, writer.ToString ());
		}
	}
}
