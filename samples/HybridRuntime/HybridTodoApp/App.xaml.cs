namespace HybridTodoApp;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		Page rootPage;
		if (!Preferences.Default.Get (DemoAuthentication.PreferenceKey, false)) {
			rootPage = new LoginPage ();
		} else {
#if HYBRID_RUNTIME
			rootPage = new HybridAppShell ();
#else
			rootPage = new AppShell ();
#endif
		}
		return new Window (rootPage);
	}
}