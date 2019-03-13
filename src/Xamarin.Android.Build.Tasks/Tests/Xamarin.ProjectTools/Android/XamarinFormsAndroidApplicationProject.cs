﻿using System;
using System.IO;

namespace Xamarin.ProjectTools
{
	public class XamarinFormsAndroidApplicationProject : XamarinAndroidApplicationProject
	{
		static readonly string default_main_activity_cs;
		static readonly string colors_xml;
		static readonly string styles_xml;
		static readonly string Tabbar_axml;
		static readonly string Toolbar_axml;
		static readonly string MainPage_xaml;
		static readonly string MainPage_xaml_cs;
		static readonly string App_xaml;
		static readonly string App_xaml_cs;

		static XamarinFormsAndroidApplicationProject ()
		{
			using (var sr = new StreamReader (typeof (XamarinAndroidApplicationProject).Assembly.GetManifestResourceStream ("Xamarin.ProjectTools.Resources.Forms.MainActivity.cs")))
				default_main_activity_cs = sr.ReadToEnd ();
			using (var sr = new StreamReader (typeof (XamarinAndroidApplicationProject).Assembly.GetManifestResourceStream ("Xamarin.ProjectTools.Resources.Forms.colors.xml")))
				colors_xml = sr.ReadToEnd ();
			using (var sr = new StreamReader (typeof (XamarinAndroidApplicationProject).Assembly.GetManifestResourceStream ("Xamarin.ProjectTools.Resources.Forms.styles.xml")))
				styles_xml = sr.ReadToEnd ();
			using (var sr = new StreamReader (typeof (XamarinAndroidApplicationProject).Assembly.GetManifestResourceStream ("Xamarin.ProjectTools.Resources.Forms.Tabbar.axml")))
				Tabbar_axml = sr.ReadToEnd ();
			using (var sr = new StreamReader (typeof (XamarinAndroidApplicationProject).Assembly.GetManifestResourceStream ("Xamarin.ProjectTools.Resources.Forms.Toolbar.axml")))
				Toolbar_axml = sr.ReadToEnd ();
			using (var sr = new StreamReader (typeof (XamarinAndroidApplicationProject).Assembly.GetManifestResourceStream ("Xamarin.ProjectTools.Resources.Forms.MainPage.xaml")))
				MainPage_xaml = sr.ReadToEnd ();
			using (var sr = new StreamReader (typeof (XamarinAndroidApplicationProject).Assembly.GetManifestResourceStream ("Xamarin.ProjectTools.Resources.Forms.MainPage.xaml.cs")))
				MainPage_xaml_cs = sr.ReadToEnd ();
			using (var sr = new StreamReader (typeof (XamarinAndroidApplicationProject).Assembly.GetManifestResourceStream ("Xamarin.ProjectTools.Resources.Forms.App.xaml")))
				App_xaml = sr.ReadToEnd ();
			using (var sr = new StreamReader (typeof (XamarinAndroidApplicationProject).Assembly.GetManifestResourceStream ("Xamarin.ProjectTools.Resources.Forms.App.xaml.cs")))
				App_xaml_cs = sr.ReadToEnd ();
		}

		public XamarinFormsAndroidApplicationProject (string debugConfigurationName = "Debug", string releaseConfigurationName = "Release")
			: base (debugConfigurationName, releaseConfigurationName)
		{
			//NOTE: we can get all the other dependencies transitively, yay!
			PackageReferences.Add (KnownPackages.XamarinForms_3_6_0_220655);

			AndroidResources.Add (new AndroidItem.AndroidResource ("Resources\\values\\colors.xml") {
				TextContent = () => colors_xml,
			});
			AndroidResources.Add (new AndroidItem.AndroidResource ("Resources\\values\\styles.xml") {
				TextContent = () => styles_xml,
			});
			AndroidResources.Add (new AndroidItem.AndroidResource ("Resources\\layout\\Tabbar.axml") {
				TextContent = () => Tabbar_axml,
			});
			AndroidResources.Add (new AndroidItem.AndroidResource ("Resources\\layout\\Toolbar.axml") {
				TextContent = () => Toolbar_axml,
			});
			OtherBuildItems.Add (new BuildItem ("EmbeddedResource", "MainPage.xaml") {
				TextContent = () => ProcessSourceTemplate (MainPage_xaml),
			});
			Sources.Add (new BuildItem.Source ("MainPage.xaml.cs") {
				TextContent = () => ProcessSourceTemplate (MainPage_xaml_cs),
			});
			OtherBuildItems.Add (new BuildItem ("EmbeddedResource", "App.xaml") {
				TextContent = () => ProcessSourceTemplate (App_xaml),
			});
			Sources.Add (new BuildItem.Source ("App.xaml.cs") {
				TextContent = () => ProcessSourceTemplate (App_xaml_cs),
			});

			MainActivity = default_main_activity_cs;
		}
	}
}
