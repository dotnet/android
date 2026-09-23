using System;

using Android.Content;

namespace Android.Hardware {

	public partial class SensorManager {

		/// <summary>Gets the <see cref="SensorManager" /> system service from a <see cref="Context" />.</summary>
		/// <param name="context">The context used to retrieve the system service.</param>
		/// <returns>The <see cref="SensorManager" /> instance, or <see langword="null" /> if the service is unavailable.</returns>
		/// <seealso href="https://developer.android.com/reference/android/hardware/SensorManager">Android documentation</seealso>
		public static SensorManager? FromContext (Context context)
		{
			return context.GetSystemService (Context.SensorService!) as SensorManager;
		}
	}
}

