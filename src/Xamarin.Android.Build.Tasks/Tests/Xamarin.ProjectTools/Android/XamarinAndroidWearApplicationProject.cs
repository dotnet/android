using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Linq;

namespace Xamarin.ProjectTools
{
	/// <summary>
	/// Migrated from Android.Support to AndroidX
	/// see: https://android-developers.googleblog.com/2016/04/build-beautifully-for-android-wear.html
	/// </summary>
	public class XamarinAndroidWearApplicationProject : XamarinAndroidApplicationProject
	{
		static readonly string default_strings_xml, default_main_activity;
		static readonly string default_layout_rect_main, default_layout_round_main;

		static XamarinAndroidWearApplicationProject ()
		{
			using (var sr = new StreamReader (typeof(XamarinAndroidApplicationProject).Assembly.GetManifestResourceStream ("Xamarin.ProjectTools.Resources.Wear.MainActivity.cs")))
				default_main_activity = sr.ReadToEnd ();
			using (var sr = new StreamReader (typeof(XamarinAndroidApplicationProject).Assembly.GetManifestResourceStream ("Xamarin.ProjectTools.Resources.Wear.Strings.xml")))
				default_strings_xml = sr.ReadToEnd ();
			using (var sr = new StreamReader (typeof(XamarinAndroidApplicationProject).Assembly.GetManifestResourceStream ("Xamarin.ProjectTools.Resources.Wear.LayoutRectMain.axml")))
				default_layout_rect_main = sr.ReadToEnd ();
			using (var sr = new StreamReader (typeof(XamarinAndroidApplicationProject).Assembly.GetManifestResourceStream ("Xamarin.ProjectTools.Resources.Wear.LayoutRoundMain.axml")))
				default_layout_round_main = sr.ReadToEnd ();
		}

		public XamarinAndroidWearApplicationProject (string debugConfigurationName = "Debug", string releaseConfigurationName = "Release", [CallerMemberName] string packageName = "")
			: base (debugConfigurationName, releaseConfigurationName, packageName)
		{
			PackageReferences.Add (KnownPackages.XamarinAndroidXWear);

			// uses-sdk:minSdkVersion 21 cannot be smaller than version 23 declared in library androidx.wear.wear.aar as the library might be using APIs not available in 21
			SupportedOSPlatformVersion = "23";

			MainActivity = default_main_activity;
			StringsXml = default_strings_xml;
			LayoutRectMain = default_layout_rect_main;
			LayoutRoundMain = default_layout_round_main;

			// Remove Resources\layout\Main.axml
			var main = AndroidResources.FirstOrDefault (a => a.Include () == "Resources\\layout\\Main.axml");
			if (main != null)
				AndroidResources.Remove (main);

			AndroidResources.Add (new AndroidItem.AndroidResource ("Resources\\layout-notround\\activity_main.axml") { TextContent = () => LayoutRectMain });
			AndroidResources.Add (new AndroidItem.AndroidResource ("Resources\\layout-round\\activity_main.axml") { TextContent = () => LayoutRoundMain });
		}

		public string LayoutRectMain { get; set; }
		public string LayoutRoundMain { get; set; }
	}
}
