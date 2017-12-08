using System;
using System.IO;
using NUnit.Framework;
using Xamarin.ProjectTools;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Xml.Linq;

namespace Xamarin.Android.Build.Tests
{
	[Parallelizable (ParallelScope.Children)]
	public class PackagingTest : BaseTest
	{
#pragma warning disable 414
		static object [] ManagedSymbolsArchiveSource = new object [] {
			//           isRelease, monoSymbolArchive, archiveShouldExists,
			new object[] { false    , false              , false },
			new object[] { true     , true               , true },
			new object[] { true     , false              , false },
		};
#pragma warning restore 414

		[Test]
		[TestCaseSource (nameof(ManagedSymbolsArchiveSource))]
		public void CheckManagedSymbolsArchive (bool isRelease, bool monoSymbolArchive, bool archiveShouldExists)
		{
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = isRelease,
			};
			proj.SetProperty (proj.ReleaseProperties, "MonoSymbolArchive", monoSymbolArchive);
			proj.SetProperty (proj.ReleaseProperties, KnownProperties.AndroidCreatePackagePerAbi, "true");
			proj.SetProperty (proj.ReleaseProperties, KnownProperties.AndroidSupportedAbis, "armeabi-v7a;x86");
			using (var b = CreateApkBuilder (Path.Combine ("temp", TestContext.CurrentContext.Test.Name))) {
				b.Verbosity = Microsoft.Build.Framework.LoggerVerbosity.Diagnostic;
				b.ThrowOnBuildFailure = false;
				Assert.IsTrue (b.Build (proj), "first build failed");
				var outputPath = Path.Combine (Root, b.ProjectDirectory, proj.OutputPath);
				var archivePath = Path.Combine (outputPath, proj.PackageName + ".apk.mSYM");
				Assert.AreEqual (archiveShouldExists, Directory.Exists (archivePath),
					string.Format ("The msym archive {0} exist.", archiveShouldExists ? "should" : "should not"));
			}
		}

		[Test]
		public void CheckBuildIdIsUnique ()
		{
			Dictionary<string, string> buildIds = new Dictionary<string, string> ();
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
			};
			proj.SetProperty (proj.ReleaseProperties, "MonoSymbolArchive", "True");
			proj.SetProperty (proj.ReleaseProperties, "DebugSymbols", "true");
			proj.SetProperty (proj.ReleaseProperties, "DebugType", "PdbOnly");
			proj.SetProperty (proj.ReleaseProperties, KnownProperties.AndroidCreatePackagePerAbi, "true");
			proj.SetProperty (proj.ReleaseProperties, KnownProperties.AndroidSupportedAbis, "armeabi-v7a;x86");
			using (var b = CreateApkBuilder (Path.Combine ("temp", TestContext.CurrentContext.Test.Name))) {
				b.Verbosity = Microsoft.Build.Framework.LoggerVerbosity.Diagnostic;
				b.ThrowOnBuildFailure = false;
				Assert.IsTrue (b.Build (proj), "first build failed");
				var outputPath = Path.Combine (Root, b.ProjectDirectory, proj.OutputPath);
				var archivePath = Path.Combine (outputPath, proj.PackageName + ".apk.mSYM");
				var allFilesInArchive = Directory.GetFiles (archivePath, "*", SearchOption.AllDirectories);
				string extension = "dll";
				Assert.IsTrue (allFilesInArchive.Any (x => Path.GetFileName (x) == $"{proj.ProjectName}.{extension}"), $"{proj.ProjectName}.{extension} should exist in {archivePath}");
				//NOTE: Windows is still generating mdb files here
				extension = IsWindows ? "dll.mdb" : "pdb";
				Assert.IsTrue (allFilesInArchive.Any (x => Path.GetFileName (x) == $"{proj.ProjectName}.{extension}"), $"{proj.ProjectName}.{extension} should exist in {archivePath}");
				foreach (var abi in new string [] { "armeabi-v7a", "x86" }) {
					using (var apk = ZipHelper.OpenZip (Path.Combine (outputPath, proj.PackageName + "-" + abi + ".apk"))) {
						var data = ZipHelper.ReadFileFromZip (apk, "environment");
						var env = Encoding.ASCII.GetString (data);
						var lines = env.Split (new char [] { '\n' });
						Assert.IsTrue (lines.Any (x => x.Contains ("XAMARIN_BUILD_ID")),
							"The environment should contain a XAMARIN_BUIL_ID");
						var buildID = lines.First (x => x.StartsWith ("XAMARIN_BUILD_ID", StringComparison.InvariantCultureIgnoreCase));
						buildIds.Add (abi, buildID);
					}
				}
				Assert.IsFalse (buildIds.Values.Any (x => buildIds.Values.Any (v => v != x)),
					"All the XAMARIN_BUILD_ID values should be the same");

				var msymDirectory = Path.Combine (Root, b.ProjectDirectory, proj.OutputPath, proj.PackageName + ".apk.mSYM");
				Assert.IsTrue (File.Exists (Path.Combine (msymDirectory, "manifest.xml")), "manifest.xml should exist in", msymDirectory);
				var doc = XDocument.Load (Path.Combine (msymDirectory, "manifest.xml"));

				Assert.IsTrue (doc.Element ("mono-debug")
					.Elements ()
					.Any (x => x.Name == "app-id" && x.Value == proj.PackageName), "app-id is has an incorrect value.");
				var buildId = buildIds.First ().Value;
				Assert.IsTrue (doc.Element ("mono-debug")
					.Elements ()
					.Any (x => x.Name == "build-id" && x.Value == buildId.Replace ("XAMARIN_BUILD_ID=", "")), "build-id is has an incorrect value.");
			}
		}

		[Test]
		public void CheckIncludedAssemblies ()
		{
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
				Packages = {
					new Package () {
						Id = "System.Runtime.InteropServices.WindowsRuntime",
						Version = "4.0.1",
						TargetFramework = "monoandroid71",
					},
				},
			};
			proj.References.Add (new BuildItem.Reference ("Mono.Data.Sqlite.dll"));
			var expectedFiles = new string [] {
				"Java.Interop.dll",
				"Mono.Android.dll",
				"mscorlib.dll",
				"System.Collections.Concurrent.dll",
				"System.Collections.dll",
				"System.Core.dll",
				"System.Diagnostics.Debug.dll",
				"System.dll",
				"System.Linq.dll",
				"System.Reflection.dll",
				"System.Reflection.Extensions.dll",
				"System.Runtime.dll",
				"System.Runtime.Extensions.dll",
				"System.Runtime.InteropServices.dll",
				"System.Runtime.Serialization.dll",
				"System.Threading.dll",
				"UnnamedProject.dll",
				"Mono.Data.Sqlite.dll",
				"Mono.Data.Sqlite.dll.config",
			};
			using (var b = CreateApkBuilder (Path.Combine ("temp", TestContext.CurrentContext.Test.Name))) {
				b.Verbosity = Microsoft.Build.Framework.LoggerVerbosity.Diagnostic;
				b.ThrowOnBuildFailure = false;
				Assert.IsTrue (b.Build (proj), "build failed");
				var apk = Path.Combine (Root, b.ProjectDirectory,
						proj.IntermediateOutputPath, "android", "bin", "UnnamedProject.UnnamedProject.apk");
				using (var zip = ZipHelper.OpenZip (apk)) {
					var existingFiles = zip.Where (a => a.FullName.StartsWith ("assemblies/", StringComparison.InvariantCultureIgnoreCase));
					var missingFiles = expectedFiles.Where (x => !zip.ContainsEntry ("assmelbies/" + Path.GetFileName (x)));
					Assert.IsTrue (missingFiles.Any (),
					string.Format ("The following Expected files are missing. {0}",
						string.Join (Environment.NewLine, missingFiles)));
					var additionalFiles = existingFiles.Where (x => !expectedFiles.Contains (Path.GetFileName (x.FullName)));
					Assert.IsTrue (!additionalFiles.Any (),
						string.Format ("Unexpected Files found! {0}",
						string.Join (Environment.NewLine, additionalFiles.Select (x => x.FullName))));
				}
			}
		}

		[Test]
		public void CheckClassesDexIsIncluded ()
		{
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
			};
			using (var b = CreateApkBuilder (Path.Combine ("temp", TestContext.CurrentContext.Test.Name))) {
				b.Verbosity = Microsoft.Build.Framework.LoggerVerbosity.Diagnostic;
				b.ThrowOnBuildFailure = false;
				Assert.IsTrue (b.Build (proj), "build failed");
				var apk = Path.Combine (Root, b.ProjectDirectory,
						proj.IntermediateOutputPath, "android", "bin", "UnnamedProject.UnnamedProject.apk");
				using (var zip = ZipHelper.OpenZip (apk)) {
					Assert.IsTrue (zip.ContainsEntry ("classes.dex"), "Apk should contain classes.dex");
				}
			}
		}

		[Test]
		public void CheckIncludedNativeLibraries ()
		{
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
			};
			proj.Packages.Add(KnownPackages.SQLitePCLRaw_Core);
			proj.SetProperty(proj.ReleaseProperties, KnownProperties.AndroidSupportedAbis, "x86");
			using (var b = CreateApkBuilder (Path.Combine ("temp", TestContext.CurrentContext.Test.Name))) {
				b.Verbosity = Microsoft.Build.Framework.LoggerVerbosity.Diagnostic;
				b.ThrowOnBuildFailure = false;
				Assert.IsTrue (b.Build (proj), "build failed");
				var apk = Path.Combine (Root, b.ProjectDirectory,
						proj.IntermediateOutputPath, "android", "bin", "UnnamedProject.UnnamedProject.apk");
				using (var zip = ZipHelper.OpenZip (apk)) {
					var libFiles = zip.Where (x => x.FullName.StartsWith("lib/") && !x.FullName.Equals("lib/", StringComparison.InvariantCultureIgnoreCase));
					var abiPaths = new string[] { "lib/x86/" };
					foreach (var file in libFiles)
						Assert.IsTrue(abiPaths.Any (x => file.FullName.Contains (x)), $"Apk contains an unnesscary lib file: {file.FullName}");
				}
			}
		}

		[Test]
		public void ExplicitPackageNamingPolicy ()
		{
			var proj = new XamarinAndroidApplicationProject ();
			proj.SetProperty (proj.DebugProperties, "AndroidPackageNamingPolicy", "Lowercase");
			using (var b = CreateApkBuilder (Path.Combine ("temp", TestContext.CurrentContext.Test.Name))) {
				b.Verbosity = Microsoft.Build.Framework.LoggerVerbosity.Diagnostic;
				Assert.IsTrue (b.Build (proj), "build failed");
				var text = b.Output.GetIntermediaryAsText (b.Output.IntermediateOutputPath, Path.Combine ("android", "src", "unnamedproject", "MainActivity.java"));
				Assert.IsTrue (text.Contains ("package unnamedproject;"), "expected package not found in the source.");
			}
		}

		[Test]
		public void CheckSignApk ([Values(true, false)] bool useApkSigner)
		{
			string ext = Environment.OSVersion.Platform != PlatformID.Unix ? ".exe" : "";
			if (useApkSigner && !File.Exists (Path.Combine (AndroidSdkPath, "build-tools", "26.0.1", "apksigner"+ ext))) {
				Assert.Ignore ("Skipping test. Required build-tools verison 26.0.1 is not installed.");
			}
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
			};
			if (useApkSigner) {
				proj.SetProperty ("AndroidUseApkSigner", "true");
				proj.SetProperty ("AndroidSdkBuildToolsVersion", "26.0.1");
			} else {
				proj.RemoveProperty ("AndroidUseApkSigner");
			}
			using (var b = CreateApkBuilder (Path.Combine ("temp", TestContext.CurrentContext.Test.Name))) {
				b.Verbosity = Microsoft.Build.Framework.LoggerVerbosity.Diagnostic;
				Assert.IsTrue (b.Build (proj), "build failed");
			}
		}

		[Test]
		public void NetStandardReferenceTest ()
		{
			var netStandardProject = new DotNetStandard () {
				Language = XamarinAndroidProjectLanguage.CSharp,
				ProjectName = "XamFormsSample",
				ProjectGuid = Guid.NewGuid ().ToString (),
				Sdk = "Microsoft.NET.Sdk",
				TargetFramework = "netstandard1.4",
				IsRelease = true,
				PackageTargetFallback = "portable-net45+win8+wpa81+wp8",
				PackageReferences = {
					KnownPackages.XamarinFormsPCL_2_3_4_231,
					new Package () {
						Id = "System.IO.Packaging",
						Version = "4.4.0",
					},
					new Package () {
						Id = "Newtonsoft.Json",
						Version = "10.0.3"
					},
				},
				OtherBuildItems = {
					new BuildItem ("None") {
						Remove = () => "**\\*.xaml",
					},
					new BuildItem ("Compile") {
						Update = () => "**\\*.xaml.cs",
						DependentUpon = () => "%(Filename)"
					},
					new BuildItem ("EmbeddedResource") {
						Include = () => "**\\*.xaml",
						SubType = () => "Designer",
						Generator = () => "MSBuild:UpdateDesignTimeXaml",
					},
				},
				Sources = {
					new BuildItem.Source ("App.xaml.cs") {
						TextContent = () => @"using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

using Xamarin.Forms;

namespace XamFormsSample
{
    public partial class App : Application
    {
        public App()
        {
            JsonConvert.DeserializeObject<string>(""test"");
            InitializeComponent();
        }

        protected override void OnStart()
        {
            // Handle when your app starts
        }

        protected override void OnSleep()
        {
            // Handle when your app sleeps
        }

        protected override void OnResume()
        {
            // Handle when your app resumes
        }
    }
}",
					},
					new BuildItem.Source ("App.xaml") {
						TextContent = () => @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<Application xmlns=""http://xamarin.com/schemas/2014/forms""
             xmlns:x=""http://schemas.microsoft.com/winfx/2009/xaml""
             x:Class=""XamFormsSample.App"">
  <Application.Resources>
    <!-- Application resource dictionary -->
  </Application.Resources>
</Application>",
					},
				},
			};

			var app = new XamarinAndroidApplicationProject () {
				ProjectName = "App1",
				IsRelease = true,
				UseLatestPlatformSdk = true,
				References = {
					new BuildItem.Reference ("Mono.Android.Export"),
					new BuildItem.ProjectReference ($"..\\{netStandardProject.ProjectName}\\{netStandardProject.ProjectName}.csproj",
						netStandardProject.ProjectName, netStandardProject.ProjectGuid),
				},
				PackageReferences = {
					KnownPackages.SupportDesign_25_4_0_1,
					KnownPackages.SupportV7CardView_24_2_1,
					KnownPackages.AndroidSupportV4_25_4_0_1,
					KnownPackages.SupportCoreUtils_25_4_0_1,
					KnownPackages.SupportMediaCompat_25_4_0_1,
					KnownPackages.SupportFragment_25_4_0_1,
					KnownPackages.SupportCoreUI_25_4_0_1,
					KnownPackages.SupportCompat_25_4_0_1,
					KnownPackages.SupportV7AppCompat_25_4_0_1,
					KnownPackages.XamarinForms_2_3_4_231,
				}
			};
			app.SetProperty (KnownProperties.AndroidSupportedAbis, "x86;armeabi-v7a");
			var expectedFiles = new string [] {
				"Java.Interop.dll",
				"Mono.Android.dll",
				"mscorlib.dll",
				"mscorlib.dll.mdb",
				"System.Collections.Concurrent.dll",
				"System.Collections.dll",
				"System.Core.dll",
				"System.Diagnostics.Debug.dll",
				"System.dll",
				"System.Linq.dll",
				"System.Reflection.dll",
				"System.Reflection.Extensions.dll",
				"System.Runtime.dll",
				"System.Runtime.Extensions.dll",
				"System.Runtime.InteropServices.dll",
				"System.Runtime.Serialization.dll",
				"System.Threading.dll",
				"System.IO.Packaging.dll",
				"System.IO.Compression.dll",
				"System.IO.Compression.pdb",
				"Mono.Android.Export.dll",
				"Mono.Android.Export.pdb",
				"App1.dll",
				"App1.pdb",
				"FormsViewGroup.dll",
				"FormsViewGroup.dll.mdb",
				"Xamarin.Android.Support.Compat.dll",
				"Xamarin.Android.Support.Core.UI.dll",
				"Xamarin.Android.Support.Core.Utils.dll",
				"Xamarin.Android.Support.Design.dll",
				"Xamarin.Android.Support.Fragment.dll",
				"Xamarin.Android.Support.Media.Compat.dll",
				"Xamarin.Android.Support.v4.dll",
				"Xamarin.Android.Support.v7.AppCompat.dll",
				"Xamarin.Android.Support.Animated.Vector.Drawable.dll",
				"Xamarin.Android.Support.Vector.Drawable.dll",
				"Xamarin.Android.Support.Transition.dll",
				"Xamarin.Android.Support.v7.MediaRouter.dll",
				"Xamarin.Android.Support.v7.RecyclerView.dll",
				"Xamarin.Android.Support.Annotations.dll",
				"Xamarin.Android.Support.v7.CardView.dll",
				"Xamarin.Forms.Core.dll",
				"Xamarin.Forms.Core.dll.mdb",
				"Xamarin.Forms.Platform.Android.dll",
				"Xamarin.Forms.Platform.Android.dll.mdb",
				"Xamarin.Forms.Platform.dll",
				"Xamarin.Forms.Xaml.dll",
				"Xamarin.Forms.Xaml.dll.mdb",
				"XamFormsSample.dll",
				"XamFormsSample.pdb",
				"Mono.Android.pdb",
				"System.Core.pdb",
				"System.pdb",
				"Mono.Security.dll",
				"Mono.Security.pdb",
				"System.Xml.dll",
				"System.Xml.pdb",
				"System.ComponentModel.Composition.dll",
				"System.ComponentModel.Composition.pdb",
				"System.Net.Http.dll",
				"System.Net.Http.pdb",
				"System.Runtime.Serialization.pdb",
				"System.ServiceModel.Internals.dll",
				"System.ServiceModel.Internals.pdb",
				"System.Threading.Tasks.dll",
				"System.ObjectModel.dll",
				"System.Globalization.dll",
				"System.ComponentModel.dll",
				"System.Xml.ReaderWriter.dll",
				"System.Linq.Expressions.dll",
				"System.IO.dll",
				"System.Dynamic.Runtime.dll",
				"System.Text.RegularExpressions.dll",
				"System.Diagnostics.Tools.dll",
				"Newtonsoft.Json.dll",
				"Microsoft.CSharp.dll",
				"System.Numerics.dll",
				"System.Xml.Linq.dll",
			};
			var path = Path.Combine ("temp", TestContext.CurrentContext.Test.Name);
			using (var builder = CreateDllBuilder (Path.Combine (path, netStandardProject.ProjectName), cleanupOnDispose: false)) {
				if (!Directory.Exists (builder.MicrosoftNetSdkDirectory))
					Assert.Ignore ("Microsoft.NET.Sdk not found.");
				builder.RequiresMSBuild = true;
				builder.Target = "Restore";
				Assert.IsTrue (builder.Build (netStandardProject), "XamFormsSample Nuget packages should have been restored.");
				builder.Target = "Build";
				Assert.IsTrue (builder.Build (netStandardProject), "XamFormsSample should have built.");
				using (var ab = CreateApkBuilder (Path.Combine (path, app.ProjectName), cleanupOnDispose: false)) {
					ab.RequiresMSBuild = true;
					ab.Target = "Restore";
					Assert.IsTrue (ab.Build (app), "App should have built.");
					ab.Target = "SignAndroidPackage";
					Assert.IsTrue (ab.Build (app), "App should have built.");
					var apk = Path.Combine (Root, ab.ProjectDirectory,
						app.IntermediateOutputPath, "android", "bin", "UnnamedProject.UnnamedProject.apk");
					using (var zip = ZipHelper.OpenZip (apk)) {
						var existingFiles = zip.Where (a => a.FullName.StartsWith ("assemblies/", StringComparison.InvariantCultureIgnoreCase));
						var missingFiles = expectedFiles.Where (x => !zip.ContainsEntry ("assmelbies/" + Path.GetFileName (x)));
						Assert.IsTrue (missingFiles.Any (),
						string.Format ("The following Expected files are missing. {0}",
							string.Join (Environment.NewLine, missingFiles)));
						var additionalFiles = existingFiles.Where (x => !expectedFiles.Contains (Path.GetFileName (x.FullName)));
						Assert.IsTrue (!additionalFiles.Any (),
							string.Format ("Unexpected Files found! {0}",
							string.Join (Environment.NewLine, additionalFiles.Select (x => x.FullName))));
					}
					if (!HasDevices)
						Assert.Ignore ("Skipping Installation. No devices available.");
					ab.Target = "Install";
					Assert.IsTrue (ab.Build (app), "App should have installed.");
				}
			}
		}
	}
}
