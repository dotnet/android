using System;

using Android.Content;

namespace Android.App {

	public partial class NotificationManager {

		/// <summary>Gets the <see cref="NotificationManager" /> system service from a <see cref="Context" />.</summary>
		/// <param name="context">The context used to retrieve the system service.</param>
		/// <returns>The <see cref="NotificationManager" /> instance, or <see langword="null" /> if the service is unavailable.</returns>
		/// <seealso href="https://developer.android.com/reference/android/app/NotificationManager">Android documentation</seealso>
		public static NotificationManager? FromContext (Context context)
		{
			return context.GetSystemService (Context.NotificationService!) as NotificationManager;
		}
	}
}

