using System;
using Android.AccessibilityServices;
using Android.OS;

namespace Android.Views
{
	public partial class View
	{
		public partial class AccessibilityDelegate
		{
#if ANDROID_16
			[Obsolete ("This method takes incorrect enum type. Please use PerformAccessibilityAction() overload with Action instead.")]
			public bool PerformAccessibilityAction (View host, GlobalAction action, Bundle args)
			{
				return PerformAccessibilityAction (host, (Android.Views.Accessibility.Action) (int) action, args);
			}
		}
#endif
	}
}

