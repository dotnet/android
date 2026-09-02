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
				string aarConfiguration = Path.Combine (path, "aar-proguard.txt");
				string manifestConfiguration = Path.Combine (path, "manifest-rules.txt");
				File.WriteAllText (acwMap, "Managed.Peer;com.example.Peer");
				File.WriteAllText (customConfiguration, "-dontwarn com.example.**");
				File.WriteAllText (aarConfiguration, "-dontwarn com.example.library.**");
				File.WriteAllText (manifestConfiguration, """
					-keep class com.example.MainActivity { <init>(); }
					""");
				var aarConfigurationItem = new TaskItem (aarConfiguration);
				aarConfigurationItem.SetMetadata ("OriginalFile", Path.Combine (path, "library.aar"));
				var manifestConfigurationItem = new TaskItem (manifestConfiguration);
				manifestConfigurationItem.SetMetadata ("AndroidGeneratedProguardConfiguration", "true");
				manifestConfigurationItem.SetMetadata ("AndroidManifestProguardConfiguration", "true");

				var task = new R8TestTask {
					BuildEngine = new MockBuildEngine (TestContext.Out),
					JarPath = "r8.jar",
					JavaPlatformJarPath = "android.jar",
					OutputDirectory = path,
					AcwMapFile = acwMap,
					ProguardGeneratedApplicationConfiguration = applicationConfiguration,
					ProguardCommonXamarinConfiguration = commonConfiguration,
					ProguardMappingFileOutput = Path.Combine (path, "mapping.txt"),
					ProguardConfigurationFiles = new ITaskItem [] { new TaskItem (customConfiguration), aarConfigurationItem, manifestConfigurationItem },
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
				Assert.That (configurationFiles, Does.Contain (aarConfiguration), "Seed R8 should honor AAR consumer rules.");
				Assert.That (configurationFiles, Does.Contain (commonConfiguration), "Seed R8 should honor runtime keep rules.");
				Assert.That (configurationFiles, Does.Contain (manifestConfiguration), "Seed R8 should preserve manifest entry names.");
				Assert.That (configurationFiles, Does.Not.Contain (applicationConfiguration), "Seed R8 must not pass the ACW keep configuration.");
				FileAssert.DoesNotExist (applicationConfiguration, "Seed R8 must not generate obfuscation-blocking ACW keep rules.");
				StringAssert.Contains ("-keep class mono.MonoRuntimeProvider", configuration);
				StringAssert.Contains ("-keep class com.example.MainActivity", configuration);
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
					EnableObfuscation = true,
					EnableShrinking = true,
					JarPath = "r8.jar",
					JavaPlatformJarPath = "android.jar",
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

		[Test]
		public void ValidateAppliedMappingAllowsIdentityManifestEntry ()
		{
			string path = Path.Combine (Path.GetTempPath (), Guid.NewGuid ().ToString ("N"));
			Directory.CreateDirectory (path);
			try {
				string seedMapping = Path.Combine (path, "seed-mapping.txt");
				string finalMapping = Path.Combine (path, "final-mapping.txt");
				string rewriteManifest = Path.Combine (path, "rewrite-manifest.txt");
				string reachabilityManifest = Path.Combine (path, "reachability-manifest.txt");
				const string mapping = """
					com.example.MainActivity -> com.example.MainActivity:
					com.example.Peer -> a:
					""";
				File.WriteAllText (seedMapping, mapping);
				File.WriteAllText (finalMapping, mapping);
				File.WriteAllText (rewriteManifest, "C\tcom/example/MainActivity\nC\tcom/example/Peer\n");
				File.WriteAllText (reachabilityManifest, "");

				var errors = new List<BuildErrorEventArgs> ();
				var task = new R8 {
					BuildEngine = new MockBuildEngine (TestContext.Out, errors),
					ProguardMappingFileInput = seedMapping,
					ProguardMappingFileOutput = finalMapping,
					ProguardMappingRequiredEntriesFile = rewriteManifest,
					ProguardMappingRequiredReachabilityEntriesFile = reachabilityManifest,
				};

				Assert.IsTrue (task.ValidateAppliedMapping (), "Matching identity and obfuscated mappings should pass validation.");
				Assert.IsEmpty (errors);
			} finally {
				Directory.Delete (path, recursive: true);
			}
		}

		[Test]
		public void R8JniObfuscationFiltersOnlySdkBaselineNativeKeepRule ()
		{
			string path = Path.Combine (Path.GetTempPath (), Guid.NewGuid ().ToString ("N"));
			Directory.CreateDirectory (path);
			string responseFile = "";
			try {
				string seedMapping = Path.Combine (path, "mapping.txt");
				string baseline = Path.Combine (path, "proguard-android.txt");
				string user = Path.Combine (path, "user.pro");
				string aar = Path.Combine (path, "aar-consumer.pro");
				string generated = Path.Combine (path, "proguard_project_references.cfg");
				string generatedAcwKeep = Path.Combine (path, "generated-acw-keep.cfg");
				string aapt = Path.Combine (path, "aapt_rules.txt");
				File.WriteAllText (seedMapping, "com.example.Peer -> a:\n");
				string nativeKeepRule = """
					-keepclasseswithmembernames,includedescriptorclasses class * {
					    native <methods>;
					}
					""";
				File.WriteAllText (baseline, $"-dontwarn before\n{nativeKeepRule}\n-dontwarn after\n");
				File.WriteAllText (user, nativeKeepRule + "\n");
				File.WriteAllText (aar, "-dontwarn com.example.library.**\n");
				File.WriteAllText (generated, "-keep,allowobfuscation class com.example.Peer\n");
				File.WriteAllText (generatedAcwKeep, "-keep class com.example.Peer { *; }\n");
				File.WriteAllText (aapt, "-keep class com.example.MainActivity { <init>(); }\n");
				var baselineItem = new TaskItem (baseline);
				baselineItem.SetMetadata ("AndroidSdkBaselineProguardConfiguration", "true");
				var aarItem = new TaskItem (aar);
				aarItem.SetMetadata ("OriginalFile", Path.Combine (path, "library.aar"));
				var generatedItem = new TaskItem (generated);
				generatedItem.SetMetadata ("AndroidGeneratedProguardConfiguration", "true");
				generatedItem.SetMetadata ("AndroidR8JniMappedProguardConfiguration", "true");
				var generatedAcwKeepItem = new TaskItem (generatedAcwKeep);
				generatedAcwKeepItem.SetMetadata ("AndroidGeneratedProguardConfiguration", "true");
				var aaptItem = new TaskItem (aapt);
				aaptItem.SetMetadata ("AndroidGeneratedProguardConfiguration", "true");
				aaptItem.SetMetadata ("AndroidAaptProguardConfiguration", "true");
				var task = CreateR8TestTask (
					path,
					new ITaskItem [] { baselineItem, new TaskItem (user), aarItem, generatedItem, generatedAcwKeepItem, aaptItem },
					enableObfuscation: true,
					seedMapping);

				task.TestGenerateCommandLineCommands ();
				responseFile = task.ResponseFilePath;
				string [] configurationFiles = GetConfigurationFiles (responseFile);
				string filteredBaseline = configurationFiles.Single (file =>
					file != user && file != aar && file != generated && file != aapt &&
					File.ReadAllText (file).Contains ("-dontwarn before"));
				string allConfiguration = string.Join ("\n", configurationFiles.Select (File.ReadAllText));

				Assert.AreNotEqual (baseline, filteredBaseline);
				Assert.AreEqual ("-dontwarn before\n-dontwarn after\n", File.ReadAllText (filteredBaseline));
				Assert.That (configurationFiles, Does.Contain (user), "User rules with identical text must remain untouched.");
				Assert.That (configurationFiles, Does.Contain (aar), "AAR consumer rules must remain untouched.");
				Assert.That (configurationFiles, Does.Contain (generated), "Mapped linked-assembly rules must reach final R8.");
				Assert.That (configurationFiles, Does.Contain (aapt), "Final AAPT manifest and resource rules must reach final R8.");
				Assert.That (configurationFiles, Does.Not.Contain (generatedAcwKeep), "Generated legacy ACW keep rules must not reach final R8.");
				StringAssert.Contains ("-applymapping", allConfiguration);
				StringAssert.Contains ("-keep,allowobfuscation class com.example.Peer", allConfiguration);
				StringAssert.Contains ("-keep class com.example.MainActivity", allConfiguration);
				Assert.AreEqual (1, allConfiguration.Split (new [] { "native <methods>;" }, StringSplitOptions.None).Length - 1,
					"Only the user-authored native rule should remain.");

				DeleteTemporaryConfigurations (configurationFiles, user, aar, generated, aapt);
			} finally {
				if (File.Exists (responseFile)) {
					File.Delete (responseFile);
				}
				Directory.Delete (path, recursive: true);
			}
		}

		[TestCase (false, true)]
		[TestCase (true, false)]
		public void GeneratedApplicationConfigurationOmitsManagedAcwsOnlyForJniObfuscation (bool enableObfuscation, bool expectManagedAcw)
		{
			string path = Path.Combine (Path.GetTempPath (), Guid.NewGuid ().ToString ("N"));
			Directory.CreateDirectory (path);
			string responseFile = "";
			try {
				string acwMap = Path.Combine (path, "acw-map.txt");
				string javaSource = Path.Combine (path, "UserJava.java");
				string applicationConfiguration = Path.Combine (path, "proguard_project_primary.cfg");
				File.WriteAllText (acwMap, "Managed.Peer;com.example.ManagedPeer\n");
				File.WriteAllText (javaSource, "package com.example; public class UserJava {}\n");
				var task = new R8TestTask {
					BuildEngine = new MockBuildEngine (TestContext.Out),
					AcwMapFile = acwMap,
					EnableObfuscation = enableObfuscation,
					EnableShrinking = true,
					JarPath = "r8.jar",
					JavaSourceFiles = new ITaskItem [] { new TaskItem (javaSource) },
					JavaPlatformJarPath = "android.jar",
					OutputDirectory = path,
					ProguardGeneratedApplicationConfiguration = applicationConfiguration,
				};

				task.TestGenerateCommandLineCommands ();
				responseFile = task.ResponseFilePath;
				string configuration = File.ReadAllText (applicationConfiguration);

				Assert.AreEqual (expectManagedAcw, configuration.Contains ("-keep class com.example.ManagedPeer", StringComparison.Ordinal));
				StringAssert.Contains ("-keep class com.example.UserJava { *; }", configuration);
			} finally {
				if (File.Exists (responseFile)) {
					File.Delete (responseFile);
				}
				Directory.Delete (path, recursive: true);
			}
		}

		[Test]
		public void R8WithoutJniObfuscationPassesSdkBaselineUnchanged ()
		{
			string path = Path.Combine (Path.GetTempPath (), Guid.NewGuid ().ToString ("N"));
			Directory.CreateDirectory (path);
			string responseFile = "";
			try {
				string baseline = Path.Combine (path, "proguard-android.txt");
				string generatedAcwKeep = Path.Combine (path, "generated-acw-keep.cfg");
				string content = "-keepclasseswithmembernames,includedescriptorclasses class * {\n    native <methods>;\n}\n";
				File.WriteAllText (baseline, content);
				File.WriteAllText (generatedAcwKeep, "-keep class com.example.Peer { *; }\n");
				var baselineItem = new TaskItem (baseline);
				baselineItem.SetMetadata ("AndroidSdkBaselineProguardConfiguration", "true");
				var generatedAcwKeepItem = new TaskItem (generatedAcwKeep);
				generatedAcwKeepItem.SetMetadata ("AndroidGeneratedProguardConfiguration", "true");
				var task = CreateR8TestTask (path, new ITaskItem [] { baselineItem, generatedAcwKeepItem }, enableObfuscation: false);

				task.TestGenerateCommandLineCommands ();
				responseFile = task.ResponseFilePath;
				string [] configurationFiles = GetConfigurationFiles (responseFile);

				Assert.That (configurationFiles, Does.Contain (baseline));
				Assert.That (configurationFiles, Does.Contain (generatedAcwKeep), "Feature-off builds must retain generated ACW keep rules.");
				Assert.AreEqual (content, File.ReadAllText (baseline));
			} finally {
				if (File.Exists (responseFile)) {
					File.Delete (responseFile);
				}
				Directory.Delete (path, recursive: true);
			}
		}

		static R8TestTask CreateR8TestTask (string path, ITaskItem [] configurations, bool enableObfuscation, string? seedMapping = null)
			=> new R8TestTask {
				BuildEngine = new MockBuildEngine (TestContext.Out),
				JarPath = "r8.jar",
				JavaPlatformJarPath = "android.jar",
				OutputDirectory = path,
				ProguardMappingFileInput = seedMapping,
				ProguardConfigurationFiles = configurations,
				EnableShrinking = true,
				EnableObfuscation = enableObfuscation,
			};

		static string [] GetConfigurationFiles (string responseFile)
		{
			string [] response = File.ReadAllLines (responseFile);
			return response
				.Select ((argument, index) => (argument, index))
				.Where (entry => entry.argument == "--pg-conf")
				.Select (entry => response [entry.index + 1])
				.ToArray ();
		}

		static void DeleteTemporaryConfigurations (IEnumerable<string> configurationFiles, params string [] retainedFiles)
		{
			foreach (string configurationFile in configurationFiles) {
				if (!retainedFiles.Contains (configurationFile, StringComparer.Ordinal)) {
					File.Delete (configurationFile);
				}
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
