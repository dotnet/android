using System;

using Android.Runtime;

namespace Android.Media {

	partial class MediaPlayer {

#if ANDROID_17
		[Obsolete ("This constant will be removed in the future version. Use Android.Media.MediaError.Io.")]
		public const int MediaErrorIo = (int) MediaError.Io;

		[Obsolete ("This constant will be removed in the future version. Use Android.Media.MediaError.Malformed.")]
		public const int MediaErrorMalformed = (int) MediaError.Malformed;

		[Obsolete ("This constant will be removed in the future version. Use Android.Media.MediaError.TimedOut.")]
		public const int MediaErrorTimedOut = (int) MediaError.TimedOut;

		[Obsolete ("This constant will be removed in the future version. Use Android.Media.MediaError.Unsupported.")]
		public const int MediaErrorUnsupported = (int) MediaError.Unsupported;
#endif
	}
}

