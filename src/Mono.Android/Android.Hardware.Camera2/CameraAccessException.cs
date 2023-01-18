using System;
using Android.Runtime;

#if ANDROID_23
namespace Android.Hardware.Camera2 
{
    // This was converted to an enum in .NET 8
    partial class CameraAccessException
    {
        [Obsolete ("This constant will be removed in the future version. Use Android.Hardware.Camera2.CameraAccessErrorType enum directly instead of this field.")]
        [Register ("MAX_CAMERAS_IN_USE", ApiSince=23)]
        public const int MaxCamerasInUse = 5;
    }
}
#endif // ANDROID_23
