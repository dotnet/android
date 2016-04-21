using System;

using Android.Content;

#if ANDROID_9

namespace Android.OS.Storage {

	public partial class StorageManager {

		public static StorageManager FromContext (Context context)
		{
			return context.GetSystemService (Context.StorageService) as StorageManager;
		}
	}
}

#endif


