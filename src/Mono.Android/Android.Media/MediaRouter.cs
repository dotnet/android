using System;
using Android.Runtime;

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

