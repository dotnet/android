using System;

using Android.Content;

namespace Android.Hardware {

	public partial class SensorManager {

		public static SensorManager FromContext (Context context)
		{
			return context.GetSystemService (Context.SensorService) as SensorManager;
		}
	}
}


