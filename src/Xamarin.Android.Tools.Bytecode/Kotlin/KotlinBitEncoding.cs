using System;
using System.IO;
using System.Linq;

namespace Xamarin.Android.Tools.Bytecode
{
	// https://github.com/JetBrains/kotlin/blob/master/core/metadata.jvm/src/org/jetbrains/kotlin/metadata/jvm/deserialization/BitEncoding.java
	public static class KotlinBitEncoding
	{
		const char UTF8_MODE_MARKER = (char) 0;
		const char _8TO7_MODE_MARKER = unchecked((char) -1);

		public static byte [] DecodeBytes (string [] data)
		{
			if (data.Length > 0 && !string.IsNullOrEmpty (data [0])) {
				var possibleMarker = data [0] [0];

				if (possibleMarker == UTF8_MODE_MARKER)
					return StringsToBytes (DropMarker (data));

				if (possibleMarker == _8TO7_MODE_MARKER)
					data = DropMarker (data);
			}

			var bytes = CombineStringArrayIntoBytes (data);

			// Adding 0x7f modulo max byte value is equivalent to subtracting 1 the same modulo, which is inverse to what happens in encodeBytes
			AddModuloByte (bytes, 0x7f);

			return Decode7to8 (bytes);
		}

		public static int ReadRawVarint32 (Stream input)
		{
			var firstByte = input.ReadByte ();

			if ((firstByte & 128) == 0)
				return firstByte;

			var result = firstByte & 127;

			int offset;
			int b;

			for (offset = 7; offset < 32; offset += 7) {

				b = input.ReadByte ();

				if (b == -1)
					throw new InvalidDataException ("Unable to read varint32 from stream");

				result |= (b & 127) << offset;

				if ((b & 128) == 0)
					return result;
			}

			while (offset < 64) {

				b = input.ReadByte ();

				if (b == -1)
					throw new InvalidDataException ("Unable to read varint32 from stream");

				if ((b & 128) == 0)
					return result;

				offset += 7;
			}

			throw new InvalidDataException ("Unable to read varint32 from stream");
		}

		static string [] DropMarker (string [] data)
		{
			// Clone because the clients should be able to use the passed array for their own purposes.
			// This is cheap because the size of the array is 1 or 2 almost always.
			var result = (string []) data.Clone ();
			result [0] = result [0].Substring (1);

			return result;
		}

		static byte [] StringsToBytes (string [] strings)
		{
			var length = strings.Sum (s => s.Length);
			var result = new byte [length];

			var i = 0;

			foreach (var s in strings)
				foreach (var c in s)
					result [i++] = (byte) c;

			return result;
		}

		static byte [] CombineStringArrayIntoBytes (string [] data)
		{
			var resultLength = 0;

			foreach (var s in data)
				resultLength += s.Length;

			var result = new byte [resultLength];
			var p = 0;

			foreach (var s in data)
				for (int i = 0, n = s.Length; i < n; i++)
					result [p++] = (byte) s [i];

			return result;
		}

		static void AddModuloByte (byte [] data, int increment)
		{
			for (int i = 0, n = data.Length; i < n; i++) {
				data [i] = (byte) ((data [i] + increment) & 0x7f);
			}
		}

		static byte [] Decode7to8 (byte [] data)
		{
			// floor(7 * data.length / 8)
			var resultLength = 7 * data.Length / 8;

			var result = new byte [resultLength];

			// We maintain a pointer to an input bit in the same fashion as in encode8to7(): it's represented as two numbers: index of the
			// current byte in the input and index of the bit in the byte
			var byteIndex = 0;
			var bit = 0;

			// A resulting byte is comprised of 8 bits, starting from the current bit. Since each input byte only "contains 7 bytes", a
			// resulting byte always consists of two parts: several most significant bits of the current byte and several least significant bits
			// of the next byte
			for (var i = 0; i < resultLength; i++) {
				var firstPart = (int) (((uint) (data [byteIndex] & 0xff)) >> bit);

				byteIndex++;

				var secondPart = (data [byteIndex] & ((1 << (bit + 1)) - 1)) << 7 - bit;

				result [i] = (byte) (firstPart + secondPart);

				if (bit == 6) {
					byteIndex++;
					bit = 0;
				} else {
					bit++;
				}
			}

			return result;
		}
	}
}
