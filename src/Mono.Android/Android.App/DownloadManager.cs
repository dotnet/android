using System;

using Android.Content;

namespace Android.App {

#if ANDROID_9

	public partial class DownloadManager {

		public static DownloadManager FromContext (Context context)
		{
			return context.GetSystemService (Context.DownloadService) as DownloadManager;
		}
	}

#endif

}


