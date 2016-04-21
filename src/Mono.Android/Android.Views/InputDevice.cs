using System;

namespace Android.Views {

#if !ANDROID_12
	public enum Axis {
    Orientation   = MotionRange.Orientation,
    Pressure      = MotionRange.Pressure,
    Size          = MotionRange.Size,
    ToolMajor     = MotionRange.ToolMajor,
    ToolMinor     = MotionRange.ToolMinor,
    TouchMajor    = MotionRange.TouchMajor,
    TouchMinor    = MotionRange.TouchMinor,
		X             = MotionRange.X,
		Y             = MotionRange.Y,
	}
#endif

	partial class InputDevice {

		[Obsolete ("Please use GetMotionRange(Android.Views.Axis)")]
		public Android.Views.InputDevice.MotionRange GetMotionRange (int rangeType)
		{
			return GetMotionRange ((Axis) rangeType);
		}
	}
}