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

		[TestCase ("package com.example.app;\npublic class Foo {}",            "com.example.app")]		[TestCase ("package com.example.app ;\npublic class Foo {}",           "com.example.app")] // space before ';'
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

		[TestCase (false, "-keep")]
		[TestCase (true,  "-keep,allowobfuscation")]
		public void KeepOption (bool enableObfuscation, string expected)
		{
			var task = new R8 { EnableObfuscation = enableObfuscation };
			Assert.AreEqual (expected, task.KeepOption);
		}

		[TestCase (false, true, false)]
		[TestCase (true, false, false)]
		[TestCase (false, true, true)]
		[TestCase (true, false, true)]
		public void GenerateCommonXamarinConfiguration_OnlyDropsDontObfuscate (bool enableObfuscation, bool expectDontObfuscate, bool nativeAot)
		{
			var path = Path.GetTempFileName ();
			try {
				var task = new R8 {
					BuildEngine = new MockBuildEngine (TestContext.Out),
					EnableObfuscation = enableObfuscation,
					UseTrimmableNativeAotProguardConfiguration = nativeAot,
					ProguardCommonXamarinConfiguration = path,
				};
				task.GenerateCommonXamarinConfiguration ();

				var lines = File.ReadAllLines (path);
				Assert.AreEqual (expectDontObfuscate, lines.Any (l => l.Trim () == "-dontobfuscate"),
					"-dontobfuscate is the only option that may be dropped.");
				Assert.IsTrue (lines.Any (l => l.Contains ("-keep class net.dot.jni.")),
					"Every other rule must survive.");
				if (nativeAot) {
					CollectionAssert.Contains (lines, "-keep class net.dot.android.ApplicationRegistration { *; <init>(...); }");
					CollectionAssert.Contains (lines, "-keep class mono.android.Runtime { *; }");
					CollectionAssert.Contains (lines, "-keep class mono.android.GCUserPeer { <init>(); }");
					CollectionAssert.Contains (lines, "-keep class mono.android.IGCUserPeer { *; }");
				}
			} finally {
				File.Delete (path);
			}
		}
	}
}
