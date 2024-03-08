using System;

using Android.Content;

namespace Android.OS {

	public partial class Vibrator {

		[global::System.Runtime.Versioning.ObsoletedOSPlatform ("android31.0", "Use VibratorManager to retrieve the default system vibrator.")]
		public static Vibrator? FromContext (Context context)
		{
			return context.GetSystemService (Context.VibratorService!) as Vibrator;
		}
	}
}


