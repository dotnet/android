using System;

using Android.Content;

namespace Android.Views.Accessibility {

	public partial class AccessibilityManager {

		public static AccessibilityManager FromContext (Context context)
		{
			return context.GetSystemService (Context.AccessibilityService) as AccessibilityManager;
		}
	}
}


