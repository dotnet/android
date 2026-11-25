using System;

using Android.App;
using Android.OS;

using Microsoft.Android.Runtime;

namespace Android.Runtime;

public static class AndroidTelemetry
{
	public const string ActivitySourceName = RuntimeTelemetry.ActivitySourceName;
	public const string InteropActivitySourceName = RuntimeTelemetry.InteropActivitySourceName;

	static readonly object lifecycleLock = new ();
	static ActivityLifecycleCallbacks? lifecycleCallbacks;

	public static int ReplayStartupActivities () => RuntimeTelemetry.ReplayStartupActivities ();

	public static void EnableActivityLifecycleTracing (Application application)
	{
		ArgumentNullException.ThrowIfNull (application);

		lock (lifecycleLock) {
			if (lifecycleCallbacks != null)
				return;

			var callbacks = new ActivityLifecycleCallbacks ();
			try {
				application.RegisterActivityLifecycleCallbacks (callbacks);
			} catch {
				callbacks.Dispose ();
				throw;
			}
			lifecycleCallbacks = callbacks;
		}
	}

	sealed class ActivityLifecycleCallbacks : Java.Lang.Object, Application.IActivityLifecycleCallbacks
	{
		static void Record (string name, Activity activity)
		{
			using var recorded = RuntimeTelemetry.Source.StartActivity (name);
			recorded?.SetTag ("android.activity.name", activity.GetType ().FullName);
		}

		public void OnActivityCreated (Activity activity, Bundle? savedInstanceState) => Record ("android.activity.created", activity);
		public void OnActivityStarted (Activity activity) => Record ("android.activity.started", activity);
		public void OnActivityResumed (Activity activity) => Record ("android.activity.resumed", activity);
		public void OnActivityPaused (Activity activity) => Record ("android.activity.paused", activity);
		public void OnActivityStopped (Activity activity) => Record ("android.activity.stopped", activity);
		public void OnActivitySaveInstanceState (Activity activity, Bundle outState) => Record ("android.activity.save_instance_state", activity);
		public void OnActivityDestroyed (Activity activity) => Record ("android.activity.destroyed", activity);
	}
}
