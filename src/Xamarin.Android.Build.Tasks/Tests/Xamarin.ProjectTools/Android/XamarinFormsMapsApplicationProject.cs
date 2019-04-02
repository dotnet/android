using System;
using System.IO;

namespace Xamarin.ProjectTools
{
	public class XamarinFormsMapsApplicationProject : XamarinFormsAndroidApplicationProject
	{
		static readonly string MainPageMaps_xaml;

		static XamarinFormsMapsApplicationProject ()
		{
			using (var sr = new StreamReader (typeof (XamarinAndroidApplicationProject).Assembly.GetManifestResourceStream ("Xamarin.ProjectTools.Resources.Forms.MainPageMaps.xaml")))
				MainPageMaps_xaml = sr.ReadToEnd ();
		}

		public XamarinFormsMapsApplicationProject ()
		{
			PackageReferences.Add (KnownPackages.XamarinFormsMaps_3_6_0_220655);
			MainActivity = MainActivity.Replace ("//${AFTER_FORMS_INIT}", "Xamarin.FormsMaps.Init (this, savedInstanceState);");
			//NOTE: API_KEY metadata just has to *exist*
			AndroidManifest = AndroidManifest.Replace ("</application>", "<meta-data android:name=\"com.google.android.maps.v2.API_KEY\" android:value=\"\" /></application>");
		}

		protected override string MainPageXaml () => ProcessSourceTemplate (MainPageMaps_xaml);
	}
}
