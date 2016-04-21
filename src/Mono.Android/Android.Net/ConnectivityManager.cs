using System;

using Android.Content;

namespace Android.Net {

	public partial class ConnectivityManager {

		public static ConnectivityManager FromContext (Context context)
		{
			return context.GetSystemService (Context.ConnectivityService) as ConnectivityManager;
		}
	}
}


