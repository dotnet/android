using System;

using Android.Content;

namespace Android.App {

	public partial class AlarmManager {

		public static AlarmManager FromContext (Context context)
		{
			return context.GetSystemService (Context.AlarmService) as AlarmManager;
		}
	}
}


