using System;

using Android.Content;

namespace Android.Media {

	public partial class AudioManager {

		public static AudioManager FromContext (Context context)
		{
			return context.GetSystemService (Context.AudioService) as AudioManager;
		}
	}
}


