using System;

namespace Xamarin.Android.LegacyThresholdConsumer;

public class LegacyThresholdConsumer : global::Android.App.Activity
{
	public Type ReadBaseThresholdType () => base.ThresholdType;

	public IntPtr ReadBaseThresholdClass () => base.ThresholdClass;
}
