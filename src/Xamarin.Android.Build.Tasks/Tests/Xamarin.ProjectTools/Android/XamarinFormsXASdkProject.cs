using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Xamarin.ProjectTools
{
	public class XamarinFormsXASdkProject : XASdkProject
	{
		static readonly string default_main_activity_cs;
		static readonly string colors_xml;
		static readonly string styles_xml;
		static readonly string Tabbar_xml;
		static readonly string Toolbar_xml;
		static readonly string MainPage_xaml;
		static readonly string MainPage_xaml_cs;
		static readonly string App_xaml;
		static readonly string App_xaml_cs;

		static XamarinFormsXASdkProject ()
		{
			var assembly = typeof (XamarinFormsXASdkProject).Assembly;
			using (var sr = new StreamReader (assembly.GetManifestResourceStream ("Xamarin.ProjectTools.Resources.Forms.MainActivity.cs")))
				default_main_activity_cs = sr.ReadToEnd ();
			using (var sr = new StreamReader (assembly.GetManifestResourceStream ("Xamarin.ProjectTools.Resources.Forms.colors.xml")))
				colors_xml = sr.ReadToEnd ();
			using (var sr = new StreamReader (assembly.GetManifestResourceStream ("Xamarin.ProjectTools.Resources.Forms.styles.xml")))
				styles_xml = sr.ReadToEnd ();
			using (var sr = new StreamReader (assembly.GetManifestResourceStream ("Xamarin.ProjectTools.Resources.AndroidX.Tabbar.xml")))
				Tabbar_xml = sr.ReadToEnd ();
			using (var sr = new StreamReader (assembly.GetManifestResourceStream ("Xamarin.ProjectTools.Resources.AndroidX.Toolbar.xml")))
				Toolbar_xml = sr.ReadToEnd ();
			using (var sr = new StreamReader (assembly.GetManifestResourceStream ("Xamarin.ProjectTools.Resources.Forms.MainPage.xaml")))
				MainPage_xaml = sr.ReadToEnd ();
			using (var sr = new StreamReader (assembly.GetManifestResourceStream ("Xamarin.ProjectTools.Resources.Forms.MainPage.xaml.cs")))
				MainPage_xaml_cs = sr.ReadToEnd ();
			using (var sr = new StreamReader (assembly.GetManifestResourceStream ("Xamarin.ProjectTools.Resources.Forms.App.xaml")))
				App_xaml = sr.ReadToEnd ();
			using (var sr = new StreamReader (assembly.GetManifestResourceStream ("Xamarin.ProjectTools.Resources.Forms.App.xaml.cs")))
				App_xaml_cs = sr.ReadToEnd ();
		}

		public XamarinFormsXASdkProject (string outputType = "Exe")
			: base (outputType)
		{
			PackageReferences.Add (KnownPackages.XamarinForms_4_7_0_1142);
			this.AddDotNetCompatPackages ();

			// Workaround for AndroidX, see: https://github.com/xamarin/AndroidSupportComponents/pull/239
			Imports.Add (new Import (() => "Directory.Build.targets") {
				TextContent = () =>
					@"<Project>
						<PropertyGroup>
							<VectorDrawableCheckBuildToolsVersionTaskBeforeTargets />
						</PropertyGroup>
					</Project>"
			});

			Sources.Add (new AndroidItem.AndroidResource ("Resources\\values\\colors.xml") {
				TextContent = () => colors_xml,
			});
			Sources.Add (new AndroidItem.AndroidResource ("Resources\\values\\styles.xml") {
				TextContent = () => styles_xml,
			});
			Sources.Add (new AndroidItem.AndroidResource ("Resources\\layout\\Tabbar.xml") {
				TextContent = () => Tabbar_xml,
			});
			Sources.Add (new AndroidItem.AndroidResource ("Resources\\layout\\Toolbar.xml") {
				TextContent = () => Toolbar_xml,
			});
			Sources.Add (new BuildItem ("EmbeddedResource", "MainPage.xaml") {
				TextContent = MainPageXaml,
			});
			Sources.Add (new BuildItem.Source ("MainPage.xaml.cs") {
				TextContent = () => ProcessSourceTemplate (MainPage_xaml_cs),
			});
			Sources.Add (new BuildItem ("EmbeddedResource", "App.xaml") {
				TextContent = () => ProcessSourceTemplate (App_xaml),
			});
			Sources.Add (new BuildItem.Source ("App.xaml.cs") {
				TextContent = () => ProcessSourceTemplate (App_xaml_cs),
			});

			MainActivity = default_main_activity_cs;
		}

		protected virtual string MainPageXaml () => ProcessSourceTemplate (MainPage_xaml);
	}
}
