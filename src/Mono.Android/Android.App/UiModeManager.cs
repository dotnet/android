using System;

using Android.Content;


namespace Android.App {

	public partial class UiModeManager {

		public static UiModeManager? FromContext (Context context)
		{
			return context.GetSystemService (Context.UiModeService!) as UiModeManager;
		}
	}
}
