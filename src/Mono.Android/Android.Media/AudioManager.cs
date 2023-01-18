using System;
using Android.Content;
using Android.Runtime;

namespace Android.Media {

	public partial class AudioManager {

		public static AudioManager? FromContext (Context context)
		{
			return context.GetSystemService (Context.AudioService!) as AudioManager;
		}
		
#if ANDROID_26
		// This was converted to an enum in .NET 8
		[Obsolete ("This constant will be removed in the future version. Use Android.Media.Stream enum directly instead of this field.")]
		[Register ("STREAM_ACCESSIBILITY", ApiSince=26)]
		public const int StreamAccessibility = 10;
#endif // ANDROID_26
	}
}


