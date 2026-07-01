using System;
using System.IO;
using System.Net;

namespace Xamarin.Android.Tools {

	static class StreamCoda {

		internal static byte ReadNetworkByte (this Stream stream)
		{
			int v   = stream.ReadByte ();
			if (v < 0)
				throw new BadImageFormatException ();
			return (byte) v;
		}

		internal static ushort ReadNetworkUInt16 (this Stream stream)
		{
			var a   = stream.ReadNetworkByte ();
			var b   = stream.ReadNetworkByte ();
			return (ushort) ((a << 8) | b);
		}

		internal static uint ReadNetworkUInt32 (this Stream stream)
		{
			var a   = stream.ReadNetworkByte ();
			var b   = stream.ReadNetworkByte ();
			var c   = stream.ReadNetworkByte ();
			var d   = stream.ReadNetworkByte ();
			return (uint) ((uint) (a << 24)) |
				((uint) (b << 16)) |
				((uint) (c << 8)) |
				((uint) d);
		}

		internal static long ReadNetworkInt64 (this Stream stream)
		{
			var hi  = (long) stream.ReadNetworkUInt32 ();
			var lo  = (long) stream.ReadNetworkUInt32 ();
			return ((hi << 32) + lo);
		}
	}
}

