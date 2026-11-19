using System;
using Android.AccessibilityServices;
using Android.OS;

namespace Android.Views.Accessibility
{
	public partial class AccessibilityNodeProvider
	{
		public bool PerformAction (int virtualViewId, GlobalAction action, Bundle arguments)
		{
			return PerformAction (virtualViewId, (Action) action, arguments);
		}
	}
}

