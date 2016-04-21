using System;

using Android.Content;

namespace Android.Net.Wifi {

	public partial class WifiManager {

		public static WifiManager FromContext (Context context)
		{
			return context.GetSystemService (Context.WifiService) as WifiManager;
		}
	}
}


