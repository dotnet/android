using System.IO;
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

		[TestCase (false)]
		[TestCase (true)]
		public void RetainedTypeMapRulesDoNotRootAllAcwsOrObfuscatePrivateMembers (bool scopedMembers)
		{
			var directory = Path.Combine (Path.GetTempPath (), "R8TypeMap_" + System.Guid.NewGuid ().ToString ("N"));
			Directory.CreateDirectory (directory);
			try {
				var map = Path.Combine (directory, "acw-map.txt");
				File.WriteAllText (map, "Unused.Type;unused.Wrapper\n");
				var source = Path.Combine (directory, "UserSource.java");
				File.WriteAllText (source, "package example;\npublic class UserSource {}");
				var task = new R8ResponseTestTask {
					BuildEngine = new MockBuildEngine (TestContext.Out),
					UseTypeMapProguardConfiguration = true,
					UseScopedTypeMapMembers = scopedMembers,
					EnableShrinking = true,
					ObfuscationMode = "private-members",
					AcwMapFile = map,
					JavaSourceFiles = [new TaskItem (source)],
					JavaPlatformJarPath = Path.Combine (directory, "android.jar"),
					ProguardGeneratedApplicationConfiguration = Path.Combine (directory, "primary.cfg"),
					ProguardCommonXamarinConfiguration = Path.Combine (directory, "common.cfg"),
					ResponseFile = Path.Combine (directory, "r8.rsp"),
				};
				var response = task.WriteResponse ();
				StringAssert.Contains ("--no-minification", response);
				StringAssert.DoesNotContain ("--no-tree-shaking", response);
				var primary = File.ReadAllText (task.ProguardGeneratedApplicationConfiguration);
				StringAssert.DoesNotContain ("unused.Wrapper", primary);
				StringAssert.Contains ("-keep class example.UserSource { *; }", primary);
				var common = File.ReadAllText (task.ProguardCommonXamarinConfiguration);
				StringAssert.Contains ("-dontobfuscate", common);
				StringAssert.DoesNotContain ("-keep,allowshrinking,allowoptimization class **", common);
				StringAssert.DoesNotContain ("-keep class mono.android.**", common);
				if (scopedMembers) {
					StringAssert.DoesNotContain ("-keepclassmembers class * {", common);
					StringAssert.Contains ("-keep class mono.android.Runtime { *; }", common);
					StringAssert.Contains ("-keep class net.dot.jni.ManagedPeer { *; }", common);
					StringAssert.Contains ("-keep interface mono.android.IGCUserPeer { *; }", common);
				} else {
					StringAssert.Contains ("-keepclassmembers class * {", common);
				}
			} finally {
				Directory.Delete (directory, recursive: true);
			}
		}

		sealed class R8ResponseTestTask : R8
		{
			public string ResponseFile { get; set; } = "";

			protected override string CreateResponseFilePath () => ResponseFile;

			public string WriteResponse () => File.ReadAllText (CreateResponseFile ());
		}
	}
}
