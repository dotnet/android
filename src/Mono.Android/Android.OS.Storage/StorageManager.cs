using System;

using Android.Content;


namespace Android.OS.Storage {

	public partial class StorageManager {

		public static StorageManager? FromContext (Context context)
		{
			return context.GetSystemService (Context.StorageService!) as StorageManager;
		}
	}
}
