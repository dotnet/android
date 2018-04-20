using System;

using Android.App;
using Android.Content;
using Android.Views;

namespace Xamarin.Android.Design
{
	public interface ILayoutBindingClient
	{
		Context Context { get; }

		T FindViewById<T> (int value) where T : View;
		void OnLayoutViewNotFound <T> (int resourceId, ref T view) where T: View;
		void OnLayoutFragmentNotFound <T> (int resourceId, ref T fragment) where T: Fragment;
	}
}
