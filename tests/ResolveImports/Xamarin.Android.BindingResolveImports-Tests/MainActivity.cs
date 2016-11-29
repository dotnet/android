using System;

using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;

namespace Xamarin.Android.BindingResolveImportsTests
{
	[Activity (Label = "Xamarin.Android.BindingResolveImports-Tests", MainLauncher = true, Icon = "@drawable/icon")]
	public class MainActivity : Activity
	{
		int count = 1;

		protected override void OnCreate (Bundle bundle)
		{
			base.OnCreate (bundle);

			// Set our view from the "main" layout resource
			SetContentView (Resource.Layout.Main);

			// Get our button from the layout resource,
			// and attach an event to it
			Button button = FindViewById<Button> (Resource.Id.myButton);
			
			button.Click += delegate {
				new Com.Xamarin.Android.Test.Binding.Resolveimport.Lib1 ().Field1 = "test";
				new Xamarin.Android.BindingResolveImportLib2.Addition ();
				new Com.Xamarin.Android.Test.Binding.Resolveimport.Lib3 ().Field1 = "test";
				new Com.Xamarin.Android.Test.Binding.Resolveimport.Lib4 ().Field1 = "test";
				button.Text = string.Format ("{0} clicks!", count++);
			};
		}
	}
}
