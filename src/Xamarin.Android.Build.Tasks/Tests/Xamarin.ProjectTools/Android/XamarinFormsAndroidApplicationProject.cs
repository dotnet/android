using System;
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
			var forms = KnownPackages.XamarinForms_3_1_0_697729;
			PackageReferences.Add (forms);
			PackageReferences.Add (KnownPackages.Android_Arch_Core_Common_26_1_0);
			PackageReferences.Add (KnownPackages.Android_Arch_Lifecycle_Common_26_1_0);
			PackageReferences.Add (KnownPackages.Android_Arch_Lifecycle_Runtime_26_1_0);
			PackageReferences.Add (KnownPackages.AndroidSupportV4_27_0_2_1);
			PackageReferences.Add (KnownPackages.SupportCompat_27_0_2_1);
			PackageReferences.Add (KnownPackages.SupportCoreUI_27_0_2_1);
			PackageReferences.Add (KnownPackages.SupportCoreUtils_27_0_2_1);
			PackageReferences.Add (KnownPackages.SupportDesign_27_0_2_1);
			PackageReferences.Add (KnownPackages.SupportFragment_27_0_2_1);
			PackageReferences.Add (KnownPackages.SupportMediaCompat_27_0_2_1);
			PackageReferences.Add (KnownPackages.SupportV7AppCompat_27_0_2_1);
			PackageReferences.Add (KnownPackages.SupportV7CardView_27_0_2_1);
			PackageReferences.Add (KnownPackages.SupportV7MediaRouter_27_0_2_1);
			PackageReferences.Add (KnownPackages.SupportV7RecyclerView_27_0_2_1);
			PackageReferences.Add (KnownPackages.VectorDrawable_27_0_2_1);

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
