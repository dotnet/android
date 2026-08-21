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

		/// <summary>Gets information about the bitmap's native pixel buffer.</summary>
		/// <returns>An <see cref="AndroidBitmapInfo"/> containing the buffer dimensions, stride, and pixel format.</returns>
		/// <exception cref="ArgumentException">The native bitmap is invalid.</exception>
		/// <exception cref="InvalidOperationException">The native operation failed.</exception>
		/// <remarks>See the <see href="https://developer.android.com/ndk/reference/group/bitmap">Android NDK Bitmap documentation</see>.</remarks>
		public AndroidBitmapInfo GetBitmapInfo ()
		{
			AndroidBitmapInfo info;
			int r = JNIEnv.AndroidBitmap_getInfo (Handle, out info);
			CheckNdkError ("AndroidBitmap_getInfo", r);
			return info;
		}

		/// <summary>Locks the bitmap's native pixel buffer for direct access.</summary>
		/// <returns>A pointer to the first byte of the pixel buffer.</returns>
		/// <exception cref="ArgumentException">The native bitmap is invalid.</exception>
		/// <exception cref="InvalidOperationException">The native operation failed.</exception>
		/// <remarks>
		/// Call <see cref="UnlockPixels"/> when access is complete. See the
		/// <see href="https://developer.android.com/ndk/reference/group/bitmap">Android NDK Bitmap documentation</see>.
		/// </remarks>
		public IntPtr LockPixels ()
		{
			IntPtr p;
			int r = JNIEnv.AndroidBitmap_lockPixels (Handle, out p);
			CheckNdkError ("AndroidBitmap_lockPixels", r);
			return p;
		}

		/// <summary>Unlocks the bitmap's native pixel buffer after direct access.</summary>
		/// <exception cref="ArgumentException">The native bitmap is invalid.</exception>
		/// <exception cref="InvalidOperationException">The native operation failed.</exception>
		/// <remarks>See the <see href="https://developer.android.com/ndk/reference/group/bitmap">Android NDK Bitmap documentation</see>.</remarks>
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
