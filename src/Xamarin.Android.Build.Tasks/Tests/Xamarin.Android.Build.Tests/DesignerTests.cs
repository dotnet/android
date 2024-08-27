using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using NUnit.Framework;
using Xamarin.ProjectTools;
using Microsoft.Build.Framework;

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
			var proj = new XamarinFormsAndroidApplicationProject () {
				References = { new BuildItem ("ProjectReference", "..\\Library1\\Library1.csproj") },
				Imports = {
				new Import ("foo.targets") {
					TextContent = () => @"<?xml version=""1.0"" encoding=""utf-16""?>
<Project ToolsVersion=""4.0"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
<PropertyGroup>
	<BeforeGenerateAndroidManifest>
		$(BeforeGenerateAndroidManifest);
		_Foo;
	</BeforeGenerateAndroidManifest>
</PropertyGroup>
<Target Name=""_Foo"">
</Target>
</Project>
"
					},
				},
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

			lib.SetProperty ("AndroidUseDesignerAssembly", "False");
			proj.SetProperty ("AndroidUseDesignerAssembly", "False");

			using (var libb = CreateDllBuilder (Path.Combine (path, lib.ProjectName), false, false))
			using (var appb = CreateApkBuilder (Path.Combine (path, proj.ProjectName), false, false)) {
				// Save the library project, but don't build it yet
				libb.Save (lib, saveProject: true);
				FileAssert.Exists (Path.Combine(Root, path, lib.ProjectName, lib.ProjectFilePath));
				appb.BuildLogFile = "build1.log";
				appb.Target = target;
				Assert.IsTrue (appb.Build (proj, parameters: DesignerParameters), $"build should have succeeded for target `{target}`");
				Assert.IsTrue (appb.Output.AreTargetsAllBuilt ("_UpdateAndroidResgen"), "_UpdateAndroidResgen should have run completely.");
				Assert.IsTrue (appb.Output.AreTargetsAllBuilt ("_Foo"), "_Foo should have run completely");
				var customViewPath = Path.Combine (Root, appb.ProjectDirectory, proj.IntermediateOutputPath, "res", "layout", "custom_text.xml");
				Assert.IsTrue (File.Exists (customViewPath), $"custom_text.xml should exist at {customViewPath}");
				var doc = XDocument.Load (customViewPath);
				Assert.IsNotNull (doc.Element ("LinearLayout").Element ("UnnamedProject.CustomTextView"),
					"UnnamedProject.CustomTextView should have not been replaced with a $(Hash).CustomTextView");
				Assert.IsNotNull (doc.Element ("LinearLayout").Element ("unnamedproject.CustomTextView"),
					"unnamedproject.CustomTextView should have not been replaced with a $(Hash).CustomTextView");
				// Build the library project now
				Assert.IsTrue (libb.Build (lib, doNotCleanupOnUpdate: true, saveProject: true), "library build should have succeeded.");
				appb.Target = "Build";
				appb.BuildLogFile = "build2.log";
				Assert.IsTrue (appb.Build (proj, doNotCleanupOnUpdate: true, saveProject: false), "app build should have succeeded.");
				Assert.IsTrue (appb.Output.AreTargetsAllBuilt ("_UpdateAndroidResgen"), "_UpdateAndroidResgen should have run completely.");
				Assert.IsTrue (appb.Output.AreTargetsAllBuilt ("_Foo"), "_Foo should have run completely");
				doc = XDocument.Load (customViewPath);
				Assert.IsNull (doc.Element ("LinearLayout").Element ("UnnamedProject.CustomTextView"),
					"UnnamedProject.CustomTextView should have been replaced with a $(Hash).CustomTextView");
				Assert.IsNull (doc.Element ("LinearLayout").Element ("unnamedproject.CustomTextView"),
					"unnamedproject.CustomTextView should have been replaced with a $(Hash).CustomTextView");
				appb.Target = target;
				appb.BuildLogFile = "build3.log";
				Assert.IsTrue (appb.DesignTimeBuild (proj, parameters: DesignerParameters, doNotCleanupOnUpdate: true), $"build should have succeeded for target `{target}`");
				Assert.IsFalse (appb.Output.AreTargetsAllSkipped ("_UpdateAndroidResgen"), "_UpdateAndroidResgen should not have been skipped.");
				Assert.IsTrue (appb.Output.AreTargetsAllBuilt ("_Foo"), "_Foo should have run completely");
				doc = XDocument.Load (customViewPath);
				Assert.IsNull (doc.Element ("LinearLayout").Element ("UnnamedProject.CustomTextView"),
					"UnnamedProject.CustomTextView should have been replaced with a $(Hash).CustomTextView");
				Assert.IsNull (doc.Element ("LinearLayout").Element ("unnamedproject.CustomTextView"),
					"unnamedproject.CustomTextView should have been replaced with a $(Hash).CustomTextView");
				appb.BuildLogFile = "build4.log";
				Assert.IsTrue (appb.DesignTimeBuild (proj, parameters: DesignerParameters, doNotCleanupOnUpdate: true), $"build should have succeeded for target `{target}`");
				Assert.IsTrue (appb.Output.AreTargetsAllSkipped ("_UpdateAndroidResgen"), "_UpdateAndroidResgen should have been skipped.");
				Assert.IsTrue (appb.Output.AreTargetsAllBuilt ("_Foo"), "_Foo should have run completely");
				doc = XDocument.Load (customViewPath);
				Assert.IsNull (doc.Element ("LinearLayout").Element ("UnnamedProject.CustomTextView"),
					"UnnamedProject.CustomTextView should have been replaced with a $(Hash).CustomTextView");
				Assert.IsNull (doc.Element ("LinearLayout").Element ("unnamedproject.CustomTextView"),
					"unnamedproject.CustomTextView should have been replaced with a $(Hash).CustomTextView");
			}
		}

		[Test]
		public void IncrementalDesignTimeBuild ()
		{
			var proj = new XamarinFormsAndroidApplicationProject ();
			using (var b = CreateApkBuilder (Path.Combine ("temp", TestName))) {
				b.Target = "SetupDependenciesForDesigner";
				Assert.IsTrue (b.Build (proj, parameters: DesignerParameters), $"{b.Target} should have succeeded.");

				// Change a layout, DTB
				proj.LayoutMain = proj.LayoutMain.Replace ("@string/hello", "hello");
				proj.Touch ("Resources\\layout\\Main.axml");
				Assert.IsTrue (b.DesignTimeBuild (proj, target: "UpdateGeneratedFiles"), "DTB should have succeeded.");

				var resourcepathscache = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath, "designtime", "libraryprojectimports.cache");
				FileAssert.Exists (resourcepathscache);
				var doc = XDocument.Load (resourcepathscache);
				Assert.AreEqual (54, doc.Root.Element ("Jars").Elements ("Jar").Count (), "libraryprojectimports.cache did not contain expected jar files");
			}
		}

		[Test]
		public void IncrementalSetupDependenciesForDesigner ()
		{
			var target = "SetupDependenciesForDesigner";
			var proj = new XamarinAndroidApplicationProject ();
			proj.OtherBuildItems.Add (new BuildItem (AndroidBuildActions.AndroidJavaSource, "Foo.java") {
				TextContent = () => "public class Foo { }",
				Encoding = Encoding.ASCII
			});
			using (var b = CreateApkBuilder (Path.Combine ("temp", TestName))) {
				Assert.IsTrue (b.RunTarget (proj, target, parameters: DesignerParameters), $"first `{target}` should have succeeded.");
				Assert.IsTrue (b.RunTarget (proj, target, parameters: DesignerParameters, doNotCleanupOnUpdate: true), $"second `{target}` should have succeeded.");
				Assert.IsTrue (b.Output.IsTargetSkipped (target), $"`{target}` should be skipped!");
				Assert.IsTrue (b.Output.IsTargetSkipped ("_GeneratePackageManagerJavaForDesigner"), "`_GeneratePackageManagerJavaForDesigner` should be skipped!");

				// Change a java file, run SetupDependenciesForDesigner
				proj.Touch ("Foo.java");
				Assert.IsTrue (b.RunTarget (proj, target, parameters: DesignerParameters, doNotCleanupOnUpdate: true), $"third `{target}` should have succeeded.");
				Assert.IsFalse (b.Output.IsTargetSkipped (target), $"`{target}` should *not* be skipped!");
				Assert.IsTrue (b.Output.IsTargetSkipped ("_GeneratePackageManagerJavaForDesigner"), "`_GeneratePackageManagerJavaForDesigner` should be skipped!");
			}
		}

		[Test]
		public void IncrementalFullBuild ()
		{
			var target = "SetupDependenciesForDesigner";
			var proj = new XamarinAndroidApplicationProject ();
			using (var b = CreateApkBuilder (Path.Combine ("temp", TestName))) {
				Assert.IsTrue (b.Build (proj), "first build should have succeeded.");

				// Change a layout, run SetupDependenciesForDesigner
				proj.LayoutMain = proj.LayoutMain.Replace ("@string/hello", "hello");
				proj.Touch ("Resources\\layout\\Main.axml");
				Assert.IsTrue (b.RunTarget (proj, target, parameters: DesignerParameters), $"`{target}` should have succeeded.");

				Assert.IsTrue (b.Build (proj), "second build should have succeeded.");

				var targets = new [] { "_CompileJava", "_CompileToDalvik" };
				foreach (var t in targets) {
					Assert.IsTrue (b.Output.IsTargetSkipped (t), $"`{t}` should be skipped!");
				}
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
			var proj = new XamarinFormsAndroidApplicationProject ();
			string jar = "gson-2.7.jar";
			proj.OtherBuildItems.Add (new BuildItem ("AndroidJavaLibrary", jar) {
				WebContent = $"https://repo1.maven.org/maven2/com/google/code/gson/gson/2.7/{jar}"
			});
			proj.OtherBuildItems.Add (new AndroidItem.AndroidAarLibrary ("android-crop-1.0.1.aar") {
				WebContent = "https://repo1.maven.org/maven2/com/soundcloud/android/android-crop/1.0.1/android-crop-1.0.1.aar"
			});
			// Each NuGet package and AAR file are in libraryprojectimports.cache, AndroidJavaSource is not
			const int libraryProjectImportsJars = 55;
			using (var b = CreateApkBuilder (Path.Combine ("temp", TestName), false, false)) {
				b.Verbosity = LoggerVerbosity.Detailed;
				// GetExtraLibraryLocationsForDesigner on new project
				Assert.IsTrue (b.RunTarget (proj, target, parameters: DesignerParameters), $"build should have succeeded for target `{target}` 1");

				// GetExtraLibraryLocationsForDesigner after DTB
				Assert.IsTrue (b.DesignTimeBuild (proj), "design-time build should have succeeded");
				var resourcepathscache = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath, "designtime", "libraryprojectimports.cache");
				FileAssert.Exists (resourcepathscache);
				var expected = XDocument.Load (resourcepathscache);
				Assert.AreEqual (libraryProjectImportsJars, expected.Root.Element ("Jars").Elements ("Jar").Count ());
				Assert.IsTrue (b.RunTarget (proj, target, parameters: DesignerParameters), $"build should have succeeded for target `{target}` 2");
				AssertJarInBuildOutput (b, "ExtraJarLocation", jar);

				// GetExtraLibraryLocationsForDesigner after SetupDependenciesForDesigner
				var setup = "SetupDependenciesForDesigner";
				Assert.IsTrue (b.RunTarget (proj, setup, parameters: DesignerParameters),  $"build should have succeeded for target `{setup}`");
				Assert.IsTrue (b.RunTarget (proj, target, parameters: DesignerParameters), $"build should have succeeded for target `{target}` 3");
				AssertJarInBuildOutput (b, "ExtraJarLocation", jar);
				FileAssert.Exists (resourcepathscache);
				var actual = XDocument.Load (resourcepathscache);
				Assert.AreEqual (libraryProjectImportsJars, actual.Root.Element ("Jars").Elements ("Jar").Count ());
				Assert.AreEqual (expected.ToString (), actual.ToString (), "libraryprojectimports.cache should not change!");

				// GetExtraLibraryLocationsForDesigner after Build
				Assert.IsTrue (b.Build (proj), "build should have succeeded");
				Assert.IsTrue (b.RunTarget (proj, target, parameters: DesignerParameters), $"build should have succeeded for target `{target}` 4");
				AssertJarInBuildOutput (b, "ExtraJarLocation", jar);
				FileAssert.Exists (resourcepathscache);
				actual = XDocument.Load (resourcepathscache);
				Assert.AreEqual (libraryProjectImportsJars, actual.Root.Element ("Jars").Elements ("Jar").Count ());
				Assert.AreEqual (expected.ToString (), actual.ToString (), "libraryprojectimports.cache should not change!");
			}
		}

		void AssertJarInBuildOutput (ProjectBuilder builder, string itemGroup, string jar)
		{
			bool added = false, inItemGroup = false;
			foreach (var line in builder.LastBuildOutput) {
				if (line.Contains ("Added Item(s)")) {
					added = true;
				} else if (added && line.Contains (itemGroup)) {
					inItemGroup = true;
				} else if (added && inItemGroup) {
					var jarPath = line.Trim ();
					if (jarPath.EndsWith (".jar", StringComparison.OrdinalIgnoreCase) && Path.GetFileName (jarPath) == jar) {
						Assert.IsTrue (Path.IsPathRooted (jarPath), $"{jarPath} should be a full path!");
						return;
					}
				} else {
					added = false;
					inItemGroup = false;
				}
			}
			Assert.Fail ($"Did not find {jar} in {itemGroup}!");
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
						found = line.Contains ("public static String[] Assemblies = new String[]{");
					}
				}
			}
			return builder.ToString ().Trim ();
		}
	}
}
