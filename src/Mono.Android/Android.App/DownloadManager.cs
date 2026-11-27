using System;

using Android.Content;

namespace Android.App {


	public partial class DownloadManager {

		public static DownloadManager? FromContext (Context context)
		{
			return (context.GetSystemService (Context.DownloadService) as DownloadManager);
		}
	}


}


