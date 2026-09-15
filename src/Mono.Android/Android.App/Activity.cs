using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Versioning;
using Android.Runtime;

namespace Android.App {

	partial class Activity {

		public T? FindViewById<
				[DynamicallyAccessedMembers (Constructors)]
				T
		> (int id)
			where T : Android.Views.View
		{
			return this.FindViewById (id)!.JavaCast<T> ();
		}

		// See: https://cs.android.com/android/platform/superproject/+/master:frameworks/base/core/java/android/app/Activity.java;l=3430
		public T RequireViewById<
				[DynamicallyAccessedMembers (Constructors)]
				T
		> (int id)
			where T : Android.Views.View
		{
			var view = FindViewById<T> (id);
			if (view == null) {
				throw new Java.Lang.IllegalArgumentException ($"Parameter 'id' of value 0x{id:X} does not reference a View of type '{typeof (T)}' inside this Activity");
			}
			return view;
		}

		public void StartActivityForResult (Type activityType, int requestCode)
		{
			var intent = new Android.Content.Intent (this, activityType);
			StartActivityForResult (intent, requestCode);
		}

		public void RunOnUiThread (Action action)
		{
			RunOnUiThread (new Java.Lang.Thread.RunnableImplementor (action));
		}

		[SupportedOSPlatform ("android19.0")]
		[Register ("reportFullyDrawn", "()V", "GetReportFullyDrawnHandler")]
		public virtual unsafe void ReportFullyDrawn ()
		{
			const string id = "reportFullyDrawn.()V";
			try {
				_members.InstanceMethods.InvokeVirtualVoidMethod (id, this, null);
			} finally {
				StartupNoGCRegion.End ();
			}
		}

		static Delegate? cb_reportFullyDrawn_ReportFullyDrawn_V;

		static Delegate GetReportFullyDrawnHandler ()
		{
			return cb_reportFullyDrawn_ReportFullyDrawn_V ??= new _JniMarshal_PP_V (n_ReportFullyDrawn);
		}

		static void n_ReportFullyDrawn (IntPtr jnienv, IntPtr native__this)
		{
			unsafe {
				Java.Interop.JniMarshal.SafeInvokeAction (jnienv, native__this, &__n_ReportFullyDrawn);
			}
		}

		static void __n_ReportFullyDrawn (IntPtr jnienv, IntPtr native__this)
		{
			var activity = Java.Lang.Object.GetObject<Activity> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			if (activity == null) {
				throw new InvalidOperationException ("Could not obtain the managed Activity instance for reportFullyDrawn.");
			}
			activity.ReportFullyDrawn ();
		}
	}
}
