using System;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

[assembly: XamlCompilation (XamlCompilationOptions.Compile)]
namespace ${ROOT_NAMESPACE}
{
	public partial class App : Application
	{
		public App ()
		{
			InitializeComponent ();

			MainPage = new MainPage ();
		}
	}
}
