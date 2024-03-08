using System;

namespace Android.Media
{
	public partial class AudioTrack
	{
		[global::System.Runtime.Versioning.ObsoletedOSPlatform ("android26.0")]
		[Obsolete ("ChannelConfiguration is obsolete. Please use another overload with ChannelOut instead")]
		public AudioTrack ([global::Android.Runtime.GeneratedEnum] Android.Media.Stream streamType, int sampleRateInHz, [global::Android.Runtime.GeneratedEnum] Android.Media.ChannelConfiguration channelConfig, [global::Android.Runtime.GeneratedEnum] Android.Media.Encoding audioFormat, int bufferSizeInBytes, [global::Android.Runtime.GeneratedEnum] Android.Media.AudioTrackMode mode)
			: this (streamType, sampleRateInHz, (ChannelOut) (int) channelConfig, audioFormat, bufferSizeInBytes, mode)
		{
		}
#if ANDROID_9
		[global::System.Runtime.Versioning.ObsoletedOSPlatform ("android26.0")]
		[Obsolete ("ChannelConfiguration is obsolete. Please use another overload with ChannelOut instead")]
		public AudioTrack ([global::Android.Runtime.GeneratedEnum] Android.Media.Stream streamType, int sampleRateInHz, [global::Android.Runtime.GeneratedEnum] Android.Media.ChannelConfiguration channelConfig, [global::Android.Runtime.GeneratedEnum] Android.Media.Encoding audioFormat, int bufferSizeInBytes, [global::Android.Runtime.GeneratedEnum] Android.Media.AudioTrackMode mode, int sessionId)
			: this (streamType, sampleRateInHz, (ChannelOut) (int) channelConfig, audioFormat, bufferSizeInBytes, mode, sessionId)
		{
		}
#endif

#if ANDROID_29
		[Obsolete ("Please do not use Android.Media.AudioTrack.RoutingChangedEventArgs since it was wrongly generated and it is not used internally.", error: true)]
		public partial class RoutingChangedEventArgs : global::System.EventArgs {

			public RoutingChangedEventArgs (Android.Media.AudioTrack audioTrack)
			{
				this.audioTrack = audioTrack;
			}

			Android.Media.AudioTrack audioTrack;
			public Android.Media.AudioTrack AudioTrack {
				get { return audioTrack; }
			}
		}
#endif  // ANDROID_29
	}
}

