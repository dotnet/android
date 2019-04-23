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

namespace Xamarin.Android.Build.Tests {
	[TestFixture]
	[Parallelizable (ParallelScope.Children)]
	public class ManagedResourceParserTests : BaseTest {

		const string StringsXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<resources>
  <string name=""hello"">Hello World, Click Me!</string>
  <string name=""app_name"">App1</string>
  <plurals name=""num_locations_reported"">
    <item quantity=""zero"">No location reported</item>
    <item quantity=""one""> One location reported</item>
    <item quantity=""other"">%d locations reported</item>
  </plurals>
  <string name=""menu_settings"">Android Beam settings</string>
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

		const string Dimen = @"<?xml version=""1.0"" encoding=""utf-8""?>
<resources>
	<dimen name=""main_text_item_size"">17dp</dimen>
</resources>";

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
</LinearLayout>
";

		[Test]
		public void GenerateDesignerFileWithÜmläüts ()
		{
			var path = Path.Combine ("temp", TestName + " Some Space");
			Directory.CreateDirectory (Path.Combine (Root, path, "res", "values"));
			Directory.CreateDirectory (Path.Combine (Root, path, "res", "transition"));

			Directory.CreateDirectory (Path.Combine (Root, path, "res", "raw"));
			Directory.CreateDirectory (Path.Combine (Root, path, "res", "layout"));

			File.WriteAllText (Path.Combine (Root, path, "res", "values", "strings.xml"), StringsXml);
			File.WriteAllText (Path.Combine (Root, path, "res", "transition", "transition.xml"), Transition);
			File.WriteAllText (Path.Combine (Root, path, "res", "raw", "foo.txt"), "Foo");
			File.WriteAllText (Path.Combine (Root, path, "res", "layout", "main.xml"), Main);

			Directory.CreateDirectory (Path.Combine (Root, path, "lp", "res", "animator"));
			Directory.CreateDirectory (Path.Combine (Root, path, "lp", "res", "font"));
			Directory.CreateDirectory (Path.Combine (Root, path, "lp", "res", "values"));
			Directory.CreateDirectory (Path.Combine (Root, path, "lp", "res", "drawable"));
			Directory.CreateDirectory (Path.Combine (Root, path, "lp", "res", "menu"));
			Directory.CreateDirectory (Path.Combine (Root, path, "lp", "res", "mipmap-hdpi"));
			File.WriteAllText (Path.Combine (Root, path, "lp", "res", "animator", "slide_in_bottom.xml"), Animator);
			File.WriteAllText (Path.Combine (Root, path, "lp", "res", "font", "arial.ttf"), "");
			File.WriteAllText (Path.Combine (Root, path, "lp", "res", "values", "strings.xml"), StringsXml2);
			File.WriteAllText (Path.Combine (Root, path, "lp", "res", "values", "dimen.xml"), Dimen);
			using (var stream = typeof (XamarinAndroidCommonProject).Assembly.GetManifestResourceStream ("Xamarin.ProjectTools.Resources.Base.Icon.png")) {
				var icon_binary_mdpi = new byte [stream.Length];
				stream.Read (icon_binary_mdpi, 0, (int)stream.Length);
				File.WriteAllBytes (Path.Combine (Root, path, "lp", "res", "drawable", "ic_menu_preferences.png"), icon_binary_mdpi);
				File.WriteAllBytes (Path.Combine (Root, path, "lp", "res", "mipmap-hdpi", "icon.png"), icon_binary_mdpi);
			}
			File.WriteAllText (Path.Combine (Root, path, "lp", "res", "menu", "Options.xml"), Menu);
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
			task.IsApplication = true;
			Assert.IsTrue (task.Execute (), "Task should have executed successfully.");
			Assert.IsTrue (File.Exists (task.NetResgenOutputFile), $"{task.NetResgenOutputFile} should have been created.");
			var expected = Path.Combine (Root, "Expected", "GenerateDesignerFileExpected.cs");
			Assert.IsTrue (FileCompare (task.NetResgenOutputFile, expected), 
			 	$"{task.NetResgenOutputFile} and {expected} do not match.");
			Directory.Delete (Path.Combine (Root, path), recursive: true);
		}
	}
}
