using System;
using System.Reflection;

using Android.Content;
using Android.Runtime;
using Android.Views;
using Xamarin.Android.Design;

namespace Android.App {

	partial class Activity : ILayoutBindingClient {

		Context ILayoutBindingClient.Context => this;

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

		protected T SetContentView <T> () where T: LayoutBinding
		{
			T items = (T)Activator.CreateInstance (
				type: typeof (T),
				bindingAttr: BindingFlags.CreateInstance | BindingFlags.DeclaredOnly | BindingFlags.NonPublic | BindingFlags.Public,
				binder: null,
				args: new Object[] {this}, // ILayoutBindingClient constructor should be looked up
				culture: null
			);

			SetContentView (items.ResourceLayoutID);
			return items;
		}

		public virtual void OnLayoutViewNotFound <T> (int resourceId, ref T view) where T: View
		{}

		public virtual void OnLayoutFragmentNotFound <T> (int resourceId, ref T fragment) where T: Fragment
		{}
	}
}


