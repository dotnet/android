using System;

using Android.App;
using Android.Views;

namespace Xamarin.Android.Design
{
	public abstract class LayoutBinding
	{
		ILayoutBindingClient client;

		public abstract int ResourceLayoutID { get; }

		protected LayoutBinding (ILayoutBindingClient client)
		{
			this.client = client ?? throw new ArgumentNullException (nameof (client));
		}

		protected T FindView <T> (int resourceId, ref T cachedField) where T: View
		{
			if (cachedField != null)
				return cachedField;

			T ret = client.FindViewById <T> (resourceId);
			if (ret == null)
				client.OnLayoutViewNotFound <T> (resourceId, ref ret);

			if (ret == null)
				throw new global::System.InvalidOperationException ($"View not found (Client: {client}; Layout ID: {ResourceLayoutID}; Resource ID: {resourceId})");

			cachedField = ret;
			return ret;
		}

		protected T FindFragment <T> (int resourceId, ref T cachedField) where T: Fragment
		{
			if (cachedField != null)
				return cachedField;

			var activity = client.Context as Activity;
			if (activity == null)
				throw new InvalidOperationException ("Finding fragments is supported only for Activity instances");

			T ret = activity.FragmentManager.FindFragmentById<T> (resourceId);
			if (ret == null)
				client.OnLayoutFragmentNotFound (resourceId, ref ret);

			if (ret == null)
				throw new InvalidOperationException ($"Fragment not found (ID: {resourceId})");

			cachedField = ret;
			return ret;
		}
	}
}
