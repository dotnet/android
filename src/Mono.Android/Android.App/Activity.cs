using System;

using Android.Runtime;

namespace Android.App {

	partial class Activity {

		public T? FindViewById<T> (int id)
			where T : Android.Views.View
		{
			return this.FindViewById (id)!.JavaCast<T> ();
		}

		// See: https://cs.android.com/android/platform/superproject/+/master:frameworks/base/core/java/android/view/View.java;l=25322
		public T RequireViewById<T> (int id)
			where T : Android.Views.View
		{
			var view = FindViewById<T> (id);
			if (view == null)
			{
				throw new ArgumentException ($"ID 0x{id:X} does not reference a View of type '{typeof (T)}' inside this View");
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
	}
}


