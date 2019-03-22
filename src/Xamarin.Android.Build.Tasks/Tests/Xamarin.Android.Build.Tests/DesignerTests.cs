using NUnit.Framework;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	public class DesignerTests : BaseTest
	{
		static readonly string [] DesignerParameters = new [] { "DesignTimeBuild=True", "AndroidUseManagedDesignTimeResourceGenerator=False" };

		/// <summary>
		/// NOTE: we shouldn't break these targets, or they will break the Xamarin.Android designer
		/// https://github.com/xamarin/designer/blob/603b9e322cc63b1fc00382ff11fc3ffae38976a6/Xamarin.Designer.Android/Xamarin.AndroidDesigner/Xamarin.AndroidDesigner/MSBuildConstants.cs#L21-L22
		/// https://github.com/xamarin/designer/blob/f37c09a0599a26421da41921961a63de38d1ab6e/Xamarin.Designer.Android/Xamarin.AndroidDesigner/Xamarin.AndroidDesigner/DesignerProject.cs#L1564
		/// </summary>
		[Test]
		public void CustomDesignerTargetSetupDependenciesForDesigner ()
		{
			var target = "SetupDependenciesForDesigner";
			var path = Path.Combine ("temp", "SetupDependenciesForDesigner");
			var lib = new XamarinAndroidLibraryProject () {
				ProjectName = "Library1",
				OtherBuildItems = {
					new AndroidItem.AndroidAsset ("Assets\\foo.txt") {
						TextContent =  () => "bar",
					},
				},
			};
			var proj = new XamarinAndroidApplicationProject () {
				Packages = {
					KnownPackages.SupportMediaCompat_25_4_0_1,
					KnownPackages.SupportFragment_25_4_0_1,
					KnownPackages.SupportCoreUtils_25_4_0_1,
					KnownPackages.SupportCoreUI_25_4_0_1,
					KnownPackages.SupportCompat_25_4_0_1,
					KnownPackages.AndroidSupportV4_25_4_0_1,
					KnownPackages.SupportV7AppCompat_25_4_0_1,
				},
				References = { new BuildItem ("ProjectReference", "..\\Library1\\Library1.csproj") },
			};
			proj.Sources.Add (new BuildItem.Source ("CustomTextView.cs") {
				TextContent = () => @"using Android.Widget;
using Android.Content;
using Android.Util;
namespace UnnamedProject
{
	public class CustomTextView : TextView
	{
		public CustomTextView(Context context, IAttributeSet attributes) : base(context, attributes)
		{
		}
	}
}"
			});
			proj.AndroidResources.Add (new AndroidItem.AndroidResource ("Resources\\layout\\custom_text.xml") {
				TextContent = () => @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<LinearLayout xmlns:android=""http://schemas.android.com/apk/res/android""
	android:orientation = ""vertical""
	android:layout_width = ""fill_parent""
	android:layout_height = ""fill_parent"">
	<UnnamedProject.CustomTextView
		android:id = ""@+id/myText1""
		android:layout_width = ""fill_parent""
		android:layout_height = ""wrap_content""
		android:text = ""namespace_lower"" />
	<unnamedproject.CustomTextView
		android:id = ""@+id/myText2""
		android:layout_width = ""fill_parent""
		android:layout_height = ""wrap_content""
		android:text = ""namespace_proper"" />
</LinearLayout>"
			});

			using (var libb = CreateDllBuilder (Path.Combine (path, lib.ProjectName), false, false))
			using (var appb = CreateApkBuilder (Path.Combine (path, proj.ProjectName), false, false)) {
				// Save the library project, but don't build it yet
				libb.Save (lib);
				appb.Target = target;
				Assert.IsTrue (appb.Build (proj, parameters: DesignerParameters), $"build should have succeeded for target `{target}`");
				Assert.IsTrue (appb.Output.AreTargetsAllBuilt ("_UpdateAndroidResgen"), "_UpdateAndroidResgen should have run completely.");
				var customViewPath = Path.Combine (Root, appb.ProjectDirectory, proj.IntermediateOutputPath, "res", "layout", "custom_text.xml");
				Assert.IsTrue (File.Exists (customViewPath), $"custom_text.xml should exist at {customViewPath}");
				var doc = XDocument.Load (customViewPath);
				Assert.IsNotNull (doc.Element ("LinearLayout").Element ("UnnamedProject.CustomTextView"),
					"UnnamedProject.CustomTextView should have not been replaced with an $(MD5Hash).CustomTextView");
				Assert.IsNotNull (doc.Element ("LinearLayout").Element ("unnamedproject.CustomTextView"),
					"unnamedproject.CustomTextView should have not been replaced with an $(MD5Hash).CustomTextView");
				// Build the library project now
				Assert.IsTrue (libb.Build (lib), "library build should have succeeded.");
				appb.Target = "Build";
				Assert.IsTrue (appb.Build (proj), "app build should have succeeded.");
				Assert.IsTrue (appb.Output.AreTargetsAllBuilt ("_UpdateAndroidResgen"), "_UpdateAndroidResgen should have run completely.");
				doc = XDocument.Load (customViewPath);
				Assert.IsNull (doc.Element ("LinearLayout").Element ("UnnamedProject.CustomTextView"),
					"UnnamedProject.CustomTextView should have been replaced with an $(MD5Hash).CustomTextView");
				Assert.IsNull (doc.Element ("LinearLayout").Element ("unnamedproject.CustomTextView"),
					"unnamedproject.CustomTextView should have been replaced with an $(MD5Hash).CustomTextView");
				appb.Target = target;
				Assert.IsTrue (appb.Build (proj, parameters: DesignerParameters), $"build should have succeeded for target `{target}`");
				Assert.IsTrue (appb.Output.AreTargetsAllSkipped ("_UpdateAndroidResgen"), "_UpdateAndroidResgen should have been skipped.");
				doc = XDocument.Load (customViewPath);
				Assert.IsNull (doc.Element ("LinearLayout").Element ("UnnamedProject.CustomTextView"),
					"UnnamedProject.CustomTextView should have been replaced with an $(MD5Hash).CustomTextView");
				Assert.IsNull (doc.Element ("LinearLayout").Element ("unnamedproject.CustomTextView"),
					"unnamedproject.CustomTextView should have been replaced with an $(MD5Hash).CustomTextView");
			}
		}

		/// <summary>
		/// This target should work in three cases:
		/// * Called on a clean project
		/// * Called after a design-time build
		/// * Called after SetupDependenciesForDesigner
		/// * Called after a full Build
		/// </summary>
		[Test]
		public void GetExtraLibraryLocationsForDesigner ()
		{
			var target = "GetExtraLibraryLocationsForDesigner";
			var proj = new XamarinAndroidApplicationProject () {
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
			using (var b = CreateApkBuilder (Path.Combine ("temp", TestName), false, false)) {
				// GetExtraLibraryLocationsForDesigner on new project
				Assert.IsTrue (b.RunTarget (proj, target, parameters: DesignerParameters), $"build should have succeeded for target `{target}` 1");

				// GetExtraLibraryLocationsForDesigner after DTB
				Assert.IsTrue (b.DesignTimeBuild (proj), "design-time build should have succeeded");
				Assert.IsFalse (b.Output.IsTargetSkipped ("_BuildAdditionalResourcesCache"), "_BuildAdditionalResourcesCache should not be skipped!");
				var resourcepathscache = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath, "designtime", "libraryprojectimports.cache");
				FileAssert.Exists (resourcepathscache);
				var expected = File.ReadAllText (resourcepathscache);
				StringAssert.DoesNotContain ("<Jars/>", expected);
				Assert.IsTrue (b.RunTarget (proj, target, parameters: DesignerParameters), $"build should have succeeded for target `{target}` 2");

				// GetExtraLibraryLocationsForDesigner after SetupDependenciesForDesigner
				var setup = "SetupDependenciesForDesigner";
				Assert.IsTrue (b.RunTarget (proj, setup, parameters: DesignerParameters),  $"build should have succeeded for target `{setup}`");
				Assert.IsFalse (b.Output.IsTargetSkipped ("_BuildAdditionalResourcesCache"), "_BuildAdditionalResourcesCache should not be skipped!");
				Assert.IsTrue (b.RunTarget (proj, target, parameters: DesignerParameters), $"build should have succeeded for target `{target}` 3");
				FileAssert.Exists (resourcepathscache);
				var actual = File.ReadAllText (resourcepathscache);
				StringAssert.DoesNotContain ("<Jars/>", actual);
				Assert.AreEqual (expected, actual, "libraryprojectimports.cache should not change!");

				// GetExtraLibraryLocationsForDesigner after Build
				Assert.IsTrue (b.Build (proj), "build should have succeeded");
				Assert.IsFalse (b.Output.IsTargetSkipped ("_BuildAdditionalResourcesCache"), "_BuildAdditionalResourcesCache should not be skipped!");
				Assert.IsTrue (b.RunTarget (proj, target, parameters: DesignerParameters), $"build should have succeeded for target `{target}` 4");
				FileAssert.Exists (resourcepathscache);
				actual = File.ReadAllText (resourcepathscache);
				StringAssert.DoesNotContain ("<Jars/>", actual);
				Assert.AreEqual (expected, actual, "libraryprojectimports.cache should not change!");
			}
		}

		[Test]
		public void DesignerBeforeNuGetRestore ([Values (true, false)] bool restoreInSingleCall)
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

			var dir = Path.Combine (Root, path);
			if (Directory.Exists (dir))
				Directory.Delete (dir, recursive: true);

			using (var libb = CreateDllBuilder (Path.Combine (path, lib.ProjectName), false, false))
			using (var appb = CreateApkBuilder (Path.Combine (path, proj.ProjectName), false, false)) {
				libb.AutomaticNuGetRestore =
					appb.AutomaticNuGetRestore = false;
				// Save the library project, but don't build it yet
				libb.Save (lib);
				appb.Target = "SetupDependenciesForDesigner";
				Assert.IsTrue (appb.Build (proj, parameters: DesignerParameters), "first build should have succeeded");

				var packageManagerPath = Path.Combine (Root, appb.ProjectDirectory, proj.IntermediateOutputPath, "android", "src", "mono", "MonoPackageManager_Resources.java");
				var before = GetAssembliesFromPackageManager (packageManagerPath);
				Assert.AreEqual ("", before, $"After first `{appb.Target}`, assemblies list would be empty.");

				// NuGet restore, either with /t:Restore in a separate MSBuild call or /restore in a single call
				if (restoreInSingleCall) {
					libb.AutomaticNuGetRestore =
						appb.AutomaticNuGetRestore = true;
				} else {
					Assert.IsTrue (libb.RunTarget (proj, "Restore", parameters: DesignerParameters), "lib nuget restore should have succeeded");
					Assert.IsTrue (appb.RunTarget (proj, "Restore", parameters: DesignerParameters), "app nuget restore should have succeeded");
				}
				Assert.IsTrue (appb.Build (proj, parameters: DesignerParameters), "second build should have succeeded");

				var after = GetAssembliesFromPackageManager (packageManagerPath);
				Assert.AreNotEqual (before, after, $"After second `{appb.Target}`, assemblies list should *not* be empty.");
				foreach (var assembly in new [] { "Xamarin.Forms.Core.dll", "Xamarin.Forms.Platform.Android.dll" }) {
					StringAssert.Contains (assembly, after);
				}
			}
		}

		string GetAssembliesFromPackageManager (string packageManagerPath)
		{
			FileAssert.Exists (packageManagerPath);

			var builder = new StringBuilder ();
			using (var reader = File.OpenText (packageManagerPath)) {
				bool found = false;
				while (!reader.EndOfStream) {
					var line = reader.ReadLine ();
					if (found) {
						if (line.Contains ("};")) {
							break;
						} else if (line.Contains ("/*")) {
							continue;
						} else {
							builder.AppendLine (line.Trim ());
						}
					} else {
						found = line.Contains ("public static final String[] Assemblies = new String[]{");
					}
				}
			}
			return builder.ToString ().Trim ();
		}
	}
}
