using System;

using Android.Content;

namespace Android.App {

	public partial class NotificationManager {

		public static NotificationManager FromContext (Context context)
		{
			return context.GetSystemService (Context.NotificationService) as NotificationManager;
		}
	}
}


