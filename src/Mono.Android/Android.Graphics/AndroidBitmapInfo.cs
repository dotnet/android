using System;

namespace Android.Graphics {

#if ANDROID_8
	public struct AndroidBitmapInfo : IEquatable<AndroidBitmapInfo> {

		internal uint width, height, stride;
		internal int format;
		internal uint flags;

		public uint Width {
			get {return width;}
		}

		public uint Height {
			get {return height;}
		}

		public uint Stride {
			get {return stride;}
		}

		public Format Format {
			get {return (Format) format;}
		}

		public override int GetHashCode ()
		{
			return (int) width ^ (int) height ^ (int) stride ^ format ^ (int) flags;
		}

		public override bool Equals (object value)
		{
			if (!(value is AndroidBitmapInfo))
				return false;
			return Equals ((AndroidBitmapInfo) value);
		}

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
