using System;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

#if !DEBUG
[assembly: XamlCompilation (XamlCompilationOptions.Compile)]
#endif

namespace Xamarin.Forms.Performance.Integration
{
	public partial class App : Application
	{
		public static bool UseMockDataStore = true;
		public static string BackendUrl = "https://localhost:5000";

		public App ()
		{
			InitializeComponent ();

			if (UseMockDataStore)
				DependencyService.Register<MockDataStore> ();
			else
				DependencyService.Register<CloudDataStore> ();

			if (Device.RuntimePlatform == Device.iOS)
				MainPage = new MainPage ();
			else
				MainPage = new NavigationPage (new MainPage ());
		}
	}
}
