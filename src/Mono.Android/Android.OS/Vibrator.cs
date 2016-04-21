using System;

using Android.Content;

namespace Android.OS {

	public partial class Vibrator {

		public static Vibrator FromContext (Context context)
		{
			return context.GetSystemService (Context.VibratorService) as Vibrator;
		}
	}
}


