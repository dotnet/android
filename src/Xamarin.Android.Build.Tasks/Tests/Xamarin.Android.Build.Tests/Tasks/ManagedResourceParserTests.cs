using System;
using System.Collections.Generic;
using NUnit.Framework;
using Xamarin.ProjectTools;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using System.Text;
using Xamarin.Android.Tasks;
using Microsoft.Build.Utilities;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Xamarin.Android.Build.Tests {
	[TestFixture]
	[Parallelizable (ParallelScope.Children)]
	public class ManagedResourceParserTests : BaseTest {
		const string ValuesXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<resources>
	<bool name=""a_bool"">false</bool>
	<color name=""a_color"">#FFFFFFFF</color>
	<integer name=""an_integer"">0</integer>
	<integer-array name=""int_array"">
		<item>0</item>
		<item>1</item>
	</integer-array>
	<array name=""array_of_colors"">
		<item>#FFFF0000</item>
		<item>#FF00FF00</item>
		<item>#FF0000FF</item>
	</array>
</resources>
";

		const string StringsXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<resources xmlns:ns1=""http://schemas.android.com/tools"" xmlns:ns2=""urn:oasis:names:tc:xliff:document:1.2"">
  <string name=""hello"">Hello World, Click Me!</string>
  <string name=""app_name"">App1</string>
  <plurals name=""num_locations_reported"">
    <item quantity=""zero"">No location reported</item>
    <item quantity=""one""> One location reported</item>
    <item quantity=""other"">%d locations reported</item>
  </plurals>
  <string name=""translatable_text"" translatable=""false""><ns2:g example=""999"" id=""maximum number"">%1$d</ns2:g><ns2:g example=""+"" id=""suffix"">%2$s</ns2:g></string>
</resources>
";

		const string StringsXml2 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<resources>
  <string name=""foo"">Hello World, Click Me!</string>
  <string-array name=""widths_array"">
    <item>Thin</item>
    <item>Thinish</item>
    <item>Medium</item>
    <item>Thickish</item>
    <item>Thick</item>
  </string-array>
  <string name=""menu_settings"">Android Beam settings</string>
  <string name=""fixed""></string>
</resources>
";
		const string Menu = @"<menu xmlns:android=""http://schemas.android.com/apk/res/android"">
<item android:id=""@+id/menu_settings""
   android:icon=""@drawable/ic_menu_preferences""
   android:showAsAction=""ifRoom""
   android:title=""@string/menu_settings"" />
</menu>";

		const string Animator = @"<?xml version=""1.0"" encoding=""utf-8""?>
<objectAnimator xmlns:android=""http://schemas.android.com/apk/res/android""
    android:duration=""600""
    android:interpolator=""@android:interpolator/fast_out_linear_in""
    android:propertyName=""y""
    android:valueFrom=""5000""
    android:valueTo=""0""
    android:valueType=""floatType"" />";

		const string Animation = @"<?xml version=""1.0"" encoding=""utf-8""?>
<set xmlns:android=""http://schemas.android.com/apk/res/android"" android:shareInterpolator=""false"">
	<alpha android:fromAlpha=""0.0"" android:toAlpha=""1.0"" />
</set>
";

		const string Array = @"<?xml version=""1.0"" encoding=""utf-8""?>
<resources>
	<array name=""alphabet"">
		<item>A</item>
		<item>Z</item>
	</array>
</resources>
";

		const string Dimen = @"<?xml version=""1.0"" encoding=""utf-8""?>
<resources>
	<dimen name=""main_text_item_size"">17dp</dimen>
</resources>";

		const string Styleable = @"<?xml version=""1.0"" encoding=""utf-8""?>
<resources>
  <declare-styleable name=""CustomFonts"">
    <attr name=""android:scrollX"" />
    <attr name=""customFont"" />
    <attr name=""customFont"" />
    <attr name=""customFont1"" />
  </declare-styleable>
  <declare-styleable name=""MultiSelectListPreference"">
    <attr name=""entries"">value</attr>
    <attr name=""android:entries""/>
    <attr name=""entryValues"">value</attr>
    <attr name=""android:entryValues""/>
  </declare-styleable>
  <attr name=""customFont"" format=""enum"">
        <enum name=""regular"" value=""0""/>
  </attr>
  <attr name=""customFont1"" format=""enum"">
        <enum name=""regular"" value=""0""/>
  </attr>
  <attr name=""entries"" format=""enum"">
        <enum name=""entry_1"" value=""0""/>
  </attr>
  <attr name=""entryValues"" format=""enum"">
        <enum name=""entry_1_value"" value=""0""/>
  </attr>
</resources>";

		const string Styleablev21 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<resources>
  <declare-styleable name=""MultiSelectListPreference"">
    <attr name=""entries"">value</attr>
    <attr name=""android:entries""/>
    <attr name=""entryValues"">value</attr>
    <attr name=""android:entryValues""/>
  </declare-styleable>
</resources>";

		const string Selector = @"<?xml version=""1.0"" encoding=""utf-8""?>
<selector xmlns:android=""http://schemas.android.com/apk/res/android"">
	<item android:state_pressed=""true"" android:color=""#ffff0000""/>
	<item android:state_focused=""true"" android:color=""#ff0000ff""/>
	<item android:color=""#ff000000""/>
</selector>
";

		const string Transition = @"<?xml version=""1.0"" encoding=""utf-8""?>
<changeBounds
  xmlns:android=""http://schemas.android.com/apk/res/android""
  android:duration=""5000""
  android:interpolator=""@android:anim/overshoot_interpolator"" />
";
		const string Main = @"<?xml version=""1.0"" encoding=""utf-8""?>
<LinearLayout xmlns:android=""http://schemas.android.com/apk/res/android"">
	<TextView android:id=""@+id/seekBar"" />
	<TextView android:id=""@+id/seekbar"" />
	<TextView android:id=""@+id/textview.withperiod"" />
	<TextView android:id=""@+id/Føø-Bar"" />
</LinearLayout>
";

		const string CustomId = @"<?xml version=""1.0"" encoding=""utf-8""?>
<LinearLayout xmlns:android=""http://schemas.android.com/apk/res/android"">
	<TextView android:id=""@+MyCustomID/HelloWorldTextView""/>
	<TextView android:id=""@+ACustomID/foo1""/>
</LinearLayout>
";

		const string CustomInterpolator = @"<?xml version=""1.0"" encoding=""utf-8""?>
<accelerateInterpolator
  xmlns:android=""http://schemas.android.com/apk/res/android""
  android:factor=""2"" />
";
		const string Xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<MyXml>
</MyXml>
";

		const string AndroidManifest = @"<?xml version='1.0'?>
<manifest xmlns:android=""http://schemas.android.com/apk/res/android"" package=""MonoAndroidApplication4.MonoAndroidApplication4"" />";

		/// <summary>
		/// When you add new resources to the code blocks above, the following
		/// Rtxt string needs to be updated with the correct values.
		/// You can do this manually OR use aapt2
		///
		/// Run the GenerateDesignerFileFromRtxt test and let it fail.
		/// You can then go to the appropriate resource directory
		/// and run the following commands.
		///
		/// aapt2 compile -o compile.flata --dir res/
		/// aapt2 compile -o lp.flata --dir lp/res/
		/// aapt2 link -o foo.apk --manifest AndroidManifest.xml -R lp.flata -R compile.flata --auto-add-overlay --output-text-symbols R.txt -I ~/android-toolchain-xa/sdk/platforms/android-Q/android.jar
		///
		/// Then copy the values from the R.txt file and place them below.
		/// </summary>
		/// int ACustomID foo1 0x7f160000
		/// int MyCustomID HelloWorldTextView 0x7f150000
		const string Rtxt = @"int anim custominterpolator 0x7f010000
int anim some_animation 0x7f010001
int animator slide_in_bottom 0x7f020000
int array alphabet 0x7f030000
int array array_of_colors 0x7f030001
int array int_array 0x7f030002
int array widths_array 0x7f030003
int attr customFont 0x7f040000
int attr customFont1 0x7f040001
int attr entries 0x7f040002
int attr entryValues 0x7f040003
int bool a_bool 0x7f050000
int color a_color 0x7f060000
int color selector1 0x7f060001
int dimen main_text_item_size 0x7f070000
int drawable ic_menu_preferences 0x7f080000
int font arial 0x7f090000
int id Føø_Bar 0x7f0a0000
int id entry_1 0x7f0a0001
int id entry_1_value 0x7f0a0002
int id menu_settings 0x7f0a0003
int id regular 0x7f0a0004
int id seekBar 0x7f0a0005
int id seekbar 0x7f0a0006
int id textview_withperiod 0x7f0a0007
int integer an_integer 0x7f0b0000
int layout main 0x7f0c0000
int menu options 0x7f0d0000
int mipmap icon 0x7f0e0000
int plurals num_locations_reported 0x7f0f0000
int raw afoo 0x7f100000
int raw foo 0x7f100001
int string app_name 0x7f110000
int string fixed 0x7f110001
int string foo 0x7f110002
int string hello 0x7f110003
int string menu_settings 0x7f110004
int string translatable_text 0x7f110005
int[] styleable CustomFonts { 0x010100d2, 0x7f040000, 0x7f040000, 0x7f040001 }
int styleable CustomFonts_android_scrollX 0
int styleable CustomFonts_customFont 1
int styleable CustomFonts_customFont 2
int styleable CustomFonts_customFont1 3
int[] styleable MultiSelectListPreference { 0x010100b2, 0x010101f8, 0x7f040002, 0x7f040003 }
int styleable MultiSelectListPreference_android_entries 0
int styleable MultiSelectListPreference_android_entryValues 1
int styleable MultiSelectListPreference_entries 2
int styleable MultiSelectListPreference_entryValues 3
int transition transition 0x7f130000
int xml myxml 0x7f140000
";

		public string AndroidSdkDirectory { get; set; } = AndroidSdkResolver.GetAndroidSdkPath ();

		public void CreateResourceDirectory (string path)
		{
			Directory.CreateDirectory (Path.Combine (Root, path));
			Directory.CreateDirectory (Path.Combine (Root, path, "res", "animator"));
			Directory.CreateDirectory (Path.Combine (Root, path, "res", "anim"));
			Directory.CreateDirectory (Path.Combine (Root, path, "res", "color"));
			Directory.CreateDirectory (Path.Combine (Root, path, "res", "drawable"));
			Directory.CreateDirectory (Path.Combine (Root, path, "res", "mipmap"));
			Directory.CreateDirectory (Path.Combine (Root, path, "res", "menu"));
			Directory.CreateDirectory (Path.Combine (Root, path, "res", "font"));
			Directory.CreateDirectory (Path.Combine (Root, path, "res", "layout"));
			Directory.CreateDirectory (Path.Combine (Root, path, "res", "values"));
			Directory.CreateDirectory (Path.Combine (Root, path, "res", "values-v21"));
			Directory.CreateDirectory (Path.Combine (Root, path, "res", "transition"));
			Directory.CreateDirectory (Path.Combine (Root, path, "res", "raw"));
			Directory.CreateDirectory (Path.Combine (Root, path, "res", "xml"));

			File.WriteAllText (Path.Combine (Root, path, "AndroidManifest.xml"), AndroidManifest);

			File.WriteAllText (Path.Combine (Root, path, "res", "color", "selector1.xml"), Selector);
			File.WriteAllText (Path.Combine (Root, path, "res", "anim", "custominterpolator.xml"), CustomInterpolator);

			File.WriteAllText (Path.Combine (Root, path, "res", "values", "arrays.xml"), Array);
			File.WriteAllText (Path.Combine (Root, path, "res", "values", "values.xml"), ValuesXml);
			File.WriteAllText (Path.Combine (Root, path, "res", "values", "strings.xml"), StringsXml);
			File.WriteAllText (Path.Combine (Root, path, "res", "values", "attrs.xml"), Styleable);
			File.WriteAllText (Path.Combine (Root, path, "res", "values-v21", "attrs.xml"), Styleablev21);
			File.WriteAllText (Path.Combine (Root, path, "res", "transition", "transition.xml"), Transition);
			File.WriteAllText (Path.Combine (Root, path, "res", "raw", "afoo.txt"), "AFoo");
			File.WriteAllText (Path.Combine (Root, path, "res", "raw", "foo.txt"), "Foo");
			File.WriteAllText (Path.Combine (Root, path, "res", "layout", "main.xml"), Main);
			File.WriteAllText (Path.Combine (Root, path, "res", "xml", "myxml.xml"), Xml);

			Directory.CreateDirectory (Path.Combine (Root, path, "lp", "res", "animator"));
			Directory.CreateDirectory (Path.Combine (Root, path, "lp", "res", "anim"));
			Directory.CreateDirectory (Path.Combine (Root, path, "lp", "res", "font"));
			Directory.CreateDirectory (Path.Combine (Root, path, "lp", "res", "values"));
			Directory.CreateDirectory (Path.Combine (Root, path, "lp", "res", "drawable"));
			Directory.CreateDirectory (Path.Combine (Root, path, "lp", "res", "menu"));
			Directory.CreateDirectory (Path.Combine (Root, path, "lp", "res", "mipmap-hdpi"));
			File.WriteAllText (Path.Combine (Root, path, "lp", "res", "animator", "slide_in_bottom.xml"), Animator);
			File.WriteAllText (Path.Combine (Root, path, "lp", "res", "anim", "some_animation.xml"), Animation);
			File.WriteAllText (Path.Combine (Root, path, "lp", "res", "font", "arial.ttf"), "");
			File.WriteAllText (Path.Combine (Root, path, "lp", "res", "values", "strings.xml"), StringsXml2);
			File.WriteAllText (Path.Combine (Root, path, "lp", "res", "values", "dimen.xml"), Dimen);
			var icon_binary_mdpi = XamarinAndroidCommonProject.GetResourceContents ("Xamarin.ProjectTools.Resources.Base.Icon.png");
			File.WriteAllBytes (Path.Combine (Root, path, "lp", "res", "drawable", "ic_menu_preferences.png"), icon_binary_mdpi);
			File.WriteAllBytes (Path.Combine (Root, path, "lp", "res", "mipmap-hdpi", "icon.png"), icon_binary_mdpi);
			File.WriteAllText (Path.Combine (Root, path, "lp", "res", "menu", "options.xml"), Menu);
			File.WriteAllText (Path.Combine (Root, path, "lp", "__res_name_case_map.txt"), "menu/Options.xml;menu/options.xml");
		}

		void BuildLibraryWithResources (string path)
		{
			var library = new XamarinAndroidLibraryProject () {
				ProjectName = "Library",
			};

			var libraryStrings = library.AndroidResources.FirstOrDefault (r => r.Include () == @"Resources\values\Strings.xml");

			library.SetProperty ("AndroidUseDesignerAssembly", "false");
			library.AndroidResources.Clear ();
			library.AndroidResources.Add (libraryStrings);
			library.AndroidResources.Add (new AndroidItem.AndroidResource (Path.Combine ("Resources", "animator", "slide_in_bottom.xml")) { TextContent = () => Animator });
			library.AndroidResources.Add (new AndroidItem.AndroidResource (Path.Combine ("Resources", "font", "arial.ttf")) { TextContent = () => "" });
			library.AndroidResources.Add (new AndroidItem.AndroidResource (Path.Combine ("Resources", "values", "strings2.xml")) { TextContent = () => StringsXml2 });
			library.AndroidResources.Add (new AndroidItem.AndroidResource (Path.Combine ("Resources", "values", "dimen.xml")) { TextContent = () => Dimen });

			var icon_binary_mdpi = XamarinAndroidCommonProject.GetResourceContents ("Xamarin.ProjectTools.Resources.Base.Icon.png");
			library.AndroidResources.Add (new AndroidItem.AndroidResource (Path.Combine ("Resources", "drawable", "ic_menu_preferences.png")) { BinaryContent = () => icon_binary_mdpi });
			library.AndroidResources.Add (new AndroidItem.AndroidResource (Path.Combine ("Resources", "mipmap-hdpi", "icon.png")) { BinaryContent = () => icon_binary_mdpi });
			library.AndroidResources.Add (new AndroidItem.AndroidResource (Path.Combine ("Resources", "menu", "options.xml")) { TextContent = () => Menu });

			using (ProjectBuilder builder = CreateDllBuilder (Path.Combine (Root, path))) {
				Assert.IsTrue (builder.Build (library), "Build should have succeeded");
			}
		}

		void CompareFilesIgnoreRuntimeInfoString (string file1, string file2)
		{
			FileAssert.Exists (file1);
			FileAssert.Exists (file2);
			var content1 = File.ReadAllText (file1);
			var content2 = File.ReadAllText (file2);
			// This string is only generated when running on mono, replace with a new line that will be stripped when comparing.
			var runtimeVersionRegex = new Regex (@"//\s*Runtime Version:.*");
			content1 = runtimeVersionRegex.Replace (content1, Environment.NewLine);
			content2 = runtimeVersionRegex.Replace (content2, Environment.NewLine);
			var replaceText = "\t[global::System.CodeDom.Compiler.GeneratedCodeAttribute(\"Xamarin.Android.Build.Tasks\", \"1.0.0.0\")]";
			var assemblyVersionRegex = new Regex (@"(.*)GeneratedCodeAttribute(.{2})Xamarin.Android.Build.Tasks(.*)");
			content1 = assemblyVersionRegex.Replace (content1, replaceText);
			content2 = assemblyVersionRegex.Replace (content2, replaceText);

			using (var s1 = new MemoryStream (Encoding.UTF8.GetBytes (content1)))
			using (var s2 = new MemoryStream (Encoding.UTF8.GetBytes (content2))) {
				if (!StreamCompare (s1, s2)) {
					TestContext.AddTestAttachment (file1, Path.GetFileName (file1));
					TestContext.AddTestAttachment (file2, Path.GetFileName (file2));
					Assert.Fail ($"{file1} and {file2} do not match.");
				}
			}
		}

		GenerateResourceCaseMap CreateCaseMapTask (string path)
		{
			var task = new GenerateResourceCaseMap () {
				BuildEngine = new MockBuildEngine (TestContext.Out)
			};
			task.ProjectDir = Path.Combine (Root, path);
			task.ResourceDirectory = Path.Combine (Root, path, "res") + Path.DirectorySeparatorChar;
			task.Resources = new TaskItem [] {
				new TaskItem (Path.Combine (Root, path, "res", "values", "strings.xml"), new Dictionary<string, string> () {
					{ "LogicalName", "values\\strings.xml" },
				}),
			};
			task.AdditionalResourceDirectories = new TaskItem [] {
				new TaskItem (Path.Combine (Root, path, "lp", "res")),
			};
			task.OutputFile = new TaskItem (Path.Combine (Root, path, "case_map.txt"));
			return task;
		}

		GenerateResourceDesigner CreateTask (string path)
		{
			var task = new GenerateResourceDesigner {
				BuildEngine = new MockBuildEngine (TestContext.Out)
			};
			task.UseManagedResourceGenerator = true;
			task.DesignTimeBuild = true;
			task.Namespace = "Foo.Foo";
			task.NetResgenOutputFile = Path.Combine (Root, path, "Resource.designer.cs");
			task.DesignTimeOutputFile = Path.Combine (Root, path, "designtime", "Resource.designer.cs");
			task.ProjectDir = Path.Combine (Root, path);
			task.ResourceDirectory = Path.Combine (Root, path, "res") + Path.DirectorySeparatorChar;
			task.Resources = new TaskItem [] {
				new TaskItem (Path.Combine (Root, path, "res", "values", "strings.xml"), new Dictionary<string, string> () {
					{ "LogicalName", "values\\strings.xml" },
				}),
			};
			task.AdditionalResourceDirectories = new TaskItem [] {
				new TaskItem (Path.Combine (Root, path, "lp", "res")),
			};
			task.CaseMapFile = Path.Combine (Root, path, "case_map.txt");
			task.IsApplication = true;
			int platform = AndroidSdkResolver.GetMaxInstalledPlatform ();
			task.JavaPlatformJarPath = Path.Combine (AndroidSdkDirectory, "platforms", $"android-{platform}", "android.jar");
			return task;
		}

		static string ExpectedOutputDir = Path.Combine (XABuildPaths.TestAssemblyOutputDirectory, "Expected");

		void AssertResourceDesigner (GenerateResourceDesigner task, string expectedFile)
		{
			var expected = Path.Combine (ExpectedOutputDir, expectedFile);

			FileAssert.Exists (task.NetResgenOutputFile);
			CompareFilesIgnoreRuntimeInfoString (task.NetResgenOutputFile, expected);

			if (!string.IsNullOrEmpty (task.DesignTimeOutputFile)) {
				FileAssert.Exists (task.DesignTimeOutputFile);
				CompareFilesIgnoreRuntimeInfoString (task.DesignTimeOutputFile, expected);
			}
		}

		[Test]
		public void GenerateDesignerFileWithÜmläüts ()
		{
			var path = Path.Combine ("temp", TestName + " Some Space");
			CreateResourceDirectory (path);
			var mapTask = CreateCaseMapTask (path);
			Assert.IsTrue (mapTask.Execute (), "Map Task should have executed successfully.");
			var task = CreateTask (path);
			Assert.IsTrue (task.Execute (), "Task should have executed successfully.");
			AssertResourceDesigner (task, "GenerateDesignerFileExpected.cs");
			Directory.Delete (Path.Combine (Root, path), recursive: true);
		}

		[Test]
		public void GenerateDesignerFileFromRtxt ([Values (false, true)] bool withLibraryReference)
		{
			var path = Path.Combine ("temp", TestName + " Some Space");
			CreateResourceDirectory (path);
			var mapTask = CreateCaseMapTask (path);
			Assert.IsTrue (mapTask.Execute (), "Map Task should have executed successfully.");
			var task = CreateTask (path);
			task.RTxtFile = Path.Combine (Root, path, "R.txt");
			File.WriteAllText (task.RTxtFile, Rtxt);
			if (withLibraryReference) {
				var libraryPath = Path.Combine (path, "Library");
				BuildLibraryWithResources (libraryPath);
				task.References = new TaskItem [] {
					new TaskItem (Path.Combine (Root, libraryPath, "bin", "Debug", "Library.dll"))
				};
			}
			Assert.IsTrue (task.Execute (), "Task should have executed successfully.");
			AssertResourceDesigner (task, withLibraryReference ? "GenerateDesignerFileWithLibraryReferenceExpected.cs" : "GenerateDesignerFileExpected.cs");
			Directory.Delete (Path.Combine (Root, path), recursive: true);
		}

		[Test]
		public void GenerateDesignerFileFromEmptyRtxt ()
		{
			var path = Path.Combine ("temp", TestName + " Some Space");
			CreateResourceDirectory (path);
			var task = CreateTask (path);
			task.RTxtFile = Path.Combine (Root, path, "R.txt");
			File.WriteAllText (task.RTxtFile, Environment.NewLine);
			Assert.IsTrue (task.Execute (), "Task should have executed successfully.");
			Assert.IsTrue (File.Exists (task.NetResgenOutputFile), $"{task.NetResgenOutputFile} should have been created.");
			Directory.Delete (Path.Combine (Root, path), recursive: true);
		}

		[Test]
		public void UpdateLayoutIdIsIncludedInDesigner ([Values(true, false)] bool useRtxt)
		{
			var path = Path.Combine ("temp", TestName + " Some Space");
			CreateResourceDirectory (path);
			var mapTask = CreateCaseMapTask (path);
			Assert.IsTrue (mapTask.Execute (), "Map Task should have executed successfully.");
			if (useRtxt)
			    File.WriteAllText (Path.Combine (Root, path, "R.txt"), Rtxt);
			IBuildEngine engine = new MockBuildEngine (TestContext.Out);
			var task = new GenerateResourceDesigner {
				BuildEngine = engine
			};
			task.UseManagedResourceGenerator = true;
			task.DesignTimeBuild = true;
			task.Namespace = "Foo.Foo";
			task.NetResgenOutputFile = Path.Combine (Root, path, "Resource.designer.cs");
			task.ProjectDir = Path.Combine (Root, path);
			task.ResourceDirectory = Path.Combine (Root, path, "res") + Path.DirectorySeparatorChar;
			task.Resources = new TaskItem [] {
				new TaskItem (Path.Combine (Root, path, "res", "values", "strings.xml"), new Dictionary<string, string> () {
					{ "LogicalName", "values\\strings.xml" },
				}),
			};
			task.AdditionalResourceDirectories = new TaskItem [] {
				new TaskItem (Path.Combine (Root, path, "lp", "res")),
			};
			task.ResourceFlagFile = Path.Combine (Root, path, "AndroidResgen.flag");
			task.CaseMapFile = Path.Combine (Root, path, "case_map.txt");
			File.WriteAllText (task.ResourceFlagFile, string.Empty);
			task.IsApplication = true;
			int platform = AndroidSdkResolver.GetMaxInstalledPlatform ();
			task.JavaPlatformJarPath = Path.Combine (AndroidSdkDirectory, "platforms", $"android-{platform}", "android.jar");
			Assert.IsTrue (task.Execute (), "Task should have executed successfully.");
			Assert.IsTrue (File.Exists (task.NetResgenOutputFile), $"{task.NetResgenOutputFile} should have been created.");
			var expected = Path.Combine (ExpectedOutputDir, "GenerateDesignerFileExpected.cs");
			CompareFilesIgnoreRuntimeInfoString (task.NetResgenOutputFile, expected);
			// Update the id, and force the managed parser to re-parse the output
			File.WriteAllText (Path.Combine (Root, path, "res", "layout", "main.xml"), Main.Replace ("@+id/textview.withperiod", "@+id/textview.withperiod2"));
			File.SetLastWriteTimeUtc (task.ResourceFlagFile, DateTime.UtcNow);
			Assert.IsTrue (task.Execute (), "Task should have executed successfully.");
			Assert.IsTrue (File.Exists (task.NetResgenOutputFile), $"{task.NetResgenOutputFile} should have been created.");
			var data = File.ReadAllText (expected);
			var expectedWithNewId = Path.Combine (Root, path, "GenerateDesignerFileExpectedWithNewId.cs");
			File.WriteAllText (expectedWithNewId, data.Replace ("withperiod", "withperiod2"));
			CompareFilesIgnoreRuntimeInfoString (task.NetResgenOutputFile, expectedWithNewId);
			Directory.Delete (Path.Combine (Root, path), recursive: true);
		}

		[Test]
		public void RtxtGeneratorOutput ()
		{
			var path = Path.Combine ("temp", TestName);
			int platform = AndroidSdkResolver.GetMaxInstalledPlatform ();
			string resPath = Path.Combine (Root, path, "res");
			string rTxt = Path.Combine (Root, path, "R.txt");
			string expectedrTxt = Path.Combine (Root, path, "expectedR.txt");
			CreateResourceDirectory (path);
			File.WriteAllText (expectedrTxt, Rtxt);
			List<BuildErrorEventArgs> errors = new List<BuildErrorEventArgs> ();
			List<BuildMessageEventArgs> messages = new List<BuildMessageEventArgs> ();
			IBuildEngine engine = new MockBuildEngine (TestContext.Out, errors: errors, messages: messages);
			var generateRtxt = new GenerateRtxt () {
				BuildEngine = engine,
				RTxtFile = rTxt,
				ResourceDirectory = resPath,
				CaseMapFile = Path.Combine (Root, path, "case_map.txt"),
				JavaPlatformJarPath = Path.Combine (AndroidSdkDirectory, "platforms", $"android-{platform}", "android.jar"),
				ResourceFlagFile = Path.Combine (Root, path, "res.flag"),
				AdditionalResourceDirectories = new string[] {
					Path.Combine (Root, path, "lp", "res"),
				},
			};
			Assert.IsTrue (generateRtxt.Execute (), "Task should have succeeded.");
			FileAssert.Exists (rTxt, $"{rTxt} should have been created.");

			CompareFilesIgnoreRuntimeInfoString (rTxt, expectedrTxt);

			Directory.Delete (Path.Combine (Root, path), recursive: true);
		}

		[Test]
		public void CompareAapt2AndManagedParserOutput ()
		{
			var path = Path.Combine ("temp", TestName);
			CreateResourceDirectory (path);
			File.WriteAllText (Path.Combine (Root, path, "foo.map"), @"a\nb");
			Directory.CreateDirectory (Path.Combine (Root, path, "java"));
			List<BuildErrorEventArgs> errors = new List<BuildErrorEventArgs> ();
			List<BuildMessageEventArgs> messages = new List<BuildMessageEventArgs> ();
			IBuildEngine engine = new MockBuildEngine (TestContext.Out, errors: errors, messages: messages);
			var aapt2Compile = new Aapt2Compile {
				BuildEngine = engine,
				ToolPath = GetPathToAapt2 (),
				ResourceDirectories = new ITaskItem [] {
					new TaskItem(Path.Combine (Root, path, "lp", "res"), new Dictionary<string, string> {
						{ "Hash", "lp" }
					}),
					new TaskItem(Path.Combine (Root, path, "res"), new Dictionary<string, string> {
						{ "Hash", "compiled" }
					}),
				},
				FlatArchivesDirectory = Path.Combine (Root, path),
				FlatFilesDirectory = Path.Combine (Root, path),
			};

			Assert.IsTrue (aapt2Compile.Execute (), $"Aapt2 Compile should have succeeded. {string.Join (" ", errors.Select (x => x.Message))}");
			int platform = AndroidSdkResolver.GetMaxInstalledPlatform ();
			string resPath = Path.Combine (Root, path, "res");
			string rTxt = Path.Combine (Root, path, "R.txt");
			var aapt2Link = new Aapt2Link {
				BuildEngine = engine,
				ToolPath = GetPathToAapt2 (),
				ResourceDirectories = new ITaskItem [] { new TaskItem (resPath) },
				ManifestFiles = new ITaskItem [] { new TaskItem (Path.Combine (Root, path, "AndroidManifest.xml")) },
				AdditionalResourceArchives = new ITaskItem [] { new TaskItem (Path.Combine (Root, path, "lp.flata")) },
				CompiledResourceFlatArchive = new TaskItem (Path.Combine (Root, path, "compiled.flata")),
				OutputFile = Path.Combine (Root, path, "foo.apk"),
				AssemblyIdentityMapFile = Path.Combine (Root, path, "foo.map"),
				JavaPlatformJarPath = Path.Combine (AndroidSdkDirectory, "platforms", $"android-{platform}", "android.jar"),
				JavaDesignerOutputDirectory = Path.Combine (Root, path, "java"),
				ResourceSymbolsTextFile = rTxt,

			};
			Assert.IsTrue (aapt2Link.Execute (), "Aapt2 Link should have succeeded.");

			FileAssert.Exists (rTxt, $"{rTxt} should have been created.");

			var task = new GenerateResourceDesigner {
				BuildEngine = engine
			};
			task.UseManagedResourceGenerator = true;
			task.DesignTimeBuild = false;
			task.Namespace = "MonoAndroidApplication4.MonoAndroidApplication4";
			task.NetResgenOutputFile = Path.Combine (Root, path, "Resource.designer.aapt2.cs");
			task.ProjectDir = Path.Combine (Root, path);
			task.CaseMapFile = Path.Combine (Root, path, "case_map.txt");
			task.ResourceDirectory = Path.Combine (Root, path, "res") + Path.DirectorySeparatorChar;
			task.Resources = new TaskItem [] {
				new TaskItem (Path.Combine (Root, path, "res", "values", "strings.xml"), new Dictionary<string, string> () {
					{ "LogicalName", "values\\strings.xml" },
				}),
			};
			task.AdditionalResourceDirectories = new TaskItem [] {
				new TaskItem (Path.Combine (Root, path, "lp", "res")),
			};
			task.ResourceFlagFile = Path.Combine (Root, path, "AndroidResgen.flag");
			task.IsApplication = true;
			task.JavaPlatformJarPath = aapt2Link.JavaPlatformJarPath;
			Assert.IsTrue (task.Execute (), "Task should have executed successfully.");

			File.WriteAllText (task.ResourceFlagFile, string.Empty);
			File.Delete (Path.Combine (Root, path, "R.txt.bak"));
			File.Move (rTxt, Path.Combine (Root, path, "R.txt.bak"));

			task.UseManagedResourceGenerator = true;
			task.DesignTimeBuild = true;
			task.NetResgenOutputFile = Path.Combine (Root, path, "Resource.designer.managed.cs");
			Assert.IsTrue (task.Execute (), "Task should have executed successfully.");
			string aapt2Designer = Path.Combine (Root, path, "Resource.designer.aapt2.cs");
			string managedDesigner = Path.Combine (Root, path, "Resource.designer.managed.cs");
			CompareFilesIgnoreRuntimeInfoString (managedDesigner, aapt2Designer);
			Directory.Delete (Path.Combine (Root, path), recursive: true);
		}

		[Test]
		[NonParallelizable] // Test measures performance
		public void CheckPerformanceOfManagedParser ()
		{
			var path = Path.Combine ("temp", TestName);
			CreateResourceDirectory (path);
			IBuildEngine engine = new MockBuildEngine (TestContext.Out);
			TaskLoggingHelper loggingHelper = new TaskLoggingHelper (engine, nameof (ManagedResourceParser));
			string resPath = Path.Combine (Root, path, "res");
			int platform = AndroidSdkResolver.GetMaxInstalledPlatform ();
			var flagFile = Path.Combine (Root, path, "AndroidResgen.flag");
			var lp = new string [] { Path.Combine (Root, path, "lp", "res") };
			Stopwatch sw = new Stopwatch ();
			long totalMS = 0;
			int i;
			for (i = 0; i < 100; i++) {
				var parser = new ManagedResourceParser () {
					Log = loggingHelper,
					JavaPlatformDirectory = Path.Combine (AndroidSdkDirectory, "platforms", $"android-{platform}"),
					ResourceFlagFile =  flagFile,
				};
				sw.Start ();
				var codeDom = parser.Parse (resPath, "R.txt", lp, isApp: true, resourceMap: new Dictionary<string, string> ());
				sw.Stop ();
				TestContext.Out.WriteLine ($"Pass {i} took {sw.ElapsedMilliseconds} ms");
				totalMS += sw.ElapsedMilliseconds;
				sw.Reset ();
				Assert.AreEqual (20, codeDom.Members.Count, "Expected 20 Classes to be generated");
			}
			TestContext.Out.WriteLine ($"Average {totalMS / i} ms");
			Assert.LessOrEqual (totalMS / i, 160, "Parser should have taken on average less than 160 ms.");
		}

		[Test]
		public void GenerateDesignerFileWithElevenStyleableAttributesFromRtxt ()
		{
			var styleable = @"<resources>
  <declare-styleable name = ""ElevenAttributes"">
    <attr name = ""attr00"" format=""string"" />
    <attr name = ""attr01"" format=""string"" />
    <attr name = ""attr02"" format=""string"" />
    <attr name = ""attr03"" format=""string"" />
    <attr name = ""attr04"" format=""string"" />
    <attr name = ""attr05"" format=""string"" />
    <attr name = ""attr06"" format=""string"" />
    <attr name = ""attr07"" format=""string"" />
    <attr name = ""attr08"" format=""string"" />
    <attr name = ""attr09"" format=""string"" />
    <attr name = ""attr10"" format=""string"" />
  </declare-styleable>
</resources>";
			var rtxt = @"int attr attr00 0x7f010000
int attr attr01 0x7f010001
int attr attr02 0x7f010002
int attr attr03 0x7f010003
int attr attr04 0x7f010004
int attr attr05 0x7f010005
int attr attr06 0x7f010006
int attr attr07 0x7f010007
int attr attr08 0x7f010008
int attr attr09 0x7f010009
int attr attr10 0x7f01000a
int[] styleable ElevenAttributes { 0x7f010000, 0x7f010001, 0x7f010002, 0x7f010003, 0x7f010004, 0x7f010005, 0x7f010006, 0x7f010007, 0x7f010008, 0x7f010009, 0x7f01000a }
int styleable ElevenAttributes_attr00 0
int styleable ElevenAttributes_attr01 1
int styleable ElevenAttributes_attr02 2
int styleable ElevenAttributes_attr03 3
int styleable ElevenAttributes_attr04 4
int styleable ElevenAttributes_attr05 5
int styleable ElevenAttributes_attr06 6
int styleable ElevenAttributes_attr07 7
int styleable ElevenAttributes_attr08 8
int styleable ElevenAttributes_attr09 9
int styleable ElevenAttributes_attr10 10";

			var path = Path.Combine ("temp", TestName);
			Directory.CreateDirectory (Path.Combine (Root, path));
			File.WriteAllText (Path.Combine (Root, path, "AndroidManifest.xml"), AndroidManifest);
			Directory.CreateDirectory (Path.Combine (Root, path, "res"));
			Directory.CreateDirectory (Path.Combine (Root, path, "res", "values"));
			File.WriteAllText (Path.Combine (Root, path, "res", "values", "attrs.xml"), styleable);
			File.WriteAllText (Path.Combine (Root, path, "R.txt"), rtxt);
			IBuildEngine engine = new MockBuildEngine (TestContext.Out);
			var task = new GenerateResourceDesigner {
				BuildEngine = engine
			};
			task.UseManagedResourceGenerator = true;
			task.DesignTimeBuild = true;
			task.Namespace = "Foo.Foo";
			task.NetResgenOutputFile = Path.Combine (Root, path, "Resource.designer.cs");
			task.ProjectDir = Path.Combine (Root, path);
			task.CaseMapFile = Path.Combine (Root, path, "case_map.txt");
			task.ResourceDirectory = Path.Combine (Root, path, "res");
			task.Resources = new TaskItem [] {};
			task.IsApplication = true;
			int platform = AndroidSdkResolver.GetMaxInstalledPlatform ();
			task.JavaPlatformJarPath = Path.Combine (AndroidSdkDirectory, "platforms", $"android-{platform}", "android.jar");
			Assert.IsTrue (task.Execute (), "Task should have executed successfully.");
			Assert.IsTrue (File.Exists (task.NetResgenOutputFile), $"{task.NetResgenOutputFile} should have been created.");
			var expected = Path.Combine (ExpectedOutputDir, "GenerateDesignerFileWithElevenStyleableAttributesExpected.cs");
			CompareFilesIgnoreRuntimeInfoString (task.NetResgenOutputFile, expected);
			Directory.Delete (Path.Combine (Root, path), recursive: true);
		}
	}
}
