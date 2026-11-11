using System;
using System.IO;
using Microsoft.Build.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests
{
	[TestClass]
	[DoNotParallelize]
	public class R8Tests
	{
		[TestMethod]
		[DataRow ("-keep class com.example.Foo { *; }", false, "")]
		[DataRow ("-dontwarn com.example.**",           false, "")]
		[DataRow ("# -printmapping comment",            false, "")]
		[DataRow ("",                                   false, "")]
		[DataRow ("-printmappingFoo foo.txt",           false, "")] // token-boundary: must not match -printmapping
		[DataRow ("-dumpsterfire",                      false, "")] // token-boundary: must not match -dump
		[DataRow ("-printmapping mapping.txt",          true,  "-printmapping")]
		[DataRow ("-printmapping",                      true,  "-printmapping")] // option with no argument
		[DataRow ("  -printmapping mapping.txt",        true,  "-printmapping")]
		[DataRow ("\t-printseeds seeds.txt",            true,  "-printseeds")]
		[DataRow ("-printusage usage.txt",              true,  "-printusage")]
		[DataRow ("-printconfiguration config.txt",     true,  "-printconfiguration")]
		[DataRow ("-dump dump.txt",                     true,  "-dump")]
		[DataRow ("-dontoptimize",                      true,  "-dontoptimize")]
		[DataRow ("-dontobfuscate",                     true,  "-dontobfuscate")]
		[DataRow ("-PrintMapping mapping.txt",          true,  "-printmapping")] // case-insensitive
		[DataRow ("-DUMP dump.txt",                     true,  "-dump")]
		[DataRow ("-DontOptimize",                      true,  "-dontoptimize")]
		public void TryGetDisallowedOption (string line, bool expected, string expectedOption)
		{
			var actual = R8.TryGetDisallowedOption (line, out var option);
			Assert.AreEqual (expected, actual);
			Assert.AreEqual (expectedOption, option);
		}

		[TestMethod]
		[DataRow ("package com.example.app;\npublic class Foo {}",            "com.example.app")]
		[DataRow ("package com.example.app ;\npublic class Foo {}",           "com.example.app")] // space before ';'
		[DataRow ("// header\n/* license */\npackage com.example.app;\nclass Foo {}", "com.example.app")] // skip comments
		[DataRow ("public class Foo {}",                                     null)] // no package
		[DataRow ("import java.util.List;\npackage com.late;\nclass Foo {}", null)] // package after import is ignored
		[DataRow ("class Foo {\npackage com.late;\n}",                       null)] // package after type is ignored
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

		[TestMethod]
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

		[TestMethod]
		public void WriteDisabledObfuscationRules ()
		{
			using var writer = new StringWriter ();
			R8.WriteObfuscationRules (writer, "disabled");

			Assert.AreEqual ("-dontobfuscate" + System.Environment.NewLine, writer.ToString ());
		}

		[TestMethod]
		public void WriteRuntimeRemappingRules ()
		{
			using var writer = new StringWriter ();
			R8.WriteObfuscationRules (writer, "runtime-remapping");

			var rules = writer.ToString ();
			Assert.DoesNotContain ("-dontobfuscate", rules);
			Assert.Contains ("-keepclassmembernames interface * { *; }", rules);
			Assert.Contains ("-keepclassmembernames,includedescriptorclasses class * { native <methods>; }", rules);
			Assert.Contains ("-keep class mono.android.Runtime { *; }", rules);
			Assert.Contains ("-keepclassmembernames class * { <fields>; }", rules);
		}

		[TestMethod]
		public void RuntimeRemappingKeepsAcwsRenameable ()
		{
			var task = new R8 {
				ObfuscationMode = "runtime-remapping",
			};

			Assert.AreEqual ("-keep,allowobfuscation", task.KeepOption);
		}

		[TestMethod]
		public void RuntimeRemappingDoesNotDisableTypeMapMinification ()
		{
			var directory = Path.Combine (Path.GetTempPath (), "R8RuntimeRemapping_" + System.Guid.NewGuid ().ToString ("N"));
			Directory.CreateDirectory (directory);
			try {
				var source = Path.Combine (directory, "UserSource.java");
				File.WriteAllText (source, "package example;\npublic class UserSource {}");
				var task = new R8ResponseTestTask {
					BuildEngine = new MockBuildEngine (Console.Out),
					UseTypeMapProguardConfiguration = true,
					UseScopedTypeMapMembers = true,
					EnableShrinking = true,
					ObfuscationMode = "runtime-remapping",
					JavaSourceFiles = [new TaskItem (source)],
					JavaPlatformJarPath = Path.Combine (directory, "android.jar"),
					ProguardGeneratedApplicationConfiguration = Path.Combine (directory, "primary.cfg"),
					ProguardCommonXamarinConfiguration = Path.Combine (directory, "common.cfg"),
					ResponseFile = Path.Combine (directory, "r8.rsp"),
				};

				var response = task.WriteResponse ();
				Assert.DoesNotContain ("--no-minification", response);
				Assert.Contains ("-keep,allowobfuscation class example.UserSource { *; }", File.ReadAllText (task.ProguardGeneratedApplicationConfiguration));
				var common = File.ReadAllText (task.ProguardCommonXamarinConfiguration);
				Assert.Contains ("-keepclassmembernames,includedescriptorclasses class * { native <methods>; }", common);
				Assert.Contains ("-keepclassmembernames class * { <fields>; }", common);
			} finally {
				Directory.Delete (directory, recursive: true);
			}
		}

		[TestMethod]
		[DataRow (false)]
		[DataRow (true)]
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
					BuildEngine = new MockBuildEngine (Console.Out),
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
				Assert.Contains ("--no-minification", response);
				Assert.DoesNotContain ("--no-tree-shaking", response);
				var primary = File.ReadAllText (task.ProguardGeneratedApplicationConfiguration);
				Assert.DoesNotContain ("unused.Wrapper", primary);
				Assert.Contains ("-keep class example.UserSource { *; }", primary);
				var common = File.ReadAllText (task.ProguardCommonXamarinConfiguration);
				Assert.Contains ("-dontobfuscate", common);
				Assert.DoesNotContain ("-keep,allowshrinking,allowoptimization class **", common);
				Assert.DoesNotContain ("-keep class mono.android.**", common);
				Assert.DoesNotContain ("net.dot.jni.ManagedPeer", common);
				Assert.DoesNotContain ("net.dot.jni.GCUserPeerable", common);
				Assert.DoesNotContain ("net.dot.jni.internal.JavaProxyObject", common);
				Assert.DoesNotContain ("net.dot.jni.internal.JavaProxyThrowable", common);
				if (scopedMembers) {
					Assert.DoesNotContain ("-keepclassmembers class * {", common);
					Assert.Contains ("-keep class mono.android.Runtime { *; }", common);
					Assert.Contains ("-keep interface mono.android.IGCUserPeer { *; }", common);
					Assert.Contains ("-keep,allowshrinking class * implements **", common);
				} else {
					Assert.Contains ("-keepclassmembers class * {", common);
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
