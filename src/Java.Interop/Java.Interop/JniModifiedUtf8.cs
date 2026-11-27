#nullable enable

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Java.Interop {

	/// <summary>
	/// JNI modified UTF-8 validation.
	/// See <see href="https://docs.oracle.com/en/java/javase/24/docs/specs/jni/types.html#modified-utf-8-strings" />.
	/// </summary>
	static class JniModifiedUtf8 {

		/// <summary>
		/// Validates a NUL-terminated JNI modified UTF-8 byte sequence before passing it to JNI.
		/// </summary>
		/// <safety>
		/// A non-null value must remain readable and unchanged through its NUL terminator.
		/// </safety>
		internal static unsafe void Validate (byte* value, [CallerArgumentExpression (nameof (value))] string? paramName = null)
		{
			if (value == null)
				return;
			// SAFETY: The caller keeps value readable through its terminator for this scan.
			Validate (unsafe (MemoryMarshal.CreateReadOnlySpanFromNullTerminated (value)), paramName);
		}

		/// <summary>
		/// Validates JNI modified UTF-8 and standard 4-byte UTF-8 sequences before JNI reads them
		/// (1-byte <c>0x01..0x7F</c>, 2-byte <c>C0 80</c> or <c>C2..DF 80..BF</c>,
		/// 3-byte <c>E0 A0..BF 80..BF</c> or <c>E1..EF 80..BF 80..BF</c>,
		/// 4-byte <c>F0 90..BF</c> / <c>F1..F3 80..BF</c> / <c>F4 80..8F</c> followed by two <c>80..BF</c>).
		/// Rejects truncated, overlong, out-of-range, and lone-continuation byte sequences.
		/// </summary>
		internal static void Validate (ReadOnlySpan<byte> value, [CallerArgumentExpression (nameof (value))] string? paramName = null)
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
				else if (lead is >= 0xf0 and <= 0xf4 && value.Length >= 4 &&
						value [1] is >= 0x80 and <= 0xbf && value [2] is >= 0x80 and <= 0xbf && value [3] is >= 0x80 and <= 0xbf &&
						(lead != 0xf0 || value [1] >= 0x90) &&
						(lead != 0xf4 || value [1] <= 0x8f))
					seqLen = 4;

				if (seqLen == 0) {
					int position = totalLength - value.Length;
					throw new ArgumentException ($"Invalid JNI modified UTF-8 sequence at byte offset {position}.", paramName);
				}
				value = value.Slice (seqLen);
			}
		}
	}
}
