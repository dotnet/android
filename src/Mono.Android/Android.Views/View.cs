using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Android.AccessibilityServices;
using Android.OS;
using Android.Runtime;

namespace Android.Views {

#if ANDROID_11 && !ANDROID_14
	public enum SystemUiFlags {
	}
#endif

	public partial class View {

#if ANDROID_16
		[Obsolete ("This method uses wrong enum type. Please use PerformAccessibilityAction(Action) instead.")]
		public bool PerformAccessibilityAction (GlobalAction action, Bundle arguments)
		{
			return PerformAccessibilityAction ((Android.Views.Accessibility.Action) (int) action, arguments);
		}
#endif

		[return: MaybeNull]
		public T FindViewById<T> (int id)
			where T : Android.Views.View
		{
			return this.FindViewById (id).JavaCast<T> ();
		}

#if NET7_0_OR_GREATER || (NET6_0_OR_GREATER && ANDROID_33) || ANDROID_34
		// See: https://cs.android.com/android/platform/superproject/+/master:frameworks/base/core/java/android/view/View.java;l=25322
		public T RequireViewById<T> (int id)
			where T : Android.Views.View
		{
			var view = FindViewById<T> (id);
			if (view == null) {
				throw new Java.Lang.IllegalArgumentException ($"Parameter 'id' of value 0x{id:X} does not reference a View of type '{typeof (T)}' inside this View");
			}
			return view;
		}
#endif //NET7_0_OR_GREATER || (NET6_0_OR_GREATER && ANDROID_33) || ANDROID_34

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

#if NET && ANDROID_34
		[global::System.Runtime.Versioning.ObsoletedOSPlatform ("android30.0", "These flags are deprecated. Use WindowInsetsController instead.")]
		public SystemUiFlags SystemUiFlags {
			get => (SystemUiFlags) SystemUiVisibility;
			set => SystemUiVisibility = (Android.Views.StatusBarVisibility) value;
		}
#endif
	}
}
