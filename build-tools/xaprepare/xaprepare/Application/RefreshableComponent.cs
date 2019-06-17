using System;
namespace Xamarin.Android.Prepare
{
	[Flags]
	public enum RefreshableComponent
	{
		None = 0,
		AndroidSDK = 1,
		AndroidNDK = 2,
		All = AndroidSDK | AndroidNDK
	}
}
