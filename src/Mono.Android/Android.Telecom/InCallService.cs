using System;
using Android.Content;

namespace Android.Telecom
{
	public abstract partial class InCallService : Android.App.Service
	{
#if ANDROID_23
		[Obsolete ("Incorrect enum parameter, use the overload that takes a CallAudioRoute parameter instead.")]
		[global::System.Runtime.Versioning.ObsoletedOSPlatform ("android34.0")]
		[global::System.Runtime.Versioning.SupportedOSPlatformAttribute ("android23.0")]
		public void SetAudioRoute ([global::Android.Runtime.GeneratedEnum] Android.Telecom.VideoQuality route)
		{
			SetAudioRoute ((CallAudioRoute) route);
		}
#endif
	}
}


