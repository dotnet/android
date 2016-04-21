using System;
using System.Runtime.InteropServices;

using Android.Runtime;

namespace Android.Graphics {

	partial class Bitmap {

#if ANDROID_8
		enum NdkError {
			Success           = 0,
			BadParameter      = -1,
			JniException      = -2,
			AllocationFailed  = -3,
		}

		static void CheckNdkError (string method, int r)
		{
			switch ((NdkError) r) {
				case NdkError.Success:
					break;
				case NdkError.BadParameter:
					throw new ArgumentException (method + " failed! error=" + r);
				case NdkError.JniException:
				case NdkError.AllocationFailed:
					throw new InvalidOperationException (method + " failed! error=" + ((NdkError) r));
				default:
					throw new InvalidOperationException (method + " failed! error=" + r);
			}
		}

		public AndroidBitmapInfo GetBitmapInfo ()
		{
			AndroidBitmapInfo info;
			int r = JNIEnv.AndroidBitmap_getInfo (Handle, out info);
			CheckNdkError ("AndroidBitmap_getInfo", r);
			return info;
		}

		public IntPtr LockPixels ()
		{
			IntPtr p;
			int r = JNIEnv.AndroidBitmap_lockPixels (Handle, out p);
			CheckNdkError ("AndroidBitmap_lockPixels", r);
			return p;
		}

		public void UnlockPixels ()
		{
			int r = JNIEnv.AndroidBitmap_unlockPixels (Handle);
			CheckNdkError ("AndroidBitmap_unlockPixels", r);
		}
#endif  // ANDROID_8
#if ANDROID_19
		[Obsolete ("Use the IsPremultiplied property getter or the SetPremultiplied(bool) method.")]
		public bool Premultiplied {
			get {return IsPremultiplied;}
			set {SetPremultiplied (value);}
		}
#endif
	}
}
