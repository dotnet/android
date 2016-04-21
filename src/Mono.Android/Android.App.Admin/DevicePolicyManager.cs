using System;

using Android.Content;

namespace Android.App.Admin {

#if ANDROID_8

	public partial class DevicePolicyManager {

		public static DevicePolicyManager FromContext (Context context)
		{
			return context.GetSystemService (Context.DevicePolicyService) as DevicePolicyManager;
		}
	}

#endif

}


