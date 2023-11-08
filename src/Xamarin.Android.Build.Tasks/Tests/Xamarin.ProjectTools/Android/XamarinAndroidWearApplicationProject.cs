using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Xamarin.ProjectTools
{
	public class XamarinAndroidWearApplicationProject : XamarinAndroidApplicationProject
	{
		static readonly string default_strings_xml, default_main_activity;
		static readonly string default_layout_main, default_layout_rect_main, default_layout_round_main;

		static XamarinAndroidWearApplicationProject ()
		{
			using (var sr = new StreamReader (typeof(XamarinAndroidApplicationProject).Assembly.GetManifestResourceStream ("Xamarin.ProjectTools.Resources.Wear.MainActivity.cs")))
				default_main_activity = sr.ReadToEnd ();
			using (var sr = new StreamReader (typeof(XamarinAndroidApplicationProject).Assembly.GetManifestResourceStream ("Xamarin.ProjectTools.Resources.Wear.Strings.xml")))
				default_strings_xml = sr.ReadToEnd ();
			using (var sr = new StreamReader (typeof(XamarinAndroidApplicationProject).Assembly.GetManifestResourceStream ("Xamarin.ProjectTools.Resources.Wear.LayoutMain.axml")))
				default_layout_main = sr.ReadToEnd ();
			using (var sr = new StreamReader (typeof(XamarinAndroidApplicationProject).Assembly.GetManifestResourceStream ("Xamarin.ProjectTools.Resources.Wear.LayoutRectMain.axml")))
				default_layout_rect_main = sr.ReadToEnd ();
			using (var sr = new StreamReader (typeof(XamarinAndroidApplicationProject).Assembly.GetManifestResourceStream ("Xamarin.ProjectTools.Resources.Wear.LayoutRoundMain.axml")))
				default_layout_round_main = sr.ReadToEnd ();
		}

		public XamarinAndroidWearApplicationProject (string debugConfigurationName = "Debug", string releaseConfigurationName = "Release", [CallerMemberName] string packageName = "")
			: base (debugConfigurationName, releaseConfigurationName, packageName)
		{
			PackageReferences.Add (KnownPackages.AndroidWear_2_2_0);

			MainActivity = default_main_activity;
			StringsXml = default_strings_xml;
			LayoutMain = default_layout_main;
			LayoutRectMain = default_layout_rect_main;
			LayoutRoundMain = default_layout_round_main;

			AndroidResources.Add (new AndroidItem.AndroidResource ("Resources\\layout\\RectangleMain.axml") { TextContent = () => LayoutRectMain });
			AndroidResources.Add (new AndroidItem.AndroidResource ("Resources\\layout\\RoundMain.axml") { TextContent = () => LayoutRoundMain });
		}

		public string LayoutRectMain { get; set; }
		public string LayoutRoundMain { get; set; }
	}
}
