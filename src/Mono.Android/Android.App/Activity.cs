using System;

using Android.Runtime;

namespace Android.App {

	partial class Activity {

		public T FindViewById<T> (int id)
			where T : Android.Views.View
		{
			return this.FindViewById (id).JavaCast<T> ();
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


