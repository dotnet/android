using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Mono.Cecil;
using NUnit.Framework;
using Xamarin.Android.Tools;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	[Parallelizable (ParallelScope.Children)]
	public class AndroidUpdateResourcesTest : BaseTest
	{
		[Test]
		public void CheckMultipleLibraryProjectReferenceAlias ([Values (true, false)] bool withGlobal, [Values (true, false)] bool useDesignerAssembly)
		{
			var path = Path.Combine (Root, "temp", TestName);
			var library1 = new XamarinAndroidLibraryProject () {
				ProjectName = "Library1",
			};
			var library2 = new XamarinAndroidLibraryProject () {
				ProjectName = "Library2",
				RootNamespace = "Library1"
			};
			var proj = new XamarinAndroidApplicationProject () {
				References = {
					new BuildItem.ProjectReference (Path.Combine("..", library1.ProjectName, Path.GetFileName (library1.ProjectFilePath)), "Library1") {
						Metadata = { { "Aliases", withGlobal ? "global,Lib1A,Lib1B" : "Lib1A,Lib1B" } },
					},
					new BuildItem.ProjectReference (Path.Combine("..", library2.ProjectName, Path.GetFileName (library2.ProjectFilePath)), "Library2") {
						Metadata = { { "Aliases", withGlobal ? "global,Lib2A,Lib2B" : "Lib2A,Lib2B" } },
					},
				},
			};
			library1.SetProperty ("AndroidUseDesignerAssembly", "false");
			library2.SetProperty ("AndroidUseDesignerAssembly", useDesignerAssembly.ToString ());
			proj.SetProperty ("AndroidUseDesignerAssembly", useDesignerAssembly.ToString ());
			using (var builder1 = CreateDllBuilder (Path.Combine (path, library1.ProjectName), cleanupAfterSuccessfulBuild: false, cleanupOnDispose: false)) {
				builder1.ThrowOnBuildFailure = false;
				Assert.IsTrue (builder1.Build (library1), "Library should have built.");
				using (var builder2 = CreateDllBuilder (Path.Combine (path, library2.ProjectName), cleanupAfterSuccessfulBuild: false, cleanupOnDispose: false)) {
					builder2.ThrowOnBuildFailure = false;
					Assert.IsTrue (builder2.Build (library2), "Library should have built.");
					using (var b = CreateApkBuilder (Path.Combine (path, proj.ProjectName), cleanupAfterSuccessfulBuild: false, cleanupOnDispose: false)) {
						b.ThrowOnBuildFailure = false;
						Assert.IsTrue (b.Build (proj), "Project should have built.");
						if (!useDesignerAssembly) {
							string resource_designer_cs = GetResourceDesignerPath (b, proj);
							string [] text = GetResourceDesignerLines (proj, resource_designer_cs);
							Assert.IsTrue (text.Count (x => x.Contains ("Library1.Resource.String.library_name")) == 2, "library_name resource should be present exactly once for each library");
							Assert.IsTrue (text.Count (x => x == "extern alias Lib1A;" || x == "extern alias Lib1B;") <= 1, "No more than one extern alias should be present for each library.");
						}
					}
				}
			}
		}

		[Test]
		public void BuildAppWithSystemNamespace ()
		{
			var path = Path.Combine (Root, "temp", TestName);
			var library = new XamarinAndroidLibraryProject () {
				ProjectName = "Library1.System",
			};
			var proj = new XamarinAndroidApplicationProject () {
				References = {
					new BuildItem.ProjectReference (Path.Combine("..", library.ProjectName, Path.GetFileName (library.ProjectFilePath)), "Library1.System") {
					},
				},
			};
			using (var builder = CreateDllBuilder (Path.Combine (path, library.ProjectName), cleanupAfterSuccessfulBuild: false, cleanupOnDispose: false)) {
				builder.ThrowOnBuildFailure = false;
				Assert.IsTrue (builder.Build (library), "Library should have built.");
				using (var b = CreateApkBuilder (Path.Combine (path, proj.ProjectName), cleanupAfterSuccessfulBuild: false, cleanupOnDispose: false)) {
					b.ThrowOnBuildFailure = false;
					Assert.IsTrue (b.Build (proj), "Project should have built.");
				}
			}
		}

		[Test]
		public void DesignTimeBuild ([Values(false, true)] bool isRelease, [Values (false, true)] bool useManagedParser)
		{
			var regEx = new Regex (@"(?<type>([a-zA-Z_0-9])+)\slibrary_name=(?<value>([0-9A-Za-z])+);", RegexOptions.Compiled | RegexOptions.Multiline );

			var path = Path.Combine (Root, "temp", $"DesignTimeBuild_{isRelease}_{useManagedParser}");
			var lib = new XamarinAndroidLibraryProject () {
				ProjectName = "Lib1",
				IsRelease = isRelease,
			};
			lib.SetProperty ("AndroidUseManagedDesignTimeResourceGenerator", useManagedParser.ToString ());
			lib.SetProperty ("AndroidUseDesignerAssembly", "false");
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = isRelease,
				References = {
					new BuildItem.ProjectReference (@"..\Lib1\Lib1.csproj", lib.ProjectName, lib.ProjectGuid),
				},
			};
			var intermediateOutputPath = Path.Combine (path, proj.ProjectName, proj.IntermediateOutputPath);
			proj.SetProperty ("AndroidUseManagedDesignTimeResourceGenerator", useManagedParser.ToString ());
			proj.SetProperty ("AndroidUseDesignerAssembly", "false");
			using (var l = CreateDllBuilder (Path.Combine (path, lib.ProjectName), false, false)) {
				using (var b = CreateApkBuilder (Path.Combine (path, proj.ProjectName), false, false)) {
					l.Target = "Build";
					Assert.IsTrue(l.Clean(lib), "Lib1 should have cleaned successfully");
					Assert.IsTrue (l.Build (lib), "Lib1 should have built successfully");
					b.ThrowOnBuildFailure = false;
					b.Target = "Compile";
					Assert.IsTrue (b.Build (proj, doNotCleanupOnUpdate: true, parameters: new string [] { "DesignTimeBuild=true" }),
						"first build failed");
					var designTimeDesigner = Path.Combine (intermediateOutputPath, "designtime", "Resource.designer.cs");
					FileAssert.Exists (designTimeDesigner, $"{designTimeDesigner} should have been created.");
					WaitFor (1000);
					b.Target = "Build";
					Assert.IsTrue (b.Build (proj, doNotCleanupOnUpdate: true, parameters: new string [] { "DesignTimeBuild=false" }), "second build failed");
					FileAssert.Exists (Path.Combine (intermediateOutputPath, "R.txt"), "R.txt should exist after IncrementalClean!");
					FileAssert.Exists (Path.Combine (intermediateOutputPath, "res.flag"), "res.flag should exist after IncrementalClean!");
					if (useManagedParser) {
						FileAssert.Exists (designTimeDesigner, $"{designTimeDesigner} should not have been deleted.");
					}
					var items = new List<string> ();
					if (!useManagedParser) {
						foreach (var file in Directory.EnumerateFiles (Path.Combine (intermediateOutputPath, "android", "src"), "R.java", SearchOption.AllDirectories)) {
							var matches = regEx.Matches (File.ReadAllText (file));
							items.AddRange (matches.Cast<System.Text.RegularExpressions.Match> ().Select (x => x.Groups ["value"].Value));
						}
						var first = items.First ();
						Assert.IsTrue (items.All (x => x == first), "All Items should have matching values");
					}
				}
			}
		}

		[Test]
		public void CheckEmbeddedAndroidXResources ()
		{
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
				PackageReferences = {
					KnownPackages.AndroidXAppCompat,
				},
			};
			using (var b = CreateApkBuilder ()) {
				Assert.IsTrue (b.Build (proj), "First build should have succeeded.");
				var Rdrawable = b.Output.GetIntermediaryPath (Path.Combine ("android", "bin", "classes", "androidx", "appcompat", "R$drawable.class"));
				Assert.IsTrue (File.Exists (Rdrawable), $"{Rdrawable} should exist");
			}
		}

		[Test]
		public void MoveResource ()
		{
			var proj = new XamarinAndroidApplicationProject ();
			BuildItem image = null;
			var image_data = XamarinAndroidCommonProject.GetResourceContents ("Xamarin.ProjectTools.Resources.Base.Icon.png");
			image = new AndroidItem.AndroidResource ("Resources\\drawable\\Image.png") { BinaryContent = () => image_data };
			proj.AndroidResources.Add (image);
			using (var b = CreateApkBuilder ("temp/MoveResource")) {
				Assert.IsTrue (b.Build (proj), "First build should have succeeded.");
				var oldpath = image.Include ().Replace ('\\', Path.DirectorySeparatorChar);
				image.Include = () => "Resources/drawable/NewImage.png";
				image.Timestamp = DateTimeOffset.UtcNow.AddMinutes (1);
				Assert.IsTrue (b.Build (proj), "Second build should have succeeded.");
				Assert.IsFalse (File.Exists (Path.Combine (b.ProjectDirectory, oldpath)), "XamarinProject.UpdateProjectFiles() failed to delete file");
				Assert.IsFalse (b.Output.IsTargetSkipped ("_Sign"), "incorrectly skipped some build");
			}
		}

		[Test]
		public void ReportAaptErrorsInOriginalFileName ()
		{
			var proj = new XamarinAndroidApplicationProject ();
			proj.LayoutMain = @"<root/>\n" + proj.LayoutMain;
			using (var b = CreateApkBuilder ("temp/ErroneousResource", false, false)) {
				b.ThrowOnBuildFailure = false;
				// The AndroidGenerateLayoutBindings=false property is necessary because otherwise build
				// will fail in code-behind generator instead of in aapt
				Assert.IsFalse (b.Build (proj, parameters: new[] { "AndroidGenerateLayoutBindings=false" }), "Build should have failed.");
				Assert.IsTrue (b.LastBuildOutput.Any (s => s.Contains (string.Format ("Resources{0}layout{0}Main.axml", Path.DirectorySeparatorChar)) && s.Contains (": error ")), "error with expected file name is not found");
				Assert.IsTrue (b.Clean (proj), "Clean should have succeeded.");
			}
		}

		[Test]
		public void ReportAaptWarningsForBlankLevel ()
		{
			//This test should get the warning `Invalid file name: must contain only [a-z0-9_.]`
			//    However, <Aapt /> still fails due to aapt failing, Resource.designer.cs is not generated
			var proj = new XamarinAndroidApplicationProject ();
			proj.AndroidResources.Add (new AndroidItem.AndroidResource ("Resources\\drawable\\Image (1).png") {
				BinaryContent = () => XamarinAndroidCommonProject.icon_binary_mdpi
			});
			using (var b = CreateApkBuilder ($"temp/{TestName}")) {
				b.ThrowOnBuildFailure = false;
				Assert.IsFalse (b.Build (proj), "Build should have failed.");
				StringAssertEx.Contains ("APT0003", b.LastBuildOutput, "An error message with a blank \"level\", should be reported as an error!");
				Assert.IsTrue (b.Clean (proj), "Clean should have succeeded.");
			}
		}

		[Test]
		public void RepetiviteBuildUpdateSingleResource ()
		{
			var proj = new XamarinAndroidApplicationProject ();
			using (var b = CreateApkBuilder ()) {
				BuildItem image1, image2;
				var image_data = XamarinAndroidCommonProject.GetResourceContents ("Xamarin.ProjectTools.Resources.Base.Icon.png");
				image1 = new AndroidItem.AndroidResource ("Resources\\drawable\\Image1.png") { BinaryContent = () => image_data };
				proj.AndroidResources.Add (image1);
				image2 = new AndroidItem.AndroidResource ("Resources\\drawable\\Image2.png") { BinaryContent = () => image_data };
				proj.AndroidResources.Add (image2);
				b.ThrowOnBuildFailure = false;
				Assert.IsTrue (b.Build (proj), "First build was supposed to build without errors");
				var firstBuildTime = b.LastBuildTime;
				Assert.IsTrue (b.Build (proj, doNotCleanupOnUpdate: true), "Second build was supposed to build without errors");
				Assert.IsTrue (firstBuildTime > b.LastBuildTime, "Second build was supposed to be quicker than the first");
				b.Output.AssertTargetIsSkipped ("_UpdateAndroidResgen");
				b.Output.AssertTargetIsSkipped ("_GenerateAndroidResourceDir");
				b.Output.AssertTargetIsSkipped ("_CompileJava");
				b.Output.AssertTargetIsSkipped (KnownTargets.LinkAssembliesNoShrink);
				b.Output.AssertTargetIsSkipped ("_CompileResources");
				image1.Timestamp = DateTimeOffset.UtcNow;
				var layout = proj.AndroidResources.First (x => x.Include() == "Resources\\layout\\Main.axml");
				layout.Timestamp = DateTimeOffset.UtcNow;
				Assert.IsTrue (b.Build (proj, doNotCleanupOnUpdate:true, saveProject: false), "Third build was supposed to build without errors");
				b.Output.AssertTargetIsNotSkipped ("_UpdateAndroidResgen",           occurrence: 2);
				b.Output.AssertTargetIsNotSkipped ("_GenerateAndroidResourceDir",    occurrence: 2);
				b.Output.AssertTargetIsSkipped ("_CompileJava",                      occurrence: 2);
				b.Output.AssertTargetIsSkipped (KnownTargets.LinkAssembliesNoShrink, occurrence: 2);
				b.Output.AssertTargetIsNotSkipped ("_CreateBaseApk",                 occurrence: 2);
				b.Output.AssertTargetIsPartiallyBuilt ("_CompileResources");
			}
		}

		[Test]
		[Category ("XamarinBuildDownload")]
		[NonParallelizable]
		public void Check9PatchFilesAreProcessed ()
		{
			var projectPath = Path.Combine ("temp", "Check9PatchFilesAreProcessed");
			var libproj = new XamarinAndroidLibraryProject () { ProjectName = "Library1"};
			var image_data = XamarinAndroidCommonProject.GetResourceContents ("Xamarin.ProjectTools.Resources.Base.Image.9.png");
			var image2 = new AndroidItem.AndroidResource ("Resources\\drawable\\Image2.9.png") { BinaryContent = () => image_data };
			libproj.AndroidResources.Add (image2);
			using (var libb = CreateDllBuilder (Path.Combine (projectPath, "Library1"))) {
				libb.Build (libproj);
				var proj = new XamarinFormsMapsApplicationProject ();
				var image1 = new AndroidItem.AndroidResource ("Resources\\drawable\\Image1.9.png") { BinaryContent = () => image_data };
				proj.AndroidResources.Add (image1);
				proj.References.Add (new BuildItem ("ProjectReference", "..\\Library1\\Library1.csproj"));
				using (var b = CreateApkBuilder (Path.Combine (projectPath, "Application1"), false, false)) {
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
					data = ZipHelper.ReadFileFromZip (path, "res/drawable-hdpi-v4/common_google_signin_btn_icon_dark_normal_background.9.png");
					Assert.IsNotNull (data, "common_google_signin_btn_icon_dark_normal_background.9.png.png should be in {0}android/bin/packaged_resources",
						proj.IntermediateOutputPath);
					png = PNGChecker.LoadFromBytes (data);
					Assert.IsTrue (png.Is9Patch, "common_google_signin_btn_icon_dark_normal_background.9.png should have been processed into a 9 patch image.");
					Directory.Delete (Path.Combine (Root,projectPath), recursive: true);
				}
			}
		}

		[Test]
		/// <summary>
		/// Based on https://bugzilla.xamarin.com/show_bug.cgi?id=29263
		/// </summary>
		public void CheckXmlResourcesFilesAreProcessed ()
		{
			var projectPath = "temp/CheckXmlResourcesFilesAreProcessed";

			var layout =  @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<LinearLayout xmlns:android=""http://schemas.android.com/apk/res/android""
	android:orientation = ""vertical""
	android:layout_width = ""fill_parent""
	android:layout_height = ""fill_parent"">
	<classlibrary1.CustomTextView
		android:id = ""@+id/myText1""
		android:layout_width = ""fill_parent""
		android:layout_height = ""wrap_content""
		android:text = ""namespace_lower"" />
	<ClassLibrary1.CustomTextView
		android:id = ""@+id/myText2""
		android:layout_width = ""fill_parent""
		android:layout_height = ""wrap_content""
		android:text = ""namespace_proper"" />
</LinearLayout>";
			var lib = new XamarinAndroidLibraryProject () {
				ProjectName = "Classlibrary1",
			};
			lib.AndroidResources.Add (new AndroidItem.AndroidResource ("Resources\\layout\\custom_text_lib.xml") {
				TextContent = () => layout,
			});
			lib.Sources.Add (new BuildItem.Source ("CustomTextView.cs") {
				TextContent = () => @"using Android.Widget;
using Android.Content;
using Android.Util;
namespace ClassLibrary1
{
	public class CustomTextView : TextView
	{
		public CustomTextView(Context context, IAttributeSet attributes) : base(context, attributes)
		{
		}
	}
}"
			});

			var proj = new XamarinAndroidApplicationProject () {
				OtherBuildItems = {
					new BuildItem.ProjectReference (@"..\Classlibrary1\Classlibrary1.csproj", "Classlibrary1", lib.ProjectGuid) {
					},
				}
			};

			proj.AndroidResources.Add (new AndroidItem.AndroidResource ("Resources\\layout\\custom_text_app.xml") {
				TextContent = () => layout,
			});

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
			proj.PackageReferences.Add (KnownPackages.AndroidXAppCompat);
			using (var libb = CreateDllBuilder (Path.Combine (projectPath, lib.ProjectName), cleanupOnDispose: false))
			using (var b = CreateApkBuilder (Path.Combine (projectPath, proj.ProjectName), cleanupOnDispose: false)) {
				Assert.IsTrue (libb.Build (lib), "Library Build should have succeeded.");
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				var intermediate = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath);
				var packaged_resources = Path.Combine (intermediate, "android", "bin", "packaged_resources");
				FileAssert.Exists (packaged_resources);
				var assemblyIdentityMap = b.Output.GetAssemblyMapCache ();
				var assemblyIndex = assemblyIdentityMap.IndexOf ($"{lib.ProjectName}.aar").ToString ();
				using (var zip = ZipHelper.OpenZip (packaged_resources)) {
					CheckCustomView (zip, intermediate, "lp", assemblyIndex, "jl", "res", "layout", "custom_text_lib.xml");
					CheckCustomView (zip, intermediate, "res", "layout", "custom_text_app.xml");
				}

				var preferencesPath = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath, "res","xml","preferences.xml");
				Assert.IsTrue (File.Exists (preferencesPath), "Preferences.xml should have been renamed to preferences.xml");
				var doc = XDocument.Load (preferencesPath);
				Assert.IsNotNull (doc.Element ("PreferenceScreen"), "PreferenceScreen should be present in preferences.xml");
				Assert.IsNull (doc.Element ("PreferenceScreen").Element ("UnnamedProject.CustomPreference"),
					"UnamedProject.CustomPreference should have been replaced with an $(Hash).CustomPreference");
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
				Assert.IsFalse (StringAssertEx.ContainsText (b.LastBuildOutput, "AndroidResgen: Warning while updating Resource XML"),
					"Warning while processing resources should not have been raised.");
				Assert.IsTrue (b.Build (proj, doNotCleanupOnUpdate: true), "Build should have succeeded.");
				Assert.IsTrue (b.Output.IsTargetSkipped ("_GenerateJavaStubs"), "Target _GenerateJavaStubs should have been skipped");

				lib.Touch ("CustomTextView.cs");

				Assert.IsTrue (libb.Build (lib, doNotCleanupOnUpdate: true, saveProject: false), "second library build should have succeeded.");
				Assert.IsTrue (b.Build (proj, doNotCleanupOnUpdate: true, saveProject: false), "second app build should have succeeded.");

				using (var zip = ZipHelper.OpenZip (packaged_resources)) {
					CheckCustomView (zip, intermediate, "lp", assemblyIndex, "jl", "res", "layout", "custom_text_lib.xml");
					CheckCustomView (zip, intermediate, "res", "layout", "custom_text_app.xml");
				}

				Assert.IsTrue (b.Clean (proj), "Clean should have succeeded.");
			}
		}

		void CheckCustomView (Xamarin.Tools.Zip.ZipArchive zip, params string [] paths)
		{
			var customViewPath = Path.Combine (paths);
			FileAssert.Exists (customViewPath, $"custom_text.xml should exist at {customViewPath}");
			var doc = XDocument.Load (customViewPath);
			Assert.IsNotNull (doc.Element ("LinearLayout"), "PreferenceScreen should be present in preferences.xml");
			Assert.IsNull (doc.Element ("LinearLayout").Element ("Classlibrary1.CustomTextView"),
				$"Classlibrary1.CustomTextView should have been replaced with an $(Hash).CustomTextView in {customViewPath}");
			Assert.IsNull (doc.Element ("LinearLayout").Element ("classlibrary1.CustomTextView"),
				$"classlibrary1.CustomTextView should have been replaced with an $(Hash).CustomTextView in {customViewPath}");

			//Now check the zip
			var customViewInZip = "res/layout/" + Path.GetFileName (customViewPath);
			var entry = zip.ReadEntry (customViewInZip);
			Assert.IsNotNull (entry, $"`{customViewInZip}` should exist in packaged_resources!");

			using (var stream = new MemoryStream ()) {
				entry.Extract (stream);
				stream.Position = 0;

				using (var reader = new StreamReader (stream)) {
					//NOTE: This is a binary format, but we can still look for text within.
					//      Don't use `StringAssert` because `contents` make the failure message unreadable.
					var contents = reader.ReadToEnd ();
					Assert.IsFalse (contents.Contains ("Classlibrary1.CustomTextView"),
							$"Classlibrary1.CustomTextView should have been replaced with an $(Hash).CustomTextView in {customViewInZip} in package");
					Assert.IsFalse (contents.Contains ("classlibrary1.CustomTextView"),
							$"classlibrary1.CustomTextView should have been replaced with an $(Hash).CustomTextView in {customViewInZip} in package");
				}
			}
		}

		static object[] ReleaseLanguage = new object[] {
			new object[] { false, XamarinAndroidProjectLanguage.CSharp },
			new object[] { true, XamarinAndroidProjectLanguage.CSharp },
			new object[] { false, XamarinAndroidProjectLanguage.FSharp },
			new object[] { true, XamarinAndroidProjectLanguage.FSharp },
		};

		[Test]
		[Parallelizable (ParallelScope.Self)]
		[TestCaseSource (nameof (ReleaseLanguage))]
		public void CheckResourceDesignerIsCreated (bool isRelease, ProjectLanguage language)
		{
			bool isFSharp = language == XamarinAndroidProjectLanguage.FSharp;

			var proj = new XamarinAndroidApplicationProject () {
				Language = language,
				IsRelease = isRelease,
			};
			proj.SetProperty ("AndroidUseIntermediateDesignerFile", "True");
			using (var b = CreateApkBuilder ()) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				// Intermediate designer file support is not compatible with F# projects using Xamarin.Android.FSharp.ResourceProvider.
				string outputFile = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath, "__Microsoft.Android.Resource.Designer" + proj.Language.DefaultDesignerExtension);
				Assert.IsTrue (File.Exists (outputFile), $"{outputFile} should have been created in {proj.IntermediateOutputPath}");
				Assert.IsTrue (b.Clean (proj), "Clean should have succeeded.");
				Assert.IsFalse (File.Exists (outputFile), "Resource.designer{1} should have been cleaned in {0}",
					proj.IntermediateOutputPath, proj.Language.DefaultDesignerExtension);
			}
		}

		[Test]
		[TestCaseSource(nameof (ReleaseLanguage))]
		public void CheckResourceDesignerIsUpdatedWhenReadOnly (bool isRelease, ProjectLanguage language)
		{
			bool isFSharp = language == XamarinAndroidProjectLanguage.FSharp;
			var proj = new XamarinAndroidApplicationProject () {
				Language = language,
				IsRelease = isRelease,
			};
			using (var b = CreateApkBuilder ()) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				var designerPath = GetResourceDesignerPath (b, proj);
				var attr = File.GetAttributes (designerPath);
				File.SetAttributes (designerPath, FileAttributes.ReadOnly);
				Assert.IsTrue ((File.GetAttributes (designerPath) & FileAttributes.ReadOnly) == FileAttributes.ReadOnly,
					"{0} should be read only", designerPath);
				var main = proj.AndroidResources.First (x => x.Include () == "Resources\\layout\\Main.axml");
				main.Timestamp = DateTimeOffset.UtcNow;
				main.TextContent = () => @"<?xml version=""1.0"" encoding=""utf-8""?>
<LinearLayout xmlns:android=""http://schemas.android.com/apk/res/android""
	android:orientation=""vertical""
	android:layout_width=""fill_parent""
	android:layout_height=""fill_parent""
	>
<Button
	android:id=""@+id/myButton""
	android:layout_width=""fill_parent""
	android:layout_height=""wrap_content""
	android:text=""Hello""
	/>
<TextView
	android:id=""@+id/myText""
	/>
</LinearLayout>";
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				Assert.IsTrue ((File.GetAttributes (designerPath) & FileAttributes.ReadOnly) != FileAttributes.ReadOnly,
					"{0} should be writable", designerPath);
			}
		}

		[Test]
		public void CheckOldResourceDesignerIsNotUsed ([Values (true, false)] bool isRelease)
		{
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = isRelease,
			};
			proj.SetProperty ("AndroidUseIntermediateDesignerFile", "True");
			proj.SetProperty ("AndroidUseManagedDesignTimeResourceGenerator", "False");
			using (var b = CreateApkBuilder ()) {
				var designer = Path.Combine ("Resources", "Resource.designer" + proj.Language.DefaultDesignerExtension);
				if (File.Exists (designer))
					File.Delete (Path.Combine (Root, b.ProjectDirectory, designer));
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				var fi = new FileInfo (Path.Combine (Root, b.ProjectDirectory, designer));
				Assert.IsFalse (fi.Length > new [] { 0xef, 0xbb, 0xbf, 0x0d, 0x0a }.Length,
					"{0} should not contain anything.", designer);
				var designerFile = "__Microsoft.Android.Resource.Designer";
				var outputFile = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath,
					designerFile + proj.Language.DefaultDesignerExtension);
				Assert.IsTrue (File.Exists (outputFile), $"{designerFile}{proj.Language.DefaultDesignerExtension} should have been created in {proj.IntermediateOutputPath}");
				Assert.IsTrue (b.Clean (proj), "Clean should have succeeded.");
				Assert.IsFalse (File.Exists (outputFile), $"{designerFile}{proj.Language.DefaultDesignerExtension} should have been cleaned in {proj.IntermediateOutputPath}");
			}
		}

		// ref https://bugzilla.xamarin.com/show_bug.cgi?id=30089
		[Test]
		public void CheckOldResourceDesignerWithWrongCasingIsRemoved ([Values (true, false)] bool isRelease)
		{
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = isRelease,
			};
			proj.SetProperty ("AndroidUseIntermediateDesignerFile", "True");
			proj.SetProperty ("AndroidResgenFile", "Resources\\Resource.designer" + proj.Language.DefaultDesignerExtension);
			using (var b = CreateApkBuilder ()) {
				var designer = proj.Sources.FirstOrDefault (x => x.Include() == "Resources\\Resource.designer" + proj.Language.DefaultDesignerExtension);
				designer = designer ?? proj.OtherBuildItems.FirstOrDefault (x => x.Include () == "Resources\\Resource.designer" + proj.Language.DefaultDesignerExtension);
				Assert.IsNotNull (designer, $"Failed to retrieve the Resource.designer.{proj.Language.DefaultDesignerExtension}");
				designer.Deleted = true;
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				Assert.IsFalse (File.Exists (Path.Combine (Root, b.ProjectDirectory, "Resources",
					"Resource.designer"  + proj.Language.DefaultDesignerExtension)),
					"{0} should not exists", designer.Include ());
				var designerFile = "__Microsoft.Android.Resource.Designer";
				var outputFile = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath,
					designerFile  + proj.Language.DefaultDesignerExtension);
				Assert.IsTrue (File.Exists (outputFile), $"{designerFile}{proj.Language.DefaultDesignerExtension} should have been created in {proj.IntermediateOutputPath}");
				Assert.IsTrue (b.Clean (proj), "Clean should have succeeded.");
				Assert.IsFalse (File.Exists (outputFile), $"{designerFile}{proj.Language.DefaultDesignerExtension} should have been cleaned in {proj.IntermediateOutputPath}");
			}
		}

		[Test]
		public void GenerateResourceDesigner_false([Values (false, true)] bool useDesignerAssembly)
		{
			var proj = new XamarinAndroidApplicationProject {
				EnableDefaultItems = true,
				Sources = {
					new AndroidItem.AndroidResource (() => "Resources\\drawable\\foo.png") {
						BinaryContent = () => XamarinAndroidCommonProject.icon_binary_mdpi,
					},
				}
			};
			proj.SetProperty (KnownProperties.OutputType, "Library");

			// Turn off Resource.designer.cs and remove usage of it
			proj.SetProperty ("AndroidGenerateResourceDesigner", "false");
			if (!useDesignerAssembly)
				proj.SetProperty ("AndroidUseDesignerAssembly", "false");
			proj.MainActivity = proj.DefaultMainActivity
				.Replace ("Resource.Layout.Main", "0")
				.Replace ("Resource.Id.myButton", "0");

			var builder = CreateDllBuilder ();
			Assert.IsTrue (builder.RunTarget(proj, "CoreCompile", parameters: new string[] { "BuildingInsideVisualStudio=true" }), "Designtime build should succeed.");
			var intermediate = Path.Combine (Root, builder.ProjectDirectory, proj.IntermediateOutputPath);
			var resource_designer_cs = Path.Combine (intermediate, "designtime",  "Resource.designer.cs");
			if (useDesignerAssembly)
				resource_designer_cs = Path.Combine (intermediate, "__Microsoft.Android.Resource.Designer.cs");
			FileAssert.DoesNotExist (resource_designer_cs);

			Assert.IsTrue (builder.Build (proj), "build should succeed");

			resource_designer_cs =  Path.Combine (intermediate, "Resource.designer.cs");
			if (useDesignerAssembly)
				resource_designer_cs = Path.Combine (intermediate, "__Microsoft.Android.Resource.Designer.cs");
			FileAssert.DoesNotExist (resource_designer_cs);

			var assemblyPath = Path.Combine (Root, builder.ProjectDirectory, proj.OutputPath, $"{proj.ProjectName}.dll");
			FileAssert.Exists (assemblyPath);
			using var assembly = AssemblyDefinition.ReadAssembly (assemblyPath);
			var typeName = $"{proj.ProjectName}.Resource";
			var type = assembly.MainModule.GetType (typeName);
			Assert.IsNull (type, $"{assemblyPath} should *not* contain {typeName}");
		}

		[Test]
		public void CheckThatXA1034IsRaisedForInvalidConfiguration ([Values (true, false)] bool isRelease)
		{
			string path = Path.Combine (Root, "temp", TestName);
			var foo = new BuildItem.Source ("Foo.cs") {
				TextContent = () => @"using System;
namespace Lib1 {
	public class Foo {
		public static string GetFoo () {
			return ""Foo"";
		}
	}
}"
			};
			var library = new XamarinAndroidLibraryProject () {
				IsRelease = isRelease,
				ProjectName = "Lib1",
				Sources = { foo },
			};
			library.SetProperty ("AndroidUseDesignerAssembly", "True");

			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = isRelease,
				ProjectName = "App1",
				References = {
					new BuildItem.ProjectReference ($"..\\{library.ProjectName}\\{library.ProjectName}.csproj", library.ProjectName, library.ProjectGuid),
				},
			};
			proj.SetProperty ("AndroidUseDesignerAssembly", "False");
			proj.MainActivity = proj.DefaultMainActivity.Replace ("//${AFTER_ONCREATE}", "Console.WriteLine (Lib1.Foo.GetFoo ());");
			using (var lb = CreateDllBuilder (Path.Combine (path, library.ProjectName))) {
				lb.ThrowOnBuildFailure = false;
				Assert.IsTrue (lb.Build (library), "Library project should have built.");
				using (var pb = CreateApkBuilder (Path.Combine (path, proj.ProjectName))) {
					pb.ThrowOnBuildFailure = false;
					Assert.IsFalse (pb.Build (proj), "Application project build should have failed.");
					StringAssertEx.ContainsText (pb.LastBuildOutput, "XA1034: ");
					StringAssertEx.ContainsText (pb.LastBuildOutput, "1 Error(s)");
				}
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
				b.ThrowOnBuildFailure = false;
				Assert.IsFalse (b.Build (proj), "Build should have failed");
				StringAssertEx.Contains ("APT2260: ", b.LastBuildOutput);
				StringAssertEx.Contains ("3 Error(s)", b.LastBuildOutput);
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
			using (var b = CreateApkBuilder ()) {
				b.ThrowOnBuildFailure = false;
				Assert.IsFalse (b.Build (proj), "Build should have failed");
				StringAssertEx.Contains ("APT2144: ", b.LastBuildOutput);
				StringAssertEx.Contains ("1 Error(s)", b.LastBuildOutput);
			}
		}

		[Test]
		public void CheckAaptErrorRaisedForInvalidFileName ()
		{
			var proj = new XamarinAndroidApplicationProject ();
			proj.AndroidResources.Add (new AndroidItem.AndroidResource ("Resources\\drawable\\icon-2.png") {
				BinaryContent = () => XamarinAndroidCommonProject.icon_binary_hdpi,
			});
			proj.AndroidResources.Add (new AndroidItem.AndroidResource ("Resources\\values\\strings-2.xml") {
				TextContent = () => @"<?xml version=""1.0"" encoding=""utf-8""?>
<resources>
	<string name=""hello"">Hello World, Click Me!</string>
</resources>",
			});
			using (var b = CreateApkBuilder ()) {
				b.ThrowOnBuildFailure = false;
				Assert.IsFalse (b.Build (proj), "Build should have failed");
				StringAssertEx.Contains ("Invalid file name:", b.LastBuildOutput);
				StringAssertEx.Contains ($"1 Error(s)", b.LastBuildOutput);
			}
		}

		[Test]
		public void CheckAaptErrorNotRaisedForInvalidFileNameWithValidLogicalName ()
		{
			var proj = new XamarinAndroidApplicationProject ();
			proj.AndroidResources.Add (new AndroidItem.AndroidResource ("Resources\\drawable\\icon-2.png") {
				Metadata = { { "LogicalName", "Resources\\drawable\\icon2.png" } },
				BinaryContent = () => XamarinAndroidCommonProject.icon_binary_hdpi,
			});
			proj.AndroidResources.Add (new AndroidItem.AndroidResource ("Resources\\values\\strings-2.xml") {
				Metadata = { { "LogicalName", "Resources\\values\\strings2.xml" } },
				TextContent = () => @"<?xml version=""1.0"" encoding=""utf-8""?>
<resources>
	<string name=""hellome"">Hello World, Click Me!</string>
</resources>",
			});
			using (var b = CreateApkBuilder ()) {
				b.ThrowOnBuildFailure = false;
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				StringAssertEx.DoesNotContain ("Invalid file name:", b.LastBuildOutput);
				StringAssertEx.DoesNotContain ("1 Error(s)", b.LastBuildOutput);
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
			using (var b = CreateApkBuilder ()) {
				b.ThrowOnBuildFailure = false;
				Assert.IsFalse (b.Build (proj), "Build should have failed");
				StringAssertEx.Contains ("APT2057: ", b.LastBuildOutput);
				StringAssertEx.Contains ("APT2222: ", b.LastBuildOutput);
				StringAssertEx.Contains ("APT2261: ", b.LastBuildOutput);
				StringAssertEx.Contains ("3 Error(s)", b.LastBuildOutput);
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
			};
			using (var builder = CreateApkBuilder ()) {
				Assert.IsTrue (builder.Build (proj), "Build should have succeeded");

				var theme = proj.AndroidResources.First (x => x.Include () == "Resources\\values\\Theme.xml");
				theme.Deleted = true;
				theme.Timestamp = DateTimeOffset.UtcNow;
				Assert.IsTrue (builder.Build (proj), "Build should have succeeded");

				Assert.IsFalse (File.Exists (Path.Combine (Root, builder.ProjectDirectory, proj.IntermediateOutputPath, "res", "values", "theme.xml")),
					"Theme.xml was NOT removed from the intermediate directory");
			}
		}

		[Test]
		public void CheckDontUpdateResourceIfNotNeeded ([Values (true, false)] bool useDesignerAssembly)
		{
			var path = Path.Combine ("temp", TestName);
			var target = "_CreateAar";
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
			var raw = new AndroidItem.AndroidResource ("Resources\\raw\\test.txt") {
				TextContent = () => @"Test Build 1",
			};
			var rawToDelete = new AndroidItem.AndroidResource ("Resources\\raw\\test2.txt") {
				TextContent = () => @"Test Raw To Delete",
			};
			var libProj = new XamarinAndroidLibraryProject () {
				IsRelease = true,
				ProjectName = "Lib1",
				Sources = {
					foo,
				},
				AndroidResources = {
					theme,
					raw,
					rawToDelete,
				},
			};
			libProj.SetProperty ("Deterministic", "true");
			libProj.SetProperty ("AndroidUseDesignerAssembly", useDesignerAssembly.ToString ());
			var appProj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
				ProjectName = "App1",
				References = {
					new BuildItem.ProjectReference (@"..\Lib1\Lib1.csproj", libProj.ProjectName, libProj.ProjectGuid),
				},
			};
			appProj.SetProperty ("AndroidUseDesignerAssembly", useDesignerAssembly.ToString ());
			using (var libBuilder = CreateDllBuilder (Path.Combine (path, libProj.ProjectName), false, false)) {
				Assert.IsTrue (libBuilder.Build (libProj), "Library project should have built");
				using (var appBuilder = CreateApkBuilder (Path.Combine (path, appProj.ProjectName), false, false)) {
					Assert.IsTrue (appBuilder.Build (appProj), "Application Build should have succeeded.");
					appBuilder.Output.AssertTargetIsNotSkipped ("_UpdateAndroidResgen");
					foo.Timestamp = DateTimeOffset.UtcNow;
					Assert.IsTrue (libBuilder.Build (libProj, doNotCleanupOnUpdate: true, saveProject: false), "Library project should have built");
					libBuilder.Output.AssertTargetIsSkipped (target);
					appBuilder.BuildLogFile = "build1.log";
					Assert.IsTrue (appBuilder.Build (appProj, doNotCleanupOnUpdate: true, saveProject: false), "Application Build should have succeeded.");
					appBuilder.Output.AssertTargetIsSkipped ("_UpdateAndroidResgen");
					// Check Contents of the file in the apk are correct.
					string apk = Path.Combine (Root, appBuilder.ProjectDirectory, appProj.OutputPath, appProj.PackageName + "-Signed.apk");
					byte[] rawContentBuildOne = ZipHelper.ReadFileFromZip (apk,
						"res/raw/test.txt");

					Assert.IsNotNull (rawContentBuildOne, "res/raw/test.txt should have been in the apk ");
					string txt = Encoding.UTF8.GetString (rawContentBuildOne ?? Array.Empty<byte> ());
					StringAssert.Contains ("Test Build 1", txt, $"res/raw/test.txt should have been 'Test Build 1' not {txt}");
					Assert.IsNotNull (ZipHelper.ReadFileFromZip (apk, "res/raw/test2.txt"), "res/raw/test2.txt should have been in the apk.");
					theme.TextContent = () => @"<?xml version=""1.0"" encoding=""utf-8""?>
<resources>
	<color name=""theme_devicedefault_background"">#00000000</color>
	<color name=""theme_devicedefault_background2"">#ffffffff</color>
</resources>";
					theme.Timestamp = DateTimeOffset.UtcNow;
					raw.TextContent = () => @"Test Build 2 Now";
					raw.Timestamp = DateTimeOffset.UtcNow;
					Assert.IsTrue (libBuilder.Build (libProj, doNotCleanupOnUpdate: true, saveProject: false), "Library project should have built");
					libBuilder.Output.AssertTargetIsNotSkipped (target);
					appBuilder.BuildLogFile = "build2.log";
					Assert.IsTrue (appBuilder.Build (appProj, doNotCleanupOnUpdate: true, saveProject: false), "Application Build should have succeeded.");
					string resource_designer_cs = GetResourceDesignerPath (appBuilder, appProj);
					string text = GetResourceDesignerText (appProj, resource_designer_cs);
					StringAssert.Contains ("theme_devicedefault_background2", text, "Resource.designer.cs was not updated.");
					appBuilder.Output.AssertTargetIsNotSkipped ("_UpdateAndroidResgen");
					appBuilder.Output.AssertTargetIsNotSkipped ("_CreateBaseApk");
					appBuilder.Output.AssertTargetIsNotSkipped ("_BuildApkEmbed");
					byte[] rawContentBuildTwo = ZipHelper.ReadFileFromZip (apk,
						"res/raw/test.txt");
					Assert.IsNotNull (rawContentBuildTwo, "res/raw/test.txt should have been in the apk ");
					txt = Encoding.UTF8.GetString (rawContentBuildTwo ?? Array.Empty<byte> ());
					StringAssert.Contains ("Test Build 2 Now", txt, $"res/raw/test.txt should have been 'Test Build 2' not {txt}");
					rawToDelete.Deleted = true;
					rawToDelete.Timestamp = DateTimeOffset.UtcNow;
					theme.Deleted = true;
					theme.Timestamp = DateTimeOffset.UtcNow;
					Assert.IsTrue (libBuilder.Build (libProj, doNotCleanupOnUpdate: true, saveProject: true), "Library project should have built");
					var themeFile = Path.Combine (Root, path, libProj.ProjectName, libProj.IntermediateOutputPath, "res", "values", "theme.xml");
					Assert.IsTrue (!File.Exists (themeFile), $"{themeFile} should have been deleted.");
					string archive;
					archive = Path.Combine (Root, path, libProj.ProjectName, libProj.OutputPath, $"{libProj.ProjectName}.aar");
					Assert.IsNull (ZipHelper.ReadFileFromZip (archive, "res/values/theme.xml"), "res/values/theme.xml should have been removed from __AndroidLibraryProjects__.zip");
					appBuilder.BuildLogFile = "build3.log";
					Assert.IsTrue (appBuilder.Build (appProj, doNotCleanupOnUpdate: true, saveProject: false), "Application Build should have succeeded.");
					Assert.IsNull (ZipHelper.ReadFileFromZip (apk, "res/raw/test2.txt"), "res/raw/test2.txt should have been removed from the apk.");
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
			appProj.SetProperty ("AndroidUseDesignerAssembly", "false");
			using (var appBuilder = CreateApkBuilder (Path.Combine (path, appProj.ProjectName))) {
				appBuilder.Verbosity = LoggerVerbosity.Detailed;
				Assert.IsTrue (appBuilder.DesignTimeBuild (appProj), "DesignTime Application Build should have succeeded.");
				Assert.IsFalse (appProj.CreateBuildOutput (appBuilder).IsTargetSkipped ("_ManagedUpdateAndroidResgen"),
					"Target '_ManagedUpdateAndroidResgen' should have run.");
				var designerFile = Path.Combine (Root, path, appProj.ProjectName, appProj.IntermediateOutputPath, "designtime", "Resource.designer.cs");
				FileAssert.Exists (designerFile, $"'{designerFile}' should have been created.");

				var designerContents = File.ReadAllText (designerFile);
				StringAssert.Contains ("hello", designerContents, $"{designerFile} should contain Resources.Strings.hello");
				StringAssert.Contains ("app_name", designerContents, $"{designerFile} should contain Resources.Strings.app_name");
				StringAssert.Contains ("myButton", designerContents, $"{designerFile} should contain Resources.Id.myButton");
				StringAssert.Contains ("Icon", designerContents, $"{designerFile} should contain Resources.Drawable.Icon");
				StringAssert.Contains ("Main", designerContents, $"{designerFile} should contain Resources.Layout.Main");
				appBuilder.BuildLogFile = "build.log";
				Assert.IsTrue (appBuilder.Build (appProj, doNotCleanupOnUpdate: true),
					"Normal Application Build should have succeeded.");
				Assert.IsTrue (appProj.CreateBuildOutput (appBuilder).IsTargetSkipped ("_ManagedUpdateAndroidResgen"),
					"Target '_ManagedUpdateAndroidResgen' should not have run.");
				appBuilder.BuildLogFile = "designtimebuild.log";
				Assert.IsTrue (appBuilder.DesignTimeBuild (appProj, doNotCleanupOnUpdate: true), "DesignTime Application Build should have succeeded.");
				Assert.IsTrue (appProj.CreateBuildOutput (appBuilder).IsTargetSkipped ("_ManagedUpdateAndroidResgen"),
					"Target '_ManagedUpdateAndroidResgen' should not have run.");

				Assert.IsTrue (appBuilder.Clean (appProj), "Clean should have succeeded");
				Assert.IsTrue (File.Exists (designerFile), $"'{designerFile}' should not have been cleaned.");

			}
		}

		[Test]
		[NonParallelizable]
		public void BuildAppWithManagedResourceParserAndLibraries ()
		{
			int maxBuildTimeMs = 10000;
			var path = Path.Combine ("temp", "BuildAppWithMRPAL");
			var theme = new AndroidItem.AndroidResource ("Resources\\values\\Theme.xml") {
				TextContent = () => @"<?xml version=""1.0"" encoding=""utf-8""?>
<resources>
	<color name=""theme_devicedefault_background"">#ffffffff</color>
	<color name=""SomeColor"">#ffffffff</color>
</resources>",
			};
			var dimen = new AndroidItem.AndroidResource ("Resources\\values\\dimen.xml") {
				TextContent = () => @"<?xml version=""1.0"" encoding=""utf-8""?>
<resources>
	<dimen name=""main_text_item_size"">17dp</dimen>
</resources>",
			};
			var libProj = new XamarinAndroidLibraryProject () {
				IsRelease = true,
				ProjectName = "Lib1",
				AndroidResources = {
					theme,
					dimen,
				},
			};
			libProj.SetProperty ("AndroidUseManagedDesignTimeResourceGenerator", "True");
			libProj.SetProperty ("AndroidUseDesignerAssembly", "false");
			var appProj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
				ProjectName = "App1",
				References = {
					new BuildItem.ProjectReference (@"..\Lib1\Lib1.csproj", libProj.ProjectName, libProj.ProjectGuid),
				},
				PackageReferences = {
					KnownPackages.AndroidXAppCompat,
				},
			};
			appProj.SetProperty ("AndroidUseManagedDesignTimeResourceGenerator", "True");
			appProj.SetProperty ("AndroidUseDesignerAssembly", "false");
			using (var libBuilder = CreateDllBuilder (Path.Combine (path, libProj.ProjectName), false, false)) {
				libBuilder.Verbosity = LoggerVerbosity.Detailed;
				libBuilder.AutomaticNuGetRestore = false;
				Assert.IsTrue (libBuilder.RunTarget (libProj, "Restore"), "Library project should have restored.");
				libBuilder.ThrowOnBuildFailure = false;
				using (var appBuilder = CreateApkBuilder (Path.Combine (path, appProj.ProjectName), false, false)) {
					appBuilder.Verbosity = LoggerVerbosity.Detailed;
					appBuilder.AutomaticNuGetRestore = false;
					Assert.IsTrue (appBuilder.RunTarget (appProj, "Restore"), "App project should have restored.");
					appBuilder.ThrowOnBuildFailure = false;
					Assert.IsTrue (libBuilder.DesignTimeBuild (libProj), "Library project should have built");
					Assert.LessOrEqual (libBuilder.LastBuildTime.TotalMilliseconds, maxBuildTimeMs, $"DesignTime build should be less than {maxBuildTimeMs} milliseconds.");
					Assert.IsFalse (libProj.CreateBuildOutput (libBuilder).IsTargetSkipped ("_ManagedUpdateAndroidResgen"),
						"Target '_ManagedUpdateAndroidResgen' should have run.");
					Assert.IsFalse (appBuilder.DesignTimeBuild (appProj), "Application project should have built");
					Assert.LessOrEqual (appBuilder.LastBuildTime.TotalMilliseconds, maxBuildTimeMs, $"DesignTime build should be less than {maxBuildTimeMs} milliseconds.");
					Assert.IsFalse (appProj.CreateBuildOutput (appBuilder).IsTargetSkipped ("_ManagedUpdateAndroidResgen"),
						"Target '_ManagedUpdateAndroidResgen' should have run.");
					var designerFile = Path.Combine (Root, path, appProj.ProjectName, appProj.IntermediateOutputPath, "designtime", "Resource.designer.cs");
					FileAssert.Exists (designerFile, $"'{designerFile}' should have been created.");

					var designerContents = File.ReadAllText (designerFile);
					StringAssert.Contains ("hello", designerContents, $"{designerFile} should contain Resources.Strings.hello");
					StringAssert.Contains ("app_name", designerContents, $"{designerFile} should contain Resources.Strings.app_name");
					StringAssert.Contains ("myButton", designerContents, $"{designerFile} should contain Resources.Id.myButton");
					StringAssert.Contains ("Icon", designerContents, $"{designerFile} should contain Resources.Drawable.Icon");
					StringAssert.Contains ("Main", designerContents, $"{designerFile} should contain Resources.Layout.Main");
					StringAssert.Contains ("material_grey_50", designerContents, $"{designerFile} should contain Resources.Color.material_grey_50");
					StringAssert.DoesNotContain ("main_text_item_size", designerContents, $"{designerFile} should not contain Resources.Dimension.main_text_item_size");
					StringAssert.DoesNotContain ("theme_devicedefault_background", designerContents, $"{designerFile} should not contain Resources.Color.theme_devicedefault_background");
					Assert.IsTrue (libBuilder.Build (libProj), "Library project should have built");
					Assert.IsTrue (libProj.CreateBuildOutput (libBuilder).IsTargetSkipped ("_ManagedUpdateAndroidResgen"),
						"Target '_ManagedUpdateAndroidResgen' should not have run.");
					Assert.IsTrue (appBuilder.DesignTimeBuild (appProj), "App project should have built");
					Assert.LessOrEqual (appBuilder.LastBuildTime.TotalMilliseconds, maxBuildTimeMs, $"DesignTime build should be less than {maxBuildTimeMs} milliseconds.");
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
					StringAssert.Contains ("main_text_item_size", designerContents, $"{designerFile} should contain Resources.Dimension.main_text_item_size");
					StringAssert.Contains ("theme_devicedefault_background", designerContents, $"{designerFile} should contain Resources.Color.theme_devicedefault_background");
					StringAssert.Contains ("main_text_item_size", designerContents, $"{designerFile} should contain Resources.Dimension.main_text_item_size");
					StringAssert.Contains ("SomeColor", designerContents, $"{designerFile} should contain Resources.Color.SomeColor");
					Assert.IsTrue (appBuilder.Build (appProj), "App project should have built");

					Assert.IsTrue (appBuilder.Clean (appProj), "Clean should have succeeded");
					Assert.IsTrue (File.Exists (designerFile), $"'{designerFile}' should not have been cleaned.");
					designerFile = Path.Combine (Root, path, libProj.ProjectName, libProj.IntermediateOutputPath, "designtime", "Resource.designer.cs");
					Assert.IsTrue (libBuilder.Clean (libProj), "Clean should have succeeded");
					Assert.IsTrue (File.Exists (designerFile), $"'{designerFile}' should not have been cleaned.");
				}
			}
		}

		[Test]
		public void CheckMaxResWarningIsEmittedAsAWarning()
		{
			var path = Path.Combine ("temp", TestName);
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
				OtherBuildItems = {
					new BuildItem.Folder ("Resources\\values-v33\\") {
					},
				},
			};
			proj.AndroidResources.Add (new AndroidItem.AndroidResource ("Resources\\values-v33\\Strings.xml") {
				TextContent = () => @"<?xml version=""1.0"" encoding=""utf-8""?>
<resources>
  <string name=""test"" >Test</string>
</resources>",
			});
			using (var builder = CreateApkBuilder (path)) {
				Assert.IsTrue (builder.Build (proj), "Build should have succeeded.");
				StringAssertEx.DoesNotContain ("APT0000", builder.LastBuildOutput, "Build output should not contain an APT0000 warning");
			}
		}

		[Test]
		public void CheckCodeBehindIsGenerated ()
		{
			var path = Path.Combine ("temp", TestName);
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
				LayoutMain = @"<?xml version=""1.0"" encoding=""utf-8""?>
<LinearLayout xmlns:android = ""http://schemas.android.com/apk/res/android""
	xmlns:tools=""http://schemas.xamarin.com/android/tools""
	android:orientation = ""vertical""
	android:layout_width = ""fill_parent""
	android:layout_height = ""fill_parent""
	tools:class=""UnnamedProject.MainActivity""
	>
	<Button
		android:id = ""@+id/myButton""
		android:layout_width = ""fill_parent""
		android:layout_height = ""wrap_content""
		android:text = ""@string/hello""
	/>
 </LinearLayout>",
				MainActivity = @"using System;
using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;

namespace UnnamedProject
{
	[Activity (Label = ""UnnamedProject"", MainLauncher = true, Icon = ""@drawable/icon"")]
	public partial class MainActivity : Activity {
			int count = 1;

			protected override void OnCreate (Bundle bundle)
			{
				base.OnCreate (bundle);
				SetContentView (Resource.Layout.Main);
				var widgets = new Binding.Main (this);
				widgets.myButton.Click += delegate {
					widgets.myButton.Text = string.Format (""{0} clicks!"", count++);
				};
			}
		}
	}
",
			};
			proj.SetProperty ("AndroidGenerateLayoutBindings", "True");
			using (var builder = CreateApkBuilder (path)) {
				Assert.IsTrue (builder.Build (proj), "Build should have succeeded.");
				FileAssert.Exists (Path.Combine (Root, path, proj.IntermediateOutputPath, "codebehind", "Binding.Main.g.cs"));
				Assert.IsTrue (builder.Build (proj), "Second build should have succeeded.");
				FileAssert.Exists (Path.Combine (Root, path, proj.IntermediateOutputPath, "codebehind", "Binding.Main.g.cs"));
			}
		}

		[Test]
		public void CheckInvalidXmlInManagedResourceParser ()
		{
			var path = Path.Combine ("temp", TestName);
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease       = true,
				LayoutMain      = @"",
			};
			proj.SetProperty ("AndroidUseManagedDesignTimeResourceGenerator", "True");
			using (var builder = CreateApkBuilder (path)) {
				builder.ThrowOnBuildFailure = false;
				builder.Target = "Compile";
				Assert.IsFalse (builder.Build (proj), "Build should have failed.");
				StringAssertEx.Contains ("warning XA1000", builder.LastBuildOutput, "Build output should contain a XA1000 warning.");
			}
		}

		//NOTE: This test was failing randomly before fixing a bug in `CopyIfChanged`.
		//      Let's set it to run 3 times, it still completes in a reasonable time ~1.5 min.
		[Test, Repeat(3)]
		public void LightlyModifyLayout ()
		{
			var proj = new XamarinAndroidApplicationProject ();
			using (var b = CreateApkBuilder (Path.Combine ("temp", TestName))) {
				Assert.IsTrue (b.Build (proj), "first build should have succeeded");

				//Just change something, doesn't matter
				var layout = Path.Combine (Root, b.ProjectDirectory, "Resources", "layout", "Main.axml");
				FileAssert.Exists (layout);
				File.AppendAllText (layout, " ");

				Assert.IsTrue (b.Build (proj), "second build should have succeeded");
				Assert.IsFalse (b.Output.IsTargetSkipped ("_UpdateAndroidResgen"), "`_UpdateAndroidResgen` should not be skipped!");
			}
		}

		[Test]
		public void CustomViewAddResourceId ()
		{
			var proj = new XamarinAndroidApplicationProject ();
			proj.LayoutMain = proj.LayoutMain.Replace ("</LinearLayout>", "<android.support.design.widget.BottomNavigationView android:id=\"@+id/navigation\" /></LinearLayout>");
			proj.PackageReferences.Add (KnownPackages.AndroidXAppCompat);
			using (var b = CreateApkBuilder (Path.Combine ("temp", TestName))) {
				b.Verbosity = LoggerVerbosity.Detailed;
				Assert.IsTrue (b.Build (proj), "first build should have succeeded");

				//Add a new android:id
				var textView1 = "textView1";
				proj.LayoutMain = proj.LayoutMain.Replace ("</LinearLayout>", $"<TextView android:id=\"@+id/{textView1}\" /></LinearLayout>");
				proj.Touch (@"Resources\layout\Main.axml");

				Assert.IsTrue (b.Build (proj, doNotCleanupOnUpdate: true), "second build should have succeeded");
				Assert.IsTrue (
					b.Output.IsTargetPartiallyBuilt ("_CompileResources"),
					"The target _CompileResources should have been partially built");
				Assert.IsTrue (
					b.Output.IsTargetSkipped ("_FixupCustomViewsForAapt2"),
					"The target _FixupCustomViewsForAapt2 should have been skipped");

				var r_java = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath, "android", "src", proj.PackageNameJavaIntermediatePath, "R.java");
				FileAssert.Exists (r_java);
				var r_java_contents = File.ReadAllLines (r_java);
				Assert.IsTrue (StringAssertEx.ContainsText (r_java_contents, textView1), $"{r_java} should contain `{textView1}`!");
			}
		}

		[Test]
		[Parallelizable (ParallelScope.Self)]
		public void CheckNoVersionVectors ()
		{
			var proj = new XamarinFormsAndroidApplicationProject ();
			using (var b = CreateApkBuilder (Path.Combine ("temp", TestName))) {
				b.Verbosity = LoggerVerbosity.Detailed;
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");

				string aaptCommand = "Executing link";
				foreach (var line in b.LastBuildOutput) {
					if (line.Contains (aaptCommand)) {
						StringAssert.Contains ("--no-version-vectors", line, "The Xamarin.Android.Support.Vector.Drawable NuGet should set `--no-version-vectors`!");
						return;
					}
				}

				Assert.Fail ($"aapt log message was not found: {aaptCommand}");
			}
		}

		[Test]
		public void InvalidFilenames ()
		{
			BuildItem CreateItem (string include) =>
				new AndroidItem.AndroidResource (include) {
					TextContent = () => "",
				};

			var proj = new XamarinAndroidApplicationProject ();
			proj.AndroidResources.Add (CreateItem ("Resources\\raw\\.foo"));
			proj.AndroidResources.Add (CreateItem ("Resources\\raw\\.git"));
			proj.AndroidResources.Add (CreateItem ("Resources\\raw\\.svn"));
			proj.AndroidResources.Add (CreateItem ("Resources\\raw\\.DS_Store"));
			using (var b = CreateApkBuilder ()) {
				Assert.IsTrue (b.Build (proj), "first build should have succeeded.");
				Assert.IsTrue (b.Build (proj), "second build should have succeeded.");
				b.Output.AssertTargetIsSkipped ("_CompileResources");
			}
		}

		[Test]
		public void SolutionBuildSeveralProjects ()
		{
			const int libraryCount = 10;
			var path = Path.Combine ("temp", TestName);
			TestOutputDirectories [TestContext.CurrentContext.Test.ID] = Path.Combine (Root, path);
			using (var sb = new SolutionBuilder ($"{TestName}.sln") {
				SolutionPath = Path.Combine (Root, path),
				MaxCpuCount = 4,
				BuildingInsideVisualStudio = false, // allow projects dependencies to build
			}) {
				var apps = new List<XamarinAndroidApplicationProject> ();
				var app1 = new XamarinAndroidApplicationProject {
					ProjectName = "App1"
				};
				apps.Add (app1);
				sb.Projects.Add (app1);

				var app2 = new XamarinAndroidApplicationProject {
					ProjectName = "App2"
				};
				apps.Add (app2);
				sb.Projects.Add (app2);

				for (var i = 0; i < libraryCount; i++) {
					var index = i;
					var lib = new XamarinAndroidLibraryProject {
						ProjectName = $"Lib{i}",
						AndroidResources = {
							new AndroidItem.AndroidResource ($"Resources\\values\\library_name{index}.xml") {
								TextContent = () =>
$@"<?xml version=""1.0"" encoding=""utf-8""?>
<resources>
	<string name=""library_name{index}"">Lib{index}</string>
</resources>"
							}
						}
					};
					foreach (var app in apps) {
						app.AddReference (lib);
					}
					sb.Projects.Add (lib);
				}

				// Add usage of Lib0.Resource.String.library_name0
				var builder = new StringBuilder ();
				for (int i = 0; i < libraryCount; i++) {
					builder.AppendLine ($"int library_name{i} = Lib{i}.Resource.String.library_name{i};");
				}
				foreach (var app in apps) {
					app.Sources.Add (new BuildItem.Source ("Foo.cs") {
						TextContent = () => $"class Foo {{ void Bar () {{ {builder} }} }}",
					});
				}

				Assert.IsTrue (sb.Build (), "Solution should have built.");
			}
		}
	}
}
