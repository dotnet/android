using System;

namespace Android.Views {


	partial class InputDevice {

		[Obsolete ("Please use GetMotionRange(Android.Views.Axis)")]
		public Android.Views.InputDevice.MotionRange? GetMotionRange (int rangeType)
		{
			return GetMotionRange ((Axis) rangeType);
		}
	}
}
