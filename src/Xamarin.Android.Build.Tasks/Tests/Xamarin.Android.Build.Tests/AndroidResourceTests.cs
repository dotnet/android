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
using System.Xml;
using System.Xml.Linq;

namespace Xamarin.Android.Build.Tests {
	[TestFixture]
	[Parallelizable (ParallelScope.Self)]
	public class AndroidResourceTests : BaseTest {
		[Test]
		public void CheckNamespaceMangle ()
		{
			var manifest = new XElement (XName.Get ("manifest"));
			manifest.SetAttributeValue (XNamespace.Xmlns + "android", "http://schemas.android.com/apk/res/android");
			var app = new XElement ("application");
			manifest.Add (app);
			var m = new global::Android.App.IntentFilterAttribute (new string [] { "foo" }) {
				DataSchemes = new string[] { "http", "https" },
			};
			var result = m.ToElement ("UnnamedProject.UnnamedProject");
			app.Add (result);
			var xml = manifest.ToFullString ();
		}

		[Test]
		public void LowercaseResources ()
		{
			var path = Path.Combine (Root, "temp", "LowercaseResources");
			Directory.CreateDirectory (path);
			var layoutDir = Path.Combine (path, "res", "layout");
			var drawableDir = Path.Combine (path, "res", "drawable");
			Directory.CreateDirectory (layoutDir);
			Directory.CreateDirectory (drawableDir);
			File.WriteAllText (Path.Combine (drawableDir, "button.png"), string.Empty);
			var main = Path.Combine (layoutDir, "main.xml");
			File.WriteAllText (main, @"<?xml version=""1.0"" encoding=""utf-8""?>
<LinearLayout xmlns:android=""http://schemas.android.com/apk/res/android""
	android:orientation=""horizontal""
	android:layout_width=""match_parent""
	android:layout_height=""match_parent"">
	<classlibrary1.CustomView xmlns:android=""http://schemas.android.com/apk/res/android""/>
	<ImageView xmlns:android=""http://schemas.android.com/apk/res/android"" android:src=""@drawable/Button"" />
</LinearLayout>");
			var acwMap = new Dictionary<string, string> () {
				{ "classlibrary1.CustomView", "md5d6f7135293df7527c983d45d07471c5e.CustomTextView" },
			};
			Monodroid.AndroidResource.UpdateXmlResource (Path.Combine (path, "res"), main, acwMap, null);
			var mainText = File.ReadAllText (main);
			Assert.True (mainText.Contains ("@drawable/button"), "'@drawable/Button' was not converted to '@drawable/button'");
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
			Monodroid.AndroidResource.UpdateXmlResource (Path.Combine (path, "res"), actions, new Dictionary<string, string> (), null);
			var actionsText = File.ReadAllText (actions);
			Assert.True (actionsText.Contains ("@layout/servinglayout"), "'@layout/ServingLayout' was not converted to '@layout/servinglayout'");
			Directory.Delete (path, recursive: true);
		}
	}
}
