using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NUnit.Framework;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	public class R8Tests : BaseTest
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

		[TestCase (false)]
		[TestCase (true)]
		public void R8JniObfuscationExplicitlyKeepsRuntimeOwnedJniTypes (bool nativeAot)
		{
			string path = Path.Combine (Path.GetTempPath (), Guid.NewGuid ().ToString ("N"));
			Directory.CreateDirectory (path);
			string responseFile = "";
			try {
				string acwMap = Path.Combine (path, "acw-map.txt");
				string applicationConfiguration = Path.Combine (path, "application.cfg");
				string commonConfiguration = Path.Combine (path, "xamarin.cfg");
				File.WriteAllText (acwMap, "Managed.GeneratedPeer;com.example.GeneratedPeer\n");
				var task = new R8TestTask {
					AcwMapFile = acwMap,
					BuildEngine = new MockBuildEngine (TestContext.Out),
					EnableShrinking = true,
					JarPath = "r8.jar",
					JavaPlatformJarPath = "android.jar",
					ObfuscationMode = "private-members",
					OutputDirectory = path,
					ProguardCommonXamarinConfiguration = commonConfiguration,
					ProguardGeneratedApplicationConfiguration = applicationConfiguration,
					UseTrimmableNativeAotProguardConfiguration = nativeAot,
				};

				task.TestGenerateCommandLineCommands ();
				responseFile = task.ResponseFilePath;
				string configuration = File.ReadAllText (commonConfiguration) + File.ReadAllText (applicationConfiguration);
				var keepTargets = Regex.Matches (configuration, @"^-keep (?:class|interface) (?<name>[^\s{]+)", RegexOptions.Multiline)
					.Cast<Match> ()
					.Select (match => match.Groups ["name"].Value)
					.ToHashSet (StringComparer.Ordinal);

				foreach (string jniName in GetNativeRuntimeJniTypeNames ()) {
					string javaName = jniName.Replace ('/', '.');
					Assert.That (keepTargets, Does.Contain (javaName), $"Runtime JNI type `{javaName}` must have an explicit keep rule.");
				}

				StringAssert.DoesNotContain ("-keep class net.dot.jni.**", configuration);
				StringAssert.DoesNotContain ("-keep class mono.android.**", configuration);
				StringAssert.Contains ("void monodroidAddReference(java.lang.Object);", configuration);
				StringAssert.Contains ("void monodroidClearReferences();", configuration);
				StringAssert.Contains ("public static native void registerNativeMembers(java.lang.Class,java.lang.String);", configuration);
				StringAssert.Contains ("public static native void construct(java.lang.Object,java.lang.String,java.lang.Object[]);", configuration);
				StringAssert.DoesNotContain ("com.example.GeneratedPeer", configuration,
					"An ordinary generated app peer must remain eligible for R8 obfuscation.");

				if (nativeAot) {
					Assert.That (keepTargets, Does.Contain ("net.dot.jni.nativeaot.JavaInteropRuntime"));
					Assert.That (keepTargets, Does.Contain ("net.dot.jni.nativeaot.NativeAotRuntimeProvider*"));
					StringAssert.Contains ("public static native void init(java.lang.ClassLoader,java.lang.String,java.lang.String,java.lang.String);", configuration);
				} else {
					Assert.That (keepTargets, Does.Not.Contain ("net.dot.jni.nativeaot.JavaInteropRuntime"));
					Assert.That (keepTargets, Does.Not.Contain ("net.dot.jni.nativeaot.NativeAotRuntimeProvider*"));
				}
			} finally {
				if (File.Exists (responseFile)) {
					File.Delete (responseFile);
				}
				Directory.Delete (path, recursive: true);
			}
		}

		[TestCase (false)]
		[TestCase (true)]
		public void R8WithoutJniObfuscationRetainsBroadRuntimeKeepRules (bool nativeAot)
		{
			string path = Path.Combine (Path.GetTempPath (), Guid.NewGuid ().ToString ("N"));
			Directory.CreateDirectory (path);
			string responseFile = "";
			try {
				string commonConfiguration = Path.Combine (path, "xamarin.cfg");
				var task = new R8TestTask {
					BuildEngine = new MockBuildEngine (TestContext.Out),
					EnableShrinking = true,
					JarPath = "r8.jar",
					JavaPlatformJarPath = "android.jar",
					ObfuscationMode = "disabled",
					OutputDirectory = path,
					ProguardCommonXamarinConfiguration = commonConfiguration,
					UseTrimmableNativeAotProguardConfiguration = nativeAot,
				};

				task.TestGenerateCommandLineCommands ();
				responseFile = task.ResponseFilePath;
				string configuration = File.ReadAllText (commonConfiguration);

				StringAssert.Contains ("-dontobfuscate", configuration);
				StringAssert.Contains ("-keep class net.dot.jni.**", configuration);
				StringAssert.Contains ("-keep class mono.android.Runtime { *; }", configuration);
				if (nativeAot) {
					StringAssert.DoesNotContain ("-keep class mono.android.**", configuration);
				} else {
					StringAssert.Contains ("-keep class mono.android.**", configuration);
				}
			} finally {
				if (File.Exists (responseFile)) {
					File.Delete (responseFile);
				}
				Directory.Delete (path, recursive: true);
			}
		}

		IEnumerable<string> GetNativeRuntimeJniTypeNames ()
		{
			string sourceRoot = GetAssemblyMetadataValue ("XamarinAndroidSourcePath");
			string headerPath = Path.Combine (sourceRoot, "src", "native", "common", "include", "shared", "runtime-jni-names.hh");
			string header = File.ReadAllText (headerPath);
			string [] names = Regex.Matches (header, @"std::string_view \w+ \{ ""(?<value>[^""]+)"" \};")
				.Cast<Match> ()
				.Select (match => match.Groups ["value"].Value)
				.ToArray ();
			var jniTypes = names
				.Where (name => name.Contains ('/'))
				.ToHashSet (StringComparer.Ordinal);

			string runtimeJavaPath = Path.Combine (sourceRoot, "src", "java-runtime", "java", "mono", "android", "Runtime.java");
			string runtimeJava = File.ReadAllText (runtimeJavaPath);
			foreach (string fieldName in names.Where (name => name.StartsWith ("mono_android_", StringComparison.Ordinal) || name.StartsWith ("net_dot_jni_", StringComparison.Ordinal))) {
				Match field = Regex.Match (runtimeJava, $@"static java\.lang\.Class {Regex.Escape (fieldName)} = (?<type>[\w.]+)\.class;");
				Assert.That (field.Success, Is.True, $"Runtime field `{fieldName}` must resolve to a Java class.");
				jniTypes.Add (field.Groups ["type"].Value.Replace ('.', '/'));
			}

			foreach (string directory in new [] {
				Path.Combine (sourceRoot, "src", "native", "clr"),
				Path.Combine (sourceRoot, "src", "native", "nativeaot"),
			}) {
				foreach (string file in Directory.EnumerateFiles (directory, "*.cc", SearchOption.AllDirectories)) {
					string source = File.ReadAllText (file);
					Assert.That (Regex.IsMatch (source, @"FindClass\s*\(\s*""(?:mono/|net/dot/)", RegexOptions.CultureInvariant), Is.False,
						$"SDK-owned FindClass names in `{file}` must use RuntimeJniNames and explicit keep coverage.");
					Assert.That (Regex.IsMatch (source, @"get_class_from_runtime_field\s*\([^;]*""(?:mono_android_|net_dot_jni_)", RegexOptions.CultureInvariant), Is.False,
						$"SDK-owned runtime fields in `{file}` must use RuntimeJniNames and explicit keep coverage.");
				}
			}

			string javaInteropPath = Path.Combine (sourceRoot, "external", "Java.Interop", "src", "Java.Interop", "Java.Interop");
			foreach (string file in Directory.EnumerateFiles (javaInteropPath, "*.cs", SearchOption.TopDirectoryOnly)) {
				string source = File.ReadAllText (file);
				foreach (Match match in Regex.Matches (source, @"JniTypeName\s*=\s*""(?<name>net/dot/jni/(?:ManagedPeer|internal/JavaProxy(?:Object|Throwable)))""")) {
					jniTypes.Add (match.Groups ["name"].Value);
				}
			}

			Assert.That (jniTypes, Does.Contain ("mono/android/Runtime"),
				"CoreCLR JNI exports and NativeAOT startup require mono.android.Runtime to remain stable.");
			return jniTypes;
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
