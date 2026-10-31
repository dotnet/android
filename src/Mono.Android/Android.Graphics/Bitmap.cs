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
			unsafe {
				// SAFETY: Handle is this Bitmap's JNI peer; the native stub writes one matching AndroidBitmapInfo.
				// CheckNdkError rejects failure before the output is returned; no pixel pointer is accessed.
				AndroidBitmapInfo info;
				int r = JNIEnv.AndroidBitmap_getInfo (Handle, out info);
				CheckNdkError ("AndroidBitmap_getInfo", r);
				return info;
			}
		}

		/// <summary>Locks the bitmap's native pixel buffer for direct access.</summary>
		/// <returns>A pointer to the first byte of the pixel buffer.</returns>
		/// <exception cref="ArgumentException">The native bitmap is invalid.</exception>
		/// <exception cref="InvalidOperationException">The native operation failed.</exception>
		/// <remarks>
		/// Call <see cref="UnlockPixels"/> when access is complete. See the
		/// <see href="https://developer.android.com/ndk/reference/group/bitmap">Android NDK Bitmap documentation</see>.
		/// The returned address does not own the bitmap: retain it, stay within the reported pixel extent,
		/// and synchronize competing accesses until the matching unlock. A failed lock supplies no usable address.
		/// </remarks>
		public IntPtr LockPixels ()
		{
			unsafe {
				// SAFETY: Handle identifies this Bitmap; the stub writes the borrowed pixel address only.
				// CheckNdkError rejects failure before publication; later dereference requires the caller to retain the lock and bitmap.
				IntPtr p;
				int r = JNIEnv.AndroidBitmap_lockPixels (Handle, out p);
				CheckNdkError ("AndroidBitmap_lockPixels", r);
				return p;
			}
		}

		/// <summary>Unlocks the bitmap's native pixel buffer after direct access.</summary>
		/// <exception cref="ArgumentException">The native bitmap is invalid.</exception>
		/// <exception cref="InvalidOperationException">The native operation failed.</exception>
		/// <remarks>See the <see href="https://developer.android.com/ndk/reference/group/bitmap">Android NDK Bitmap documentation</see>.</remarks>
		/// <safety>
		/// Own a successful unmatched pixel lock on this bitmap. Stop all accesses through borrowed pixel
		/// aliases before unlocking, and exclude concurrent disposal or another unlock until this call returns
		/// or throws. An exception does not prove that previously borrowed pixel addresses remain usable.
		/// </safety>
		public unsafe void UnlockPixels ()
		{
			unsafe {
				// SAFETY: the caller owns this Bitmap's matching lock and has stopped pixel-alias access;
				// the native return code is checked without dereferencing any pixel pointer.
				int r = JNIEnv.AndroidBitmap_unlockPixels (Handle);
				CheckNdkError ("AndroidBitmap_unlockPixels", r);
			}
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
