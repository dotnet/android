using System;

using Android.Content;

namespace Android.App.Admin {


	public partial class DevicePolicyManager {

		public static DevicePolicyManager? FromContext (Context context)
		{
			return context.GetSystemService (Context.DevicePolicyService) as DevicePolicyManager;
		}
	}


}


