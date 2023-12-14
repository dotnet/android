using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using Microsoft.Build.Framework;
using Mono.Cecil;
using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.Android.Tools;
using Xamarin.ProjectTools;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Build.Tests
{
	[Category ("Node-1")]
	[Parallelizable (ParallelScope.Children)]
	public partial class BuildTest : BaseTest
	{
		static object [] MonoComponentMaskChecks () => new object [] {
			new object[] {
				true,  // enableProfiler
				true,  // useInterpreter
				true,  // debugBuild
				0x07U, // expectedMask
			},

			new object[] {
				true,  // enableProfiler
				false, // useInterpreter
				true,  // debugBuild
				0x05U, // expectedMask
			},

			new object[] {
				false, // enableProfiler
				false, // useInterpreter
				true,  // debugBuild
				0x01U, // expectedMask
			},

			new object[] {
				true,  // enableProfiler
				false, // useInterpreter
				false, // debugBuild
				0x04U, // expectedMask
			},
		};

		[Test]
		[TestCaseSource (nameof (MonoComponentMaskChecks))]
		public void CheckMonoComponentsMask (bool enableProfiler, bool useInterpreter, bool debugBuild, uint expectedMask)
		{
			if (!Builder.UseDotNet) {
				Assert.Ignore ("Valid only for NET6+ builds");
				return;
			}

			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = !debugBuild,
			};

			proj.SetProperty (proj.ActiveConfigurationProperties, "AndroidEnableProfiler", enableProfiler.ToString ());
			proj.SetProperty (proj.ActiveConfigurationProperties, "AndroidUseInterpreter", useInterpreter.ToString ());

			var abis = new [] { "armeabi-v7a", "x86" };
			proj.SetAndroidSupportedAbis (abis);

			using (var b = CreateApkBuilder ()) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				string objPath = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath);

				List<EnvironmentHelper.EnvironmentFile> envFiles = EnvironmentHelper.GatherEnvironmentFiles (objPath, String.Join (";", abis), true);
				EnvironmentHelper.ApplicationConfig app_config = EnvironmentHelper.ReadApplicationConfig (envFiles);
				Assert.That (app_config, Is.Not.Null, "application_config must be present in the environment files");
				Assert.IsTrue (app_config.mono_components_mask == expectedMask, "Expected Mono Components mask 0x{expectedMask:x}, got 0x{app_config.mono_components_mask:x}");
			}
		}

		static object [] CheckAssemblyCountsSource = new object [] {
			new object[] {
				/*isRelease*/ false,
				/*aot*/       false,
			},
			new object[] {
				/*isRelease*/ true,
				/*aot*/       false,
			},
			new object[] {
				/*isRelease*/ true,
				/*aot*/       true,
			},
		};

		[Test]
		[TestCaseSource (nameof (CheckAssemblyCountsSource))]
		[NonParallelizable]
		[Category ("SmokeTests")]
		public void CheckAssemblyCounts (bool isRelease, bool aot)
		{
			var proj = new XamarinFormsAndroidApplicationProject {
				IsRelease = isRelease,
				EmbedAssembliesIntoApk = true,
				AotAssemblies = aot,
			};
			proj.PackageReferences.Add (KnownPackages.AndroidXMigration);
			proj.PackageReferences.Add (KnownPackages.AndroidXAppCompat);
			proj.PackageReferences.Add (KnownPackages.AndroidXAppCompatResources);
			proj.PackageReferences.Add (KnownPackages.AndroidXBrowser);
			proj.PackageReferences.Add (KnownPackages.AndroidXMediaRouter);
			proj.PackageReferences.Add (KnownPackages.AndroidXLegacySupportV4);
			proj.PackageReferences.Add (KnownPackages.AndroidXLifecycleLiveData);
			proj.PackageReferences.Add (KnownPackages.XamarinGoogleAndroidMaterial);

			var abis = new [] { "armeabi-v7a", "x86" };
			proj.SetAndroidSupportedAbis (abis);
			proj.SetProperty (proj.ActiveConfigurationProperties, "AndroidUseAssemblyStore", "True");

			using (var b = CreateApkBuilder ()) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				string objPath = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath);

				List<EnvironmentHelper.EnvironmentFile> envFiles = EnvironmentHelper.GatherEnvironmentFiles (objPath, String.Join (";", abis), true);
				EnvironmentHelper.ApplicationConfig app_config = EnvironmentHelper.ReadApplicationConfig (envFiles);
				Assert.That (app_config, Is.Not.Null, "application_config must be present in the environment files");

				if (aot) {
					foreach (var env in envFiles) {
						StringAssert.Contains ("libaot-Mono.Android.dll.so", File.ReadAllText (env.Path));
					}
				}

				string apk = Path.Combine (Root, b.ProjectDirectory, proj.OutputPath, $"{proj.PackageName}-Signed.apk");
				var helper = new ArchiveAssemblyHelper (apk, useAssemblyStores: true);

				Assert.IsTrue (app_config.number_of_assemblies_in_apk == (uint)helper.GetNumberOfAssemblies (), "Assembly count must be equal between ApplicationConfig and the archive contents");
			}
		}

		// DotNet fails, see https://github.com/dotnet/runtime/issues/65484
		// Enable the commented out signature (and AOT) once the above is fixed
		[Test]
		[Category ("SmokeTests")]
		public void SmokeTestBuildWithSpecialCharacters ([Values (false, true)] bool forms, [Values (false /*, true*/)] bool aot)
		{
			var testName = "テスト";

			var rootPath = Path.Combine (Root, "temp", TestName);
			var proj = forms ?
				new XamarinFormsAndroidApplicationProject () :
				new XamarinAndroidApplicationProject ();
			proj.ProjectName = testName;
			proj.IsRelease = true;
			proj.AotAssemblies = aot;

			if (forms) {
				proj.PackageReferences.Clear ();
				proj.PackageReferences.Add (KnownPackages.XamarinForms_4_7_0_1142);
			}
			using (var builder = CreateApkBuilder (Path.Combine (rootPath, proj.ProjectName))){
				Assert.IsTrue (builder.Build (proj), "Build should have succeeded.");
			}
		}

		public static string GetLinkedPath (ProjectBuilder builder, bool isRelease, string filename)
		{
			return Builder.UseDotNet && isRelease ?
				builder.Output.GetIntermediaryPath (Path.Combine ("android-arm64", "linked", filename)) :
				builder.Output.GetIntermediaryPath (Path.Combine ("android", "assets", filename));
		}

		[Test]
		[TestCaseSource (nameof (RuntimeChecks))]
		public void CheckWhichRuntimeIsIncluded (string supportedAbi, bool debugSymbols, string debugType, bool? optimize, bool? embedAssemblies, string expectedRuntime) {
			var proj = new XamarinAndroidApplicationProject ();
			proj.SetAndroidSupportedAbis (supportedAbi);
			proj.SetProperty (proj.ActiveConfigurationProperties, "DebugSymbols", debugSymbols);
			proj.SetProperty (proj.ActiveConfigurationProperties, "DebugType", debugType);
			if (optimize.HasValue)
				proj.SetProperty (proj.ActiveConfigurationProperties, "Optimize", optimize.Value);
			else
				proj.RemoveProperty (proj.ActiveConfigurationProperties, "Optimize");
			if (embedAssemblies.HasValue)
				proj.SetProperty (proj.ActiveConfigurationProperties, KnownProperties.EmbedAssembliesIntoApk, embedAssemblies.Value);
			else
				proj.RemoveProperty (proj.ActiveConfigurationProperties, KnownProperties.EmbedAssembliesIntoApk);
			using (var b = CreateApkBuilder (Path.Combine ("temp", TestName))) {
				var runtimeInfo = b.GetSupportedRuntimes ();
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				var apkPath = Path.Combine (Root, b.ProjectDirectory,
					proj.OutputPath, $"{proj.PackageName}-Signed.apk");
				using (var apk = ZipHelper.OpenZip (apkPath)) {
					var runtime = runtimeInfo.FirstOrDefault (x => x.Abi == supportedAbi && x.Runtime == expectedRuntime);
					Assert.IsNotNull (runtime, "Could not find the expected runtime.");
					var inApk = ZipHelper.ReadFileFromZip (apk, $"lib/{supportedAbi}/{runtime.Name}");
					var inApkRuntime = runtimeInfo.FirstOrDefault (x => x.Abi == supportedAbi && x.Size == inApk.Length);
					Assert.IsNotNull (inApkRuntime, "Could not find the actual runtime used.");
					// TODO: file sizes will not match for .NET 5
					// TODO: libmono-profiler-log.so is not available in .NET 5 yet
					if (!Builder.UseDotNet) {
						Assert.AreEqual (runtime.Size, inApkRuntime.Size, "expected {0} got {1}", expectedRuntime, inApkRuntime.Runtime);
						inApk = ZipHelper.ReadFileFromZip (apk, $"lib/{supportedAbi}/libmono-profiler-log.so");
						if (string.Compare (expectedRuntime, "debug", StringComparison.OrdinalIgnoreCase) == 0) {
							if (inApk == null)
								Assert.Fail ("libmono-profiler-log.so should exist in the apk.");
						} else {
							if (inApk != null)
								Assert.Fail ("libmono-profiler-log.so should not exist in the apk.");
						}
					}
				}
			}
		}

		[Test]
		[Category ("AOT"), Category ("MonoSymbolicate")]
		[TestCaseSource (nameof (SequencePointChecks))]
		public void CheckSequencePointGeneration (bool isRelease, bool monoSymbolArchive, bool aotAssemblies,
		       bool debugSymbols, string debugType, bool embedMdb, string expectedRuntime, bool usesAssemblyBlobs)
		{
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = isRelease,
				AotAssemblies = aotAssemblies
			};
			var abis = new [] { "armeabi-v7a", "x86" };
			proj.SetAndroidSupportedAbis (abis);
			proj.SetProperty (proj.ActiveConfigurationProperties, "MonoSymbolArchive", monoSymbolArchive);
			proj.SetProperty (proj.ActiveConfigurationProperties, "DebugSymbols", debugSymbols);
			proj.SetProperty (proj.ActiveConfigurationProperties, "DebugType", debugType);
			proj.SetProperty (proj.ActiveConfigurationProperties, "AndroidUseAssemblyStore", usesAssemblyBlobs.ToString ());
			using (var b = CreateApkBuilder ()) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				var apk = Path.Combine (Root, b.ProjectDirectory,
					proj.OutputPath, $"{proj.PackageName}-Signed.apk");
				var msymarchive = Path.Combine (Root, b.ProjectDirectory, proj.OutputPath, proj.PackageName + ".apk.mSYM");
				var helper = new ArchiveAssemblyHelper (apk, usesAssemblyBlobs);
				var mdbExits = helper.Exists ("assemblies/UnnamedProject.dll.mdb") || helper.Exists ("assemblies/UnnamedProject.pdb");
				Assert.AreEqual (embedMdb, mdbExits,
				                 $"assemblies/UnnamedProject.dll.mdb or assemblies/UnnamedProject.pdb should{0}be in the {proj.PackageName}-Signed.apk", embedMdb ? " " : " not ");
				if (aotAssemblies) {
					foreach (var abi in abis) {
						var assemblies = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath,
						                               "aot", abi, "libaot-UnnamedProject.dll.so");
						var shouldExist = monoSymbolArchive && debugSymbols && (debugType == "PdbOnly" || debugType == "Portable");
						var symbolicateFile = Directory.GetFiles (Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath,
						                                                        "aot", abi), "UnnamedProject.dll.msym", SearchOption.AllDirectories).FirstOrDefault ();
						if (shouldExist)
							Assert.IsNotNull (symbolicateFile, "UnnamedProject.dll.msym should exist");
						else
							Assert.IsNull (symbolicateFile, "{0} should not exist", symbolicateFile);
						if (shouldExist) {
							var foundMsyms = Directory.GetFiles (Path.Combine (msymarchive), "UnnamedProject.dll.msym", SearchOption.AllDirectories).Any ();
							Assert.IsTrue (foundMsyms, "UnnamedProject.dll.msym should exist in the archive {0}", msymarchive);
						}
						Assert.IsTrue (File.Exists (assemblies), "{0} libaot-UnnamedProject.dll.so does not exist", abi);
						Assert.IsTrue (helper.Exists ($"lib/{abi}/libaot-UnnamedProject.dll.so"),
						               $"lib/{0}/libaot-UnnamedProject.dll.so should be in the {proj.PackageName}-Signed.apk", abi);
						Assert.IsTrue (helper.Exists ("assemblies/UnnamedProject.dll"),
						               $"UnnamedProject.dll should be in the {proj.PackageName}-Signed.apk");
					}
				}
				var runtimeInfo = b.GetSupportedRuntimes ();
				foreach (var abi in abis) {
					var runtime = runtimeInfo.FirstOrDefault (x => x.Abi == abi && x.Runtime == expectedRuntime);
					Assert.IsNotNull (runtime, "Could not find the expected runtime.");
					var inApk = ZipHelper.ReadFileFromZip (apk, String.Format ("lib/{0}/{1}", abi, runtime.Name));
					var inApkRuntime = runtimeInfo.FirstOrDefault (x => x.Abi == abi && x.Size == inApk.Length);
					Assert.IsNotNull (inApkRuntime, "Could not find the actual runtime used.");
					Assert.AreEqual (runtime.Size, inApkRuntime.Size, "expected {0} got {1}", expectedRuntime, inApkRuntime.Runtime);
				}

				b.Clean (proj);
				Assert.IsTrue (!Directory.Exists (msymarchive), "{0} should have been deleted on Clean", msymarchive);
			}
		}

		[Test]
		[NonParallelizable]
		[Category ("SmokeTests")]
		public void BuildWithNativeLibraries ([Values (true, false)] bool isRelease)
		{
			var dll = new XamarinAndroidLibraryProject () {
				ProjectName = "Library1",
				IsRelease = isRelease,
				OtherBuildItems = {
					new AndroidItem.EmbeddedNativeLibrary ("foo\\armeabi-v7a\\libtest.so") {
						BinaryContent = () => new byte[10],
						MetadataValues = "Link=libs\\armeabi-v7a\\libtest.so",
					},
					new AndroidItem.EmbeddedNativeLibrary ("foo\\x86\\libtest.so") {
						BinaryContent = () => new byte[10],
						MetadataValues = "Link=libs\\x86\\libtest.so",
					},
				},
			};
			var dll2 = new XamarinAndroidLibraryProject () {
				ProjectName = "Library2",
				IsRelease = isRelease,
				References = {
					new BuildItem ("ProjectReference","..\\Library1\\Library1.csproj"),
				},
				OtherBuildItems = {
					new AndroidItem.EmbeddedNativeLibrary ("foo\\armeabi-v7a\\libtest1.so") {
						BinaryContent = () => new byte[10],
						MetadataValues = "Link=libs\\armeabi-v7a\\libtest1.so",
					},
					new AndroidItem.EmbeddedNativeLibrary ("foo\\x86\\libtest1.so") {
						BinaryContent = () => new byte[10],
						MetadataValues = "Link=libs\\x86\\libtest1.so",
					},
				},
			};
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = isRelease,
				References = {
					new BuildItem ("ProjectReference","..\\Library1\\Library1.csproj"),
					new BuildItem ("ProjectReference","..\\Library2\\Library2.csproj"),
				},
				OtherBuildItems = {
					new AndroidItem.AndroidNativeLibrary ("armeabi-v7a\\libRSSupport.so") {
						BinaryContent = () => new byte[10],
					},
				},
				PackageReferences = {
					KnownPackages.Xamarin_Android_Support_v8_RenderScript_28_0_0_3,
				}
			};
			proj.SetAndroidSupportedAbis ("armeabi-v7a", "x86");
			if (!Builder.UseDotNet) {
				//NOTE: Mono.Data.Sqlite and Mono.Posix do not exist in .NET 5+
				proj.References.Add (new BuildItem.Reference ("Mono.Data.Sqlite"));
				proj.References.Add (new BuildItem.Reference ("Mono.Posix"));
				proj.MainActivity = proj.DefaultMainActivity.Replace ("int count = 1;", @"int count = 1;
Mono.Data.Sqlite.SqliteConnection connection = null;
Mono.Unix.UnixFileInfo fileInfo = null;");
			}
			var path = Path.Combine (Root, "temp", string.Format ("BuildWithNativeLibraries_{0}", isRelease));
			using (var b1 = CreateDllBuilder (Path.Combine (path, dll2.ProjectName))) {
				Assert.IsTrue (b1.Build (dll2), "Build should have succeeded.");
				using (var b = CreateDllBuilder (Path.Combine (path, dll.ProjectName))) {
					Assert.IsTrue (b.Build (dll), "Build should have succeeded.");
					using (var builder = CreateApkBuilder (Path.Combine (path, proj.ProjectName))) {
						Assert.IsTrue (builder.Build (proj), "Build should have succeeded.");
						var apk = Path.Combine (Root, builder.ProjectDirectory,
							proj.OutputPath, $"{proj.PackageName}-Signed.apk");
						FileAssert.Exists (apk);
						Assert.IsTrue (StringAssertEx.ContainsText (builder.LastBuildOutput, "warning XA4301: APK already contains the item lib/armeabi-v7a/libRSSupport.so; ignoring."),
							"warning about skipping libRSSupport.so should have been raised");
						using (var zipFile = ZipHelper.OpenZip (apk)) {
							var data = ZipHelper.ReadFileFromZip (zipFile, "lib/x86/libtest.so");
							Assert.IsNotNull (data, "libtest.so for x86 should exist in the apk.");
							data = ZipHelper.ReadFileFromZip (zipFile, "lib/armeabi-v7a/libtest.so");
							Assert.IsNotNull (data, "libtest.so for armeabi-v7a should exist in the apk.");
							data = ZipHelper.ReadFileFromZip (zipFile, "lib/x86/libtest1.so");
							Assert.IsNotNull (data, "libtest1.so for x86 should exist in the apk.");
							data = ZipHelper.ReadFileFromZip (zipFile, "lib/armeabi-v7a/libtest1.so");
							Assert.IsNotNull (data, "libtest1.so for armeabi-v7a should exist in the apk.");
							data = ZipHelper.ReadFileFromZip (zipFile, "lib/armeabi-v7a/libRSSupport.so");
							Assert.IsNotNull (data, "libRSSupport.so for armeabi-v7a should exist in the apk.");
							if (Builder.UseDotNet) {
								data = ZipHelper.ReadFileFromZip (zipFile, "lib/x86/libSystem.Native.so");
								Assert.IsNotNull (data, "libSystem.Native.so for x86 should exist in the apk.");
								data = ZipHelper.ReadFileFromZip (zipFile, "lib/armeabi-v7a/libSystem.Native.so");
								Assert.IsNotNull (data, "libSystem.Native.so for armeabi-v7a should exist in the apk.");
							} else {
								data = ZipHelper.ReadFileFromZip (zipFile, "lib/x86/libmono-native.so");
								Assert.IsNotNull (data, "libmono-native.so for x86 should exist in the apk.");
								data = ZipHelper.ReadFileFromZip (zipFile, "lib/armeabi-v7a/libmono-native.so");
								Assert.IsNotNull (data, "libmono-native.so for armeabi-v7a should exist in the apk.");
								data = ZipHelper.ReadFileFromZip (zipFile, "lib/x86/libMonoPosixHelper.so");
								Assert.IsNotNull (data, "libMonoPosixHelper.so for x86 should exist in the apk.");
								data = ZipHelper.ReadFileFromZip (zipFile, "lib/armeabi-v7a/libMonoPosixHelper.so");
								Assert.IsNotNull (data, "libMonoPosixHelper.so for armeabi-v7a should exist in the apk.");
								data = ZipHelper.ReadFileFromZip (zipFile, "lib/x86/libsqlite3_xamarin.so");
								Assert.IsNotNull (data, "libsqlite3_xamarin.so for x86 should exist in the apk.");
								data = ZipHelper.ReadFileFromZip (zipFile, "lib/armeabi-v7a/libsqlite3_xamarin.so");
								Assert.IsNotNull (data, "libsqlite3_xamarin.so for armeabi-v7a should exist in the apk.");
							}
						}
					}
				}
			}
			Directory.Delete (path, recursive: true);
		}

		[Test]
		public void BuildWithNativeLibraryUnknownAbi ()
		{
			var proj = new XamarinAndroidApplicationProject () {
				OtherBuildItems = {
					new AndroidItem.AndroidNativeLibrary ("not-a-real-abi\\libtest.so") {
						BinaryContent = () => new byte[10],
					},
				}
			};
			proj.SetAndroidSupportedAbis ("armeabi-v7a", "x86");

			using (var builder = CreateApkBuilder (Path.Combine ("temp", TestContext.CurrentContext.Test.Name))) {
				builder.ThrowOnBuildFailure = false;
				Assert.IsFalse (builder.Build (proj), "Build should have failed.");
				Assert.IsTrue (StringAssertEx.ContainsText (builder.LastBuildOutput, $"error XA4301: Cannot determine ABI of native library 'not-a-real-abi{Path.DirectorySeparatorChar}libtest.so'. Move this file to a directory with a valid Android ABI name such as 'libs/armeabi-v7a/'."),
					"error about libtest.so should have been raised");
			}
		}

		[Test]
		[Category ("SmokeTests")]
		public void BuildWithExternalJavaLibrary ()
		{
			var path = Path.Combine ("temp", TestName);
			var binding = new XamarinAndroidBindingProject {
				ProjectName = "BuildWithExternalJavaLibraryBinding",
				AndroidClassParser = "class-parse",
			};
			using (var bbuilder = CreateDllBuilder (Path.Combine (path, "BuildWithExternalJavaLibraryBinding"))) {
				string multidex_jar = Path.Combine (TestEnvironment.AndroidMSBuildDirectory, "android-support-multidex.jar");
				binding.Jars.Add (new AndroidItem.InputJar (() => multidex_jar));

				Assert.IsTrue (bbuilder.Build (binding), "Binding build should succeed.");
				var proj = new XamarinAndroidApplicationProject {
					References = { new BuildItem ("ProjectReference", "..\\BuildWithExternalJavaLibraryBinding\\BuildWithExternalJavaLibraryBinding.csproj"), },
					OtherBuildItems = { new BuildItem ("AndroidExternalJavaLibrary", multidex_jar) },
					Sources = {
						new BuildItem ("Compile", "Foo.cs") {
							TextContent = () => "public class Foo { public void X () { new Android.Support.Multidex.MultiDexApplication (); } }"
						}
					},
				};
				using (var builder = CreateApkBuilder (Path.Combine (path, "BuildWithExternalJavaLibrary"))) {
					Assert.IsTrue (builder.Build (proj), "App build should succeed");
				}
			}
		}

		[Test]
		public void CheckItemMetadata ([Values (true, false)] bool isRelease)
		{
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = isRelease,
				Imports = {
					new Import (() => "My.Test.target") {
						TextContent = () => @"<Project xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
	<Target Name=""CustomTarget"" AfterTargets=""UpdateAndroidAssets"" BeforeTargets=""UpdateAndroidInterfaceProxies"" >
		<Message Text=""Foo""/>
		<Message Text=""@(_AndroidAssetsDest->'%(CustomData)')"" />
	</Target>
	<Target Name=""CustomTarget2"" AfterTargets=""UpdateAndroidResources"" >
		<Message Text=""@(_AndroidResourceDest->'%(CustomData)')"" />
	</Target>
</Project>
						"
					},
				},
				OtherBuildItems = {
					new AndroidItem.AndroidAsset (() => "Assets\\foo.txt") {
						TextContent = () => "Foo",
						MetadataValues = "CustomData=AssetMetaDataOK"
					},
				}
			};

			var mainAxml = proj.AndroidResources.First (x => x.Include () == "Resources\\layout\\Main.axml");
			mainAxml.MetadataValues = "CustomData=ResourceMetaDataOK";

			using (var builder = CreateApkBuilder (string.Format ("temp/CheckItemMetadata_{0}", isRelease))) {
				builder.Build (proj);
				StringAssertEx.Contains ("AssetMetaDataOK", builder.LastBuildOutput, "Metadata was not copied for AndroidAsset");
				StringAssertEx.Contains ("ResourceMetaDataOK", builder.LastBuildOutput, "Metadata was not copied for AndroidResource");
			}
		}

		// Context https://bugzilla.xamarin.com/show_bug.cgi?id=29706
		[Test]
		public void CheckLogicalNamePathSeperators ([Values (false, true)] bool isRelease)
		{
			var illegalSeperator = IsWindows ? "/" : @"\";
			var dll = new XamarinAndroidLibraryProject () {
				ProjectName = "Library1",
				IsRelease = isRelease,
				AndroidResources = {
					new AndroidItem.AndroidResource (() => "Resources\\Test\\Test2.png") {
						BinaryContent = () => XamarinAndroidApplicationProject.icon_binary_mdpi,
						MetadataValues = string.Format ("LogicalName=drawable{0}foo2.png", illegalSeperator)
					},
				},
			};
			var proj = new XamarinAndroidApplicationProject () {
				ProjectName = "Application1",
				IsRelease = isRelease,
				AndroidResources = {
					new AndroidItem.AndroidResource (() => "Resources\\Test\\Test.png") {
						BinaryContent = () => XamarinAndroidApplicationProject.icon_binary_mdpi,
						MetadataValues = string.Format ("LogicalName=drawable{0}foo.png", illegalSeperator)
					},
				},
				References = {
					new BuildItem ("ProjectReference","..\\Library1\\Library1.csproj"),
				},
			};
			var path = Path.Combine ("temp", TestName);
			using (var b = CreateDllBuilder (Path.Combine (path, dll.ProjectName))) {
				Assert.IsTrue (b.Build (dll), "Build should have succeeded.");
				using (var builder = CreateApkBuilder (Path.Combine (path, proj.ProjectName), isRelease)) {
					Assert.IsTrue (builder.Build (proj), "Build should have succeeded");
					string resource_designer_cs;
					if (Builder.UseDotNet) {
						resource_designer_cs = Path.Combine (Root, builder.ProjectDirectory, proj.IntermediateOutputPath, "Resource.designer.cs");
					} else {
						resource_designer_cs = Path.Combine (Root, builder.ProjectDirectory, "Resources", "Resource.designer.cs");
					}
					var contents = File.ReadAllText (resource_designer_cs);
					StringAssert.Contains ("public const int foo = ", contents);
					StringAssert.Contains ("public const int foo2 = ", contents);
				}
			}
		}

		[Test]
		public void ApplicationJavaClassProperties ()
		{
			var proj = new XamarinAndroidApplicationProject ();
			proj.SetProperty ("AndroidApplicationJavaClass", "android.test.mock.MockApplication");
			var builder = CreateApkBuilder ("temp/ApplicationJavaClassProperties");
			builder.Build (proj);
			var appsrc = File.ReadAllText (Path.Combine (Root, builder.ProjectDirectory, "obj", "Debug", "android", "AndroidManifest.xml"));
			Assert.IsTrue (appsrc.Contains ("android.test.mock.MockApplication"), "app class");
			builder.Dispose ();
		}

		[Test]
		public void ApplicationIdPlaceholder ()
		{
			var proj = new XamarinAndroidApplicationProject ();
			proj.AndroidManifest = proj.AndroidManifest.Replace ("</application>", "<provider android:name='${applicationId}' android:authorities='example' /></application>");
			using (var builder = CreateApkBuilder ("temp/ApplicationIdPlaceholder")) {
				builder.Build (proj);
				var manifest = XDocument.Load (Path.Combine (Root, builder.ProjectDirectory, "obj", "Debug", "android", "AndroidManifest.xml"));
				var namespaceResolver = new XmlNamespaceManager (new NameTable ());
				namespaceResolver.AddNamespace ("android", "http://schemas.android.com/apk/res/android");
				var element = manifest.XPathSelectElement ($"/manifest/application/provider[@android:name='{proj.PackageName}']", namespaceResolver);
				Assert.IsNotNull (element, "placeholder not replaced");
			}
		}

		[Test]
		[Category ("XamarinBuildDownload")]
		public void ExtraAaptManifest ()
		{
			var proj = new XamarinAndroidApplicationProject ();
			proj.MainActivity = proj.DefaultMainActivity.Replace ("base.OnCreate (bundle);", "base.OnCreate (bundle);\nCrashlytics.Crashlytics.HandleManagedExceptions();");
			proj.PackageReferences.Add (KnownPackages.Xamarin_Android_Crashlytics);
			proj.PackageReferences.Add (KnownPackages.Xamarin_Android_Fabric);
			proj.PackageReferences.Add (KnownPackages.Xamarin_Build_Download);
			using (var builder = CreateApkBuilder (Path.Combine ("temp", TestName))) {
				builder.Target = "Restore";
				Assert.IsTrue (builder.Build (proj), "Restore should have succeeded.");
				builder.Target = "Build";
				Assert.IsTrue (builder.Build (proj), "Build should have succeeded.");
				var manifest = File.ReadAllText (Path.Combine (Root, builder.ProjectDirectory, "obj", "Debug", "android", "AndroidManifest.xml"));
				Assert.IsTrue (manifest.Contains ($"android:authorities=\"{proj.PackageName}.crashlyticsinitprovider\""), "placeholder not replaced");
				Assert.IsFalse (manifest.Contains ("dollar_openBracket_applicationId_closeBracket"), "`aapt/AndroidManifest.xml` not ignored");
			}
		}

		[Test]
		public void AarContentExtraction ([Values (false, true)] bool useAapt2)
		{
			AssertAaptSupported (useAapt2);
			var aar = new AndroidItem.AndroidAarLibrary ("Jars\\android-crop-1.0.1.aar") {
				// https://mvnrepository.com/artifact/com.soundcloud.android/android-crop/1.0.1
				WebContent = "https://repo1.maven.org/maven2/com/soundcloud/android/android-crop/1.0.1/android-crop-1.0.1.aar"
			};
			var proj = new XamarinAndroidApplicationProject () {
				OtherBuildItems = {
					aar,
					new AndroidItem.AndroidAarLibrary ("fragment-1.2.2.aar") {
						WebContent = "https://maven.google.com/androidx/fragment/fragment/1.2.2/fragment-1.2.2.aar"
					}
				},
			};
			proj.AndroidUseAapt2 = useAapt2;
			using (var builder = CreateApkBuilder ()) {
				Assert.IsTrue (builder.Build (proj), "Build should have succeeded");
				var cache = builder.Output.GetIntermediaryPath ("libraryprojectimports.cache");
				Assert.IsTrue (File.Exists (cache), $"{cache} should exist.");
				var assemblyIdentityMap = builder.Output.GetAssemblyMapCache ();
				var libraryProjects = Path.Combine (Root, builder.ProjectDirectory, proj.IntermediateOutputPath, "lp");
				FileAssert.Exists (Path.Combine (libraryProjects, assemblyIdentityMap.IndexOf ("android-crop-1.0.1.aar").ToString (), "jl", "classes.jar"),
					"classes.jar was not extracted from the aar.");
				Assert.IsTrue (builder.Build (proj), "Build should have succeeded");
				Assert.IsTrue (builder.Output.IsTargetSkipped ("_ResolveLibraryProjectImports"),
					"_ResolveLibraryProjectImports should not have run.");

				var doc = XDocument.Load (cache);
				var expectedCount = doc.Elements ("Paths").Elements ("ResolvedResourceDirectories").Count ();

				aar.Timestamp = DateTimeOffset.UtcNow.Add (TimeSpan.FromMinutes (2));
				Assert.IsTrue (builder.Build (proj), "Build should have succeeded");
				Assert.IsFalse (builder.Output.IsTargetSkipped ("_ResolveLibraryProjectImports"),
					"_ResolveLibraryProjectImports should have run.");

				doc = XDocument.Load (cache);
				var count = doc.Elements ("Paths").Elements ("ResolvedResourceDirectories").Count ();
				Assert.AreEqual (expectedCount, count, "The same number of resource directories should have been resolved.");

				//NOTE: the designer requires the paths to be full paths
				foreach (var paths in doc.Elements ("Paths")) {
					foreach (var element in paths.Elements ("Path")) {
						var path = element.Value;
						if (!string.IsNullOrEmpty (path)) {
							Assert.IsTrue (path == Path.GetFullPath (path), $"`{path}` is not a full path!");
						}
					}
				}
				Assert.IsFalse (Directory.EnumerateFiles (libraryProjects, "lint.jar", SearchOption.AllDirectories).Any (),
					"`lint.jar` should not be extracted!");
			}
		}

		[Test]
		[Category ("DotNetIgnore")] // n/a in .NET 5+
		public void CheckTargetFrameworkVersion ([Values (true, false)] bool isRelease)
		{
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = isRelease,
				TargetSdkVersion = null,
				MinSdkVersion = null,
			};
			proj.SetProperty ("AndroidUseLatestPlatformSdk", "False");
			using (var builder = CreateApkBuilder ()) {
				builder.GetTargetFrameworkVersionRange (out var _, out string firstFrameworkVersion, out var _, out string lastFrameworkVersion, out string[] _);
				proj.SetProperty ("TargetFrameworkVersion", firstFrameworkVersion);
				if (!Directory.Exists (Path.Combine (TestEnvironment.MonoAndroidFrameworkDirectory, firstFrameworkVersion)))
					Assert.Ignore ("This is a Pull Request Build. Ignoring test.");
				Assert.IsTrue (builder.Build (proj), "Build should have succeeded.");
				Assert.IsTrue (StringAssertEx.ContainsText (builder.LastBuildOutput, $"Output Property: TargetFrameworkVersion={firstFrameworkVersion}"), $"TargetFrameworkVerson should be {firstFrameworkVersion}");
				Assert.IsTrue (builder.Build (proj, parameters: new [] { $"TargetFrameworkVersion={lastFrameworkVersion}" }), "Build should have succeeded.");
				Assert.IsTrue (StringAssertEx.ContainsText (builder.LastBuildOutput, $"Output Property: TargetFrameworkVersion={lastFrameworkVersion}"), $"TargetFrameworkVersion should be {lastFrameworkVersion}");
			}
		}

#pragma warning disable 414
		public static object [] GeneratorValidateEventNameArgs = new object [] {
			new object [] { false, true, string.Empty, string.Empty },
			new object [] { false, false, "<attr path=\"/api/package/class[@name='Test']/method[@name='setOn123Listener']\" name='eventName'>OneTwoThree</attr>", string.Empty },
			new object [] { true, true, string.Empty, "String s" },
		};
#pragma warning restore 414

		[Test]
		[TestCaseSource (nameof (GeneratorValidateEventNameArgs))]
		public void GeneratorValidateEventName (bool failureExpected, bool warningExpected, string metadataFixup, string methodArgs)
		{
			string java = @"
package com.xamarin.testing;

public class Test
{
	public void setOnAbcListener (OnAbcListener listener)
	{
	}

	public void setOn123Listener (On123Listener listener)
	{
	}

	public interface OnAbcListener
	{
		public void onAbc ();
	}

	public interface On123Listener
	{
		public void onAbc (%%ARGS%%);
	}
}
".Replace ("%%ARGS%%", methodArgs);
			var path = Path.Combine (Root, "temp", $"GeneratorValidateEventName{failureExpected}{warningExpected}");
			var javaDir = Path.Combine (path, "java", "com", "xamarin", "testing");
			if (Directory.Exists (javaDir))
				Directory.Delete (javaDir, true);
			Directory.CreateDirectory (javaDir);
			var proj = new XamarinAndroidBindingProject () {
				AndroidClassParser = "class-parse",
			};
			proj.MetadataXml = "<metadata>" + metadataFixup + "</metadata>";
			proj.Jars.Add (new AndroidItem.EmbeddedJar (Path.Combine ("java", "test.jar")) {
				BinaryContent = new JarContentBuilder () {
					BaseDirectory = Path.Combine (path, "java"),
					JarFileName = "test.jar",
					JavaSourceFileName = Path.Combine ("com", "xamarin", "testing", "Test.java"),
					JavaSourceText = java
				}.Build
			});
			using (var builder = CreateDllBuilder (path, false, false)) {
				bool result = false;
				try {
					result = builder.Build (proj);
					Assert.AreEqual (warningExpected, builder.LastBuildOutput.ContainsText ("warning BG8504"), "warning BG8504 is expected: " + warningExpected);
				} catch (FailedBuildException) {
					if (!failureExpected)
						throw;
				}
				Assert.AreEqual (failureExpected, !result, "Should build fail?");
			}
		}

#pragma warning disable 414
		public static object [] GeneratorValidateMultiMethodEventNameArgs = new object [] {
			new object [] { false, "BG8505", string.Empty, string.Empty },
			new object [] { false, null, "<attr path=\"/api/package/interface[@name='Test.OnFooListener']/method[@name='on123']\" name='eventName'>One23</attr>", string.Empty },
			new object [] { false, null, @"
					<attr path=""/api/package/interface[@name='Test.OnFooListener']/method[@name='on123']"" name='eventName'>One23</attr>
					<attr path=""/api/package/interface[@name='Test.OnFooListener']/method[@name='on123']"" name='argsType'>OneTwoThreeEventArgs</attr>
				", "String s" },
			new object [] { true, "BG8504", string.Empty, "String s" },
		};
#pragma warning restore 414

		[Test]
		[TestCaseSource (nameof (GeneratorValidateMultiMethodEventNameArgs))]
		public void GeneratorValidateMultiMethodEventName (bool failureExpected, string expectedWarning, string metadataFixup, string methodArgs)
		{
			string java = @"
package com.xamarin.testing;

public class Test
{
	public void setOnFooListener (OnFooListener listener)
	{
	}

	public interface OnFooListener
	{
		public void onAbc ();
		public void on123 (%%ARGS%%);
	}
}
".Replace ("%%ARGS%%", methodArgs);
			var path = Path.Combine (Root, "temp", $"GeneratorValidateMultiMethodEventName{failureExpected}{expectedWarning}{methodArgs}");
			var javaDir = Path.Combine (path, "java", "com", "xamarin", "testing");
			if (Directory.Exists (javaDir))
				Directory.Delete (javaDir, true);
			Directory.CreateDirectory (javaDir);
			var proj = new XamarinAndroidBindingProject () {
				AndroidClassParser = "class-parse",
			};
			proj.MetadataXml = "<metadata>" + metadataFixup + "</metadata>";
			proj.Jars.Add (new AndroidItem.EmbeddedJar (Path.Combine ("java", "test.jar")) {
				BinaryContent = new JarContentBuilder () {
					BaseDirectory = Path.Combine (path, "java"),
					JarFileName = "test.jar",
					JavaSourceFileName = Path.Combine ("com", "xamarin", "testing", "Test.java"),
					JavaSourceText = java
				}.Build
			});
			using (var builder = CreateDllBuilder (path, false, false)) {
				try {
					builder.Build (proj);
					if (failureExpected)
						Assert.Fail ("Build should fail.");
					if (expectedWarning == null)
						Assert.IsFalse (builder.LastBuildOutput.ContainsText ("warning BG850"), "warning BG850* is NOT expected");
					else
						Assert.IsTrue (builder.LastBuildOutput.ContainsText ("warning " + expectedWarning), "warning " + expectedWarning + " is expected.");
				} catch (FailedBuildException) {
					if (!failureExpected)
						throw;
				}
			}
		}

		[Test]
		[Category ("SmokeTests"), Category ("AOT")]
		[NonParallelizable]
		public void BuildApplicationWithSpacesInPath ([Values (true, false)] bool enableMultiDex, [Values ("", "r8")] string linkTool)
		{
			var folderName = $"BuildReleaseApp AndÜmläüts({enableMultiDex}{linkTool})";
			var lib = new XamarinAndroidLibraryProject {
				IsRelease = true,
				ProjectName = "Library1"
			};
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
				AotAssemblies = true,
				LinkTool = linkTool,
				References = { new BuildItem ("ProjectReference", $"..\\{folderName}Library1\\Library1.csproj") }
			};
			proj.OtherBuildItems.Add (new BuildItem ("AndroidJavaLibrary", "Hello (World).jar") { BinaryContent = () => Convert.FromBase64String (@"
UEsDBBQACAgIAMl8lUsAAAAAAAAAAAAAAAAJAAQATUVUQS1JTkYv/soAAAMAUEsHCAAAAAACAAAAA
AAAAFBLAwQUAAgICADJfJVLAAAAAAAAAAAAAAAAFAAAAE1FVEEtSU5GL01BTklGRVNULk1G803My0
xLLS7RDUstKs7Mz7NSMNQz4OVyLkpNLElN0XWqBAlY6BnEG5oaKWj4FyUm56QqOOcXFeQXJZYA1Wv
ycvFyAQBQSwcIbrokAkQAAABFAAAAUEsDBBQACAgIAIJ8lUsAAAAAAAAAAAAAAAASAAAAc2FtcGxl
L0hlbGxvLmNsYXNzO/Vv1z4GBgYTBkEuBhYGXg4GPnYGfnYGAUYGNpvMvMwSO0YGZg3NMEYGFuf8l
FRGBn6fzLxUv9LcpNSikMSkHKAIa3l+UU4KI4OIhqZPVmJZon5OYl66fnBJUWZeujUjA1dwfmlRcq
pbJkgtl0dqTk6+HkgZDwMrAxvQFrCIIiMDT3FibkFOqj6Yz8gggDDKPykrNbmEQZGBGehCEGBiYAR
pBpLsQJ4skGYE0qxa2xkYNwIZjAwcQJINIggkORm4oEqloUqZhZg2oClkB5LcYLN5AFBLBwjQMrpO
0wAAABMBAABQSwECFAAUAAgICADJfJVLAAAAAAIAAAAAAAAACQAEAAAAAAAAAAAAAAAAAAAATUVUQ
S1JTkYv/soAAFBLAQIUABQACAgIAMl8lUtuuiQCRAAAAEUAAAAUAAAAAAAAAAAAAAAAAD0AAABNRV
RBLUlORi9NQU5JRkVTVC5NRlBLAQIUABQACAgIAIJ8lUvQMrpO0wAAABMBAAASAAAAAAAAAAAAAAA
AAMMAAABzYW1wbGUvSGVsbG8uY2xhc3NQSwUGAAAAAAMAAwC9AAAA1gEAAAAA") });
			if (enableMultiDex)
				proj.SetProperty ("AndroidEnableMultiDex", "True");

			proj.Imports.Add (new Import ("foo.targets") {
				TextContent = () => @"<?xml version=""1.0"" encoding=""utf-16""?>
<Project ToolsVersion=""4.0"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
<Target Name=""_Foo"" AfterTargets=""_SetLatestTargetFrameworkVersion"">
	<PropertyGroup>
		<AotAssemblies Condition=""!Exists('$(MonoAndroidBinDirectory)" + Path.DirectorySeparatorChar + @"cross-arm')"">False</AotAssemblies>
	</PropertyGroup>
	<Message Text=""$(AotAssemblies)"" />
</Target>
</Project>
",
			});
			using (var libb = CreateDllBuilder (Path.Combine ("temp", $"{folderName}Library1")))
			using (var b = CreateApkBuilder (Path.Combine ("temp", folderName))) {
				libb.Build (lib);
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				Assert.IsFalse (b.LastBuildOutput.ContainsText ("Duplicate zip entry"), "Should not get warning about [META-INF/MANIFEST.MF]");

				var className = "Lmono/MonoRuntimeProvider;";
				var dexFile = b.Output.GetIntermediaryPath (Path.Combine ("android", "bin", "classes.dex"));
				FileAssert.Exists (dexFile);
				Assert.IsTrue (DexUtils.ContainsClass (className, dexFile, AndroidSdkPath), $"`{dexFile}` should include `{className}`!");
			}
		}

		[Test]
		public void BuildReleaseApplicationWithNugetPackages ()
		{
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
				PackageReferences = {
					KnownPackages.AndroidSupportV4_27_0_2_1,
				},
			};
			using (var b = CreateApkBuilder ()) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				var assets = b.Output.GetIntermediaryAsText (Path.Combine ("..", "project.assets.json"));
				StringAssert.Contains ("Xamarin.Android.Support.v4", assets,
					"Nuget Package Xamarin.Android.Support.v4.21.0.3.0 should have been restored.");
				var src = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath, "android", "src");
				var main_r_java = Path.Combine (src, proj.PackageNameJavaIntermediatePath, "R.java");
				FileAssert.Exists (main_r_java);
				var lib_r_java = Path.Combine (src, "android", "support", "compat", "R.java");
				FileAssert.Exists (lib_r_java);
			}
		}

		[Test]
		[NonParallelizable]
		[Category ("PackagesConfig")]
		public void BuildWithResolveAssembliesFailure ([Values (true, false)] bool usePackageReference)
		{
			var path = Path.Combine ("temp", TestContext.CurrentContext.Test.Name);
			var app = new XamarinAndroidApplicationProject {
				ProjectName = "MyApp",
				Sources = {
					new BuildItem.Source ("Foo.cs") {
						TextContent = () => "public class Foo : Bar { }"
					},
				}
			};
			var lib = new XamarinAndroidLibraryProject {
				ProjectName = "MyLibrary",
				Sources = {
					new BuildItem.Source ("Bar.cs") {
						TextContent = () => "public class Bar { void EventHubs () { Microsoft.Azure.EventHubs.EventHubClient c; } }"
					},
				}
			};
			if (usePackageReference)
				lib.PackageReferences.Add (KnownPackages.Microsoft_Azure_EventHubs);
			else
				lib.Packages.Add (KnownPackages.Microsoft_Azure_EventHubs);
			app.References.Add (new BuildItem.ProjectReference ($"..\\{lib.ProjectName}\\{lib.ProjectName}.csproj", lib.ProjectName, lib.ProjectGuid));

			using (var libBuilder = CreateDllBuilder (Path.Combine (path, lib.ProjectName), false))
			using (var appBuilder = CreateApkBuilder (Path.Combine (path, app.ProjectName))) {
				libBuilder.Target = "Restore";
				Assert.IsTrue (libBuilder.Build (lib), "Restore should have succeeded.");
				libBuilder.Target = "Build";
				Assert.IsTrue (libBuilder.Build (lib), "Build should have succeeded.");

				appBuilder.ThrowOnBuildFailure = false;
				Assert.IsFalse (appBuilder.Build (app), "Build should have failed.");

				const string error = "error XA2002: Can not resolve reference:";
				if (usePackageReference) {
					Assert.IsTrue (appBuilder.LastBuildOutput.ContainsText ($"{error} `Microsoft.Azure.EventHubs`, referenced by `MyLibrary`. Please add a NuGet package or assembly reference for `Microsoft.Azure.EventHubs`, or remove the reference to `MyLibrary`."),
						$"Should recieve '{error}' regarding `Microsoft.Azure.EventHubs`!");
				} else {
					Assert.IsTrue (appBuilder.LastBuildOutput.ContainsText ($"{error} `Microsoft.Azure.Amqp`, referenced by `Microsoft.Azure.EventHubs`. Please add a NuGet package or assembly reference for `Microsoft.Azure.Amqp`, or remove the reference to `Microsoft.Azure.EventHubs`"),
						$"Should recieve '{error}' regarding `Microsoft.Azure.Amqp`!");
				}
				//Now add the PackageReference to the app to see a different error message
				if (usePackageReference) {
					app.PackageReferences.Add (KnownPackages.Microsoft_Azure_EventHubs);
					appBuilder.Target = "Restore";
					Assert.IsTrue (appBuilder.Build (app), "Restore should have succeeded.");
					appBuilder.Target = "Build";
				} else {
					app.Packages.Add (KnownPackages.Microsoft_Azure_EventHubs);
				}

				bool shouldBuild = usePackageReference;
				Assert.AreEqual (shouldBuild ,appBuilder.Build (app), $"Build should have {(shouldBuild ? "built" : "failed")}.");

				//NOTE: we get a different message when using <PackageReference /> due to automatically getting the Microsoft.Azure.Amqp (and many other) transient dependencies
				if (usePackageReference) {
					Assert.IsFalse (appBuilder.LastBuildOutput.ContainsText ($"{error} `Microsoft.Azure.Services.AppAuthentication`, referenced by `Microsoft.Azure.EventHubs`. Please add a NuGet package or assembly reference for `Microsoft.Azure.Services.AppAuthentication`, or remove the reference to `Microsoft.Azure.EventHubs`."),
						$"Should not recieve '{error}' regarding `Microsoft.Azure.Services.AppAuthentication`!");
				} else {
					Assert.IsTrue (appBuilder.LastBuildOutput.ContainsText ($"{error} `Microsoft.Azure.Amqp`, referenced by `Microsoft.Azure.EventHubs`. Please add a NuGet package or assembly reference for `Microsoft.Azure.Amqp`, or remove the reference to `Microsoft.Azure.EventHubs`."),
						$"Should recieve '{error}' regarding `Microsoft.Azure.Services.Amqp`!");
				}
			}
		}

		[Test]
		public void BuildAfterAddingNuget ()
		{
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
				TargetFrameworkVersion = "7.1",
			};
			using (var b = CreateApkBuilder ()) {
				Assert.IsTrue (b.Build (proj), "first build should have succeeded.");
				string build_props = b.Output.GetIntermediaryPath ("build.props");
				FileAssert.Exists (build_props, "build.props should exist after first build.");
				proj.PackageReferences.Add (KnownPackages.SupportV7CardView_27_0_2_1);

				Assert.IsTrue (b.Build (proj, doNotCleanupOnUpdate: true), "second build should have succeeded.");
				FileAssert.Exists (build_props, "build.props should exist after second build.");

				proj.MainActivity = proj.DefaultMainActivity.Replace ("clicks", "CLICKS");
				proj.Touch ("MainActivity.cs");
				Assert.IsTrue (b.Build (proj, doNotCleanupOnUpdate: true), "third build should have succeeded.");
				Assert.IsTrue (b.Output.IsTargetSkipped ("_CleanIntermediateIfNeeded"), "A build with no changes to NuGets should *not* trigger `_CleanIntermediateIfNeeded`!");
				FileAssert.Exists (build_props, "build.props should exist after third build.");
			}
		}

		//This test validates the _CleanIntermediateIfNeeded target
		[Test]
		[Category ("DotNetIgnore")] // Xamarin.Forms version is too old, uses net45 MSBuild tasks
		[NonParallelizable]
		public void BuildAfterUpgradingNuget ()
		{
			var proj = new XamarinAndroidApplicationProject ();
			proj.MainActivity = proj.DefaultMainActivity.Replace ("public class MainActivity : Activity", "public class MainActivity : Xamarin.Forms.Platform.Android.FormsAppCompatActivity");

			proj.PackageReferences.Add (KnownPackages.XamarinForms_2_3_4_231);
			proj.PackageReferences.Add (KnownPackages.AndroidSupportV4_27_0_2_1);
			proj.PackageReferences.Add (KnownPackages.SupportCompat_27_0_2_1);
			proj.PackageReferences.Add (KnownPackages.SupportCoreUI_27_0_2_1);
			proj.PackageReferences.Add (KnownPackages.SupportCoreUtils_27_0_2_1);
			proj.PackageReferences.Add (KnownPackages.SupportDesign_27_0_2_1);
			proj.PackageReferences.Add (KnownPackages.SupportFragment_27_0_2_1);
			proj.PackageReferences.Add (KnownPackages.SupportMediaCompat_27_0_2_1);
			proj.PackageReferences.Add (KnownPackages.SupportV7AppCompat_27_0_2_1);
			proj.PackageReferences.Add (KnownPackages.SupportV7CardView_27_0_2_1);
			proj.PackageReferences.Add (KnownPackages.SupportV7MediaRouter_27_0_2_1);

			using (var b = CreateApkBuilder (Path.Combine ("temp", TestContext.CurrentContext.Test.Name))) {
				//[TearDown] will still delete if test outcome successful, I need logs if assertions fail but build passes
				b.CleanupAfterSuccessfulBuild =
					b.CleanupOnDispose = false;
				var projectDir = Path.Combine (Root, b.ProjectDirectory);
				if (Directory.Exists (projectDir))
					Directory.Delete (projectDir, true);
				Assert.IsTrue (b.Build (proj), "first build should have succeeded.");
				Assert.IsFalse (b.Output.IsTargetSkipped ("_CleanIntermediateIfNeeded"), "`_CleanIntermediateIfNeeded` should have run!");

				var nugetStamp = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath, "stamp", "_CleanIntermediateIfNeeded.stamp");
				FileAssert.Exists (nugetStamp, "`_CleanIntermediateIfNeeded` did not create stamp file!");
				string build_props = b.Output.GetIntermediaryPath ("build.props");
				FileAssert.Exists (build_props, "build.props should exist after first build.");

				proj.PackageReferences.Clear ();
				//NOTE: we can get all the other dependencies transitively, yay!
				proj.PackageReferences.Add (KnownPackages.XamarinForms_4_0_0_425677);
				b.Save (proj, doNotCleanupOnUpdate: true);
				Assert.IsTrue (b.Build (proj), "second build should have succeeded.");
				Assert.IsFalse (b.Output.IsTargetSkipped ("_CleanIntermediateIfNeeded"), "`_CleanIntermediateIfNeeded` should have run!");
				FileAssert.Exists (nugetStamp, "`_CleanIntermediateIfNeeded` did not create stamp file!");
				Assert.IsTrue (StringAssertEx.ContainsText (b.LastBuildOutput, "Refreshing Xamarin.Android.Support.v7.AppCompat.dll"), "`ResolveLibraryProjectImports` should not skip `Xamarin.Android.Support.v7.AppCompat.dll`!");
				FileAssert.Exists (build_props, "build.props should exist after second build.");

				proj.MainActivity = proj.MainActivity.Replace ("clicks", "CLICKS");
				proj.Touch ("MainActivity.cs");
				Assert.IsTrue (b.Build (proj), "third build should have succeeded.");
				Assert.IsTrue (b.Output.IsTargetSkipped ("_CleanIntermediateIfNeeded"), "A build with no changes to NuGets should *not* trigger `_CleanIntermediateIfNeeded`!");
				FileAssert.Exists (build_props, "build.props should exist after third build.");
			}
		}

		[Test]
		[Category ("DotNetIgnore")] // Xamarin.Forms version is too old, uses net45 MSBuild tasks
		[NonParallelizable]
		public void CompileBeforeUpgradingNuGet ()
		{
			var proj = new XamarinAndroidApplicationProject ();
			proj.MainActivity = proj.DefaultMainActivity.Replace ("public class MainActivity : Activity", "public class MainActivity : Xamarin.Forms.Platform.Android.FormsAppCompatActivity");

			proj.PackageReferences.Add (KnownPackages.XamarinForms_2_3_4_231);
			proj.PackageReferences.Add (KnownPackages.AndroidSupportV4_27_0_2_1);
			proj.PackageReferences.Add (KnownPackages.SupportCompat_27_0_2_1);
			proj.PackageReferences.Add (KnownPackages.SupportCoreUI_27_0_2_1);
			proj.PackageReferences.Add (KnownPackages.SupportCoreUtils_27_0_2_1);
			proj.PackageReferences.Add (KnownPackages.SupportDesign_27_0_2_1);
			proj.PackageReferences.Add (KnownPackages.SupportFragment_27_0_2_1);
			proj.PackageReferences.Add (KnownPackages.SupportMediaCompat_27_0_2_1);
			proj.PackageReferences.Add (KnownPackages.SupportV7AppCompat_27_0_2_1);
			proj.PackageReferences.Add (KnownPackages.SupportV7CardView_27_0_2_1);
			proj.PackageReferences.Add (KnownPackages.SupportV7MediaRouter_27_0_2_1);

			using (var b = CreateApkBuilder (Path.Combine ("temp", TestName))) {
				b.ThrowOnBuildFailure = false;
				var projectDir = Path.Combine (Root, b.ProjectDirectory);
				if (Directory.Exists (projectDir))
					Directory.Delete (projectDir, true);
				Assert.IsTrue (b.DesignTimeBuild (proj), "design-time build should have succeeded.");

				proj.PackageReferences.Clear ();
				//NOTE: we can get all the other dependencies transitively, yay!
				proj.PackageReferences.Add (KnownPackages.XamarinForms_4_4_0_991265);
				Assert.IsTrue (b.Restore (proj, doNotCleanupOnUpdate: true), "Restore should have worked.");
				Assert.IsTrue (b.Build (proj, saveProject: true, doNotCleanupOnUpdate: true), "second build should have succeeded.");
				Assert.IsTrue (StringAssertEx.ContainsText (b.LastBuildOutput, "Refreshing Xamarin.Android.Support.v7.AppCompat.dll"), "`ResolveLibraryProjectImports` should not skip `Xamarin.Android.Support.v7.AppCompat.dll`!");
				Assert.IsTrue (StringAssertEx.ContainsText (b.LastBuildOutput, "Deleting unknown jar: support-annotations.jar"), "`support-annotations.jar` should be deleted!");
			}
		}

		[Test]
		[Category ("SmokeTests")]
		[Category ("DotNetIgnore")] // .mdb and non-portable .pdb files not supported in .NET 5+
		public void BuildBasicApplicationCheckPdb ()
		{
			var proj = new XamarinAndroidApplicationProject {
				EmbedAssembliesIntoApk = true,
			};
			using (var b = CreateApkBuilder ()) {
				var reference = new BuildItem.Reference ("PdbTestLibrary.dll") {
					WebContentFileNameFromAzure = "PdbTestLibrary.dll"
				};
				proj.References.Add (reference);
				var pdb = new BuildItem.NoActionResource ("PdbTestLibrary.pdb") {
					WebContentFileNameFromAzure = "PdbTestLibrary.pdb"
				};
				proj.References.Add (pdb);
				var netStandardRef = new BuildItem.Reference ("NetStandard16.dll") {
					WebContentFileNameFromAzure = "NetStandard16.dll"
				};
				proj.References.Add (netStandardRef);
				var netStandardpdb = new BuildItem.NoActionResource ("NetStandard16.pdb") {
					WebContentFileNameFromAzure = "NetStandard16.pdb"
				};
				proj.References.Add (netStandardpdb);
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				var pdbToMdbPath = Path.Combine (Root, b.ProjectDirectory, "PdbTestLibrary.dll.mdb");
				Assert.IsTrue (
					File.Exists (pdbToMdbPath),
					"PdbTestLibrary.dll.mdb must be generated next to the .pdb");
				Assert.IsTrue (
					File.Exists (Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath, "android", "assets", "UnnamedProject.pdb")),
					"UnnamedProject.pdb must be copied to the Intermediate directory");
				Assert.IsFalse (
					File.Exists (Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath, "android", "assets", "PdbTestLibrary.pdb")),
					"PdbTestLibrary.pdb must not be copied to Intermediate directory");
				Assert.IsTrue (
					File.Exists (Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath, "android", "assets", "PdbTestLibrary.dll.mdb")),
					"PdbTestLibrary.dll.mdb must be copied to Intermediate directory");
				FileAssert.AreNotEqual (pdbToMdbPath,
					Path.Combine (Root, b.ProjectDirectory, "PdbTestLibrary.pdb"),
					"The .pdb should NOT match the .mdb");
				Assert.IsTrue (
					File.Exists (Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath, "android", "assets", "NetStandard16.pdb")),
					"NetStandard16.pdb must be copied to Intermediate directory");
				var apk = Path.Combine (Root, b.ProjectDirectory,
					proj.OutputPath, $"{proj.PackageName}-Signed.apk");
				using (var zipFile = ZipHelper.OpenZip (apk)) {
					Assert.IsNotNull (ZipHelper.ReadFileFromZip (zipFile,
							"assemblies/NetStandard16.pdb"),
							"assemblies/NetStandard16.pdb should exist in the apk.");
					Assert.IsNotNull (ZipHelper.ReadFileFromZip (zipFile,
							"assemblies/PdbTestLibrary.dll.mdb"),
							"assemblies/PdbTestLibrary.dll.mdb should exist in the apk.");
					Assert.IsNull (ZipHelper.ReadFileFromZip (zipFile,
							"assemblies/PdbTestLibrary.pdb"),
							"assemblies/PdbTestLibrary.pdb should not exist in the apk.");
				}
				b.BuildLogFile = "build1.log";
				Assert.IsTrue (b.Build (proj, doNotCleanupOnUpdate: true), "second build failed");
				b.BuildLogFile = "build2.log";
				var lastTime = File.GetLastWriteTimeUtc (pdbToMdbPath);
				pdb.Timestamp = DateTimeOffset.UtcNow;
				Assert.IsTrue (b.Build (proj, doNotCleanupOnUpdate: true), "third build failed");
				Assert.Less (lastTime,
					File.GetLastWriteTimeUtc (pdbToMdbPath),
					"{0} should have been updated", pdbToMdbPath);
			}
		}

		[Test]
		public void BuildInDesignTimeMode ([Values(false, true)] bool useManagedParser)
		{
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
			};
			proj.SetProperty ("AndroidUseManagedDesignTimeResourceGenerator", useManagedParser.ToString ());
			using (var builder = CreateApkBuilder ()) {
				builder.Target = "UpdateAndroidResources";
				builder.Build (proj, parameters: new string[] { "DesignTimeBuild=true" });
				Assert.IsFalse (builder.Output.IsTargetSkipped ("_CreatePropertiesCache"), "target \"_CreatePropertiesCache\" should have been run.");
				Assert.IsFalse (builder.Output.IsTargetSkipped ("_ResolveLibraryProjectImports"), "target \"_ResolveLibraryProjectImports\' should have been run.");
				var intermediate = Path.Combine (Root, builder.ProjectDirectory, proj.IntermediateOutputPath);
				var librarycache = Path.Combine (intermediate, "designtime", "libraryprojectimports.cache");
				Assert.IsTrue (File.Exists (librarycache), $"'{librarycache}' should exist.");
				librarycache = Path.Combine (intermediate, "libraryprojectimports.cache");
				Assert.IsFalse (File.Exists (librarycache), $"'{librarycache}' should not exist.");
				builder.Build (proj, parameters: new string[] { "DesignTimeBuild=true" });
				Assert.IsFalse (builder.Output.IsTargetSkipped ("_CreatePropertiesCache"), "target \"_CreatePropertiesCache\" should have been run.");
				Assert.IsTrue (builder.Output.IsTargetSkipped ("_ResolveLibraryProjectImports"), "target \"_ResolveLibraryProjectImports\' should have been skipped.");
				Assert.IsTrue (builder.Clean (proj), "Clean Should have succeeded");
				builder.Target = "_CleanDesignTimeIntermediateDir";
				Assert.IsTrue (builder.Build (proj), "_CleanDesignTimeIntermediateDir should have succeeded");
				librarycache = Path.Combine (intermediate, "designtime", "libraryprojectimports.cache");
				Assert.IsFalse (File.Exists (librarycache), $"'{librarycache}' should not exist.");
			}
		}

		[Test]
		[Category ("DotNetIgnore")] // n/a in .NET 5+, test validates __AndroidLibraryProjects__.zip generation
		public void CheckLibraryImportsUpgrade ()
		{
			var path = Path.Combine ("temp", TestContext.CurrentContext.Test.Name);
			var libproj = new XamarinAndroidLibraryProject () {
				IsRelease = true,
				ProjectName = "Library1"
			};
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
				ProjectName = "App1",
			};
			proj.References.Add (new BuildItem ("ProjectReference", $"..\\Library1\\Library1.csproj"));
			proj.SetProperty ("_AndroidLibrayProjectIntermediatePath", Path.Combine (proj.IntermediateOutputPath, "__library_projects__"));
			using (var libb = CreateDllBuilder (Path.Combine (path, libproj.ProjectName), false, false)) {
				Assert.IsTrue (libb.Build (libproj), "Build should have succeeded.");
				using (var builder = CreateApkBuilder (Path.Combine (path, proj.ProjectName), false, false)) {
					Assert.IsTrue (builder.Build (proj), "Build should have succeeded.");
					Assert.IsTrue (Directory.Exists (Path.Combine (Root, path, proj.ProjectName, proj.IntermediateOutputPath, "__library_projects__")),
						"The __library_projects__ directory should exist.");
					proj.RemoveProperty ("_AndroidLibrayProjectIntermediatePath");
					Assert.IsTrue (builder.Build (proj), "Build should have succeeded.");
					Assert.IsFalse (Directory.Exists (Path.Combine (Root, path, proj.ProjectName, proj.IntermediateOutputPath, "__library_projects__")),
						"The __library_projects__ directory should not exist, due to IncrementalClean.");
					Assert.IsTrue (libb.Clean (libproj), "Clean should have succeeded.");
					Assert.IsTrue (libb.Build (libproj), "Build should have succeeded.");
					Assert.IsTrue (builder.Build (proj), "Build should have succeeded.");
					var zipFile = libb.Output.GetIntermediaryPath ("__AndroidLibraryProjects__.zip");
					Assert.IsTrue (File.Exists (zipFile));
					using (var zip = ZipHelper.OpenZip (zipFile)) {
						Assert.IsTrue (zip.ContainsEntry ("library_project_imports/__res_name_case_map.txt"), $"{zipFile} should contain a library_project_imports/__res_name_case_map.txt entry");
					}
					Assert.IsFalse (Directory.Exists (Path.Combine (Root, path, proj.ProjectName, proj.IntermediateOutputPath, "__library_projects__")),
						"The __library_projects__ directory should not exist.");
					Assert.IsTrue (Directory.Exists (Path.Combine (Root, path, proj.ProjectName, proj.IntermediateOutputPath, "lp")),
						"The lp directory should exist.");

				}
			}
		}

		[Test]
		[Category ("DotNetIgnore")] // n/a in .NET 5+, because it uses 'netcoreapp1.0\pclcrypto.dll'
		public void ResolveLibraryImportsWithInvalidZip ()
		{
			var proj = new XamarinAndroidApplicationProject {
				PackageReferences = {
					KnownPackages.PCLCrypto_Alpha,
				},
			};
			using (var b = CreateApkBuilder ()) {
				b.Target = "Build";
				b.ThrowOnBuildFailure = false;
				if (b.Build (proj)) {
					//NOTE: `:` in a file path should fail on Windows, but passes on macOS
					if (IsWindows)
						Assert.Fail ("Build should have failed.");
				} else {
					Assert.IsTrue (StringAssertEx.ContainsText (b.LastBuildOutput, "error XA4303: Error extracting resources from"), "Should receive XA4303 error.");
				}
			}
		}

		[Test]
		[Category ("DotNetIgnore")] // n/a in .NET 5+, test validates __AndroidLibraryProjects__.zip generation
		public void AndroidLibraryProjectsZipWithOddPaths ()
		{
			var proj = new XamarinAndroidLibraryProject ();
			proj.Imports.Add (new Import ("foo.props") {
				TextContent = () => $@"
					<Project>
					  <PropertyGroup>
						<IntermediateOutputPath>$(MSBuildThisFileDirectory)../{TestContext.CurrentContext.Test.Name}/obj/$(Configuration)/foo/</IntermediateOutputPath>
					  </PropertyGroup>
					</Project>"
			});
			proj.AndroidResources.Add (new AndroidItem.AndroidResource ("Resources\\values\\foo.xml") {
				TextContent = () => @"<?xml version=""1.0"" encoding=""utf-8""?><resources><string name=""foo"">bar</string></resources>",
			});
			using (var b = CreateDllBuilder ()) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");

				var zipFile = Path.Combine (Root, b.ProjectDirectory, b.Output.IntermediateOutputPath, "foo", "__AndroidLibraryProjects__.zip");
				FileAssert.Exists (zipFile);
				using (var zip = ZipHelper.OpenZip (zipFile)) {
					Assert.IsTrue (zip.ContainsEntry ("library_project_imports/res/values/foo.xml"), $"{zipFile} should contain a library_project_imports/res/values/foo.xml entry");
				}
			}
		}

#pragma warning disable 414
		static object [] validateJavaVersionTestCases = new object [] {
			new object [] {
				/*targetFrameworkVersion*/ "v7.1",
				/*buildToolsVersion*/ "24.0.1",
				/*JavaVersion*/ "1.8.0_101",
				/*latestSupportedJavaVersion*/ "1.8.0",
				/*expectedResult*/ true,
			},
			new object [] {
				/*targetFrameworkVersion*/ "v7.1",
				/*buildToolsVersion*/ "24.0.1",
				/*JavaVersion*/ "1.7.0_101",
				/*latestSupportedJavaVersion*/ "1.8.0",
				/*expectedResult*/ false,
			},
			new object [] {
				/*targetFrameworkVersion*/ "v7.1",
				/*buildToolsVersion*/ "24.0.1",
				/*JavaVersion*/ "1.6.0_101",
				/*latestSupportedJavaVersion*/ "1.8.0",
				/*expectedResult*/ false,
			},
			new object [] {
				/*targetFrameworkVersion*/ "v6.0",
				/*buildToolsVersion*/ "24.0.1",
				/*JavaVersion*/ "1.8.0_101",
				/*latestSupportedJavaVersion*/ "1.8.0",
				/*expectedResult*/ true,
			},
			new object [] {
				/*targetFrameworkVersion*/ "v6.0",
				/*buildToolsVersion*/ "24.0.0",
				/*JavaVersion*/ "1.7.0_101",
				/*latestSupportedJavaVersion*/ "1.8.0",
				/*expectedResult*/ true,
			},
			new object [] {
				/*targetFrameworkVersion*/ "v6.0",
				/*buildToolsVersion*/ "24.0.0",
				/*JavaVersion*/ "1.6.0_101",
				/*latestSupportedJavaVersion*/ "1.8.0",
				/*expectedResult*/ false,
			},
			new object [] {
				/*targetFrameworkVersion*/ "v5.0",
				/*buildToolsVersion*/ "24.0.1",
				/*JavaVersion*/ "1.8.0_101",
				/*latestSupportedJavaVersion*/ "1.8.0",
				/*expectedResult*/ true,
			},
			new object [] {
				/*targetFrameworkVersion*/ "v5.0",
				/*buildToolsVersion*/ "24.0.0",
				/*JavaVersion*/ "1.7.0_101",
				/*latestSupportedJavaVersion*/ "1.8.0",
				/*expectedResult*/ true,
			},
			new object [] {
				/*targetFrameworkVersion*/ "v5.0",
				/*buildToolsVersion*/ "24.0.0",
				/*JavaVersion*/ "1.6.0_101",
				/*latestSupportedJavaVersion*/ "1.8.0",
				/*expectedResult*/ true,
			},
			new object [] {
				/*targetFrameworkVersion*/ "v5.0",
				/*buildToolsVersion*/ "24.0.1",
				/*JavaVersion*/ "1.6.0_101",
				/*latestSupportedJavaVersion*/ "1.8.0",
				/*expectedResult*/ false,
			},
			new object [] {
				/*targetFrameworkVersion*/ "v7.1",
				/*buildToolsVersion*/ "24.0.1",
				/*JavaVersion*/ "1.6.x_101",
				/*latestSupportedJavaVersion*/ "1.8.0",
				/*expectedResult*/ true,
			},
			new object [] {
				/*targetFrameworkVersion*/ "v8.1",
				/*buildToolsVersion*/ "24.0.1",
				/*JavaVersion*/ "9.0.4",
				/*latestSupportedJavaVersion*/ "1.8.0",
				/*expectedResult*/ false,
			},
		};
#pragma warning restore 414

		[Test]
		[TestCaseSource (nameof (validateJavaVersionTestCases))]
		[Category ("DotNetIgnore")] // n/a under .NET 5+
		public void ValidateJavaVersion (string targetFrameworkVersion, string buildToolsVersion, string javaVersion, string latestSupportedJavaVersion, bool expectedResult)
		{
			var path = Path.Combine ("temp", $"ValidateJavaVersion_{targetFrameworkVersion}_{buildToolsVersion}_{latestSupportedJavaVersion}_{javaVersion}");
			string javaExe = "java";
			string javacExe;
			var javaPath = CreateFauxJavaSdkDirectory (Path.Combine (path, "JavaSDK"), javaVersion, out javaExe, out javacExe);
			var AndroidSdkDirectory = CreateFauxAndroidSdkDirectory (Path.Combine (path, "android-sdk"), buildToolsVersion);
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
				TargetFrameworkVersion = targetFrameworkVersion,
				UseLatestPlatformSdk = false,
			};
			using (var builder = CreateApkBuilder (Path.Combine (path, proj.ProjectName), false, false)) {
				if (!Directory.Exists (Path.Combine (TestEnvironment.MonoAndroidFrameworkDirectory, targetFrameworkVersion)))
					Assert.Ignore ("This is a Pull Request Build. Ignoring test.");
				builder.ThrowOnBuildFailure = false;
				builder.Target = "_SetLatestTargetFrameworkVersion";
				Assert.AreEqual (expectedResult, builder.Build (proj, parameters: new string[] {
					$"JavaSdkDirectory={javaPath}",
					$"JavaToolExe={javaExe}",
					$"JavacToolExe={javacExe}",
					$"AndroidSdkBuildToolsVersion={buildToolsVersion}",
					$"AndroidSdkDirectory={AndroidSdkDirectory}",
					$"LatestSupportedJavaVersion={latestSupportedJavaVersion}",
				}), string.Format ("Build should have {0}", expectedResult ? "succeeded" : "failed"));
			}
			Directory.Delete (javaPath, recursive: true);
			Directory.Delete (AndroidSdkDirectory, recursive: true);
		}

		[Test]
		public void IfAndroidJarDoesNotExistThrowXA5207 ()
		{
			var path = Path.Combine ("temp", TestName);
			var AndroidSdkDirectory = CreateFauxAndroidSdkDirectory (Path.Combine (path, "android-sdk"), "24.0.1", new ApiInfo [] { new ApiInfo { Id = "30" } });
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
				UseLatestPlatformSdk = false,
			};

			using (var builder = CreateApkBuilder (Path.Combine (path, proj.ProjectName), false, false)) {
				if (!Builder.UseDotNet)
					proj.TargetFrameworkVersion = builder.LatestTargetFrameworkVersion ();
				builder.ThrowOnBuildFailure = false;
				Assert.IsTrue (builder.DesignTimeBuild (proj), "DesignTime build should succeed.");
				Assert.IsFalse (builder.LastBuildOutput.ContainsText ("error XA5207:"), "XA5207 should not have been raised.");
				builder.Target = "AndroidPrepareForBuild";
				Assert.IsFalse (builder.Build (proj, parameters: new string [] {
					$"AndroidSdkBuildToolsVersion=24.0.1",
					$"AndroidSdkDirectory={AndroidSdkDirectory}",
				}), "Build should have failed");
				Assert.IsTrue (builder.LastBuildOutput.ContainsText ("error XA5207:"), "XA5207 should have been raised.");
				Assert.IsTrue (builder.LastBuildOutput.ContainsText ($"Could not find android.jar for API level {proj.TargetSdkVersion}"), "XA5207 should have had a good error message.");
			}
			Directory.Delete (AndroidSdkDirectory, recursive: true);
		}

		[Test]
		[Category ("DotNetIgnore")] // n/a under .NET 5+
		public void ValidateUseLatestAndroid ()
		{
			var apis = new ApiInfo [] {
				new ApiInfo () { Id = "23", Level = 23, Name = "Marshmallow", FrameworkVersion = "v6.0", Stable = true },
				new ApiInfo () { Id = "26", Level = 26, Name = "Oreo", FrameworkVersion = "v8.0", Stable = true },
				new ApiInfo () { Id = "27", Level = 27, Name = "Oreo", FrameworkVersion = "v8.1", Stable = true },
				new ApiInfo () { Id = "P", Level = 28, Name = "P", FrameworkVersion="v8.99", Stable = false },
			};
			var path = Path.Combine ("temp", TestName);
			var androidSdkPath = CreateFauxAndroidSdkDirectory (Path.Combine (path, "android-sdk"),
					"23.0.6", apis);
			var referencesPath = CreateFauxReferencesDirectory (Path.Combine (path, "xbuild-frameworks"), apis);
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
				TargetFrameworkVersion = "v8.0",
				UseLatestPlatformSdk = false,
			};
			var parameters = new string [] {
				$"TargetFrameworkRootPath={referencesPath}",
				$"AndroidSdkDirectory={androidSdkPath}",
			};
			var envVar = new Dictionary<string, string>  {
				{ "XBUILD_FRAMEWORK_FOLDERS_PATH", referencesPath },
			};
			using (var builder = CreateApkBuilder (Path.Combine (path, proj.ProjectName), false, false)) {
				builder.ThrowOnBuildFailure = false;
				builder.Target = "_SetLatestTargetFrameworkVersion";
				Assert.True (builder.Build (proj, parameters: parameters, environmentVariables: envVar),
					string.Format ("First Build should have succeeded"));

				//NOTE: these are generally of this form, from diagnostic log output:
				//    Task Parameter:TargetFrameworkVersion=v8.0
				//    ...
				//    Output Property: TargetFrameworkVersion=v8.0
				// ValidateJavaVersion and ResolveAndroidTooling take input, ResolveAndroidTooling has final output

				Assert.IsTrue (builder.LastBuildOutput.ContainsOccurances ("Task Parameter:TargetFrameworkVersion=v8.0", 2), "TargetFrameworkVersion should initially be v8.0");
				Assert.IsTrue (builder.LastBuildOutput.ContainsOccurances ("Output Property: TargetFrameworkVersion=v8.0", 1), "TargetFrameworkVersion should be v8.0");

				proj.TargetFrameworkVersion = "v8.0";
				Assert.True (builder.Build (proj, parameters: parameters, environmentVariables: envVar),
					string.Format ("Second Build should have succeeded"));
				Assert.IsTrue (builder.LastBuildOutput.ContainsOccurances ("Task Parameter:TargetFrameworkVersion=v8.0", 2), "TargetFrameworkVersion should initially be v8.0");
				Assert.IsTrue (builder.LastBuildOutput.ContainsOccurances ("Output Property: TargetFrameworkVersion=v8.0", 1), "TargetFrameworkVersion should be v8.0");

				proj.UseLatestPlatformSdk = true;
				proj.TargetFrameworkVersion = "v8.1";
				Assert.True (builder.Build (proj, parameters: parameters, environmentVariables: envVar),
					string.Format ("Third Build should have succeeded"));
				Assert.IsTrue (builder.LastBuildOutput.ContainsOccurances ("Task Parameter:TargetFrameworkVersion=v8.1", 2), "TargetFrameworkVersion should initially be v8.1");
				Assert.IsTrue (builder.LastBuildOutput.ContainsOccurances ("Output Property: TargetFrameworkVersion=v8.1", 1), "TargetFrameworkVersion should be v8.1");

				proj.UseLatestPlatformSdk = true;
				proj.TargetFrameworkVersion = "v8.99";
				Assert.True (builder.Build (proj, parameters: parameters, environmentVariables: envVar),
					string.Format ("Third Build should have succeeded"));
				Assert.IsTrue (builder.LastBuildOutput.ContainsOccurances ("Task Parameter:TargetFrameworkVersion=v8.99", 2), "TargetFrameworkVersion should initially be v8.99");
				Assert.IsTrue (builder.LastBuildOutput.ContainsOccurances ("Output Property: TargetFrameworkVersion=v8.99", 1), "TargetFrameworkVersion should be v8.99");

				proj.UseLatestPlatformSdk = true;
				proj.TargetFrameworkVersion = "v6.0";
				Assert.True (builder.Build (proj, parameters: parameters, environmentVariables: envVar),
					string.Format ("Forth Build should have succeeded"));
				Assert.IsTrue (builder.LastBuildOutput.ContainsOccurances ("Task Parameter:TargetFrameworkVersion=v6.0", 2), "TargetFrameworkVersion should initially be v6.0");
				Assert.IsTrue (builder.LastBuildOutput.ContainsOccurances ("Output Property: TargetFrameworkVersion=v8.1", 1), "TargetFrameworkVersion should be v8.1");
			}
			Directory.Delete (referencesPath, recursive: true);
		}

		[Test]
		public void XA4212 ()
		{
			var proj = new XamarinAndroidApplicationProject () {
			};
			proj.Sources.Add (new BuildItem ("Compile", "MyBadJavaObject.cs") { TextContent = () => @"
using System;
using Android.Runtime;
namespace UnnamedProject {
    public class MyBadJavaObject : IJavaObject
    {
        public IntPtr Handle {
			get {return IntPtr.Zero;}
        }

        public void Dispose ()
        {
        }
    }
}" });
			using (var builder = CreateApkBuilder (Path.Combine ("temp", TestContext.CurrentContext.Test.Name))) {
				builder.ThrowOnBuildFailure = false;
				Assert.IsFalse (builder.Build (proj), "Build should have failed with XA4212.");
				StringAssertEx.Contains ($"error XA4", builder.LastBuildOutput, "Error should be XA4212");
				StringAssertEx.Contains ($"Type `UnnamedProject.MyBadJavaObject` implements `Android.Runtime.IJavaObject`", builder.LastBuildOutput, "Error should mention MyBadJavaObject");
				Assert.IsTrue (builder.Build (proj, parameters: new [] { "AndroidErrorOnCustomJavaObject=False" }), "Build should have succeeded.");
				StringAssertEx.Contains ($"warning XA4", builder.LastBuildOutput, "warning XA4212");
			}
		}

		[Test]
		[Category ("DotNetIgnore")] // n/a for .NET 5+
		public void RunXABuildInParallel ()
		{
			var xabuild = new ProjectBuilder ("temp/RunXABuildInParallel").BuildTool;
			var psi     = new ProcessStartInfo (xabuild, "/version") {
				CreateNoWindow         = true,
				RedirectStandardOutput = true,
				RedirectStandardError  = true,
				WindowStyle            = ProcessWindowStyle.Hidden,
				UseShellExecute        = false,
			};

			Parallel.For (0, 10, i => {
				using (var p = Process.Start (psi)) {
					p.WaitForExit ();
					Assert.AreEqual (0, p.ExitCode);
				}
			});
		}

		[Test]
		public void Desugar ([Values (true, false)] bool isRelease, [Values ("", "r8")] string linkTool)
		{
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = isRelease,
				// desugar is on by default with r8
				LinkTool = linkTool,
			};

			//Add a BroadcastReceiver
			proj.Sources.Add (new BuildItem.Source ("MyReceiver.cs") {
				TextContent = () => @"
using Android.Content;

[BroadcastReceiver(Process = "":remote"", Name = ""foo.MyReceiver"")]
public class MyReceiver : BroadcastReceiver
{
    public override void OnReceive(Context context, Intent intent) { }
}",
			});

			//Okhttp and Okio
			//https://github.com/square/okhttp
			//https://github.com/square/okio
			if (!string.IsNullOrEmpty (linkTool)) {
				//NOTE: these are just enough rules to get it to build, not optimal
				var rules = new List<string> {
					"-dontwarn com.google.devtools.build.android.desugar.**",
					"-dontwarn javax.annotation.**",
					"-dontwarn org.codehaus.mojo.animal_sniffer.*",
				};
				//FIXME: We aren't de-BOM'ing proguard files?
				var bytes = Files.UTF8withoutBOM.GetBytes (string.Join (Environment.NewLine, rules));
				proj.OtherBuildItems.Add (new BuildItem ("ProguardConfiguration", "okhttp3.pro") {
					BinaryContent = () => bytes,
				});
			}
			proj.OtherBuildItems.Add (new BuildItem ("AndroidJavaLibrary", "okio-1.13.0.jar") {
				WebContent = "https://repo1.maven.org/maven2/com/squareup/okio/okio/1.13.0/okio-1.13.0.jar"
			});
			proj.OtherBuildItems.Add (new BuildItem ("AndroidJavaLibrary", "okhttp-3.8.0.jar") {
				WebContent = "https://repo1.maven.org/maven2/com/squareup/okhttp3/okhttp/3.8.0/okhttp-3.8.0.jar"
			});
			proj.OtherBuildItems.Add (new BuildItem ("AndroidJavaLibrary", "retrofit-2.3.0.jar") {
				WebContent = "https://repo1.maven.org/maven2/com/squareup/retrofit2/retrofit/2.3.0/retrofit-2.3.0.jar"
			});
			proj.OtherBuildItems.Add (new BuildItem ("AndroidJavaLibrary", "converter-gson-2.3.0.jar") {
				WebContent = "https://repo1.maven.org/maven2/com/squareup/retrofit2/converter-gson/2.3.0/converter-gson-2.3.0.jar"
			});
			proj.OtherBuildItems.Add (new BuildItem ("AndroidJavaLibrary", "gson-2.7.jar") {
				WebContent = "https://repo1.maven.org/maven2/com/google/code/gson/gson/2.7/gson-2.7.jar"
			});
			/* The source is simple:
			 *
				public class Lambda
				{
				    public void foo()
				    {
				        Runnable r = () -> System.out.println("whee");
					r.run();
				    }
				}
			 *
			 * We wanted to use AndroidJavaSource to simply compile it, but with
			 * android.jar as bootclasspath, it is impossible to compile lambdas.
			 * Therefore we compiled it without android.jar (javac Lambda.java)
			 * and then manually archived it (jar cvf Lambda.jar Lambda.class).
			 */

			proj.OtherBuildItems.Add (new BuildItem ("AndroidJavaLibrary", "Lambda.jar") { BinaryContent = () => Convert.FromBase64String (@"
UEsDBBQACAgIAECRZ0sAAAAAAAAAAAAAAAAJAAQATUVUQS1JTkYv/soAAAMAUEsHCAAAAAACAAAA
AAAAAFBLAwQUAAgICABBkWdLAAAAAAAAAAAAAAAAFAAAAE1FVEEtSU5GL01BTklGRVNULk1G803M
y0xLLS7RDUstKs7Mz7NSMNQz4OVyLkpNLElN0XWqBAlY6BnEGxobKmj4FyUm56QqOOcXFeQXJZYA
1WvycvFyAQBQSwcIUQoqTEQAAABFAAAAUEsDBBQACAgIACWRZ0sAAAAAAAAAAAAAAAAMAAAATGFt
YmRhLmNsYXNznVNdTxNBFD1DaafdLrQWip+gxaJdEIrfDzU+2MRIUoVYwvu0HWBhO9PszmL6s/RB
Iw/+AONvMt7pEotWiXEf5sz9OOfeuTvz9fvpFwCP8NRBFpdKtF/I4zKu5HAV17K47uAGFjmWOG4y
ZJ75yjfPGVI1b49huql7kqHQ8pV8E/c7MtwVnYA8qX2tGdxA9Ds9USWjusngtHUcduVL32bkW6PY
xpE4ES5ycBiKL7Q2kQnF4LU0h7oXFTK4VYRDUHGxjNscVYsOx4qLO7hLDbw7lJKj5sLDKrWXiJKU
la0HQh3UtztHsmscrOGeA451ai6MFcNCzWuNs97GStnWGwylSe8vgu1hZGSfZHRsGMqJiK/rO6Gv
TNuEUvRJZe4PbgY+sFZA5cu1c9Up7KuDhrfHseGijocuZu1ElscpvjrRx7KeHJDmI/ZF1+hwSJPs
jy2Ox3YKWh/HA5r/llIybAYiimTE8O18yTO9ZNKvhOoFMqomxMZkZ38j7g4H8v+CScmLud5ktCmC
oO0b2eB4wrDyT+dhWLo4DxW6GFnYLwVmLyOtebIWCRlhevUT2Hva0ExpzYycNjTzM3WdcIpw5tRC
a+0zUgxjyiwpkw5RM2TzomN/8Bm1MiICuQ+YLqU/IvN7pTSRC4RTKOIBoSVu0pO9T0/UPliX7DnK
mUcZ8z8AUEsHCLuHtAn+AQAA0QMAAFBLAQIUABQACAgIAECRZ0sAAAAAAgAAAAAAAAAJAAQAAAAA
AAAAAAAAAAAAAABNRVRBLUlORi/+ygAAUEsBAhQAFAAICAgAQZFnS1EKKkxEAAAARQAAABQAAAAA
AAAAAAAAAAAAPQAAAE1FVEEtSU5GL01BTklGRVNULk1GUEsBAhQAFAAICAgAJZFnS7uHtAn+AQAA
0QMAAAwAAAAAAAAAAAAAAAAAwwAAAExhbWJkYS5jbGFzc1BLBQYAAAAAAwADALcAAAD7AgAAAAA=
				") });
			using (var builder = CreateApkBuilder (Path.Combine ("temp", TestName))) {
				Assert.IsTrue (builder.Build (proj), "Build should have succeeded");
				Assert.IsFalse (builder.LastBuildOutput.ContainsText ("Duplicate zip entry"), "Should not get warning about [META-INF/MANIFEST.MF]");

				var className = "Lmono/MonoRuntimeProvider;";
				var dexFile = builder.Output.GetIntermediaryPath (Path.Combine ("android", "bin", "classes.dex"));
				FileAssert.Exists (dexFile);
				Assert.IsTrue (DexUtils.ContainsClass (className, dexFile, AndroidSdkPath), $"`{dexFile}` should include `{className}`!");
				className = "Lmono/MonoRuntimeProvider_1;";
				Assert.IsTrue (DexUtils.ContainsClass (className, dexFile, AndroidSdkPath), $"`{dexFile}` should include `{className}`!");
			}
		}

		//See: https://developer.android.com/about/versions/marshmallow/android-6.0-changes#behavior-apache-http-client
		[Test]
		public void MissingOrgApacheHttpClient ()
		{
			var proj = new XamarinAndroidApplicationProject {
				Sources = {
					new BuildItem.Source ("ApacheHttpClient.cs") {
						BinaryContent = () => ResourceData.ApacheHttpClient_cs,
					},
				},
			};
			proj.AndroidManifest = proj.AndroidManifest.Replace ("</application>",
				"<uses-library android:name=\"org.apache.http.legacy\" android:required=\"false\" /></application>");
			proj.SetProperty ("AndroidEnableMultiDex", "True");

			using (var b = CreateApkBuilder ()) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded");
			}
		}

		//NOTE: tests type forwarders in Mono.Android.dll to System.Drawing.Common.dll
		[Test]
		[Category ("DotNetIgnore")] // Fails with: error CS0433: The type 'Color' exists in both 'Splat' and 'System.Drawing.Primitives'
		public void SystemDrawingCommon ()
		{
			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
				Sources = {
					new BuildItem.Source ("Foo.cs") {
						TextContent = () => "class Foo { System.Drawing.Color bar; }"
					}
				},
				PackageReferences = {
					KnownPackages.Acr_UserDialogs,
					KnownPackages.Xamarin_Build_Download_0_4_11,
				}
			};
			using (var b = CreateApkBuilder (Path.Combine ("temp", TestName))) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
			}
		}

		//NOTE: Referencing only Microsoft.Extensions.Http, surfaced a bug in <ResolveAssemblies/>
		[Test]
		public void MicrosoftExtensionsHttp ()
		{
			// The goal is to create a project with only this <PackageReference/>
			var proj = new XamarinAndroidApplicationProject {
				PackageReferences = {
					KnownPackages.Microsoft_Extensions_Http,
				}
			};
			proj.References.Clear ();
			proj.Sources.Clear ();
			// We have to add a custom Target to remove Java.Interop and System.Runtime
			proj.Imports.Add (new Import ("foo.targets") {
				TextContent = () => @"<?xml version=""1.0"" encoding=""utf-16""?>
<Project ToolsVersion=""4.0"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
	<Target Name=""_Foo"" BeforeTargets=""_ResolveAssemblies"">
		<ItemGroup>
			<_Remove Include=""@(_ReferencePath)"" Condition=""'%(FileName)' == 'Java.Interop' Or '%(FileName)' == 'System.Runtime'"" />
			<_ReferencePath Remove=""@(_Remove)"" />
		</ItemGroup>
	</Target>
</Project>"
			});
			using (var b = CreateApkBuilder (Path.Combine ("temp", TestName))) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
			}
		}

		[Test]
		[Category ("DotNetIgnore")] // n/a on .NET 5+, does not use $(AndroidSupportedAbis)
		[TestCase ("armeabi;armeabi-v7a", TestName = "XA0115")]
		[TestCase ("armeabi,armeabi-v7a", TestName = "XA0115Commas")]
		public void XA0115 (string abis)
		{
			var proj = new XamarinAndroidApplicationProject ();
			proj.SetProperty (KnownProperties.AndroidSupportedAbis, abis);
			using (var builder = CreateApkBuilder ()) {
				builder.ThrowOnBuildFailure = false;
				Assert.IsFalse (builder.Build (proj), "Build should have failed with XA0115.");
				StringAssertEx.Contains ($"error XA0115", builder.LastBuildOutput, "Error should be XA0115");
				Assert.IsTrue (builder.Clean (proj), "Clean should have succeeded.");
			}
		}

		[Test]
		public void XA0119 ()
		{
			var proj = new XamarinAndroidApplicationProject ();
			proj.SetProperty ("_XASupportsFastDev", "True");
			proj.SetProperty (proj.DebugProperties, "AndroidLinkMode", "Full");
			using (var b = CreateApkBuilder (Path.Combine ("temp", TestName))) {
				b.Target = "Build"; // SignAndroidPackage would fail for OSS builds
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				Assert.IsTrue (StringAssertEx.ContainsText (b.LastBuildOutput, "XA0119"), "Output should contain XA0119 warnings");
			}
		}

		[Test]
		public void XA0119AAB ()
		{
			AssertCommercialBuild ();

			var proj = new XamarinAndroidApplicationProject ();
			proj.SetProperty ("_XASupportsFastDev", "True");
			proj.SetProperty ("AndroidPackageFormat", "aab");
			using (var builder = CreateApkBuilder ()) {
				builder.ThrowOnBuildFailure = false;
				Assert.IsTrue (builder.Build (proj), "Build should have succeeded.");
				Assert.IsTrue (StringAssertEx.ContainsText (builder.LastBuildOutput, "XA0119"), "Output should contain XA0119 warnings");
			}
		}

		[Test]
		public void XA0119Interpreter ()
		{
			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
				AotAssemblies = true,
			};
			proj.SetProperty ("UseInterpreter", "true");
			using (var builder = CreateApkBuilder ()) {
				builder.ThrowOnBuildFailure = false;
				Assert.IsTrue (builder.Build (proj), "Build should have succeeded.");
				Assert.IsTrue (StringAssertEx.ContainsText (builder.LastBuildOutput, "XA0119"), "Output should contain XA0119 warnings");
			}
		}

		[Test]
		public void FastDeploymentDoesNotAddContentProvider ()
		{
			AssertCommercialBuild ();

			var proj = new XamarinAndroidApplicationProject {
				EmbedAssembliesIntoApk = false,
			};
			proj.SetProperty ("_XASupportsFastDev", "True");
			using (var b = CreateApkBuilder (Path.Combine ("temp", TestName))) {
				//NOTE: build will fail, due to $(_XASupportsFastDev)
				b.ThrowOnBuildFailure = false;
				b.Build (proj);

				var manifest = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath, "android", "AndroidManifest.xml");
				FileAssert.Exists (manifest);
				var content = File.ReadAllLines (manifest);
				var type = "mono.android.ResourcePatcher";

				//NOTE: only $(AndroidFastDeploymentType) containing "dexes" should add this to the manifest
				Assert.IsFalse (StringAssertEx.ContainsText (content, type), $"`{type}` should not exist in `AndroidManifest.xml`!");
			}
		}

		[Test]
		public void DuplicateJCWNames ()
		{
			var source = @"[Android.Runtime.Register (""examplelib.EmptyClass"")] public class EmptyClass : Java.Lang.Object { }";
			var library1 = new XamarinAndroidLibraryProject () {
				ProjectName = "Library1",
				Sources = {
					new BuildItem.Source ("EmptyClass.cs") {
						TextContent = () => source
					}
				}
			};
			var library2 = new XamarinAndroidLibraryProject () {
				ProjectName = "Library2",
				Sources = {
					new BuildItem.Source ("EmptyClass.cs") {
						TextContent = () => source
					}
				}
			};
			var app = new XamarinAndroidApplicationProject {
				ProjectName = "App1",
				References = {
					new BuildItem ("ProjectReference", "..\\Library1\\Library1.csproj"),
					new BuildItem ("ProjectReference", "..\\Library2\\Library2.csproj")
				},
			};
			var projectPath = Path.Combine ("temp", TestName);
			using (var lib1b = CreateDllBuilder (Path.Combine (projectPath, library1.ProjectName), cleanupAfterSuccessfulBuild: false))
			using (var lib2b = CreateDllBuilder (Path.Combine (projectPath, library2.ProjectName), cleanupAfterSuccessfulBuild: false)) {
				Assert.IsTrue (lib1b.Build (library1), "Build of Library1 should have succeeded");
				Assert.IsTrue (lib2b.Build (library2), "Build of Library2 should have succeeded");
				using (var appb = CreateApkBuilder (Path.Combine (projectPath, app.ProjectName))) {
					appb.ThrowOnBuildFailure = false;
					Assert.IsFalse (appb.Build (app), "Build of App1 should have failed");
					IEnumerable<string> errors = appb.LastBuildOutput.Where (x => x.Contains ("error XA4215"));
					Assert.NotNull (errors, "Error should be XA4215");
					StringAssertEx.Contains ("EmptyClass", errors, "Error should mention the conflicting type name");
					StringAssertEx.Contains ("Library1", errors, "Error should mention all of the assemblies with conflicts");
					StringAssertEx.Contains ("Library2", errors, "Error should mention all of the assemblies with conflicts");
				}
			}
		}

		[Test]
		public void DuplicateManagedNames ()
		{
			var source = @"public class EmptyClass : Java.Lang.Object { }";
			var library1 = new XamarinAndroidLibraryProject () {
				ProjectName = "Library1",
				Sources = {
					new BuildItem.Source ("EmptyClass.cs") {
						TextContent = () => source
					}
				}
			};
			var library2 = new XamarinAndroidLibraryProject () {
				ProjectName = "Library2",
				Sources = {
					new BuildItem.Source ("EmptyClass.cs") {
						TextContent = () => source
					}
				}
			};
			var app = new XamarinAndroidApplicationProject {
				ProjectName = "App1",
				References = {
					new BuildItem ("ProjectReference", "..\\Library1\\Library1.csproj"),
					new BuildItem ("ProjectReference", "..\\Library2\\Library2.csproj")
				},
			};
			var projectPath = Path.Combine ("temp", TestName);
			using (var lib1b = CreateDllBuilder (Path.Combine (projectPath, library1.ProjectName), cleanupAfterSuccessfulBuild: false))
			using (var lib2b = CreateDllBuilder (Path.Combine (projectPath, library2.ProjectName), cleanupAfterSuccessfulBuild: false)) {
				Assert.IsTrue (lib1b.Build (library1), "Build of Library1 should have succeeded");
				Assert.IsTrue (lib2b.Build (library2), "Build of Library2 should have succeeded");
				using (var appb = CreateApkBuilder (Path.Combine (projectPath, app.ProjectName))) {
					appb.ThrowOnBuildFailure = false;
					Assert.IsTrue (appb.Build (app), "Build of App1 should have succeeded");
					IEnumerable<string> warnings = appb.LastBuildOutput.Where (x => x.Contains ("warning XA4214"));
					Assert.NotNull (warnings, "Warning should be XA4214");
					StringAssertEx.Contains ("EmptyClass", warnings, "Warning should mention the conflicting type name");
					StringAssertEx.Contains ("Library1", warnings, "Warning should mention all of the assemblies with conflicts");
					StringAssertEx.Contains ("Library2", warnings, "Warning should mention all of the assemblies with conflicts");
				}
			}
		}

		[Test]
		public void BuildOutsideVisualStudio ()
		{
			var path = Path.Combine ("temp", TestName);
			var lib = new XamarinAndroidLibraryProject {
				ProjectName = "Library1",
				Sources = {
					new BuildItem.Source ("Foo.cs") {
						TextContent = () => "public class Foo { }",
					}
				},
			};
			var proj = new XamarinFormsAndroidApplicationProject {
				ProjectName = "App1",
				References = { new BuildItem ("ProjectReference", "..\\Library1\\Library1.csproj") },
				Sources = {
					new BuildItem.Source ("Bar.cs") {
						TextContent = () => "public class Bar : Foo { }",
					}
				},
			};
			using (var libb = CreateDllBuilder (Path.Combine (path, lib.ProjectName)))
			using (var appb = CreateApkBuilder (Path.Combine (path, proj.ProjectName))) {
				libb.BuildingInsideVisualStudio =
					appb.BuildingInsideVisualStudio = false;
				appb.Target = "SignAndroidPackage";
				//Save, but don't build
				libb.Save (lib);
				Assert.IsTrue (appb.Build (proj), "build should have succeeded.");
			}
		}

		[Test]
		public void WarningForMinSdkVersion ()
		{
			int minSdkVersion = XABuildConfig.NDKMinimumApiAvailable;
			int tooLowSdkVersion = minSdkVersion - 1;
			var proj = new XamarinAndroidApplicationProject {
				MinSdkVersion = tooLowSdkVersion.ToString (),
				TargetSdkVersion = null,
			};
			using (var b = CreateApkBuilder (Path.Combine ("temp", TestName))) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				Assert.IsTrue (
					StringAssertEx.ContainsText (
						b.LastBuildOutput,
						$"warning XA4216: AndroidManifest.xml //uses-sdk/@android:minSdkVersion '{tooLowSdkVersion}' is less than API-{minSdkVersion}, this configuration is not supported."
					),
					$"Should receive a warning when //uses-sdk/@android:minSdkVersion=\"{tooLowSdkVersion}\""
				);
			}
		}

		[Test]
		public void RemoveOldMonoPackageManager ()
		{
			var proj = new XamarinAndroidApplicationProject ();
			using (var b = CreateApkBuilder (Path.Combine ("temp", TestName))) {
				b.ThrowOnBuildFailure = false;
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				var targets = new [] {
					"_CleanIntermediateIfNeeded",
					"_GeneratePackageManagerJava",
					"_CompileJava",
				};
				var intermediate = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath);
				var oldMonoPackageManager = Path.Combine (intermediate, "android", "src", "mono", "MonoPackageManager.java");
				var notifyTimeZoneChanges = Path.Combine (intermediate, "android", "src", "mono", "android", "app", "NotifyTimeZoneChanges.java");
				Directory.CreateDirectory (Path.GetDirectoryName (notifyTimeZoneChanges));
				File.WriteAllText (oldMonoPackageManager, @"package mono;
public class MonoPackageManager { }
class MonoPackageManager_Resources { }");
				File.WriteAllText (notifyTimeZoneChanges, @"package mono.android.app;
public class ApplicationRegistration { }");
				var oldMonoPackageManagerClass = Path.Combine (intermediate, "android", "bin", "classes" , "mono", "MonoPackageManager.class");
				File.WriteAllText (oldMonoPackageManagerClass, "");
				// Change $(XamarinAndroidVersion) to trigger _CleanIntermediateIfNeeded
				var property = Builder.UseDotNet ? "AndroidNETSdkVersion" : "XamarinAndroidVersion";
				Assert.IsTrue (b.Build (proj, parameters: new [] { $"{property}=99.99" }, doNotCleanupOnUpdate: true), "Build should have succeeded.");
				foreach (var target in targets) {
					Assert.IsFalse (b.Output.IsTargetSkipped (target), $"`{target}` should *not* be skipped.");
				}
				// Old files that should *not* exist
				FileAssert.DoesNotExist (oldMonoPackageManager);
				FileAssert.DoesNotExist (oldMonoPackageManagerClass);
				FileAssert.DoesNotExist (notifyTimeZoneChanges);
				// New files that should exist
				var monoPackageManager_Resources = Path.Combine (intermediate, "android", "src", "mono", "MonoPackageManager_Resources.java");
				var monoPackageManager_ResourcesClass = Path.Combine (intermediate, "android", "bin", "classes", "mono", "MonoPackageManager_Resources.class");
				FileAssert.Exists (monoPackageManager_Resources);
				FileAssert.Exists (monoPackageManager_ResourcesClass);
			}
		}

		[Test]
		public void CompilerErrorShouldNotRunLinkAssemblies ()
		{
			var proj = new XamarinAndroidApplicationProject ();
			proj.Sources.Add (new BuildItem.Source ("SyntaxError.cs") {
				TextContent = () => "class SyntaxError {"
			});
			using (var b = CreateApkBuilder (Path.Combine ("temp", TestName))) {
				b.ThrowOnBuildFailure = false;
				Assert.IsFalse (b.Build (proj), "Build should have failed.");
				Assert.IsFalse (StringAssertEx.ContainsText (b.LastBuildOutput, "The \"LinkAssemblies\" task failed unexpectedly"), "The LinkAssemblies MSBuild task should not run!");
			}
		}

		/// <summary>
		/// This assembly weirdly has no [assembly: System.Runtime.Versioning.TargetFrameworkAttribute()], at all...
		/// </summary>
		[Test]
		public void AssemblyWithMissingTargetFramework ()
		{
			var proj = new XamarinFormsAndroidApplicationProject {
				AndroidResources = {
					new AndroidItem.AndroidResource ("Resources\\layout\\test.axml") {
						TextContent = () =>
@"<?xml version=""1.0"" encoding=""utf-8""?>
<ScrollView
    xmlns:android=""http://schemas.android.com/apk/res/android""
    xmlns:local=""http://schemas.android.com/apk/res-auto"">
    <refractored.controls.CircleImageView local:civ_border_width=""0dp"" />
</ScrollView>"
					}
				}
			};
			proj.PackageReferences.Add (KnownPackages.CircleImageView);
			using (var b = CreateApkBuilder (Path.Combine ("temp", TestName))) {
				Assert.IsTrue (b.Build (proj), "build should have succeeded.");

				// We should have a java stub
				var javaStubDir = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath, "android", "src");
				var files = Directory.GetFiles (javaStubDir, "CircleImageView.java", SearchOption.AllDirectories);
				CollectionAssert.IsNotEmpty (files, $"{javaStubDir} should contain CircleImageView.java!");
			}
		}

		[Test]
		[Category ("Commercial")]
		public void LibraryProjectsShouldSkipGetPrimaryCpuAbi ()
		{
			if (!CommercialBuildAvailable)
				Assert.Ignore ("Not required on Open Source Builds");
			const string target = "_GetPrimaryCpuAbi";
			var proj = new XamarinAndroidLibraryProject ();
			using (var b = CreateDllBuilder (Path.Combine ("temp", TestName))) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				Assert.IsTrue (b.Output.IsTargetSkipped (target), $"`{target}` should be skipped!");
			}
		}

		[Test]
		[Category ("Commercial")]
		public void LibraryReferenceWithHigherTFVShouldDisplayWarning ([Values (true, false)] bool isRelease)
		{
			if (!CommercialBuildAvailable || Builder.UseDotNet)
				Assert.Ignore ("Not applicable to One .NET or single framework OSS builds.");

			var libproj = new XamarinAndroidLibraryProject () {
				IsRelease = isRelease,
				ProjectName = "Library1",
			};
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = isRelease,
				ProjectName = "App1",
				UseLatestPlatformSdk = false,
				TargetFrameworkVersion = "v9.0",
				References = {
					new BuildItem ("ProjectReference", $"..\\{libproj.ProjectName}\\{Path.GetFileName (libproj.ProjectFilePath)}")
				},
			};
			using (var libBuilder = CreateDllBuilder (Path.Combine ("temp", TestName, libproj.ProjectName)))
			using (var appBuilder = CreateApkBuilder (Path.Combine ("temp", TestName, proj.ProjectName))) {
				Assert.IsTrue (libBuilder.Build (libproj), "Library build should have succeeded.");
				Assert.IsTrue (appBuilder.Build (proj), "App build should have succeeded.");
				StringAssertEx.Contains ("warning XA0105", appBuilder.LastBuildOutput, "Build should have produced warning XA0105.");
			}
		}

		[Test]
		public void AllResourcesInClassLibrary ([Values (true, false)] bool useAapt2)
		{
			AssertAaptSupported (useAapt2);
			var path = Path.Combine ("temp", TestName);

			// Create a "library" with all the application stuff in it
			var lib = new XamarinAndroidApplicationProject {
				ProjectName = "MyLibrary",
				Sources = {
					new BuildItem.Source ("Bar.cs") {
						TextContent = () => "public class Bar { }"
					},
				}
			};
			lib.SetProperty ("AndroidApplication", "False");
			lib.AndroidUseAapt2 = useAapt2;
			if (Builder.UseDotNet) {
				lib.RemoveProperty ("OutputType");
			}

			// Create an "app" that is basically empty and references the library
			var app = new XamarinAndroidLibraryProject {
				ProjectName = "MyApp",
				Sources = {
					new BuildItem.Source ("Foo.cs") {
						TextContent = () => "public class Foo : Bar { }"
					},
				},
				OtherBuildItems = {
					new BuildItem ("None", "Properties\\AndroidManifest.xml") {
						TextContent = () => lib.AndroidManifest,
					},
				}
			};
			app.AndroidResources.Clear (); // No Resources
			if (Builder.UseDotNet) {
				app.SetProperty (KnownProperties.OutputType, "Exe");
			} else {
				app.SetProperty ("AndroidResgenFile", "Resources\\Resource.designer.cs");
				app.SetProperty ("AndroidApplication", "True");
			}
			app.AndroidUseAapt2 = useAapt2;

			app.References.Add (new BuildItem.ProjectReference ($"..\\{lib.ProjectName}\\{lib.ProjectName}.csproj", lib.ProjectName, lib.ProjectGuid));

			using (var libBuilder = CreateDllBuilder (Path.Combine (path, lib.ProjectName)))
			using (var appBuilder = CreateApkBuilder (Path.Combine (path, app.ProjectName))) {
				Assert.IsTrue (libBuilder.Build (lib), "library build should have succeeded.");
				Assert.IsTrue (appBuilder.Build (app), "app build should have succeeded.");

				var r_txt = Path.Combine (Root, appBuilder.ProjectDirectory, app.IntermediateOutputPath, "R.txt");
				FileAssert.Exists (r_txt);

				var resource_designer_cs = GetResourceDesignerPath (appBuilder, app);
				FileAssert.Exists (resource_designer_cs);
				var contents = File.ReadAllText (resource_designer_cs);
				Assert.AreNotEqual ("", contents);
			}
		}

		[Test]
		[Category ("DotNetIgnore")] // n/a on .NET 5+, does not use $(AndroidSupportedAbis)
		public void AbiDelimiters ([Values ("armeabi-v7a%3bx86", "armeabi-v7a,x86")] string abis)
		{
			var proj = new XamarinAndroidApplicationProject ();
			proj.SetProperty (KnownProperties.AndroidSupportedAbis, abis);
			using (var b = CreateApkBuilder (Path.Combine ("temp", $"{nameof (AbiDelimiters)}_{abis.GetHashCode ()}"))) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
			}
		}

		[Test]
		public void WorkManager ()
		{
			var proj = new XamarinFormsAndroidApplicationProject ();
			proj.Sources.Add (new BuildItem.Source ("MyWorker.cs") {
				TextContent = () =>
@"using System;
using Android.Content;
using AndroidX.Work;

public class MyWorker : Worker
{
	public MyWorker (Context c, WorkerParameters p) : base (c, p) { }

	public override Result DoWork () => Result.InvokeSuccess ();
}
"
			});
			proj.PackageReferences.Add (
				Builder.UseDotNet ? KnownPackages.AndroidXWorkRuntime : KnownPackages.Android_Arch_Work_Runtime);
			using (var b = CreateApkBuilder (Path.Combine ("temp", TestName))) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
			}
		}

		[Test]
		public void NuGetizer3000 ()
		{
			var proj = new XamarinAndroidApplicationProject ();
			proj.PackageReferences.Add (KnownPackages.NuGet_Build_Packaging);
			using (var b = CreateApkBuilder (Path.Combine ("temp", nameof (NuGetizer3000)))) {
				b.Target = "GetPackageContents";
				Assert.IsTrue (b.Build (proj), $"{b.Target} should have succeeded.");
			}
		}

		[Test]
		public void NetworkSecurityConfig ()
		{
			var proj = new XamarinAndroidApplicationProject ();
			proj.Sources.Add (new BuildItem ("Compile", "CustomApp.cs") { TextContent = () => @"
using System;
using Android.App;
using Android.Runtime;

namespace UnnamedProject
{
	[Application(Name = ""com.xamarin.android.CustomApp"", NetworkSecurityConfig = ""@xml/network_security_config"")]
	public class CustomApp : Application
	{
		public CustomApp(IntPtr handle, JniHandleOwnership ownerShip) : base(handle, ownerShip) { }
	}
}" });
			proj.AndroidResources.Add (new AndroidItem.AndroidResource (@"Resources\xml\network_security_config.xml") {
				TextContent = () =>
@"<?xml version=""1.0"" encoding=""utf-8""?>
<network-security-config>
    <domain-config>
        <domain includeSubdomains=""true"">example.com</domain>
        <trust-anchors>
            <certificates src=""@raw/my_ca""/>
        </trust-anchors>
    </domain-config>
</network-security-config>"
			});
			proj.AndroidResources.Add (new AndroidItem.AndroidResource (@"Resources\raw\my_ca") {
				BinaryContent = () => Array.Empty<byte> (), // doesn't have to be real, just *exist*
			});

			using (var b = CreateApkBuilder (Path.Combine ("temp", TestName))) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");

				var manifest = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath, "android", "AndroidManifest.xml");
				FileAssert.Exists (manifest);
				var contents = File.ReadAllText (manifest);
				StringAssert.Contains ("android:networkSecurityConfig=\"@xml/network_security_config\"", contents);
			}
		}

		[Test]
		public void AbiNameInIntermediateOutputPath ()
		{
			var proj = new XamarinAndroidApplicationProject ();
			proj.PackageReferences.Add (KnownPackages.Akavache);
			proj.OutputPath = Path.Combine ("bin", "x86", "Debug");
			proj.IntermediateOutputPath = Path.Combine ("obj", "x86", "Debug");
			proj.MainActivity = proj.DefaultMainActivity.Replace ("//${AFTER_ONCREATE}", "var task = Akavache.BlobCache.LocalMachine.GetAllKeys();");
			using (var b = CreateApkBuilder (Path.Combine ("temp", TestName))) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				Assert.IsFalse (StringAssertEx.ContainsText (b.LastBuildOutput, Path.Combine ("armeabi", "libe_sqlite3.so")), "Build should not use `armeabi`.");
			}
		}

		[Test]
		public void PackageNamingPolicy ([Values ("LowercaseMD5", "LowercaseCrc64")] string packageNamingPolicy)
		{
			var proj = new XamarinAndroidApplicationProject ();
			proj.SetProperty ("UseInterpreter", "true");
			proj.SetProperty ("AndroidPackageNamingPolicy", packageNamingPolicy);
			proj.SetAndroidSupportedAbis ("armeabi-v7a", "x86");
			using (var b = CreateApkBuilder ()) {
				Assert.IsTrue (b.Build (proj), "build should have succeeded.");
				var environment = b.Output.GetIntermediaryPath (Path.Combine ("__environment__.txt"));
				FileAssert.Exists (environment);
				var values = new List<string> {
					$"__XA_PACKAGE_NAMING_POLICY__={packageNamingPolicy}"
				};
				if (Builder.UseDotNet) {
					values.Add ("mono.enable_assembly_preload=0");
					values.Add ("DOTNET_MODIFIABLE_ASSEMBLIES=Debug");
				}
				Assert.AreEqual (string.Join (Environment.NewLine, values), File.ReadAllText (environment).Trim ());
			}
		}

		[Test]
		public void KotlinServiceLoader ([Values ("apk", "aab")] string packageFormat)
		{
			var proj = new XamarinAndroidApplicationProject ();
			proj.SetProperty ("AndroidPackageFormat", packageFormat);
			if (packageFormat == "aab")
				// Disable fast deployment for aabs because it is not currently compatible and so gives an XA0119 build error.
				proj.EmbedAssembliesIntoApk = true;
			proj.OtherBuildItems.Add (new BuildItem ("AndroidJavaLibrary", "kotlinx-coroutines-android-1.3.2.jar") {
				WebContent = "https://repo1.maven.org/maven2/org/jetbrains/kotlinx/kotlinx-coroutines-android/1.3.2/kotlinx-coroutines-android-1.3.2.jar"
			});
			proj.OtherBuildItems.Add (new BuildItem ("AndroidJavaLibrary", "gson-2.7.jar") {
				WebContent = "https://repo1.maven.org/maven2/com/google/code/gson/gson/2.7/gson-2.7.jar"
			});
			using (var b = CreateApkBuilder ()) {
				Assert.IsTrue (b.Build (proj), "build should have succeeded.");
				var archive = Path.Combine (Root, b.ProjectDirectory,
					proj.OutputPath, $"{proj.PackageName}.{packageFormat}");
				var prefix = packageFormat == "apk" ? "" : "base/root/";
				var expectedFiles = new [] {
					prefix + "META-INF/maven/com.google.code.gson/gson/pom.xml",
					prefix + "META-INF/services/kotlinx.coroutines.internal.MainDispatcherFactory",
					prefix + "META-INF/services/kotlinx.coroutines.CoroutineExceptionHandler",
				};
				var manifest = prefix + "META-INF/MANIFEST.MF";
				using (var zip = ZipHelper.OpenZip (archive)) {
					Assert.IsFalse (zip.ContainsEntry (manifest, caseSensitive: true), $"{manifest} should *not* exist in {archive}");
					foreach (var expected in expectedFiles) {
						Assert.IsTrue (zip.ContainsEntry (expected, caseSensitive: true), $"{expected} should exist in {archive}");
					}
				}
			}
		}

		[Test]
		public void XA1018 ()
		{
			var proj = new XamarinAndroidApplicationProject ();
			proj.SetProperty ("AndroidManifest", "DoesNotExist");
			using (var builder = CreateApkBuilder ()) {
				builder.ThrowOnBuildFailure = false;
				Assert.IsFalse (builder.Build (proj), "Build should have failed.");
				string error = builder.LastBuildOutput
						.SkipWhile (x => !x.StartsWith ("Build FAILED.", StringComparison.Ordinal))
						.FirstOrDefault (x => x.Contains ("error XA1018:"));
				Assert.IsNotNull (error, "Build should have failed with XA1018.");
				StringAssert.Contains ("DoesNotExist", error, "Error should include the name of the nonexistent file");
			}
		}

		[Test]
		public void XA4313 ([Values ("OpenTK-1.0", "Xamarin.Android.NUnitLite")] string reference)
		{
			var proj = new XamarinAndroidApplicationProject () {
				References = {
					new BuildItem.Reference (reference)
				},
			};
			bool shouldSucceed = !Builder.UseDotNet;
			string expectedText = shouldSucceed ? "succeeded" : "FAILED";
			string warnOrError = shouldSucceed ? "warning" : "error";
			using (var builder = CreateApkBuilder ()) {
				builder.ThrowOnBuildFailure = false;
				Assert.AreEqual (shouldSucceed, builder.Build (proj), $"Build should have {expectedText}.");
				string error = builder.LastBuildOutput
						.SkipWhile (x => !x.StartsWith ($"Build {expectedText}.", StringComparison.Ordinal))
						.FirstOrDefault (x => x.Contains ($"{warnOrError} XA4313"));
				Assert.IsNotNull (error, $"Build should have {expectedText} with XA4313 {warnOrError}.");
			}
		}

		[Test]
		public void OpenTKNugetWorks ()
		{
			var proj = new XamarinAndroidApplicationProject ();
			proj.PackageReferences.Add (KnownPackages.Xamarin_Legacy_OpenTK);
			using (var builder = CreateApkBuilder ()) {
				builder.ThrowOnBuildFailure = false;
				Assert.IsTrue (builder.Build (proj), "Build should have succeeded.");
			}
		}

		[Test]
		public void NUnitLiteNugetWorks ()
		{
			var proj = new XamarinAndroidApplicationProject ();
			proj.PackageReferences.Add (KnownPackages.Xamarin_Legacy_NUnitLite);
			using (var builder = CreateApkBuilder ()) {
				builder.ThrowOnBuildFailure = false;
				Assert.IsTrue (builder.Build (proj), "Build should have succeeded.");
			}
		}

		static readonly object [] XA1027XA1028Source = new object [] {
			new object [] {
				/* linkTool */                   "r8",
				/* enableProguard */             null,
				/* androidEnableProguard */      "true",
				/* expectedBuildResult */        true,
				/* expectedWarning */            "0 Warning(s)",
			},
			new object [] {
				/* linkTool */                   "proguard",
				/* enableProguard */             null,
				/* androidEnableProguard */      "true",
				/* expectedBuildResult */        false,
				/* expectedWarning */            "0 Warning(s)",
			},
			new object [] {
				/* linkTool */                   null,
				/* enableProguard */             null,
				/* androidEnableProguard */      null,
				/* expectedBuildResult */        true,
				/* expectedWarning */            "0 Warning(s)",
			},
			new object [] {
				/* linkTool */                   null,
				/* enableProguard */             "true",
				/* androidEnableProguard */      null,
				/* expectedBuildResult */        false,
				/* expectedWarning */            "warning XA1027:",
			},
			new object [] {
				/* linkTool */                   null,
				/* enableProguard */             null,
				/* androidEnableProguard */      "true",
				/* expectedBuildResult */        false,
				/* expectedWarning */            "warning XA1028:",
			}
		};

		[Test]
		[TestCaseSource (nameof (XA1027XA1028Source))]
		public void XA1027XA1028 (string linkTool, string enableProguard, string androidEnableProguard, bool expectedBuildResult, string expectedWarning)
		{
			var proj = new XamarinAndroidApplicationProject {
				LinkTool = linkTool,
				IsRelease = true
			};
			proj.SetProperty ("EnableProguard", enableProguard);
			proj.SetProperty ("AndroidEnableProguard", androidEnableProguard);
			using (var builder = CreateApkBuilder ()) {
				builder.Target = "_CheckNonIdealConfigurations";
				builder.ThrowOnBuildFailure = expectedBuildResult;
				builder.Build (proj);
				Assert.IsNotNull(
					builder.LastBuildOutput
						.SkipWhile (x => !x.StartsWith (expectedBuildResult ? "Build succeeded." : "Build FAILED.", StringComparison.Ordinal))
						.FirstOrDefault (x => x.Contains (expectedWarning)),
					$"Build output should contain '{expectedWarning}'.");
			}
		}

		[Test]
		public void XA4310 ([Values ("apk", "aab")] string packageFormat)
		{
			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
			};
			proj.SetProperty ("AndroidKeyStore", "true");
			proj.SetProperty ("AndroidSigningKeyStore", "DoesNotExist");
			proj.SetProperty ("AndroidSigningStorePass", "android");
			proj.SetProperty ("AndroidSigningKeyAlias", "mykey");
			proj.SetProperty ("AndroidSigningKeyPass", "android");
			proj.SetProperty ("AndroidPackageFormat", packageFormat);
			using (var builder = CreateApkBuilder ()) {
				builder.ThrowOnBuildFailure = false;
				Assert.IsFalse (builder.Build (proj), "Build should have failed with XA4310.");

				StringAssertEx.Contains ("error XA4310", builder.LastBuildOutput, "Error should be XA4310");
				StringAssertEx.Contains ("`DoesNotExist`", builder.LastBuildOutput, "Error should include the name of the nonexistent file");
				builder.AssertHasNoWarnings ();
			}
		}

		[Test]
		[NonParallelizable]
		public void CheckLintErrorsAndWarnings ()
		{
			if (TestEnvironment.IsUsingJdk8)
				Assert.Ignore ("https://github.com/xamarin/xamarin-android/issues/5698");

			string disabledIssues = "StaticFieldLeak,ObsoleteSdkInt,AllowBackup,ExportedReceiver,RedundantLabel";

			var proj = new XamarinAndroidApplicationProject () {
				PackageReferences = {
					KnownPackages.AndroidSupportV4_27_0_2_1,
					KnownPackages.SupportConstraintLayout_1_0_2_2,
				},
			};
			proj.UseLatestPlatformSdk = false;
			proj.SetProperty ("AndroidLintEnabled", true.ToString ());
			proj.SetProperty ("AndroidLintDisabledIssues", disabledIssues);
			proj.SetProperty ("AndroidLintEnabledIssues", "");
			proj.SetProperty ("AndroidLintCheckIssues", "");
			proj.MainActivity = proj.DefaultMainActivity.Replace ("public class MainActivity : Activity", @"
		[IntentFilter (new[] { Android.Content.Intent.ActionView },
			Categories = new [] { Android.Content.Intent.CategoryDefault, Android.Content.Intent.CategoryBrowsable },
			DataHost = ""mydomain.com"",
			DataScheme = ""http""
		)]
		public class MainActivity : Activity
			");
			proj.AndroidResources.Add (new AndroidItem.AndroidResource ("Resources\\layout\\test.axml") {
				TextContent = () => {
					return @"<?xml version=""1.0"" encoding=""utf-8""?>
<ConstraintLayout xmlns:android=""http://schemas.android.com/apk/res/android""
	xmlns:app=""http://schemas.android.com/apk/res-auto""
	android:orientation=""vertical""
	android:layout_width=""fill_parent""
	android:layout_height=""fill_parent"">
	<TextView android:id=""@+id/foo""
		android:layout_width=""150dp""
		android:layout_height=""wrap_content""
		app:layout_constraintTop_toTopOf=""parent""
	/>
</ConstraintLayout>";
				}
			});
			using (var b = CreateApkBuilder ("temp/CheckLintErrorsAndWarnings", cleanupOnDispose: false)) {
				int maxApiLevel = AndroidSdkResolver.GetMaxInstalledPlatform ();
				string apiLevel;
				proj.TargetFrameworkVersion = b.LatestTargetFrameworkVersion (out apiLevel);
				if (int.TryParse (apiLevel, out int a) && a < maxApiLevel)
					disabledIssues += ",OldTargetApi";
				proj.SetProperty ("AndroidLintDisabledIssues", disabledIssues);
				proj.MinSdkVersion = "24";
				proj.TargetSdkVersion = apiLevel;
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				StringAssertEx.DoesNotContain ("XA0102", b.LastBuildOutput, "Output should not contain any XA0102 warnings");
				StringAssertEx.DoesNotContain ("XA0103", b.LastBuildOutput, "Output should not contain any XA0103 errors");
				Assert.IsTrue (b.Clean (proj), "Clean should have succeeded.");
			}
		}

		[Test]
		public void CheckLintConfigMerging ()
		{
			if (TestEnvironment.IsUsingJdk8)
				Assert.Ignore ("https://github.com/xamarin/xamarin-android/issues/5698");

			var proj = new XamarinAndroidApplicationProject ();
			proj.SetProperty ("AndroidLintEnabled", true.ToString ());
			proj.OtherBuildItems.Add (new AndroidItem.AndroidLintConfig ("lint1.xml") {
				TextContent = () => @"<?xml version=""1.0"" encoding=""UTF-8""?>
<lint>
	<issue id=""NewApi"" severity=""warning"" />
</lint>"
			});
			proj.OtherBuildItems.Add (new AndroidItem.AndroidLintConfig ("lint2.xml") {
				TextContent = () => @"<?xml version=""1.0"" encoding=""UTF-8""?>
<lint>
	<issue id=""MissingApplicationIcon"" severity=""ignore"" />
</lint>"
			});
			using (var b = CreateApkBuilder ("temp/CheckLintConfigMerging", false, false)) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				var lintFile = Path.Combine (Root, "temp", "CheckLintConfigMerging", proj.IntermediateOutputPath, "lint.xml");
				Assert.IsTrue (File.Exists (lintFile), "{0} should have been created.", lintFile);
				var doc = XDocument.Load (lintFile);
				Assert.IsNotNull (doc, "Document should have loaded successfully.");
				Assert.IsNotNull (doc.Element ("lint"), "The xml file should have a lint element.");
				Assert.IsNotNull (doc.Element ("lint")
					.Elements ()
					.Any (x => x.Name == "Issue" && x.Attribute ("id").Value == "MissingApplicationIcon"), "Element is missing");
				Assert.IsNotNull (doc.Element ("lint")
					.Elements ()
					.Any (x => x.Name == "Issue" && x.Attribute ("id").Value == "NewApi"), "Element is missing");
				Assert.IsTrue (b.Clean (proj), "Clean should have succeeded.");
				Assert.IsFalse (File.Exists (lintFile), "{0} should have been deleted on clean.", lintFile);
			}
		}

		[Test]
		[NonParallelizable] // fails on NuGet restore
		/// <summary>
		/// Reference https://bugzilla.xamarin.com/show_bug.cgi?id=29568
		/// </summary>
		public void BuildLibraryWhichUsesResources ([Values (false, true)] bool isRelease)
		{
			var proj = new XamarinAndroidLibraryProject { IsRelease = isRelease };
			proj.PackageReferences.Add (KnownPackages.AndroidXAppCompat);
			proj.AndroidResources.Add (new AndroidItem.AndroidResource ("Resources\\values\\Styles.xml") {
				TextContent = () => @"<?xml version=""1.0"" encoding=""UTF-8"" ?>
<resources>
	<style name=""AppTheme"" parent=""Theme.AppCompat.Light.NoActionBar"" />
</resources>"
			});
			proj.SetProperty ("AndroidResgenClass", "Resource");
			proj.SetProperty ("AndroidResgenFile", () => "Resources\\Resource.designer" + proj.Language.DefaultExtension);
			using (var b = CreateDllBuilder ()) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
			}
		}

		[Test]
		public void AndroidXClassLibraryNoResources ()
		{
			var proj = new XamarinAndroidLibraryProject ();
			proj.AndroidResources.Clear ();
			proj.PackageReferences.Add (KnownPackages.AndroidXLegacySupportV4);
			using (var b = CreateDllBuilder ()) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
			}
		}

#pragma warning disable 414
		static object [] BuildApplicationWithJavaSourceChecks = new object [] {
			new object[] {
				/* isRelease */           false,
				/* expectedResult */      true,
			},
			new object[] {
				/* isRelease */           true,
				/* expectedResult */      true,
			},
		};
#pragma warning restore 414

		[Test]
		[TestCaseSource (nameof (BuildApplicationWithJavaSourceChecks))]
		public void BuildApplicationWithJavaSource (bool isRelease, bool expectedResult)
		{
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = isRelease,
				OtherBuildItems = {
					new BuildItem (AndroidBuildActions.AndroidJavaSource, "TestMe.java") {
						TextContent = () => "public class TestMe { }",
						Encoding = Encoding.ASCII
					},
				}
			};
			proj.SetProperty ("TargetFrameworkVersion", "v5.0");
			using (var b = CreateApkBuilder ()) {
				b.ThrowOnBuildFailure = false;
				Assert.AreEqual (expectedResult, b.Build (proj), "Build should have {0}", expectedResult ? "succeeded" : "failed");
				if (expectedResult)
					StringAssertEx.DoesNotContain ("XA9002", b.LastBuildOutput, "XA9002 should not have been raised");
				else
					StringAssertEx.Contains ("XA9002", b.LastBuildOutput, "XA9002 should have been raised");
				Assert.IsTrue (b.Clean (proj), "Clean should have succeeded.");
			}
		}

		[Test]
		public void CheckContentBuildAction ()
		{
			var metadata = "CopyToOutputDirectory=PreserveNewest";
			var path = Path.Combine ("temp", TestName);

			var lib = new XamarinAndroidLibraryProject {
				ProjectName = "Library1",
				Sources = {
					new BuildItem.Source ("Bar.cs") {
						TextContent = () => "public class Bar { }"
					},
				},
				OtherBuildItems = {
					new BuildItem.Content ("TestContent.txt") {
						TextContent = () => "Test Content from Library",
						MetadataValues = metadata,
					},
					new BuildItem.Content ("TestContent2.txt") {
						TextContent = () => "Content excluded from check",
						MetadataValues = "ExcludeFromContentCheck=true",
					}
				}
			};

			var proj = new XamarinAndroidApplicationProject {
				ProjectName = "App",
				Sources = {
					new BuildItem.Source ("Foo.cs") {
						TextContent = () => "public class Foo : Bar { }"
					},
				},
				References = {
					new BuildItem ("ProjectReference", "..\\Library1\\Library1.csproj"),
				}
			};
			using (var libBuilder = CreateDllBuilder (Path.Combine (path, lib.ProjectName)))
			using (var appBuilder = CreateApkBuilder (Path.Combine (path, proj.ProjectName))) {
				Assert.IsTrue (libBuilder.Build (lib), "library should have built successfully");
				StringAssertEx.Contains ("TestContent.txt : warning XA0101: @(Content) build action is not supported", libBuilder.LastBuildOutput,
					"Build Output did not contain 'TestContent.txt : warning XA0101'.");

				proj.AndroidResources.Add (new BuildItem.Content ("TestContent.txt") {
					TextContent = () => "Test Content",
					MetadataValues = metadata,
				});
				proj.AndroidResources.Add (new BuildItem.Content ("TestContent1.txt") {
					TextContent = () => "Test Content 1",
					MetadataValues = metadata,
				});
				Assert.IsTrue (appBuilder.Build (proj), "app should have built successfully");
				StringAssertEx.Contains ("TestContent.txt : warning XA0101: @(Content) build action is not supported", appBuilder.LastBuildOutput,
					"Build Output did not contain 'TestContent.txt : warning XA0101'.");
				StringAssertEx.Contains ("TestContent1.txt : warning XA0101: @(Content) build action is not supported", appBuilder.LastBuildOutput,
					"Build Output did not contain 'TestContent1.txt : warning XA0101'.");
				// Ensure items excluded from check do not produce warnings.
				StringAssertEx.DoesNotContain ("TestContent2.txt : warning XA0101", libBuilder.LastBuildOutput,
					"Build Output contains 'TestContent2.txt : warning XA0101'.");
			}
		}

		[Test]
		public void BuildApplicationCheckThatAddStaticResourcesTargetDoesNotRerun ()
		{
			var proj = new XamarinAndroidApplicationProject ();
			using (var b = CreateApkBuilder ()) {
				b.ThrowOnBuildFailure = false;
				Assert.IsTrue (b.Build (proj), "Build should not have failed");
				Assert.IsFalse (
					b.Output.IsTargetSkipped ("_AddStaticResources"),
					"The _AddStaticResources should have been run");
				Assert.IsTrue (b.Build (proj), "Build should not have failed");
				Assert.IsTrue (
					b.Output.IsTargetSkipped ("_AddStaticResources"),
					"The _AddStaticResources should NOT have been run");
			}
		}

		[Test]
		public void CheckJavaError ()
		{
			var proj = new XamarinAndroidApplicationProject ();
			proj.OtherBuildItems.Add (new BuildItem (AndroidBuildActions.AndroidJavaSource, "TestMe.java") {
				TextContent = () => "public classo TestMe { }",
				Encoding = Encoding.ASCII
			});
			proj.OtherBuildItems.Add (new BuildItem (AndroidBuildActions.AndroidJavaSource, "TestMe2.java") {
				TextContent = () => "public class TestMe2 {" +
					"public vod Test ()" +
					"}",
				Encoding = Encoding.ASCII
			});
			using (var b = CreateApkBuilder ("temp/CheckJavaError")) {
				b.ThrowOnBuildFailure = false;
				Assert.IsFalse (b.Build (proj), "Build should have failed.");
				var ext = b.IsUnix ? "" : ".exe";
				var text = $"TestMe.java(1,8): javac{ext} error JAVAC0000:  error: class, interface, or enum expected";
				Assert.IsTrue (StringAssertEx.ContainsText (b.LastBuildOutput, text), "TestMe.java(1,8) expected");
				text = $"TestMe2.java(1,41): javac{ext} error JAVAC0000:  error: ';' expected";
				Assert.IsTrue (StringAssertEx.ContainsText (b.LastBuildOutput, text), "TestMe2.java(1,41) expected");
				Assert.IsTrue (b.Clean (proj), "Clean should have succeeded.");
			}
		}

		[Test]
		/// <summary>
		/// Based on issue raised in
		/// https://bugzilla.xamarin.com/show_bug.cgi?id=28721
		/// </summary>
		public void DuplicateValuesInResourceCaseMap ()
		{
			var proj = new XamarinAndroidApplicationProject ();
			proj.AndroidResources.Add (new AndroidItem.AndroidResource ("Resources\\layout\\test.axml") {
				TextContent = () => {
					return "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<LinearLayout xmlns:android=\"http://schemas.android.com/apk/res/android\"\n    android:orientation=\"vertical\"\n    android:layout_width=\"fill_parent\"\n    android:layout_height=\"fill_parent\"\n    />";
				}
			});
			proj.AndroidResources.Add (new AndroidItem.AndroidResource ("Resources\\layout\\test.axml") {
				MetadataValues = "Link=Resources\\layout-xhdpi\\test.axml"
			});
			proj.AndroidResources.Add (new AndroidItem.AndroidResource ("Resources\\layout\\test.axml") {
				MetadataValues = "Link=Resources\\layout-xhdpi\\Test.axml"
			});
			using (var b = CreateApkBuilder ()) {
				b.ThrowOnBuildFailure = false;
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				Assert.IsTrue (b.Clean (proj), "Clean should have succeeded.");
			}
		}

		[Test]
		public void CheckLintResourceFileReferencesAreFixed ()
		{
			if (TestEnvironment.IsUsingJdk8)
				Assert.Ignore ("https://github.com/xamarin/xamarin-android/issues/5698");

			var proj = new XamarinAndroidApplicationProject () {
				PackageReferences = {
					KnownPackages.AndroidSupportV4_27_0_2_1,
					KnownPackages.SupportConstraintLayout_1_0_2_2,
				},
			};
			proj.SetProperty ("AndroidLintEnabled", true.ToString ());
			proj.AndroidResources.Add (new AndroidItem.AndroidResource ("Resources\\layout\\test.axml") {
				TextContent = () => {
					return @"<?xml version=""1.0"" encoding=""utf-8""?>
<ConstraintLayout xmlns:android=""http://schemas.android.com/apk/res/android""
	xmlns:app=""http://schemas.android.com/apk/res-auto""
	android:orientation=""vertical""
	android:layout_width=""fill_parent""
	android:layout_height=""fill_parent"">
	<TextView android:id=""@+id/foo""
		android:layout_width=""150dp""
		android:layout_height=""wrap_content""
		app:layout_constraintTop_toTopOf=""parent""
	/>
	<EditText
		android:id=""@+id/phone""
		android:layout_width=""fill_parent""
		android:layout_height=""wrap_content""
		android:hint=""Hint me up.""
	/>
 </ConstraintLayout>";
				}
			});
			using (var b = CreateApkBuilder (Path.Combine ("temp", TestName))) {
				Assert.IsTrue (b.Build (proj), "Project should have built.");
				StringAssertEx.Contains ("XA0102", b.LastBuildOutput, "Output should contain XA0102 warnings");
				var errorFilePath = Path.Combine (proj.IntermediateOutputPath, "android", proj.IntermediateOutputPath, "res", "layout", "test.xml");
				StringAssertEx.DoesNotContain (errorFilePath, b.LastBuildOutput, $"Path {errorFilePath} should have been replaced.");
			}
		}
	}
}
