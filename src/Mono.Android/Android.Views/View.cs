using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Android.AccessibilityServices;
using Android.OS;
using Android.Runtime;

namespace Android.Views {


	public partial class View {
		[Obsolete ("This method uses wrong enum type. Please use PerformAccessibilityAction(Action) instead.")]
		public bool PerformAccessibilityAction (GlobalAction action, Bundle arguments)
		{
			return PerformAccessibilityAction ((Android.Views.Accessibility.Action) (int) action, arguments);
		}

		public T? FindViewById<
				[DynamicallyAccessedMembers (Constructors)]
				T
		> (int id)
			where T : Android.Views.View
		{
			return this.FindViewById (id).JavaCast<T> ();
		}

		// See: https://cs.android.com/android/platform/superproject/+/master:frameworks/base/core/java/android/view/View.java;l=25322
		public T RequireViewById<
				[DynamicallyAccessedMembers (Constructors)]
				T
		> (int id)
			where T : Android.Views.View
		{
			var view = FindViewById<T> (id);
			if (view == null) {
				throw new Java.Lang.IllegalArgumentException ($"Parameter 'id' of value 0x{id:X} does not reference a View of type '{typeof (T)}' inside this View");
			}
			return view;
		}

		public bool Post (Action action)
		{
			var runnable    = new Java.Lang.Thread.RunnableImplementor (action, removable: true);
			if (Post (runnable)) {
				return true;
			}
			runnable.Dispose ();
			return false;
		}

		public bool PostDelayed (Action action, long delayMillis)
		{
			var runnable    = new Java.Lang.Thread.RunnableImplementor (action, removable: true);
			if (PostDelayed (runnable, delayMillis)) {
				return true;
			}
			runnable.Dispose ();
			return false;
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

		[Obsolete ("Please Use DispatchSystemUiVisibilityChanged(SystemUiFlags)")]
		[global::System.Runtime.Versioning.ObsoletedOSPlatform ("android30.0")]
		public void DispatchSystemUiVisibilityChanged (int visibility)
		{
			DispatchSystemUiVisibilityChanged ((SystemUiFlags) visibility);
		}

		[global::System.Runtime.Versioning.ObsoletedOSPlatform ("android30.0", "These flags are deprecated. Use WindowInsetsController instead.")]
		public SystemUiFlags SystemUiFlags {
#pragma warning disable CS0618 // Access the legacy property while exposing the correctly enumified replacement.
			get => (SystemUiFlags) SystemUiVisibility;
			set => SystemUiVisibility = (Android.Views.StatusBarVisibility) value;
#pragma warning restore CS0618
		}
	}
}
