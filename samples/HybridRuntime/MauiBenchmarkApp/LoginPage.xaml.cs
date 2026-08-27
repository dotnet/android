using Microsoft.Maui.Storage;

namespace MauiBenchmarkApp;

public partial class LoginPage : ContentPage
{
	public LoginPage ()
	{
		InitializeComponent ();
	}

	async void OnLoginClicked (object? sender, EventArgs e)
	{
		Preferences.Default.Set ("authenticated", true);
		await Navigation.PushAsync (new LandingPage ());
		Navigation.RemovePage (this);
	}
}
