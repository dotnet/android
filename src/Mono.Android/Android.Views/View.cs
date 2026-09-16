using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Android.AccessibilityServices;
using Android.OS;
using Android.Runtime;
using JniArgumentValue = Java.Interop.JniArgumentValue;
using JniObjectReference = Java.Interop.JniObjectReference;

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
			return Java.Lang.Thread.RunnableImplementor.Remove (
				action,
				this,
				static (view, runnable) => view.RemoveCallbacks (runnable));
		}

		public void ScheduleDrawable (Android.Graphics.Drawables.Drawable who, Action what, long when)
		{
			ScheduleDrawable (who, new Java.Lang.Thread.RunnableImplementor (what, true), when);
		}

		public void UnscheduleDrawable (Android.Graphics.Drawables.Drawable who, Action what)
		{
			Java.Lang.Thread.RunnableImplementor.Remove (what, this, who, static (runnable, view, who) => view.UnscheduleDrawable (who, runnable));
		}

		unsafe bool RemoveCallbacks (JniObjectReference runnable)
		{
			const string id = "removeCallbacks.(Ljava/lang/Runnable;)Z";
			JniArgumentValue* args = stackalloc JniArgumentValue [1];
			args [0] = new JniArgumentValue (runnable.Handle);
			return _members.InstanceMethods.InvokeVirtualBooleanMethod (id, this, args);
		}

		unsafe void UnscheduleDrawable (Android.Graphics.Drawables.Drawable who, JniObjectReference runnable)
		{
			const string id = "unscheduleDrawable.(Landroid/graphics/drawable/Drawable;Ljava/lang/Runnable;)V";
			try {
				JniArgumentValue* args = stackalloc JniArgumentValue [2];
				args [0] = new JniArgumentValue (who.Handle);
				args [1] = new JniArgumentValue (runnable.Handle);
				_members.InstanceMethods.InvokeVirtualVoidMethod (id, this, args);
			} finally {
				GC.KeepAlive (who);
			}
		}

#if ANDROID_11
		[Obsolete ("Please Use DispatchSystemUiVisibilityChanged(SystemUiFlags)")]
		[global::System.Runtime.Versioning.ObsoletedOSPlatform ("android30.0")]
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

#if ANDROID_34
		[global::System.Runtime.Versioning.ObsoletedOSPlatform ("android30.0", "These flags are deprecated. Use WindowInsetsController instead.")]
		public SystemUiFlags SystemUiFlags {
			get => (SystemUiFlags) SystemUiVisibility;
			set => SystemUiVisibility = (Android.Views.StatusBarVisibility) value;
		}
#endif
	}
}
