namespace HybridTodoApp;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		Page rootPage = Preferences.Default.Get (DemoAuthentication.PreferenceKey, false)
			? new AppShell ()
			: new LoginPage ();
		return new Window (rootPage);
	}
}