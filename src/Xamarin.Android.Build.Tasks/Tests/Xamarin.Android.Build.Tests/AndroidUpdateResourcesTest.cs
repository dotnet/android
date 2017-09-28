﻿﻿using System;
using Xamarin.ProjectTools;
using NUnit.Framework;
using System.Linq;
using System.IO;
using System.Threading;
using System.Text;
using System.Collections.Generic;
using Microsoft.Build.Framework;
using System.Xml.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	[Parallelizable (ParallelScope.Children)]
	public class AndroidUpdateResourcesTest : BaseTest
	{
		[Test]
		public void RepetitiveBuild ()
		{
			if (Directory.Exists ("temp/RepetitiveBuild"))
				Directory.Delete ("temp/RepetitiveBuild", true);
			var proj = new XamarinAndroidApplicationProject ();
			using (var b = CreateApkBuilder ("temp/RepetitiveBuild")) {
				b.Verbosity = Microsoft.Build.Framework.LoggerVerbosity.Diagnostic;
				b.ThrowOnBuildFailure = false;
				Assert.IsTrue (b.Build (proj), "first build failed");
				Assert.IsTrue (b.Build (proj), "second build failed");
				Assert.IsTrue (b.LastBuildOutput.Contains ("Skipping target \"_Sign\" because"), "failed to skip some build");
				proj.AndroidResources.First ().Timestamp = null; // means "always build"
				Assert.IsTrue (b.Build (proj), "third build failed");
				Assert.IsFalse (b.LastBuildOutput.Contains ("Skipping target \"_Sign\" because"), "incorrectly skipped some build");
			}
		}

		[Test]
		public void DesignTimeBuild ([Values(false, true)] bool isRelease, [Values (false, true)] bool useManagedParser)
		{
			var regEx = new Regex (@"(?<type>([a-zA-Z_0-9])+)\slibrary_name=(?<value>([0-9A-Za-z])+);", RegexOptions.Compiled | RegexOptions.Multiline ); 

			var path = Path.Combine (Root, "temp", $"DesignTimeBuild_{isRelease}_{useManagedParser}");
			var cachePath = Path.Combine (path, "Cache");
			var envVar = new Dictionary<string, string> () {
				{ "XAMARIN_CACHEPATH", cachePath },
			};
			var url = "http://dl-ssl.google.com/android/repository/build-tools_r24-macosx.zip";
			var md5 = MD5.Create ();
			var hash = string.Concat (md5.ComputeHash (Encoding.UTF8.GetBytes (url)).Select (b => b.ToString ("X02")));
			var zipPath = Path.Combine (cachePath, "zips", $"{hash}.zip");
			if (File.Exists (zipPath))
				File.Delete (zipPath);

			var extractedDir = Path.Combine (cachePath, "Lib1");
			if (Directory.Exists (extractedDir))
				Directory.Delete (extractedDir, recursive: true);

			var lib = new XamarinAndroidLibraryProject () {
				ProjectName = "Lib1",
				IsRelease = isRelease,
				AssemblyInfo = @"using System.Reflection;
using System.Runtime.CompilerServices;

[assembly: Android.NativeLibraryReferenceAttribute (""android-N/renderscript/lib/packaged/x86/librsjni.so"",
	SourceUrl="""+ url +@""",
	Version=""1"", PackageName=""Lib1"")]
",
			};
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = isRelease,
				References = {
					new BuildItem.ProjectReference (@"..\Lib1\Lib1.csproj", lib.ProjectName, lib.ProjectGuid),
				},
			};
			proj.SetProperty ("AndroidUseManagedDesignTimeResourceGenerator", useManagedParser.ToString ());
			using (var l = CreateDllBuilder (Path.Combine (path, lib.ProjectName), false, false)) {
				using (var b = CreateApkBuilder (Path.Combine (path, proj.ProjectName), false, false)) {
					l.Verbosity = LoggerVerbosity.Diagnostic;
					Assert.IsTrue(l.Clean(lib), "Lib1 should have cleaned successfully");
					Assert.IsTrue (l.Build (lib), "Lib1 should have built successfully");
					b.Verbosity = LoggerVerbosity.Diagnostic;
					b.ThrowOnBuildFailure = false;
					Assert.IsTrue (b.Clean(proj), "App should have cleaned successfully");
					Assert.IsTrue (b.UpdateAndroidResources (proj, doNotCleanupOnUpdate: true, parameters: new string [] { "DesignTimeBuild=true" }, environmentVariables: envVar),
						"first build failed");
					Assert.AreEqual (!useManagedParser, b.LastBuildOutput.Contains ("Skipping download of "),
						"failed to skip the downloading of files.");
					var items = new List<string> ();
					string first = null;
					if (!useManagedParser) {
						foreach (var file in Directory.EnumerateFiles (Path.Combine (path, proj.ProjectName, proj.IntermediateOutputPath, "android"), "R.java", SearchOption.AllDirectories)) {
							var matches = regEx.Matches (File.ReadAllText (file));
							items.AddRange (matches.Cast<System.Text.RegularExpressions.Match> ().Select (x => x.Groups ["value"].Value));
						}
						first = items.First ();
						Assert.IsTrue (items.All (x => x == first), "All Items should have matching values");
					}
					WaitFor (1000);
					b.Target = "Build";
					Assert.IsTrue (b.Build (proj, doNotCleanupOnUpdate: true, parameters: new string [] { "DesignTimeBuild=false" }, environmentVariables: envVar), "second build failed");
					Assert.IsFalse(b.Output.IsTargetSkipped ("_BuildAdditionalResourcesCache"), "_BuildAdditionalResourcesCache should have run.");
					Assert.IsTrue (b.LastBuildOutput.Contains ($"Downloading {url}") || b.LastBuildOutput.Contains ($"reusing existing archive: {zipPath}"), $"{url} should have been downloaded.");
					Assert.IsTrue (File.Exists (Path.Combine (extractedDir, "1", "content", "android-N", "aapt")), $"Files should have been extracted to {extractedDir}");
					items.Clear ();
					if (!useManagedParser) {
						foreach (var file in Directory.EnumerateFiles (Path.Combine (path, proj.ProjectName, proj.IntermediateOutputPath, "android"), "R.java", SearchOption.AllDirectories)) {
							var matches = regEx.Matches (File.ReadAllText (file));
							items.AddRange (matches.Cast<System.Text.RegularExpressions.Match> ().Select (x => x.Groups ["value"].Value));
						}
						first = items.First ();
						Assert.IsTrue (items.All (x => x == first), "All Items should have matching values");
					}
				}
			}
		}

		[Test]
		public void CheckEmbeddedSupportLibraryResources ()
		{
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
				Packages = {
					KnownPackages.SupportMediaCompat_25_4_0_1,
					KnownPackages.SupportFragment_25_4_0_1,
					KnownPackages.SupportCoreUtils_25_4_0_1,
					KnownPackages.SupportCoreUI_25_4_0_1,
					KnownPackages.SupportCompat_25_4_0_1,
					KnownPackages.AndroidSupportV4_25_4_0_1,
					KnownPackages.SupportV7AppCompat_25_4_0_1,
				},
				TargetFrameworkVersion = "v7.1",
			};
			using (var b = CreateApkBuilder ("temp/CheckEmbeddedSupportLibraryResources")) {
				Assert.IsTrue (b.Build (proj), "First build should have succeeded.");
				var Rdrawable = b.Output.GetIntermediaryPath (Path.Combine ("android", "bin", "classes", "android", "support", "v7", "appcompat", "R$drawable.class"));
				Assert.IsTrue (File.Exists (Rdrawable), $"{Rdrawable} should exist");
			}
		}

		[Test]
		public void MoveResource ()
		{
			var proj = new XamarinAndroidApplicationProject ();
			BuildItem image = null;
			using (var stream = typeof (XamarinAndroidCommonProject).Assembly.GetManifestResourceStream ("Xamarin.ProjectTools.Resources.Base.Icon.png")) {
				var image_data = new byte [stream.Length];
				stream.Read (image_data, 0, (int)stream.Length);
				image = new AndroidItem.AndroidResource ("Resources\\drawable\\Image.png") { BinaryContent = () => image_data };
				proj.AndroidResources.Add (image); 
			}
			using (var b = CreateApkBuilder ("temp/MoveResource")) {
				Assert.IsTrue (b.Build (proj), "First build should have succeeded.");
				var oldpath = image.Include ().Replace ('\\', Path.DirectorySeparatorChar);
				image.Include = () => "Resources/drawable/NewImage.png";
				image.Timestamp = DateTimeOffset.UtcNow.AddMinutes (1);
				Assert.IsTrue (b.Build (proj), "Second build should have succeeded.");
				Assert.IsFalse (File.Exists (Path.Combine (b.ProjectDirectory, oldpath)), "XamarinProject.UpdateProjectFiles() failed to delete file");
				Assert.IsFalse (b.LastBuildOutput.Contains ("Skipping target \"_Sign\" because"), "incorrectly skipped some build");
			}
		}

		[Test]
		public void ReportAaptErrorsInOriginalFileName ()
		{
			var proj = new XamarinAndroidApplicationProject ();
			proj.LayoutMain = @"<root/>\n" + proj.LayoutMain;
			using (var b = CreateApkBuilder ("temp/ErroneousResource")) {
				b.ThrowOnBuildFailure = false;
				Assert.IsFalse (b.Build (proj), "Build should have failed.");
				Assert.IsTrue (b.LastBuildOutput.Split ('\n').Any (s => s.Contains (string.Format ("Resources{0}layout{0}Main.axml", Path.DirectorySeparatorChar)) && s.Contains (": error ")), "error with expected file name is not found");
				Assert.IsTrue (b.Clean (proj), "Clean should have succeeded.");
			}
		}

		[Test]
		public void RepetiviteBuildUpdateSingleResource ()
		{
			var proj = new XamarinAndroidApplicationProject ();
			using (var b = CreateApkBuilder ("temp/RepetiviteBuildUpdateSingleResource", cleanupAfterSuccessfulBuild:false)) {
				b.Verbosity = Microsoft.Build.Framework.LoggerVerbosity.Diagnostic;
				BuildItem image1, image2;
				using (var stream = typeof (XamarinAndroidCommonProject).Assembly.GetManifestResourceStream ("Xamarin.ProjectTools.Resources.Base.Icon.png")) {
					var image_data = new byte [stream.Length];
					stream.Read (image_data, 0, (int)stream.Length);
					image1 = new AndroidItem.AndroidResource ("Resources\\drawable\\Image1.png") { BinaryContent = () => image_data };
					proj.AndroidResources.Add (image1); 
					image2 = new AndroidItem.AndroidResource ("Resources\\drawable\\Image2.png") { BinaryContent = () => image_data };
					proj.AndroidResources.Add (image2); 
				}
				b.ThrowOnBuildFailure = false;
				Assert.IsTrue (b.Build (proj), "First build was supposed to build without errors");
				var firstBuildTime = b.LastBuildTime;
				Assert.IsTrue (b.Build (proj), "Second build was supposed to build without errors");
				Assert.IsTrue (firstBuildTime > b.LastBuildTime, "Second build was supposed to be quicker than the first");
				Assert.IsTrue (
					b.LastBuildOutput.Contains ("Skipping target \"_GenerateAndroidResourceDir\" because"),
					"The Target _GenerateAndroidResourceDir should have been skipped");
				Assert.IsTrue (
					b.LastBuildOutput.Contains ("Skipping target \"_CompileJava\" because"),
					"The Target _CompileJava should have been skipped");
				image1.Timestamp = DateTime.UtcNow;
				var layout = proj.AndroidResources.First (x => x.Include() == "Resources\\layout\\Main.axml");
				layout.Timestamp = DateTime.UtcNow;
				Assert.IsTrue (b.Build (proj, doNotCleanupOnUpdate:true, saveProject: false), "Third build was supposed to build without errors");
				Assert.IsTrue (
					b.LastBuildOutput.Contains ("Target _GenerateAndroidResourceDir needs to be built as input file") ||
                    b.LastBuildOutput.Contains ("Building target \"_GenerateAndroidResourceDir\" completely."),
					"The Target _GenerateAndroidResourceDir should not have been skipped");
				Assert.IsTrue (
					b.LastBuildOutput.Contains ("Skipping target \"_CompileJava\" because"),
					"The Target _CompileJava (2) should have been skipped");
				Assert.IsTrue (
					b.LastBuildOutput.Contains ("Target _CreateBaseApk needs to be built as input file") ||
					b.LastBuildOutput.Contains ("Building target \"_CreateBaseApk\" completely."),
					"The Target _CreateBaseApk should not have been skipped");
			}
		}

		[Test]
		public void Check9PatchFilesAreProcessed ([Values(false, true)] bool explicitCrunch)
		{
			var projectPath = string.Format ("temp/Check9PatchFilesAreProcessed_{0}", explicitCrunch.ToString ());
			var libproj = new XamarinAndroidLibraryProject () { ProjectName = "Library1"};
			using (var stream = typeof (XamarinAndroidCommonProject).Assembly.GetManifestResourceStream ("Xamarin.ProjectTools.Resources.Base.Image.9.png")) {
				var image_data = new byte [stream.Length];
				stream.Read (image_data, 0, (int)stream.Length);
				var image2 = new AndroidItem.AndroidResource ("Resources\\drawable\\Image2.9.png") { BinaryContent = () => image_data };
				libproj.AndroidResources.Add (image2); 
			}
			using (var libb = CreateDllBuilder (Path.Combine (projectPath, "Library1"))) {
				libb.Build (libproj);
				var proj = new XamarinAndroidApplicationProject ();
				using (var stream = typeof (XamarinAndroidCommonProject).Assembly.GetManifestResourceStream ("Xamarin.ProjectTools.Resources.Base.Image.9.png")) {
					var image_data = new byte [stream.Length];
					stream.Read (image_data, 0, (int)stream.Length);
					var image1 = new AndroidItem.AndroidResource ("Resources\\drawable\\Image1.9.png") { BinaryContent = () => image_data };
					proj.AndroidResources.Add (image1); 
				}
				proj.References.Add (new BuildItem ("ProjectReference", "..\\Library1\\Library1.csproj"));
				proj.Packages.Add (KnownPackages.AndroidSupportV4_21_0_3_0);
				proj.Packages.Add (KnownPackages.SupportV7AppCompat_21_0_3_0);
				proj.Packages.Add (KnownPackages.SupportV7MediaRouter_21_0_3_0);
				proj.Packages.Add (KnownPackages.GooglePlayServices_22_0_0_2);
				proj.AndroidExplicitCrunch = explicitCrunch;
				proj.SetProperty ("TargetFrameworkVersion", "v5.0");
				proj.SetProperty (proj.DebugProperties, "JavaMaximumHeapSize", "1G");
				proj.SetProperty (proj.ReleaseProperties, "JavaMaximumHeapSize", "1G");
				using (var b = CreateApkBuilder (Path.Combine (projectPath, "Application1"), false, false)) {
					b.Verbosity = LoggerVerbosity.Diagnostic;
					Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
					var path = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath, "android/bin/packaged_resources");
					var data = ZipHelper.ReadFileFromZip (path, "res/drawable/image1.9.png");
					Assert.IsNotNull (data, "image1.9.png should be in {0}android/bin/packaged_resources",
						proj.IntermediateOutputPath);
					var png = PNGChecker.LoadFromBytes (data);
					Assert.IsTrue (png.Is9Patch, "image1.9.png should have been processed into a 9 patch image.");
					data = ZipHelper.ReadFileFromZip (path, "res/drawable/image2.9.png");
					Assert.IsNotNull (data, "image2.9.png should be in {0}android/bin/packaged_resources",
						proj.IntermediateOutputPath);
					png = PNGChecker.LoadFromBytes (data);
					Assert.IsTrue (png.Is9Patch, "image2.9.png should have been processed into a 9 patch image.");
					data = ZipHelper.ReadFileFromZip (path, "res/drawable-hdpi-v4/common_signin_btn_icon_normal_dark.9.png");
					Assert.IsNotNull (data, "common_signin_btn_icon_normal_dark.9.png should be in {0}android/bin/packaged_resources",
						proj.IntermediateOutputPath);
					png = PNGChecker.LoadFromBytes (data);
					Assert.IsTrue (png.Is9Patch, "common_signin_btn_icon_normal_dark.9.png should have been processed into a 9 patch image.");
					Directory.Delete (Path.Combine (Root,projectPath), recursive: true);
				}
			}
		}

		[Test]
		/// <summary>
		/// Based on https://bugzilla.xamarin.com/show_bug.cgi?id=29263
		/// </summary>
		public void CheckXmlResourcesFilesAreProcessed ([Values(false, true)] bool isRelease)
		{
			var projectPath = String.Format ("temp/CheckXmlResourcesFilesAreProcessed_{0}", isRelease);
			var proj = new XamarinAndroidApplicationProject () { IsRelease = isRelease };

			proj.AndroidResources.Add (new AndroidItem.AndroidResource ("Resources\\drawable\\UPPER_image.png") {
				BinaryContent = () => XamarinAndroidCommonProject.icon_binary_mdpi
			});

			proj.AndroidResources.Add (new AndroidItem.AndroidResource ("Resources\\xml\\Preferences.xml") {
				TextContent = () => @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<PreferenceScreen xmlns:android=""http://schemas.android.com/apk/res/android"">
	<EditTextPreference
		android:key=""pref_a""
		android:title=""EditText Preference""
		android:singleLine=""true""
		android:inputType=""textUri|textNoSuggestions""/>
	<UnnamedProject.CustomPreference
		android:key=""pref_b""
	/> 
</PreferenceScreen>"
			});

			proj.AndroidResources.Add (new AndroidItem.AndroidResource ("Resources\\values\\Strings1.xml") {
				TextContent = () => @"<?xml version=""1.0"" encoding=""utf-8""?>
<resources>
	<string name=""title_custompreference"">Custom Preference</string>
</resources>"
			});

			proj.AndroidResources.Add (new AndroidItem.AndroidResource ("Resources\\values\\Styles.xml") {
				TextContent = () => @"<?xml version=""1.0"" encoding=""utf-8""?>
<resources>
	<color name=""deep_purple_A200"">#e040fb</color>
	<style name=""stylename"">
		<item name=""android:background"">@drawable/UPPER_image</item>
		<item name=""android:textColorPrimary"">@android:color/white</item>
	</style>
	<style name=""MyTheme.Base"" parent=""Theme.AppCompat.Light.DarkActionBar"">
		<item name=""colorAccent"">@color/deep_purple_A200</item>
	</style>
</resources>"
			});

			proj.Sources.Add (new BuildItem.Source ("CustomPreference.cs") {
				TextContent = () => @"using System;
using Android.Preferences;
using Android.Content;
using Android.Util;
namespace UnnamedProject
{
	public class CustomPreference : Preference
	{		
		public CustomPreference(Context context, IAttributeSet attrs) : base(context, attrs)
		{
			SetTitle(Resource.String.title_custompreference);
		}
		protected override void OnClick()
		{ 			
		}
	}
}"
			});
			proj.Packages.Add (KnownPackages.AndroidSupportV4_22_1_1_1);
			proj.Packages.Add (KnownPackages.SupportV7AppCompat_22_1_1_1);
			proj.Packages.Add (KnownPackages.SupportV7Palette_22_1_1_1);
			proj.SetProperty ("TargetFrameworkVersion", "v5.0");
			proj.SetProperty (proj.DebugProperties, "JavaMaximumHeapSize", "1G");
			proj.SetProperty (proj.ReleaseProperties, "JavaMaximumHeapSize", "1G");
			using (var b = CreateApkBuilder (Path.Combine (projectPath))) {
				b.Verbosity = LoggerVerbosity.Diagnostic;
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				var preferencesPath = Path.Combine (Root, projectPath, proj.IntermediateOutputPath, "res","xml","preferences.xml");
				Assert.IsTrue (File.Exists (preferencesPath), "Preferences.xml should have been renamed to preferences.xml");
				var doc = XDocument.Load (preferencesPath);
				Assert.IsNotNull (doc.Element ("PreferenceScreen"), "PreferenceScreen should be present in preferences.xml");
				Assert.IsNull (doc.Element ("PreferenceScreen").Element ("UnnamedProject.CustomPreference"),
					"UnamedProject.CustomPreference should have been replaced with an $(MD5Hash).CustomPreference");
				var style = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath, "res", "values", "styles.xml"); 
				Assert.IsTrue (File.Exists (style));
				doc = XDocument.Load (style);
				var item = doc.Element ("resources").Elements ("style")
					.Where(x => x.Attribute ("name").Value == "stylename")
					.Elements ("item")
					.FirstOrDefault (x => x.Attribute("name").Value == "android:background");
				Assert.IsNotNull (item, "The Style should contain an Item");
				Assert.AreEqual ("@drawable/upper_image", item.Value, "item value should be @drawable/upper_image");
				item = doc.Element ("resources").Elements ("style")
					.Where(x => x.Attribute ("name").Value == "MyTheme.Base")
					.Elements ("item")
					.FirstOrDefault (x => x.Attribute("name").Value == "colorAccent");
				Assert.IsNotNull (item, "The Style should contain an Item");
				Assert.AreEqual ("@color/deep_purple_A200", item.Value, "item value should be @color/deep_purple_A200");
				StringAssert.DoesNotContain ("AndroidResgen: Warning while updating Resource XML", b.LastBuildOutput,
					"Warning while processing resources should not have been raised.");
				Assert.IsTrue (b.Clean (proj), "Clean should have succeeded.");
			}
		}

		static object[] ReleaseLanguage = new object[] {
			new object[] { false, XamarinAndroidProjectLanguage.CSharp },
			new object[] { true, XamarinAndroidProjectLanguage.CSharp },
			new object[] { false, XamarinAndroidProjectLanguage.FSharp },
			new object[] { true, XamarinAndroidProjectLanguage.FSharp },
		};

		[Test]
		[TestCaseSource("ReleaseLanguage")]
		public void CheckResourceDesignerIsCreated (bool isRelease, ProjectLanguage language)
		{
			var proj = new XamarinAndroidApplicationProject () {
				Language = language,
				IsRelease = isRelease,
			};
			proj.SetProperty ("AndroidUseIntermediateDesignerFile", "True");
			using (var b = CreateApkBuilder (Path.Combine ("temp", TestName))) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				var outputFile = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath,
					"Resource.Designer"  + proj.Language.DefaultExtension);
				Assert.IsTrue (File.Exists (outputFile), "Resource.Designer{1} should have been created in {0}",
					proj.IntermediateOutputPath, proj.Language.DefaultExtension);
				Assert.IsTrue (b.Clean (proj), "Clean should have succeeded.");
				Assert.IsFalse (File.Exists (outputFile), "Resource.Designer{1} should have been cleaned in {0}",
					proj.IntermediateOutputPath, proj.Language.DefaultExtension);
			}
		}

		[Test]
		[TestCaseSource("ReleaseLanguage")]
		public void CheckResourceDesignerIsUpdatedWhenReadOnly (bool isRelease, ProjectLanguage language)
		{
			var proj = new XamarinAndroidApplicationProject () {
				Language = language,
				IsRelease = isRelease,
			};
			using (var b = CreateApkBuilder (Path.Combine ("temp", TestName))) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				var designerPath = Path.Combine (Root, b.ProjectDirectory, "Resources", "Resource.designer" + language.DefaultDesignerExtension);
				var attr = File.GetAttributes (designerPath);
				File.SetAttributes (designerPath, FileAttributes.ReadOnly);
				Assert.IsTrue ((File.GetAttributes (designerPath) & FileAttributes.ReadOnly) == FileAttributes.ReadOnly,
					"{0} should be read only", designerPath);
				var mainAxml = Path.Combine (Root, b.ProjectDirectory, "Resources", "layout", "Main.axml");
				File.SetLastWriteTimeUtc (mainAxml, DateTime.UtcNow);
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				Assert.IsTrue ((File.GetAttributes (designerPath) & FileAttributes.ReadOnly) != FileAttributes.ReadOnly,
					"{0} should be writable", designerPath);
			}
		}

		[Test]
		[TestCaseSource("ReleaseLanguage")]
		public void CheckOldResourceDesignerIsNotUsed (bool isRelease, ProjectLanguage language)
		{
			if (language == XamarinAndroidProjectLanguage.FSharp)
				Assert.Ignore ("Skipping CheckOldResourceDesignerIsNotUsed for FSharp until Xamarin.Android.FSharp.ResourceProvider supports it.");
			var proj = new XamarinAndroidApplicationProject () {
				Language = language,
				IsRelease = isRelease,
			};
			proj.SetProperty ("AndroidUseIntermediateDesignerFile", "True");
			proj.SetProperty ("AndroidUseManagedDesignTimeResourceGenerator", "False");
			using (var b = CreateApkBuilder (Path.Combine ("temp", TestContext.CurrentContext.Test.Name))) {
				var designer = Path.Combine ("Resources", "Resource.designer" + proj.Language.DefaultDesignerExtension);
				if (File.Exists (designer))
					File.Delete (Path.Combine (Root, b.ProjectDirectory, designer));
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				var fi = new FileInfo (Path.Combine (Root, b.ProjectDirectory, designer));
				Assert.IsFalse (fi.Length > new [] { 0xef, 0xbb, 0xbf, 0x0d, 0x0a }.Length,
					"{0} should not contain anything.", designer);
				var outputFile = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath,
					"Resource.Designer"  + proj.Language.DefaultDesignerExtension);
				Assert.IsTrue (File.Exists (outputFile), "Resource.Designer{1} should have been created in {0}",
					proj.IntermediateOutputPath, proj.Language.DefaultDesignerExtension);
				Assert.IsTrue (b.Clean (proj), "Clean should have succeeded.");
				Assert.IsFalse (File.Exists (outputFile), "Resource.Designer{1} should have been cleaned in {0}",
					proj.IntermediateOutputPath, proj.Language.DefaultDesignerExtension);
			}
		}

		// ref https://bugzilla.xamarin.com/show_bug.cgi?id=30089
		[Test]
		[TestCaseSource("ReleaseLanguage")]
		public void CheckOldResourceDesignerWithWrongCasingIsRemoved (bool isRelease, ProjectLanguage language)
		{
			if (language == XamarinAndroidProjectLanguage.FSharp)
				Assert.Ignore ("Skipping CheckOldResourceDesignerIsNotUsed for FSharp until Xamarin.Android.FSharp.ResourceProvider supports it.");
			var proj = new XamarinAndroidApplicationProject () {
				Language = language,
				IsRelease = isRelease,
			};
			proj.SetProperty ("AndroidUseIntermediateDesignerFile", "True");
			proj.SetProperty ("AndroidResgenFile", "Resources\\Resource.Designer" + proj.Language.DefaultExtension);
			using (var b = CreateApkBuilder (Path.Combine ("temp", TestContext.CurrentContext.Test.Name))) {
				var designer = proj.Sources.FirstOrDefault (x => x.Include() == "Resources\\Resource.designer" + proj.Language.DefaultDesignerExtension);
				designer = designer ?? proj.OtherBuildItems.FirstOrDefault (x => x.Include () == "Resources\\Resource.designer" + proj.Language.DefaultDesignerExtension);
				Assert.IsNotNull (designer, $"Failed to retrieve the Resource.designer.{proj.Language.DefaultDesignerExtension}");
				designer.Deleted = true;
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				Assert.IsFalse (File.Exists (Path.Combine (Root, b.ProjectDirectory, "Resources",
					"Resource.designer"  + proj.Language.DefaultDesignerExtension)),
					"{0} should not exists", designer.Include ());
				var outputFile = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath,
					"Resource.Designer"  + proj.Language.DefaultDesignerExtension);
				Assert.IsTrue (File.Exists (outputFile), "Resource.Designer{1} should have been created in {0}",
					proj.IntermediateOutputPath, proj.Language.DefaultDesignerExtension);
				Assert.IsTrue (b.Clean (proj), "Clean should have succeeded.");
				Assert.IsFalse (File.Exists (outputFile), "Resource.Designer{1} should have been cleaned in {0}",
					proj.IntermediateOutputPath, proj.Language.DefaultDesignerExtension);
			}
		}

		[Test]
		public void TargetGenerateJavaDesignerForComponentIsSkipped ([Values(false, true)] bool isRelease)
		{ 
			// build with packages... then tweak a package..
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = isRelease,
			};
			proj.Packages.Add (KnownPackages.AndroidSupportV4_21_0_3_0);
			proj.Packages.Add (KnownPackages.SupportV7AppCompat_21_0_3_0);
			proj.SetProperty ("TargetFrameworkVersion", "v5.0");
			using (var b = CreateApkBuilder (Path.Combine ("temp", TestContext.CurrentContext.Test.Name))) {
				b.Verbosity = LoggerVerbosity.Diagnostic;
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				StringAssert.DoesNotContain ("Skipping target \"_GenerateJavaDesignerForComponent\" because",
					b.LastBuildOutput, "Target _GenerateJavaDesignerForComponent should not have been skipped");
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				StringAssert.Contains ("Skipping target \"_GenerateJavaDesignerForComponent\" because",
					b.LastBuildOutput, "Target _GenerateJavaDesignerForComponent should have been skipped");
				var files = Directory.EnumerateFiles (Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath, "resourcecache")
					, "abc_fade_in.xml", SearchOption.AllDirectories);
				Assert.AreEqual (1, files.Count (), "There should only be one abc_fade_in.xml in the resourcecache");
				var resFile = files.First ();
				Assert.IsTrue (File.Exists (resFile), "{0} should exist", resFile);
				File.SetLastWriteTime (resFile, DateTime.UtcNow);
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				StringAssert.DoesNotContain ("Skipping target \"_GenerateJavaDesignerForComponent\" because",
					b.LastBuildOutput, "Target _GenerateJavaDesignerForComponent should not have been skipped");
			}
		}

		[Test]
		public void CheckAaptErrorRaisedForMissingResource ()
		{
			var proj = new XamarinAndroidApplicationProject ();
			var main = proj.AndroidResources.First (x => x.Include () == "Resources\\layout\\Main.axml");
			main.TextContent = () => @"<?xml version=""1.0"" encoding=""utf-8""?>
<LinearLayout xmlns:android=""http://schemas.android.com/apk/res/android""
	android:orientation=""vertical""
	android:layout_width=""fill_parent""
	android:layout_height=""fill_parent""
	>
<Button  
	android:id=""@id/myButton""
	android:layout_width=""fill_parent"" 
	android:layout_height=""wrap_content"" 
	android:text=""@string/foo""
	/>
</LinearLayout>";
			var projectPath = string.Format ("temp/CheckAaptErrorRaisedForMissingResource");
			using (var b = CreateApkBuilder (Path.Combine (projectPath, "UnamedApp"), false, false)) {
				b.Verbosity = LoggerVerbosity.Diagnostic;
				b.ThrowOnBuildFailure = false;
				Assert.IsFalse (b.Build (proj), "Build should have failed");
				StringAssert.Contains ("APT0000: ", b.LastBuildOutput);
				StringAssert.Contains ("2 Error(s)", b.LastBuildOutput);
			}
		}

		[Test]
		public void CheckAaptErrorRaisedForInvalidDirectoryName ()
		{
			var proj = new XamarinAndroidApplicationProject ();
			proj.AndroidResources.Add (new AndroidItem.AndroidResource("Resources\\booboo\\stuff.xml") {
				TextContent = () => @"<?xml version=""1.0"" encoding=""utf-8""?>
<resources>
</resources>"
			});
			var projectPath = string.Format ("temp/CheckAaptErrorRaisedForInvalidDirectoryName");
			using (var b = CreateApkBuilder (Path.Combine (projectPath, "UnamedApp"), false, false)) {
				b.Verbosity = LoggerVerbosity.Diagnostic;
				b.ThrowOnBuildFailure = false;
				Assert.IsFalse (b.Build (proj), "Build should have failed");
				StringAssert.Contains ("APT0000: ", b.LastBuildOutput);
				StringAssert.Contains ("1 Error(s)", b.LastBuildOutput);
			}
		}

		[Test]
		public void CheckAaptErrorRaisedForInvalidFileName ()
		{
			var proj = new XamarinAndroidApplicationProject ();
			proj.AndroidResources.Add (new AndroidItem.AndroidResource ("Resources\\drawable\\icon-2.png") {
				BinaryContent = () => XamarinAndroidCommonProject.icon_binary_hdpi,
			});
			var projectPath = string.Format ("temp/CheckAaptErrorRaisedForInvalidFileName");
			using (var b = CreateApkBuilder (Path.Combine (projectPath, "UnamedApp"), false, false)) {
				b.Verbosity = LoggerVerbosity.Diagnostic;
				b.ThrowOnBuildFailure = false;
				Assert.IsFalse (b.Build (proj), "Build should have failed");
				StringAssert.Contains ("Invalid file name:", b.LastBuildOutput);
				StringAssert.Contains ("1 Error(s)", b.LastBuildOutput);
			}
		}

		[Test]
		public void CheckAaptErrorRaisedForDuplicateResourceinApp ()
		{
			var proj = new XamarinAndroidApplicationProject ();
			var stringsXml = proj.AndroidResources.First (x => x.Include () == "Resources\\values\\Strings.xml");
			stringsXml.TextContent = () => @"<?xml version=""1.0"" encoding=""utf-8""?>
<resources>
	<string name=""hello"">Hello World, Click Me!</string>
	<string name=""app_name"">Application one</string>
	<string name=""some_string_value"">Hello Me From the App</string>
	<string name=""some_string_value"">Hello Me From the App 2</string>
</resources>";
			var projectPath = string.Format ("temp/CheckAaptErrorRaisedForDuplicateResourceinApp");
			using (var b = CreateApkBuilder (Path.Combine (projectPath, "UnamedApp"), false, false)) {
				b.Verbosity = LoggerVerbosity.Diagnostic;
				b.ThrowOnBuildFailure = false;
				Assert.IsFalse (b.Build (proj), "Build should have failed");
				StringAssert.Contains ("APT0000: ", b.LastBuildOutput);
				StringAssert.Contains ("2 Error(s)", b.LastBuildOutput);
			}
		}

		[Test]
		[Ignore ("Ignore until our own MergeResources is implemented")]
		public void CheckWarningsRaisedForDuplicateResourcesAcrossEntireProject () 
		{
			var lib1 = new XamarinAndroidLibraryProject () {
				ProjectName = "Library1",
				AndroidResources = { 
					new AndroidItem.AndroidResource ("Resources\\values\\Styles.xml") {
					TextContent = () => @"<?xml version=""1.0"" encoding=""utf-8""?>
<resources>
	<style name=""Foo"" />
</resources>"
					},
				}
			};
			var stringRes = lib1.AndroidResources.First (x => x.Include () == "Resources\\values\\Strings.xml");
			stringRes.TextContent = () => @"<?xml version=""1.0"" encoding=""utf-8""?>
<resources>
	<string name=""library_name"">Library1</string>
	<string name=""some_string_value"">Hello Me</string>
</resources>";

			var proj = new XamarinAndroidApplicationProject () {
				References =  { new BuildItem ("ProjectReference", "..\\Library1\\Library1.csproj")},
				AndroidResources = { 
					new AndroidItem.AndroidResource ("Resources\\values\\Styles.xml") {
					TextContent = () => @"<?xml version=""1.0"" encoding=""utf-8""?>
<resources>
	<style name=""CustomText"">
		<item name=""android:textSize"">20sp</item>
		<item name=""android:textColor"">#008</item>
	</style>
</resources>"
					},
				},
			};

			stringRes = proj.AndroidResources.First (x => x.Include () == "Resources\\values\\Strings.xml");
			stringRes.TextContent = () => @"<?xml version=""1.0"" encoding=""utf-8""?>
<resources>
	<string name=""hello"">Hello World, Click Me!</string>
	<string name=""app_name"">Application one</string>
	<string name=""some_string_value"">Hello Me From the App</string>
</resources>";

			var projectPath = string.Format ("temp/CheckWarningsRaisedForDuplicateResourcesAcrossEntireProject");
			using (var dllBuilder = CreateDllBuilder (Path.Combine (projectPath, "Library1"))) {
				dllBuilder.Verbosity = LoggerVerbosity.Diagnostic;
				dllBuilder.Build (lib1);
				using (var b = CreateApkBuilder (Path.Combine (projectPath, "UnamedApp"))) {
					b.Verbosity = LoggerVerbosity.Diagnostic;
					b.ThrowOnBuildFailure = false;
					Assert.IsTrue (b.Build (proj), "Build should have succeeded");
					StringAssert.Contains ("XA5215: Duplicate Resource found for", b.LastBuildOutput);
					var stylesXml = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath, "res", "values", "styles.xml");
					Assert.IsTrue (File.Exists (stylesXml), "{0} should exist", stylesXml);
					var doc = XDocument.Load (stylesXml);
					Assert.IsTrue (doc.Element ("resources").Elements ().Any (x => x.Attribute ("name").Value == "CustomText"), "CustomText should exist in styles.xml");
					Assert.IsTrue (doc.Element ("resources").Elements ().Any (x => x.Attribute ("name").Value == "Foo"), "Foo should exist in styles.xml");
					var stringsXml = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath, "res", "values", "strings.xml");
					Assert.IsTrue (File.Exists (stringsXml), "{0} should exist", stylesXml);
					doc = XDocument.Load (stringsXml);
					Assert.IsTrue (doc.Element ("resources").Elements ().Any (x => x.Attribute ("name").Value == "some_string_value"), "some_string_value should exist in strings.xml");
					Assert.IsTrue (doc.Element ("resources").Elements ().Any (x => x.Attribute ("name").Value == "some_string_value" && x.Value == "Hello Me From the App"),
						"some_string_value should have the value of \"Hello Me From the App\"");
				}
			}
		}

		[Test]
		[Ignore ("Enable once Merge Resources is finished")]
		public void InternationalResourceTest ([Values (false, true)] bool explicitCrunch)
		{
			var library = new XamarinAndroidLibraryProject () {
				ProjectName = "Library1",
				AndroidResources = {
					new AndroidItem.AndroidResource ("Resources\\values-de\\Strings.xml") {
						TextContent = () => @"<?xml version=""1.0"" encoding=""utf-8""?>
<resources>
	<string name=""greeting"">Ja</string>
</resources>",
					},
				},
			};

			var strings = library.AndroidResources.First (x => x.Include () == "Resources\\values\\Strings.xml");
			strings.TextContent = () => @"<?xml version=""1.0"" encoding=""utf-8""?>
<resources>
	<string name=""greeting"">Yes</string>
	<string name=""library_name"">Library1</string>
</resources>";
			var project = new XamarinAndroidApplicationProject () {
				ProjectName = "Application1",
				AndroidExplicitCrunch = explicitCrunch,
				References =  { new BuildItem ("ProjectReference", "..\\Library1\\Library1.csproj")},
					AndroidResources ={
						new AndroidItem.AndroidResource ("Resources\\values-de\\Strings.xml") {
							TextContent = () => @"<?xml version=""1.0"" encoding=""utf-8""?>
<resources>
	<string name=""hello"">Hallo Welt, Klick mich!</string>
</resources>",
					}
				},
			};
			var projectPath = string.Format ("temp/InternationalResourceTest{0}", explicitCrunch);
			using (var dllBuilder = CreateDllBuilder (Path.Combine (projectPath, "Library1"))) {
				dllBuilder.Verbosity = LoggerVerbosity.Diagnostic;
				Assert.IsTrue (dllBuilder.Build (library), "Library1 build should have succeeded");
				using (var b = CreateApkBuilder (Path.Combine (projectPath, "Application1"))) {
					b.Verbosity = LoggerVerbosity.Diagnostic;
					b.ThrowOnBuildFailure = false;
					Assert.IsTrue (b.Build (project),"Applications1 build should have succeeded");

					var stringsXml = Path.Combine (Root, b.ProjectDirectory, project.IntermediateOutputPath, "res", "values", "strings.xml");
					Assert.IsTrue (File.Exists (stringsXml), "{0} should exist", stringsXml);
					var doc = XDocument.Load (stringsXml);
					Assert.IsTrue (doc.Element ("resources").Elements ().Any (x => x.Attribute ("name").Value == "greeting" && x.Value == "Yes"), "greeting should have a value of Yes");
					Assert.IsTrue (doc.Element ("resources").Elements ().Any (x => x.Attribute ("name").Value == "hello" && x.Value == "Hello World, Click Me!"), "hello should have a value of Hello World, Click Me!");

					var stringsXml_de = Path.Combine (Root, b.ProjectDirectory, project.IntermediateOutputPath, "res", "values-de", "strings.xml");
					Assert.IsTrue (File.Exists (stringsXml), "{0} should exist", stringsXml_de);

					doc = XDocument.Load (stringsXml_de);
					Assert.IsTrue (doc.Element ("resources").Elements ().Any (x => x.Attribute ("name").Value == "greeting" && x.Value == "Ja"), "greeting should have a value of Ja");
					Assert.IsTrue (doc.Element ("resources").Elements ().Any (x => x.Attribute ("name").Value == "hello" && x.Value == "Hallo Welt, Klick mich!"), "hello should have a value of Hallo Welt, Klick mich!");

					Assert.IsTrue (dllBuilder.Build (library), "Second Library1 build should have succeeded");
					Assert.IsTrue (b.Build (project), "Second Applications1 build should have succeeded");

					var strings_de = (AndroidItem.AndroidResource)library.AndroidResources.First (x => x.Include() == "Resources\\values-de\\Strings.xml");
					strings_de.TextContent = () => @"<?xml version=""1.0"" encoding=""utf-8""?>
<resources>
	<string name=""greeting"">Foo</string>
</resources>";
					strings_de.Timestamp = DateTime.Now;

					Assert.IsTrue (dllBuilder.Build (library), "Third Library1 build should have succeeded");
					Assert.IsTrue (b.Build (project), "Third Applications1 build should have succeeded");

					doc = XDocument.Load (stringsXml_de);
					Assert.IsTrue (doc.Element ("resources").Elements ().Any (x => x.Attribute ("name").Value == "greeting" && x.Value == "Foo"), "greeting should have a value of Foo");
					Assert.IsTrue (doc.Element ("resources").Elements ().Any (x => x.Attribute ("name").Value == "hello" && x.Value == "Hallo Welt, Klick mich!"), "hello should have a value of Hallo Welt, Klick mich!");

					Directory.Delete (projectPath, recursive: true);
				}
			}
		}

		[Test]
		[Ignore ("Enable once Merge Resources is finished")]
		public void MergeResources ([Values(false, true)] bool explicitCrunch)
		{
			var lib1 = new XamarinAndroidLibraryProject () {
				ProjectName = "Library1",
				AndroidResources = { new AndroidItem.AndroidResource ("Resources\\values\\Colors.xml") {
						TextContent = () => @"<?xml version=""1.0"" encoding=""utf-8""?>
<resources>
	<color name=""dark_red"">#440000</color>
	<color name=""dark_blue"">#000044</color>
</resources>"
					},
				}
			};

			var proj = new XamarinAndroidApplicationProject () {
				AndroidExplicitCrunch = explicitCrunch,
				References =  { new BuildItem ("ProjectReference", "..\\Library1\\Library1.csproj")},
				AndroidResources = { new AndroidItem.AndroidResource ("Resources\\values\\Values.xml") {
						TextContent = () => @"<?xml version=""1.0"" encoding=""utf-8""?>
<resources>
	<color name=""dark_red"">#FF0000</color>
	<color name=""xamarin_green"">#00FF00</color>
	<color name=""xamarin_green1"">#00FF00</color>
</resources>"
					},
					new AndroidItem.AndroidResource ("Resources\\values\\Colors.xml") {
						TextContent = () => @"<?xml version=""1.0"" encoding=""utf-8""?>
<resources>
	<color name=""dark_blue"">#0000FF</color>
</resources>"
					},
				},
			};
			var projectPath = string.Format ("temp/MergeResources_{0}", explicitCrunch);
			using (var dllBuilder = CreateDllBuilder (Path.Combine (projectPath, "Library1"), false, false)) {
				dllBuilder.Verbosity = LoggerVerbosity.Diagnostic;
				Assert.IsTrue (dllBuilder.Build (lib1), "Library project should have built");
				using (var b = CreateApkBuilder (Path.Combine (projectPath, "UnamedApp"), false, false)) {
					b.Verbosity = LoggerVerbosity.Diagnostic;
					b.ThrowOnBuildFailure = false;
					Assert.IsTrue (b.Build (proj), "Build should have succeeded");
					StringAssert.Contains ("XA5215: Duplicate Resource found for", b.LastBuildOutput);
					var colorsXml = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath, "res", "values", "colors.xml");
					Assert.IsTrue (File.Exists (colorsXml), "{0} should exist", colorsXml);
					var doc = XDocument.Load (colorsXml);
					Assert.IsTrue (doc.Element ("resources").Elements ().Any (x => x.Attribute ("name").Value == "dark_red" && x.Value == "#FF0000"), "dark_red should have a value of #FF0000");
					Assert.IsTrue (doc.Element ("resources").Elements ().Any (x => x.Attribute ("name").Value == "dark_blue" && x.Value == "#0000FF"), "dark_blue should have a value of #0000FF");

					var valuesXml = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath, "res", "values", "values.xml");
					Assert.IsTrue (File.Exists (valuesXml), "{0} should exist", valuesXml);
					doc = XDocument.Load (valuesXml);
					Assert.IsTrue (doc.Element ("resources").Elements ().Any (x => x.Attribute ("name").Value == "xamarin_green"), "xamarin_green should exist in values.xml");
					Assert.IsFalse (doc.Element ("resources").Elements ().Any (x => x.Attribute ("name").Value == "dark_red"), "dark_red should exist in values.xml");
					Assert.IsFalse (doc.Element ("resources").Elements ().Any (x => x.Attribute ("name").Value == "dark_blue"), "dark_blue should not exist in values.xml");

					Assert.IsTrue (b.Build (proj), "Second Build should have succeeded");

					var values = (AndroidItem.AndroidResource)proj.AndroidResources.First (x => x.Include() == "Resources\\values\\Values.xml");
					values.TextContent = () => @"<?xml version=""1.0"" encoding=""utf-8""?>
<resources>
	<color name=""dark_red"">#FFFFFF</color>
	<color name=""xamarin_green"">#00AA00</color>
</resources>";
					values.Timestamp = DateTime.Now;
					Assert.IsTrue (b.Build (proj), "Third Build should have succeeded");

					Assert.IsTrue (File.Exists (colorsXml), "{0} should exist", colorsXml);
					doc = XDocument.Load (colorsXml);
					Assert.IsTrue (doc.Element ("resources").Elements ().Any (x => x.Attribute ("name").Value == "dark_red" && x.Value == "#FFFFFF"), "dark_red should have a value of #FFFFFF");
					Assert.IsTrue (doc.Element ("resources").Elements ().Any (x => x.Attribute ("name").Value == "dark_blue" && x.Value == "#0000FF"), "dark_blue should have a value of #0000FF");

					Assert.IsTrue (File.Exists (valuesXml), "{0} should exist", valuesXml);
					doc = XDocument.Load (valuesXml);
					Assert.IsTrue (doc.Element ("resources").Elements ().Any (x => x.Attribute ("name").Value == "xamarin_green"), "xamarin_green should exist in values.xml");
					Assert.IsFalse (doc.Element ("resources").Elements ().Any (x => x.Attribute ("name").Value == "dark_red"), "dark_red should not exist in values.xml");
					Assert.IsFalse (doc.Element ("resources").Elements ().Any (x => x.Attribute ("name").Value == "dark_blue"), "dark_blue should not exist in values.xml");

					Assert.IsTrue (doc.Element ("resources").Elements ().Any (x => x.Attribute ("name").Value == "xamarin_green" && x.Value == "#00AA00"), "xamarin_green should have a value of #00AA00");
					Assert.IsFalse (doc.Element ("resources").Elements ().Any (x => x.Attribute ("name").Value == "xamarin_green1"), "xamarin_green1 should not exist in values.xml");

					Directory.Delete (projectPath, recursive: true);
				}
			}
		}

		[Test]
		public void CheckFilesAreRemoved () {

			var proj = new XamarinAndroidApplicationProject () {
				AndroidResources = { new AndroidItem.AndroidResource ("Resources\\values\\Theme.xml") {
					TextContent = () => @"<?xml version=""1.0"" encoding=""utf-8""?>
<resources>
	<color name=""theme_devicedefault_background"">#ff448aff</color>
	<style name=""Theme.Custom"" parent=""@android:style/Theme.DeviceDefault"">
		<item name=""android:colorBackground"">@color/theme_devicedefault_background</item>
		<item name=""android:windowBackground"">?android:attr/colorBackground</item>
	</style>
</resources>",
					}
				},
				Packages = {
					new Package (KnownPackages.AndroidSupportV13_21_0_3_0, audoAddReferences:true),
					new Package (KnownPackages.AndroidSupportV4_21_0_3_0, audoAddReferences:true),
					new Package (KnownPackages.SupportV7AppCompat_21_0_3_0, audoAddReferences:true),
				},
			};
			proj.SetProperty (KnownProperties.TargetFrameworkVersion, "v5.1");
			using (var builder = CreateApkBuilder ("temp/CheckFilesAreRemoved", false, false)) {
				builder.Verbosity = LoggerVerbosity.Diagnostic;
				Assert.IsTrue (builder.Build (proj), "Build should have succeeded");

				var theme = proj.AndroidResources.First (x => x.Include () == "Resources\\values\\Theme.xml");
				theme.Deleted = true;
				theme.Timestamp = DateTime.Now;
				Assert.IsTrue (builder.Build (proj), "Build should have succeeded");

				Assert.IsFalse (File.Exists (Path.Combine (Root, builder.ProjectDirectory, proj.IntermediateOutputPath, "res", "values", "theme.xml")),
					"Theme.xml was NOT removed from the intermediate directory");
			}
		}

		[Test]
		public void CheckDontUpdateResourceIfNotNeeded ()
		{
			var path = Path.Combine ("temp", "CheckDontUpdateResourceIfNotNeeded");
			var foo = new BuildItem.Source ("Foo.cs") {
				TextContent = () => @"using System;
namespace Lib1 {
	public class Foo {
		public string GetFoo () {
			return ""Foo"";
		}
	}
}"
			};
			var theme = new AndroidItem.AndroidResource ("Resources\\values\\Theme.xml") {
				TextContent = () => @"<?xml version=""1.0"" encoding=""utf-8""?>
<resources>
	<color name=""theme_devicedefault_background"">#ffffffff</color>
</resources>", 
			};
			var libProj = new XamarinAndroidLibraryProject () {
				IsRelease = true,
				ProjectName = "Lib1",
				Sources = {
					foo,
				},
				AndroidResources = {
					theme,
				},
			};
			var appProj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
				ProjectName = "App1",
				References = {
					new BuildItem.ProjectReference (@"..\Lib1\Lib1.csproj", libProj.ProjectName, libProj.ProjectGuid),
				},
			};
			using (var libBuilder = CreateDllBuilder (Path.Combine (path, libProj.ProjectName), false, false)) {
				libBuilder.Verbosity = LoggerVerbosity.Diagnostic;
				Assert.IsTrue (libBuilder.Build (libProj), "Library project should have built");
				using (var appBuilder = CreateApkBuilder (Path.Combine (path, appProj.ProjectName), false, false)) {
					appBuilder.Verbosity = LoggerVerbosity.Diagnostic;
					Assert.IsTrue (appBuilder.Build (appProj), "Application Build should have succeeded.");
					Assert.IsFalse (appBuilder.Output.IsTargetSkipped ("_UpdateAndroidResgen"), "_UpdateAndroidResgen target not should be skipped.");
					foo.Timestamp = DateTime.UtcNow;
					Assert.IsTrue (libBuilder.Build (libProj, doNotCleanupOnUpdate: true, saveProject: false), "Library project should have built");
					Assert.IsTrue (libBuilder.Output.IsTargetSkipped ("_AddLibraryProjectsEmbeddedResourceToProject"), "_AddLibraryProjectsEmbeddedResourceToProject should be skipped.");
					Assert.IsTrue (appBuilder.Build (appProj, doNotCleanupOnUpdate: true, saveProject: false), "Application Build should have succeeded.");
					Assert.IsTrue (appBuilder.Output.IsTargetSkipped ("_UpdateAndroidResgen"), "_UpdateAndroidResgen target should be skipped.");
					theme.TextContent = () => @"<?xml version=""1.0"" encoding=""utf-8""?>
<resources>
	<color name=""theme_devicedefault_background"">#00000000</color>
	<color name=""theme_devicedefault_background2"">#ffffffff</color>
</resources>";
					theme.Timestamp = DateTime.UtcNow;
					Assert.IsTrue (libBuilder.Build (libProj, doNotCleanupOnUpdate: true, saveProject: false), "Library project should have built");
					Assert.IsFalse (libBuilder.Output.IsTargetSkipped ("_AddLibraryProjectsEmbeddedResourceToProject"), "_AddLibraryProjectsEmbeddedResourceToProject should not be skipped.");
					Assert.IsTrue (appBuilder.Build (appProj, doNotCleanupOnUpdate: true, saveProject: false), "Application Build should have succeeded.");
					string text = File.ReadAllText (Path.Combine (Root, path, appProj.ProjectName, "Resources", "Resource.designer.cs"));
					Assert.IsTrue (text.Contains ("theme_devicedefault_background2"), "Resource.designer.cs was not updated.");
					Assert.IsFalse (appBuilder.Output.IsTargetSkipped ("_UpdateAndroidResgen"), "_UpdateAndroidResgen target should NOT be skipped.");
					theme.Deleted = true;
					theme.Timestamp = DateTime.UtcNow;
					Assert.IsTrue (libBuilder.Build (libProj, saveProject: true), "Library project should have built");
					var themeFile = Path.Combine (Root, path, libProj.ProjectName, libProj.IntermediateOutputPath, "res", "values", "theme.xml");
					Assert.IsTrue (!File.Exists (themeFile), $"{themeFile} should have been deleted.");
					var archive = Path.Combine (Root, path, libProj.ProjectName, libProj.IntermediateOutputPath, "__AndroidLibraryProjects__.zip");
					Assert.IsNull (ZipHelper.ReadFileFromZip (archive, "res/values/theme.xml"), "res/values/theme.xml should have been removed from __AndroidLibraryProjects__.zip");
				}
			}
		}

		[Test]
		public void BuildAppWithManagedResourceParser()
		{
			var path = Path.Combine ("temp", "BuildAppWithManagedResourceParser");
			var appProj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
				ProjectName = "App1",
			};
			appProj.SetProperty ("AndroidUseManagedDesignTimeResourceGenerator", "True");
			using (var appBuilder = CreateApkBuilder (Path.Combine (path, appProj.ProjectName))) {
				appBuilder.Verbosity = LoggerVerbosity.Diagnostic;
				appBuilder.Target = "Compile";
				Assert.IsTrue (appBuilder.Build (appProj, parameters: new string[] { "DesignTimeBuild=true"} ),
					"DesignTime Application Build should have succeeded.");
				Assert.IsFalse (appProj.CreateBuildOutput (appBuilder).IsTargetSkipped ("_ManagedUpdateAndroidResgen"),
					"Target '_ManagedUpdateAndroidResgen' should have run.");
				var designerFile = Path.Combine (Root, path, appProj.ProjectName, appProj.IntermediateOutputPath, "designtime", "Resource.Designer.cs");
				FileAssert.Exists (designerFile, $"'{designerFile}' should have been created.");

				var designerContents = File.ReadAllText (designerFile);
				StringAssert.Contains ("hello", designerContents, $"{designerFile} should contain Resources.Strings.hello");
				StringAssert.Contains ("app_name", designerContents, $"{designerFile} should contain Resources.Strings.app_name");
				StringAssert.Contains ("myButton", designerContents, $"{designerFile} should contain Resources.Id.myButton");
				StringAssert.Contains ("Icon", designerContents, $"{designerFile} should contain Resources.Drawable.Icon");
				StringAssert.Contains ("Main", designerContents, $"{designerFile} should contain Resources.Layout.Main");
				appBuilder.Target = "SignAndroidPackage";
				Assert.IsTrue (appBuilder.Build (appProj),
					"Normal Application Build should have succeeded.");
				Assert.IsTrue (appProj.CreateBuildOutput (appBuilder).IsTargetSkipped ("_ManagedUpdateAndroidResgen"),
					"Target '_ManagedUpdateAndroidResgen' should not have run.");

				Assert.IsTrue (appBuilder.Clean (appProj), "Clean should have succeeded");
				Assert.IsFalse (File.Exists (designerFile), $"'{designerFile}' should have been cleaned.");

			}
		}

		[Test]
		public void BuildAppWithManagedResourceParserAndLibraries ()
		{
			int maxBuildTimeMs = 8000;
			var path = Path.Combine ("temp", "BuildAppWithManagedResourceParserAndLibraries");
			var theme = new AndroidItem.AndroidResource ("Resources\\values\\Theme.xml") {
				TextContent = () => @"<?xml version=""1.0"" encoding=""utf-8""?>
<resources>
	<color name=""theme_devicedefault_background"">#ffffffff</color>
	<color name=""SomeColor"">#ffffffff</color>
</resources>",
			};
			var libProj = new XamarinAndroidLibraryProject () {
				IsRelease = true,
				ProjectName = "Lib1",
				AndroidResources = {
					theme,
				},
			};
			libProj.SetProperty ("AndroidUseManagedDesignTimeResourceGenerator", "True");
			var appProj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
				ProjectName = "App1",
				References = {
					new BuildItem.ProjectReference (@"..\Lib1\Lib1.csproj", libProj.ProjectName, libProj.ProjectGuid),
				},
				Packages = {
					KnownPackages.SupportMediaCompat_25_4_0_1,
					KnownPackages.SupportFragment_25_4_0_1,
					KnownPackages.SupportCoreUtils_25_4_0_1,
					KnownPackages.SupportCoreUI_25_4_0_1,
					KnownPackages.SupportCompat_25_4_0_1,
					KnownPackages.AndroidSupportV4_25_4_0_1,
					KnownPackages.SupportV7AppCompat_25_4_0_1,
				},
			};
			appProj.SetProperty ("AndroidUseManagedDesignTimeResourceGenerator", "True");
			using (var libBuilder = CreateDllBuilder (Path.Combine (path, libProj.ProjectName), false, false)) {
				libBuilder.Verbosity = LoggerVerbosity.Diagnostic;
				using (var appBuilder = CreateApkBuilder (Path.Combine (path, appProj.ProjectName), false, false)) {
					appBuilder.Verbosity = LoggerVerbosity.Diagnostic;
					libBuilder.Target = "Compile";
					Assert.IsTrue (libBuilder.Build (libProj, parameters: new string [] { "DesignTimeBuild=true" }), "Library project should have built");
					Assert.LessOrEqual (libBuilder.LastBuildTime.TotalMilliseconds, maxBuildTimeMs, "DesingTime build should be less than 5 seconds.");
					Assert.IsFalse (libProj.CreateBuildOutput (libBuilder).IsTargetSkipped ("_ManagedUpdateAndroidResgen"),
						"Target '_ManagedUpdateAndroidResgen' should have run.");
					appBuilder.Target = "Compile";
					Assert.IsTrue (appBuilder.Build (appProj, parameters: new string [] { "DesignTimeBuild=true" }), "Library project should have built");
					Assert.LessOrEqual (appBuilder.LastBuildTime.TotalMilliseconds, maxBuildTimeMs, "DesingTime build should be less than 5 seconds.");
					Assert.IsFalse (appProj.CreateBuildOutput (appBuilder).IsTargetSkipped ("_ManagedUpdateAndroidResgen"),
						"Target '_ManagedUpdateAndroidResgen' should have run.");
					var designerFile = Path.Combine (Root, path, appProj.ProjectName, appProj.IntermediateOutputPath, "designtime", "Resource.Designer.cs");
					FileAssert.Exists (designerFile, $"'{designerFile}' should have been created.");

					var designerContents = File.ReadAllText (designerFile);
					StringAssert.Contains ("hello", designerContents, $"{designerFile} should contain Resources.Strings.hello");
					StringAssert.Contains ("app_name", designerContents, $"{designerFile} should contain Resources.Strings.app_name");
					StringAssert.Contains ("myButton", designerContents, $"{designerFile} should contain Resources.Id.myButton");
					StringAssert.Contains ("Icon", designerContents, $"{designerFile} should contain Resources.Drawable.Icon");
					StringAssert.Contains ("Main", designerContents, $"{designerFile} should contain Resources.Layout.Main");
					StringAssert.Contains ("material_grey_50", designerContents, $"{designerFile} should contain Resources.Color.material_grey_50");
					StringAssert.DoesNotContain ("theme_devicedefault_background", designerContents, $"{designerFile} should not contain Resources.Color.theme_devicedefault_background");
					libBuilder.Target = "Build";
					Assert.IsTrue (libBuilder.Build (libProj), "Library project should have built");
					Assert.IsTrue (libProj.CreateBuildOutput (libBuilder).IsTargetSkipped ("_ManagedUpdateAndroidResgen"),
						"Target '_ManagedUpdateAndroidResgen' should not have run.");
					appBuilder.Target = "Compile";
					Assert.IsTrue (appBuilder.Build (appProj, parameters: new string [] { "DesignTimeBuild=true" }), "App project should have built");
					Assert.LessOrEqual (appBuilder.LastBuildTime.TotalMilliseconds, maxBuildTimeMs, "DesingTime build should be less than 5 seconds.");
					Assert.IsFalse (appProj.CreateBuildOutput (appBuilder).IsTargetSkipped ("_ManagedUpdateAndroidResgen"),
					"Target '_ManagedUpdateAndroidResgen' should have run.");
					FileAssert.Exists (designerFile, $"'{designerFile}' should have been created.");

					designerContents = File.ReadAllText (designerFile);
					StringAssert.Contains ("hello", designerContents, $"{designerFile} should contain Resources.Strings.hello");
					StringAssert.Contains ("app_name", designerContents, $"{designerFile} should contain Resources.Strings.app_name");
					StringAssert.Contains ("myButton", designerContents, $"{designerFile} should contain Resources.Id.myButton");
					StringAssert.Contains ("Icon", designerContents, $"{designerFile} should contain Resources.Drawable.Icon");
					StringAssert.Contains ("Main", designerContents, $"{designerFile} should contain Resources.Layout.Main");
					StringAssert.Contains ("material_grey_50", designerContents, $"{designerFile} should contain Resources.Color.material_grey_50");
					StringAssert.Contains ("theme_devicedefault_background", designerContents, $"{designerFile} should contain Resources.Color.theme_devicedefault_background");
					StringAssert.Contains ("SomeColor", designerContents, $"{designerFile} should contain Resources.Color.SomeColor");

					appBuilder.Target = "SignAndroidPackage";
					Assert.IsTrue (appBuilder.Build (appProj), "App project should have built");


					Assert.IsTrue (appBuilder.Clean (appProj), "Clean should have succeeded");
					Assert.IsFalse (File.Exists (designerFile), $"'{designerFile}' should have been cleaned.");
					designerFile = Path.Combine (Root, path, libProj.ProjectName, libProj.IntermediateOutputPath, "designtime", "Resource.Designer.cs");
					Assert.IsTrue (libBuilder.Clean (libProj), "Clean should have succeeded");
					Assert.IsFalse (File.Exists (designerFile), $"'{designerFile}' should have been cleaned.");


				}
			}
		}
	}
}
