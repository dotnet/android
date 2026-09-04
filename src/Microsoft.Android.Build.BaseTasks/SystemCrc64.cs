using System;

namespace Microsoft.Android.Build.Tasks;

static class SystemCrc64
{
	public static unsafe void Hash (byte* source, int sourceLength, byte* destination, int destinationLength)
	{
		if (sourceLength < 0) {
			throw new ArgumentOutOfRangeException (nameof (sourceLength));
		}
		if (destinationLength < 0) {
			throw new ArgumentOutOfRangeException (nameof (destinationLength));
		}

		System.IO.Hashing.Crc64.Hash (
			new ReadOnlySpan<byte> (source, sourceLength),
			new Span<byte> (destination, destinationLength));
	}
}
