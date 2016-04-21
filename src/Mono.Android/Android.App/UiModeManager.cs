using System;

using Android.Content;

#if ANDROID_8

namespace Android.App {

	public partial class UiModeManager {

		public static UiModeManager FromContext (Context context)
		{
			return context.GetSystemService (Context.UiModeService) as UiModeManager;
		}
	}
}

#endif


