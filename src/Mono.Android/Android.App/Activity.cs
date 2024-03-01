using System;
using System.Diagnostics.CodeAnalysis;
using Android.Runtime;

namespace Android.App {

	partial class Activity {

		internal const DynamicallyAccessedMemberTypes Constructors = DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors;

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
	}
}


