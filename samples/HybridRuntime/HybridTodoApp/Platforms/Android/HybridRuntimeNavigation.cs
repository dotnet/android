using Android.Content;
using Microsoft.Maui.ApplicationModel;

namespace HybridTodoApp;

static class HybridRuntimeNavigation
{
	public static bool IsEnabled {
		get {
#if HYBRID_RUNTIME
			return true;
#else
			return false;
#endif
		}
	}

	public static void StartCoreClrWarmup ()
	{
#if HYBRID_RUNTIME
		Android.App.Activity activity = Platform.CurrentActivity
			?? throw new InvalidOperationException ("The current Android activity is unavailable.");
		var intent = new Intent ();
		intent.SetClassName (activity.PackageName, "net.dot.hybrid.CoreClrWarmupReceiver");
		activity.SendBroadcast (intent);
#endif
	}

	public static bool OpenFullWorkspace (string route = "main", int? id = null)
	{
#if HYBRID_RUNTIME
		Android.App.Activity activity = Platform.CurrentActivity
			?? throw new InvalidOperationException ("The current Android activity is unavailable.");
		var intent = new Intent ();
		intent.SetClassName (activity.PackageName, "net.dot.hybrid.CoreClrBootstrapActivity");
		intent.PutExtra ("hybrid-route", route);
		if (id.HasValue) {
			intent.PutExtra ("hybrid-id", id.Value);
		}
		activity.StartActivity (intent);
		return true;
#else
		return false;
#endif
	}
}
