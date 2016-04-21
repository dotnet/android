using System;

using Android.Content;

namespace Android.OS {

#if ANDROID_8

	public partial class DropBoxManager {

		public static DropBoxManager FromContext (Context context)
		{
			return context.GetSystemService (Context.DropboxService) as DropBoxManager;
		}
	}

#endif  // ANDROID_8
}


