using Microsoft.Maui.Storage;

namespace MauiBenchmarkApp;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		bool authenticated = Preferences.Default.Get ("authenticated", false);
		Page initialPage = authenticated ? new LandingPage () : new LoginPage ();
#if ANDROID
#if BENCHMARK_NATIVEAOT
		const string runtime = "NativeAOT";
#else
		const string runtime = "CoreCLR";
#endif
		Android.Util.Log.Info ("MauiStartupBenchmark", $"{runtime} first page: {initialPage.GetType ().Name}");
#endif
		return new Window (new NavigationPage (initialPage));
	}
}