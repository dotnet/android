using Android.App;
using Android.Content.PM;
using Android.OS;

namespace HybridTodoApp;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
#if HYBRID_RUNTIME
	const long CoreClrWarmupDelayMilliseconds = 1500;

	Handler? mainHandler;
	WarmupRunnable? warmupRunnable;
	bool warmupRequested;
#endif

	protected override void OnCreate (Bundle? savedInstanceState)
	{
		base.OnCreate (savedInstanceState);
#if HYBRID_RUNTIME
		mainHandler = new Handler (Looper.MainLooper ?? throw new InvalidOperationException ("The Android main looper is unavailable."));
		warmupRunnable = new WarmupRunnable (this);
#endif
	}

#if HYBRID_RUNTIME
	protected override void OnPostResume ()
	{
		base.OnPostResume ();
		if (warmupRequested || mainHandler is null || warmupRunnable is null) {
			return;
		}
		mainHandler.RemoveCallbacks (warmupRunnable);
		mainHandler.PostDelayed (warmupRunnable, CoreClrWarmupDelayMilliseconds);
	}

	protected override void OnPause ()
	{
		if (mainHandler is not null && warmupRunnable is not null) {
			mainHandler.RemoveCallbacks (warmupRunnable);
		}
		base.OnPause ();
	}

	protected override void OnDestroy ()
	{
		warmupRunnable?.Dispose ();
		mainHandler?.Dispose ();
		warmupRunnable = null;
		mainHandler = null;
		base.OnDestroy ();
	}

	void RequestCoreClrWarmup ()
	{
		if (warmupRequested || IsFinishing || IsDestroyed) {
			return;
		}
		warmupRequested = true;
		HybridRuntimeNavigation.StartCoreClrWarmup ();
	}

	sealed class WarmupRunnable (MainActivity activity) : Java.Lang.Object, Java.Lang.IRunnable
	{
		public void Run ()
		{
			activity.RequestCoreClrWarmup ();
		}
	}
#endif
}
