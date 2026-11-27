#nullable enable

using System;
using System.Buffers;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Security.Cryptography;
using System.Text;

namespace Java.Interop {

	static class JniModifiedUtf8 {

		// JNI modified UTF-8 byte patterns:
		// https://docs.oracle.com/en/java/javase/24/docs/specs/jni/types.html#modified-utf-8-strings
		const byte MinNonNullAsciiByte = 0x01;
		const byte MaxAsciiByte = 0x7f;
		const char MaxTwoByteCodeUnit = '\u07ff';
		const byte TwoByteLeadPrefix = 0xc0;
		const byte MinNonNullTwoByteLead = 0xc2;
		const byte MaxTwoByteLead = 0xdf;
		const byte ThreeByteLeadPrefix = 0xe0;
		const byte MaxThreeByteLead = 0xef;
		const byte MinThreeByteE0Continuation = 0xa0;
		const byte FourByteLeadPrefix = 0xf0;
		const byte MaxUtf8FourByteLead = 0xf4;
		const byte ContinuationBytePrefix = 0x80;
		const byte MaxContinuationByte = 0xbf;
		const byte ContinuationPayloadMask = 0x3f;
		const int ContinuationPayloadBits = 6;
		const int SingleByteSequenceLength = 1;
		const int TwoByteSequenceLength = 2;
		const int ThreeByteSequenceLength = 3;
		const int ThreeByteLeadShift = 2 * ContinuationPayloadBits;

		// JNI encodes each UTF-16 code unit, including NUL and both halves of a surrogate pair.
		internal static int GetByteCount (ReadOnlySpan<char> value)
		{
			int count = 0;
			checked {
				if (Vector128.IsHardwareAccelerated) {
					while (value.Length >= Vector128<ushort>.Count) {
						// SAFETY: char and ushort have equal size and alignment and contain no references;
						// the read-only cast preserves the span bounds, checked by Vector128.Create.
						var chars = Vector128.Create (unsafe (MemoryMarshal.Cast<char, ushort> (value)));
						// Like Utf16Utility's byte-count adjustment, but NUL also needs two bytes
						// and surrogate code units each keep their three-byte encoding.
						var twoBytes = Vector128.GreaterThan (chars, Vector128.Create ((ushort) MaxAsciiByte)) |
							Vector128.Equals (chars, Vector128<ushort>.Zero);
						var threeBytes = Vector128.GreaterThan (chars, Vector128.Create ((ushort) MaxTwoByteCodeUnit));
						count += Vector128<ushort>.Count + Vector128.Sum (Vector128<ushort>.Zero - twoBytes - threeBytes);
						value = value.Slice (Vector128<ushort>.Count);
					}
				}
				foreach (char c in value)
					count += c is > '\0' and <= (char) MaxAsciiByte ? SingleByteSequenceLength :
						c <= MaxTwoByteCodeUnit ? TwoByteSequenceLength : ThreeByteSequenceLength;
			}
			return count;
		}

		internal static void GetBytes (ReadOnlySpan<char> value, Span<byte> destination)
		{
			int offset = 0;
			if (!value.IsEmpty && value [0] is > '\0' and <= (char) MaxAsciiByte) {
				int asciiLength = value.IndexOfAnyExceptInRange ((char) MinNonNullAsciiByte, (char) MaxAsciiByte);
				if (asciiLength < 0)
					asciiLength = value.Length;
				if (Ascii.FromUtf16 (value.Slice (0, asciiLength), destination, out offset) != OperationStatus.Done)
					throw new ArgumentException ("Destination is too short.", nameof (destination));
				value = value.Slice (asciiLength);
			}
			foreach (char c in value)
				WriteChar (c, destination, ref offset);
		}

		static void WriteChar (char c, Span<byte> destination, ref int offset)
		{
			if (c is > '\0' and <= (char) MaxAsciiByte) {
				destination [offset++] = (byte) c;
			} else if (c <= MaxTwoByteCodeUnit) {
				destination [offset++] = (byte) (TwoByteLeadPrefix | (c >> ContinuationPayloadBits));
				destination [offset++] = (byte) (ContinuationBytePrefix | (c & ContinuationPayloadMask));
			} else {
				destination [offset++] = (byte) (ThreeByteLeadPrefix | (c >> ThreeByteLeadShift));
				destination [offset++] = (byte) (ContinuationBytePrefix | ((c >> ContinuationPayloadBits) & ContinuationPayloadMask));
				destination [offset++] = (byte) (ContinuationBytePrefix | (c & ContinuationPayloadMask));
			}
		}

		internal static IntPtr Alloc (string value)
		{
			ArgumentNullException.ThrowIfNull (value);
			int length = checked (GetByteCount (value) + 1);
			IntPtr result = Marshal.AllocCoTaskMem (length);
			try {
				unsafe {
					// SAFETY: result owns length writable bytes until Free, including the final terminator.
					var destination = new Span<byte> ((void*) result, length);
					GetBytes (value, destination);
					destination [length - 1] = 0;
				}
				return result;
			} catch {
				unsafe {
					// SAFETY: The allocation is owned here and was not published.
					Marshal.FreeCoTaskMem (result);
				}
				throw;
			}
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

			int position = 0;
			int index;
			while ((index = value.IndexOfAnyExceptInRange (MinNonNullAsciiByte, MaxAsciiByte)) >= 0) {
				position += index;
				value = value.Slice (index);
				byte lead = value [0];
				if (lead == TwoByteLeadPrefix) {
					if (value.Length < TwoByteSequenceLength || value [1] != ContinuationBytePrefix)
						throw new ArgumentException ($"Invalid JNI modified UTF-8 sequence at byte offset {position}.", paramName);
					position += TwoByteSequenceLength;
					value = value.Slice (TwoByteSequenceLength);
				} else if (lead is >= MinNonNullTwoByteLead and <= MaxTwoByteLead) {
					if (value.Length < TwoByteSequenceLength || value [1] is < ContinuationBytePrefix or > MaxContinuationByte)
						throw new ArgumentException ($"Invalid JNI modified UTF-8 sequence at byte offset {position}.", paramName);
					position += TwoByteSequenceLength;
					value = value.Slice (TwoByteSequenceLength);
				} else if (lead is >= ThreeByteLeadPrefix and <= MaxThreeByteLead) {
					if (value.Length < ThreeByteSequenceLength ||
							value [1] is < ContinuationBytePrefix or > MaxContinuationByte ||
							value [2] is < ContinuationBytePrefix or > MaxContinuationByte ||
							(lead == ThreeByteLeadPrefix && value [1] < MinThreeByteE0Continuation))
						throw new ArgumentException ($"Invalid JNI modified UTF-8 sequence at byte offset {position}.", paramName);
					position += ThreeByteSequenceLength;
					value = value.Slice (ThreeByteSequenceLength);
				} else if (lead is >= FourByteLeadPrefix and <= MaxUtf8FourByteLead) {
					throw new ArgumentException (
						$"Invalid JNI modified UTF-8 sequence at byte offset {position}: 4-byte UTF-8 sequences are not valid modified UTF-8. Encode supplementary characters as UTF-16 surrogate pairs or use the string overload.",
						paramName);
				} else {
					throw new ArgumentException ($"Invalid JNI modified UTF-8 sequence at byte offset {position}.", paramName);
				}
			}
		}

		internal static void Free (IntPtr value)
		{
			if (value == IntPtr.Zero)
				return;
			unsafe {
				// SAFETY: Every allocation is NUL-terminated and is released exactly once after its last use.
				byte* bytes = (byte*) value;
				int length = MemoryMarshal.CreateReadOnlySpanFromNullTerminated (bytes).Length;
				CryptographicOperations.ZeroMemory (new Span<byte> (bytes, length));
				Marshal.FreeCoTaskMem (value);
			}
		}
	}

	readonly ref struct JniModifiedUtf8String {
		readonly IntPtr owned;

		/// <safety>
		/// The address must only be consumed while this wrapper is alive.
		/// </safety>
		internal unsafe IntPtr Pointer => owned;

		internal JniModifiedUtf8String (string value)
		{
			owned = JniModifiedUtf8.Alloc (value);
		}

		public void Dispose () => JniModifiedUtf8.Free (owned);
	}
}
