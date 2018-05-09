using System;
using System.Collections.Generic;
using Android.AccessibilityServices;
using Android.OS;
using Android.Runtime;
using Xamarin.Android.Design;

namespace Android.Views {

#if ANDROID_11 && !ANDROID_14
	public enum SystemUiFlags {
	}
#endif

	public partial class View : ILayoutBindingClient {

		global::Android.Content.Context ILayoutBindingClient.Context => ((View)this).Context;

#if ANDROID_16
		[Obsolete ("This method uses wrong enum type. Please use PerformAccessibilityAction(Action) instead.")]
		public bool PerformAccessibilityAction (GlobalAction action, Bundle arguments)
		{
			return PerformAccessibilityAction ((Android.Views.Accessibility.Action) (int) action, arguments);
		}
#endif

		public T FindViewById<T> (int id)
			where T : Android.Views.View
		{
			return this.FindViewById (id).JavaCast<T> ();
		}

		public bool Post (Action action)
		{
			return Post (new Java.Lang.Thread.RunnableImplementor (action, true));
		}

		public bool PostDelayed (Action action, long delayMillis)
		{
			return PostDelayed (new Java.Lang.Thread.RunnableImplementor (action, true), delayMillis);
		}

		public bool RemoveCallbacks (Action action)
		{
			var runnable = Java.Lang.Thread.RunnableImplementor.Remove (action);
			if (runnable == null)
				return false;
			bool result = RemoveCallbacks (runnable);
			runnable.Dispose ();
			return result;
		}

		public void ScheduleDrawable (Android.Graphics.Drawables.Drawable who, Action what, long when)
		{
			ScheduleDrawable (who, new Java.Lang.Thread.RunnableImplementor (what, true), when);
		}

		public void UnscheduleDrawable (Android.Graphics.Drawables.Drawable who, Action what)
		{
			var runnable = Java.Lang.Thread.RunnableImplementor.Remove (what);
			if (runnable == null)
				return;
			UnscheduleDrawable (who, runnable);
			runnable.Dispose ();
		}

#if ANDROID_11
		[Obsolete ("Please Use DispatchSystemUiVisibilityChanged(SystemUiFlags)")]
		public void DispatchSystemUiVisibilityChanged (int visibility)
		{
			DispatchSystemUiVisibilityChanged ((SystemUiFlags) visibility);
		}
#endif  // ANDROID_11
#if ANDROID_14 && !ANDROID_16
		[Obsolete ("The View.fitsSystemWindows() method was REMOVED by Google in API-16. DO NOT USE.)", error:true)]
		public bool FitsSystemWindows ()
		{
			return InvokeFitsSystemWindows ();
		}
#endif
		public virtual void OnLayoutViewNotFound <T> (int resourceId, ref T view) where T: View
		{
			var activity = Context as global::Android.App.Activity;
			activity?.OnLayoutViewNotFound <T> (resourceId, ref view);
		}

#if ANDROID_11
		public virtual void OnLayoutFragmentNotFound <T> (int resourceId, ref T fragment) where T: global::Android.App.Fragment
		{
			var activity = Context as global::Android.App.Activity;
			activity?.OnLayoutFragmentNotFound <T> (resourceId, ref fragment);
		}
#endif  // ANDROID_11
	}
}
