#nullable enable

using System;
using System.Buffers;
using System.Runtime.InteropServices;
using System.Text;

namespace Java.Interop {

	// JNI modified UTF-8 encoding and validation:
	// https://docs.oracle.com/en/java/javase/24/docs/specs/jni/types.html#modified-utf-8-strings
	static class JniModifiedUtf8 {

		// JNI encodes each UTF-16 code unit, including NUL (C0 80) and both halves of a surrogate pair.
		internal static int GetByteCount (ReadOnlySpan<char> value)
		{
			int ascii = value.IndexOfAnyExceptInRange ('\u0001', '\u007f');
			if (ascii < 0)
				return value.Length;

			int count = ascii;
			checked {
				foreach (char c in value.Slice (ascii))
					count += c is > '\0' and <= '\u007f' ? 1 : c <= '\u07ff' ? 2 : 3;
			}
			return count;
		}

		internal static void GetBytes (ReadOnlySpan<char> value, Span<byte> destination)
		{
			int ascii = value.IndexOfAnyExceptInRange ('\u0001', '\u007f');
			if (ascii < 0)
				ascii = value.Length;

			int offset = 0;
			if (ascii > 0) {
				Ascii.FromUtf16 (value.Slice (0, ascii), destination, out offset);
				value = value.Slice (ascii);
			}
			foreach (char c in value) {
				if (c is > '\0' and <= '\u007f') {
					destination [offset++] = (byte) c;
				} else if (c <= '\u07ff') {
					destination [offset++] = (byte) (0xc0 | (c >> 6));
					destination [offset++] = (byte) (0x80 | (c & 0x3f));
				} else {
					destination [offset++] = (byte) (0xe0 | (c >> 12));
					destination [offset++] = (byte) (0x80 | ((c >> 6) & 0x3f));
					destination [offset++] = (byte) (0x80 | (c & 0x3f));
				}
			}
		}

		internal static IntPtr Alloc (string value)
		{
			ArgumentNullException.ThrowIfNull (value);
			int length = checked (GetByteCount (value) + 1);
			IntPtr result = Marshal.AllocCoTaskMem (length);
			unsafe {
				// SAFETY: result owns length writable bytes until Free, including the final terminator.
				var destination = new Span<byte> ((void*) result, length);
				GetBytes (value, destination);
				destination [length - 1] = 0;
			}
			return result;
		}

		/// <safety>
		/// A non-null value must remain readable and unchanged through its NUL terminator.
		/// </safety>
		internal static unsafe void Validate (byte* value, string paramName)
		{
			if (value == null)
				return;
			// SAFETY: The caller keeps value readable through its terminator for this scan.
			Validate (unsafe (MemoryMarshal.CreateReadOnlySpanFromNullTerminated (value)), paramName);
		}

		// Validates strict JNI modified UTF-8 (1-byte 0x01..0x7F, 2-byte C0 80 or C2..DF 80..BF,
		// 3-byte E0 A0..BF 80..BF or E1..EF 80..BF 80..BF). Rejects truncated, overlong, and 4-byte
		// standard UTF-8 sequences before JNI reads them.
		internal static void Validate (ReadOnlySpan<byte> value, string paramName)
		{
			int terminator = value.IndexOf ((byte) 0);
			if (terminator >= 0)
				value = value.Slice (0, terminator);

			int totalLength = value.Length;
			int index;
			while ((index = value.IndexOfAnyExceptInRange ((byte) 0x01, (byte) 0x7f)) >= 0) {
				value = value.Slice (index);
				byte lead = value [0];
				int seqLen = 0;
				if (lead == 0xc0 && value.Length >= 2 && value [1] == 0x80)
					seqLen = 2;
				else if (lead is >= 0xc2 and <= 0xdf && value.Length >= 2 && value [1] is >= 0x80 and <= 0xbf)
					seqLen = 2;
				else if (lead is >= 0xe0 and <= 0xef && value.Length >= 3 &&
						value [1] is >= 0x80 and <= 0xbf && value [2] is >= 0x80 and <= 0xbf &&
						(lead != 0xe0 || value [1] >= 0xa0))
					seqLen = 3;

				if (seqLen == 0) {
					int position = totalLength - value.Length;
					string message = lead is >= 0xf0 and <= 0xf4
						? $"Invalid JNI modified UTF-8 sequence at byte offset {position}: 4-byte UTF-8 sequences are not valid modified UTF-8. Encode supplementary characters as UTF-16 surrogate pairs or use the string overload."
						: $"Invalid JNI modified UTF-8 sequence at byte offset {position}.";
					throw new ArgumentException (message, paramName);
				}
				value = value.Slice (seqLen);
			}
		}

		internal static void Free (IntPtr value)
		{
			unsafe {
				// SAFETY: Every allocation from Alloc is NUL-terminated and released exactly once after its last use.
				Marshal.ZeroFreeCoTaskMemUTF8 (value);
			}
		}
	}

	readonly ref struct JniModifiedUtf8String {
		readonly IntPtr owned;

		/// <safety>
		/// The address must only be consumed while this wrapper is alive.
		/// </safety>
		internal unsafe IntPtr Pointer => owned;

		internal JniModifiedUtf8String (string value) => owned = JniModifiedUtf8.Alloc (value);

		public void Dispose () => JniModifiedUtf8.Free (owned);
	}
}
