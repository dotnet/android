#nullable enable

using System;

namespace Xamarin.Android.Tasks.JniRemapping
{
	/// <summary>
	/// ECMA-335 II.23.2 compressed unsigned integer encode/decode helpers, used when
	/// re-serializing CustomAttribute value blobs by hand.
	/// </summary>
	static class MetadataEncoding
	{
		public static byte [] EncodeCompressedInteger (int value)
		{
			if (value < 0) {
				throw new ArgumentOutOfRangeException (nameof (value));
			}
			if (value <= 0x7F) {
				return new [] { (byte) value };
			}
			if (value <= 0x3FFF) {
				return new [] {
					(byte) (0x80 | (value >> 8)),
					(byte) (value & 0xFF),
				};
			}
			if (value <= 0x1FFFFFFF) {
				return new [] {
					(byte) (0xC0 | (value >> 24)),
					(byte) ((value >> 16) & 0xFF),
					(byte) ((value >> 8) & 0xFF),
					(byte) (value & 0xFF),
				};
			}
			throw new ArgumentOutOfRangeException (nameof (value), "Value too large to encode as an ECMA-335 compressed integer.");
		}

		/// <summary>
		/// Reads the compressed integer at <paramref name="offset"/>, returning its width in
		/// bytes and (via <paramref name="value"/>) the decoded value.
		/// </summary>
		public static int ReadCompressedInteger (byte [] data, int offset, out int value)
		{
			if (offset < 0 || offset >= data.Length) {
				throw new JniRewriteException ("Malformed metadata blob: truncated compressed integer.");
			}

			byte first = data [offset];
			if ((first & 0x80) == 0) {
				value = first;
				return 1;
			}
			if ((first & 0xC0) == 0x80) {
				RequireLength (data, offset, 2);
				value = ((first & 0x3F) << 8) | data [offset + 1];
				return 2;
			}
			if ((first & 0xE0) == 0xC0) {
				RequireLength (data, offset, 4);
				value = ((first & 0x1F) << 24) | (data [offset + 1] << 16) | (data [offset + 2] << 8) | data [offset + 3];
				return 4;
			}

			throw new JniRewriteException ("Malformed metadata blob: invalid compressed integer prefix.");
		}

		static void RequireLength (byte [] data, int offset, int width)
		{
			if (offset + width > data.Length) {
				throw new JniRewriteException ("Malformed metadata blob: truncated compressed integer.");
			}
		}
	}
}
