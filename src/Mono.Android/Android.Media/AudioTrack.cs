using System;

namespace Android.Media
{
	public partial class AudioTrack
	{
		[Obsolete ("ChannelConfiguration is obsolete. Please use another overload with ChannelOut instead")]
		public AudioTrack ([global::Android.Runtime.GeneratedEnum] Android.Media.Stream streamType, int sampleRateInHz, [global::Android.Runtime.GeneratedEnum] Android.Media.ChannelConfiguration channelConfig, [global::Android.Runtime.GeneratedEnum] Android.Media.Encoding audioFormat, int bufferSizeInBytes, [global::Android.Runtime.GeneratedEnum] Android.Media.AudioTrackMode mode)
			: this (streamType, sampleRateInHz, (ChannelOut) (int) channelConfig, audioFormat, bufferSizeInBytes, mode)
		{
		}
#if ANDROID_9
		[Obsolete ("ChannelConfiguration is obsolete. Please use another overload with ChannelOut instead")]
		public AudioTrack ([global::Android.Runtime.GeneratedEnum] Android.Media.Stream streamType, int sampleRateInHz, [global::Android.Runtime.GeneratedEnum] Android.Media.ChannelConfiguration channelConfig, [global::Android.Runtime.GeneratedEnum] Android.Media.Encoding audioFormat, int bufferSizeInBytes, [global::Android.Runtime.GeneratedEnum] Android.Media.AudioTrackMode mode, int sessionId)
			: this (streamType, sampleRateInHz, (ChannelOut) (int) channelConfig, audioFormat, bufferSizeInBytes, mode, sessionId)
		{
		}
#endif
	}
}

