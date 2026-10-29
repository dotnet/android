using System;
using System.Collections.Generic;
using NUnit.Framework;
using Xamarin.ProjectTools;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Xamarin.Android.Tasks;
using Microsoft.Build.Utilities;
using System.Reflection.PortableExecutable;
using Mono.Cecil;

namespace Xamarin.Android.Build.Tests {
	[TestFixture]
	[Parallelizable (ParallelScope.Self)]
	public class AndroidResourceTests : BaseTest
	{
		[TestCase (true, true)]
		[TestCase (true, false)]
		[TestCase (false, true)]
		[TestCase (false, false)]
		public void ResourceDesignerAssemblyDeterministic (bool deterministic, bool isApplication)
		{
			var path = Path.Combine (Root, "temp", TestName);
			Directory.CreateDirectory (path);
			var rtxt = Path.Combine (path, "R.txt");
			File.WriteAllText (rtxt, """
				int string app_name 0x7f010001
				int[] styleable TestView { 0x7f010001, 0x7f010002 }
				int styleable TestView_text 0
				""");
			var output = Path.Combine (path, "_Microsoft.Android.Resource.Designer.dll");
			var start = DateTimeOffset.UtcNow.ToUnixTimeSeconds ();

			void Generate ()
			{
				var task = new GenerateResourceDesignerAssembly {
					BuildEngine = new MockBuildEngine (TestContext.Out),
					RTxtFile = new TaskItem (rtxt),
					OutputFile = new TaskItem (output),
					CaseMapFile = Path.Combine (path, "case_map.txt"),
					ProjectDir = path,
					ResourceDirectory = Path.Combine (path, "res"),
					FrameworkDirectories = [new TaskItem (Path.GetDirectoryName (typeof (object).Assembly.Location))],
					Deterministic = deterministic,
					IsApplication = isApplication,
					AssemblyName = "TestApp",
				};
				Assert.IsTrue (task.Execute (), "Designer generation should succeed.");
				using var stream = File.OpenRead (output);
				using var reader = new PEReader (stream);
				var timestamp = reader.PEHeaders.CoffHeader.TimeDateStamp;
				if (deterministic)
					Assert.AreEqual (0, timestamp, "Deterministic output must have a fixed PE timestamp.");
				else
					Assert.That ((long) timestamp, Is.InRange (start, DateTimeOffset.UtcNow.ToUnixTimeSeconds ()), "Non-deterministic output must retain the current PE timestamp.");
			}

			Generate ();
			var first = File.ReadAllBytes (output);
			Guid firstMvid;
			using (var firstAssembly = AssemblyDefinition.ReadAssembly (output))
				firstMvid = firstAssembly.MainModule.Mvid;
			File.Delete (output);
			Generate ();
			var second = File.ReadAllBytes (output);
			using var secondAssembly = AssemblyDefinition.ReadAssembly (output);
			if (deterministic) {
				CollectionAssert.AreEqual (first, second, "Clean generations with identical inputs must be byte-identical.");
				Assert.AreEqual (firstMvid, secondAssembly.MainModule.Mvid);
			} else {
				Assert.AreNotEqual (firstMvid, secondAssembly.MainModule.Mvid);
			}
		}

		[Test]
		public void RTxtParserTest ()
		{
			var path = Path.Combine (Root, "temp", TestName);
			var rtxt = Path.Combine (path, "R.txt");
			Directory.CreateDirectory (path);
			File.WriteAllText (rtxt, ResourceData.RTxt);
			IBuildEngine4 engine = new MockBuildEngine (System.Console.Out);
			var log = new TaskLoggingHelper (engine, TestName);
			var parser = new RtxtParser ();
			var resources = parser.Parse (rtxt, log, new Dictionary<string, string> ());
			var lines = File.ReadAllLines (rtxt);
			Assert.AreEqual (lines.Count () - 1, resources.Count ());
		}

		[Test]
		public void HeaderLayout ()
		{
			var path = Path.Combine (Root, "temp", TestName);
			Directory.CreateDirectory (path);
			var layoutDir = Path.Combine (path, "res", "layout");
			var menuDir = Path.Combine (path, "res", "menu");
			Directory.CreateDirectory (layoutDir);
			Directory.CreateDirectory (menuDir);
			var main = Path.Combine (layoutDir, "main.xml");
			File.WriteAllText (main, @"<?xml version=""1.0"" encoding=""utf-8""?>
<LinearLayout xmlns:android=""http://schemas.android.com/apk/res/android""
	xmlns:app=""http://schemas.android.com/apk/res-auto""
	android:orientation = ""horizontal""
	android:layout_width = ""match_parent""
	android:layout_height = ""match_parent""
	app:headerLayout=""@layout/headerLayout""
	>
</LinearLayout>");

			var headerLayout = Path.Combine (layoutDir, "headerlayout.xml");
			File.WriteAllText (headerLayout, @"<?xml version=""1.0"" encoding=""utf-8""?>
<LinearLayout>
</LinearLayout>
");
			Monodroid.AndroidResource.UpdateXmlResource (Path.Combine (path, "res"), main, null);
			var mainText = File.ReadAllText (main);
			Assert.True (mainText.Contains ("@layout/headerlayout"), "'@layout/headerLayout' was not converted to '@layout/headerlayout'");
			Directory.Delete (path, recursive: true);
		}

		[Test]
		public void MenuActionLayout ()
		{
			var path = Path.Combine (Root, "temp", "MenuActionLayout");
			Directory.CreateDirectory (path);
			var layoutDir = Path.Combine (path, "res", "layout");
			var menuDir = Path.Combine (path, "res", "menu");
			Directory.CreateDirectory (layoutDir);
			Directory.CreateDirectory (menuDir);
			File.WriteAllText (Path.Combine (layoutDir, "servinglayout.xml"), @"<?xml version=""1.0"" encoding=""utf-8""?>
<LinearLayout xmlns:android=""http://schemas.android.com/apk/res/android""
	android:orientation = ""horizontal""
	android:layout_width = ""match_parent""
	android:layout_height = ""match_parent"" >");

			var actions = Path.Combine (menuDir, "actions.xml");
			File.WriteAllText (actions, @"<?xml version=""1.0"" encoding=""utf-8""?>
<menu
	xmlns:android = ""http://schemas.android.com/apk/res/android""
	xmlns:app = ""http://schemas.android.com/apk/res-auto"">
	<item
		android:id = ""@+id/addToFavorites""
		android:title = ""Add to Favorites""
		app:showAsAction = ""always""
	/>
	<item
		android:title = ""Servings""
		android:icon = ""@drawable/ic_people_white_24dp""
		app:showAsAction = ""always"">
		<menu>
			<group android:checkableBehavior = ""single"">

			<item
				android:id = ""@+id/oneServing""
				android:title = ""1 serving""
				android:checked= ""true""
				app:actionLayout = ""@layout/ServingLayout""
			/>
			<item
				android:id = ""@+id/twoServings""
				android:title = ""2 servings"" />
			<item
				android:id = ""@+id/fourServings""
				android:title = ""4 servings"" />
			</group>
		</menu>
	</item>
	<item
		android:id = ""@+id/about""
		android:title = ""About""
		app:showAsAction = ""never"" />

</menu>");
			Monodroid.AndroidResource.UpdateXmlResource (Path.Combine (path, "res"), actions, null);
			var actionsText = File.ReadAllText (actions);
			Assert.True (actionsText.Contains ("@layout/servinglayout"), "'@layout/ServingLayout' was not converted to '@layout/servinglayout'");
			Directory.Delete (path, recursive: true);
		}

		[Test]
		public void UserLayout ()
		{
			var path = Path.Combine (Root, "temp", TestName);
			Directory.CreateDirectory (path);
			var layoutDir = Path.Combine (path, "res", "layout");
			var valuesDir = Path.Combine (path, "res", "values");
			Directory.CreateDirectory (layoutDir);
			Directory.CreateDirectory (valuesDir);
			var main = Path.Combine (layoutDir, "main.xml");
			File.WriteAllText (main, @"<?xml version=""1.0"" encoding=""utf-8""?>
<LinearLayout xmlns:android=""http://schemas.android.com/apk/res/android""
	xmlns:app=""http://schemas.android.com/apk/res-auto""
	android:orientation = ""horizontal""
	android:layout_width = ""match_parent""
	android:layout_height = ""match_parent""
	app:UserLayout=""FixedWidth""
	>
</LinearLayout>");

			var attrs = Path.Combine (valuesDir, "attrs.xml");
			File.WriteAllText (attrs, @"<?xml version=""1.0"" encoding=""utf-8""?>
<resources>
	<declare-styleable name=""UserLayoutAttributes"">
		<attr name=""UserLayout"">
			<enum name=""FixedWidth"" value=""0""/>
		</attr>
	</declare-styleable>
</resources>
");
			Monodroid.AndroidResource.UpdateXmlResource (Path.Combine (path, "res"), attrs, null);
			Monodroid.AndroidResource.UpdateXmlResource (Path.Combine (path, "res"), main, null);
			var mainText = File.ReadAllText (main);
			Assert.True (mainText.Contains ("FixedWidth"), "'FixedWidth' was converted to 'fixedwidth'");
			Directory.Delete (path, recursive: true);
		}

		[Test]
		public void AdaptiveIcon ([Values (AndroidRuntime.CoreCLR, AndroidRuntime.NativeAOT)] AndroidRuntime runtime)
		{
			bool isRelease = runtime == AndroidRuntime.NativeAOT;
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}

			var proj = new XamarinAndroidApplicationProject {
				IsRelease = isRelease,
				SupportedOSPlatformVersion = "26",
				AndroidResources = {
					new AndroidItem.AndroidResource ("Resources\\values\\colors.xml") {
						TextContent = () => """
							<resources>
								<color name="adaptive_icon_background">#2C3E50</color>
								<color name="adaptive_icon_foreground">#FFFFFF</color>
							</resources>
						""",
					},
					new AndroidItem.AndroidResource ("Resources\\drawable\\ic_shortcut_add.xml") {
						TextContent = () => """
							<adaptive-icon xmlns:android="http://schemas.android.com/apk/res/android">
								<background android:drawable="@color/adaptive_icon_background"/>
								<foreground android:drawable="@color/adaptive_icon_foreground"/>
							</adaptive-icon>
						""",
					},
					new AndroidItem.AndroidResource ("Resources\\mipmap-anydpi-v26\\adaptiveicon.xml") {
						TextContent = () => """
							<adaptive-icon xmlns:android="http://schemas.android.com/apk/res/android">
								<background android:drawable="@mipmap/AdaptiveIcon_background" />
								<foreground android:drawable="@mipmap/AdaptiveIcon_foreground" />
							</adaptive-icon>
						""",
					},
					new AndroidItem.AndroidResource ("Resources\\mipmap-mdpi\\AdaptiveIcon_background.png") {
						BinaryContent = () => XamarinAndroidCommonProject.icon_binary_mdpi,
					},
					new AndroidItem.AndroidResource ("Resources\\mipmap-mdpi\\AdaptiveIcon_foreground.png") {
						BinaryContent = () => XamarinAndroidCommonProject.icon_binary_mdpi,
					},
				}
			};
			proj.SetRuntime (runtime);

			using var b = CreateApkBuilder ();
			Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
		}
	}
}
