using System;
using Android.Runtime;

namespace Android.Media
{

	public partial class AudioRecord
	{

#if ANDROID_29
		[Obsolete ("Please do not use Android.Media.AudioRecord.RoutingChangedEventArgs since it was wrongly generated and it is not used internally.", error: true)]
		public partial class RoutingChangedEventArgs : global::System.EventArgs {

			public RoutingChangedEventArgs (Android.Media.AudioRecord audioRecord)
			{
				this.audioRecord = audioRecord;
			}

			Android.Media.AudioRecord audioRecord;
			public Android.Media.AudioRecord AudioRecord {
				get { return audioRecord; }
			}
		}
#endif  // ANDROID_29
	}
}
