using System;

using Android.Content;

namespace Android.OS {

	public partial class PowerManager {

		public static PowerManager FromContext (Context context)
		{
			return context.GetSystemService (Context.PowerService) as PowerManager;
		}
	}
}


