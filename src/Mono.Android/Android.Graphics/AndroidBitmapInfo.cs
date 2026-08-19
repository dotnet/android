using System;

namespace Android.Graphics {

#if ANDROID_8
	/// <summary>
	/// Provides information about the pixel buffer of an Android <see cref="Android.Graphics.Bitmap"/>.
	/// This struct corresponds to the native <c>AndroidBitmapInfo</c> type from the Android NDK.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Use this struct with <see cref="Android.Graphics.Bitmap"/> to inspect the dimensions,
	/// stride, and pixel format of a bitmap's underlying pixel buffer.
	/// </para>
	/// <para>
	/// See the <see href="https://developer.android.com/ndk/reference/group/bitmap">Android NDK Bitmap documentation</see>
	/// for more information about the native type.
	/// </para>
	/// </remarks>
	public struct AndroidBitmapInfo : IEquatable<AndroidBitmapInfo> {

		internal uint width, height, stride;
		internal int format;
		internal uint flags;

		/// <summary>
		/// Gets the width of the bitmap in pixels.
		/// </summary>
		public uint Width {
			get {return width;}
		}

		/// <summary>
		/// Gets the height of the bitmap in pixels.
		/// </summary>
		public uint Height {
			get {return height;}
		}

		/// <summary>
		/// Gets the number of bytes between rows in the pixel buffer.
		/// </summary>
		public uint Stride {
			get {return stride;}
		}

		/// <summary>
		/// Gets the pixel format of the bitmap.
		/// </summary>
		public Format Format {
			get {return (Format) format;}
		}

		/// <inheritdoc />
		public override int GetHashCode ()
		{
			return (int) width ^ (int) height ^ (int) stride ^ format ^ (int) flags;
		}

		/// <inheritdoc />
		public override bool Equals (object? value)
		{
			if (!(value is AndroidBitmapInfo))
				return false;
			return Equals ((AndroidBitmapInfo) value);
		}

		/// <summary>
		/// Determines whether the specified <see cref="AndroidBitmapInfo"/> is equal to this instance.
		/// </summary>
		/// <param name="value">The <see cref="AndroidBitmapInfo"/> to compare with this instance.</param>
		/// <returns><see langword="true"/> if the specified value is equal to this instance; otherwise, <see langword="false"/>.</returns>
		public bool Equals (AndroidBitmapInfo value)
		{
			return value.width == width &&
				value.height == height &&
				value.stride == stride &&
				value.format == format &&
				value.flags == flags;
		}
	}
#endif
}
