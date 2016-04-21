//
// ActivityTracker.cs
//
// Author:
//   Sandy Armstrong <sandy@xamarin.com>
//
// Copyright 2015 Xamarin Inc. All rights reserved.

using System;
using System.Collections.Generic;

using Android.OS;

namespace Android.App
{
	#if ANDROID_14 // For IActivityLifecycleCallbacks
	// For internal use by inspector
	class ActivityTracker : Java.Lang.Object, Application.IActivityLifecycleCallbacks
	{
		readonly List<Activity> startedActivities = new List<Activity> ();

		public event EventHandler ActivityStarted;

		public IReadOnlyList<Activity> StartedActivities {
			get { return startedActivities; }
		}

		public void OnActivityStarted (Activity activity)
		{
			startedActivities.Add (activity);
			ActivityStarted?.Invoke (this, EventArgs.Empty);
		}

		public void OnActivityStopped (Activity activity)
		{
			startedActivities.Remove (activity);
		}

		public void OnActivityCreated (Activity activity, Bundle savedInstanceState)
		{
		}

		public void OnActivityDestroyed (Activity activity)
		{
		}

		public void OnActivityPaused (Activity activity)
		{
		}

		public void OnActivityResumed (Activity activity)
		{
		}

		public void OnActivitySaveInstanceState (Activity activity, Bundle outState)
		{
		}
	}
	#endif
}

