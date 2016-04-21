using System;
using Android.Runtime;

#if ANDROID_16
namespace Android.Media {

	partial class MediaRouter {

		partial class UserRouteInfo {

			[Obsolete ("Use SetPlaybackStream(Android.Media.Stream)")]
			public void SetPlaybackStream (int stream)
			{
				SetPlaybackStream ((Stream) stream);
			}
		}
	}
}
#endif  // ANDROID_16

