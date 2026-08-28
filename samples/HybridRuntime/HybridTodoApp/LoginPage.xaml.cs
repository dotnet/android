namespace HybridTodoApp;

public partial class LoginPage : ContentPage
{
	public LoginPage ()
	{
		InitializeComponent ();
	}

	void OnSignInClicked (object? sender, EventArgs e)
	{
		if (Email.Text != DemoAuthentication.Email || Password.Text != DemoAuthentication.Password) {
			ErrorMessage.IsVisible = true;
			return;
		}

		Preferences.Default.Set (DemoAuthentication.PreferenceKey, true);
		Application? application = Application.Current;
		if (application is null || application.Windows.Count == 0) {
			throw new InvalidOperationException ("The application window is unavailable.");
		}

		application.Windows [0].Page = new AppShell ();
	}
}
