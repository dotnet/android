using System;

using Android.Content;

namespace Android.Telephony {

	public partial class TelephonyManager {

		public static TelephonyManager FromContext (Context context)
		{
			return context.GetSystemService (Context.TelephonyService) as TelephonyManager;
		}
	}
}


