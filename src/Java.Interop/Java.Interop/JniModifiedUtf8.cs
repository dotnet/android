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
		const byte ThreeByteLeadPrefix = 0xe0;
		const byte ContinuationBytePrefix = 0x80;
		const byte MaxContinuationByte = 0xbf;
		const byte ContinuationPayloadMask = 0x3f;
		const int ContinuationPayloadBits = 6;
		const int SingleByteSequenceLength = 1;
		const int TwoByteSequenceLength = 2;
		const int ThreeByteSequenceLength = 3;
		const int ThreeByteLeadShift = 2 * ContinuationPayloadBits;

		// ART CheckUtfBytes accepts these structural lead-byte ranges, including non-scalar sequences:
		// https://android.googlesource.com/platform/art/+/refs/heads/main/runtime/jni/check_jni.cc
		const byte MaxTwoByteLead = 0xdf;
		const byte MaxThreeByteLead = 0xef;
		const byte FourByteLeadPrefix = 0xf0;
		const byte MaxArtFourByteLead = 0xf7;

		// RFC 3629 section 4 limits standard UTF-8 four-byte scalar leads to F0..F4:
		// https://www.rfc-editor.org/rfc/rfc3629.html#section-4
		const byte MaxUtf8FourByteLead = 0xf4;
		const int FourByteSequenceLength = 4;

		// UTF-16 surrogate encoding, RFC 2781 section 2.1:
		// https://www.rfc-editor.org/rfc/rfc2781.html#section-2.1
		const int SurrogatePairCodeUnitCount = 2;
		const int SupplementaryConversionExtraBytes = SurrogatePairCodeUnitCount * ThreeByteSequenceLength - FourByteSequenceLength;
		const int FirstSupplementaryCodePoint = 0x10000;
		const int HighSurrogateStart = 0xd800;
		const int LowSurrogateStart = 0xdc00;
		const int SurrogatePayloadMask = 0x3ff;
		const int SurrogatePayloadBits = 10;

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
			GetExtraByteCount (unsafe (MemoryMarshal.CreateReadOnlySpanFromNullTerminated (value)), paramName);
		}

		/// <safety>
		/// A non-null value must remain readable and unchanged through its NUL terminator.
		/// </safety>
		internal static unsafe bool RequiresConversion (byte* value, string paramName)
		{
			if (value == null)
				return false;
			// SAFETY: The caller keeps value readable through its terminator for this scan.
			return GetExtraByteCount (unsafe (MemoryMarshal.CreateReadOnlySpanFromNullTerminated (value)), paramName) != 0;
		}

		/// <safety>
		/// A non-null value must remain readable and unchanged through its NUL terminator.
		/// The caller owns a nonzero result and must release it with Free.
		/// </safety>
		internal static unsafe IntPtr AllocConvertedUtf8 (byte* value, string paramName)
		{
			if (value == null)
				return IntPtr.Zero;
			// SAFETY: The caller keeps value readable through its terminator while the bytes are copied.
			var source = unsafe (MemoryMarshal.CreateReadOnlySpanFromNullTerminated (value));
			int extra = GetExtraByteCount (source, paramName);
			if (extra == 0)
				return IntPtr.Zero;

			return AllocUtf8AsModifiedUtf8 (source, extra);
		}

		static IntPtr AllocUtf8AsModifiedUtf8 (ReadOnlySpan<byte> source, int extra)
		{
			int length = checked (source.Length + extra + 1);
			IntPtr result = Marshal.AllocCoTaskMem (length);
			try {
				unsafe {
					// SAFETY: result owns length writable bytes; source remains readable throughout the copy.
					var destination = new Span<byte> ((void*) result, length);
					int offset = 0;
					int index;
					while ((index = IndexOfSupplementary (source, out Rune rune)) >= 0) {
						source.Slice (0, index).CopyTo (destination.Slice (offset));
						offset += index;
						int scalar = rune.Value - FirstSupplementaryCodePoint;
						WriteChar ((char) (HighSurrogateStart + (scalar >> SurrogatePayloadBits)), destination, ref offset);
						WriteChar ((char) (LowSurrogateStart + (scalar & SurrogatePayloadMask)), destination, ref offset);
						source = source.Slice (index + FourByteSequenceLength);
					}
					source.CopyTo (destination.Slice (offset));
					offset += source.Length;
					destination [offset] = 0;
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

		// Accepts exactly what ART CheckJNI's CheckUtfBytes accepts. Release ART decoders trust lead
		// bytes and would read past the NUL of a truncated sequence, so reject before calling JNI.
		// Existing modified UTF-8 needs no conversion; only valid four-byte UTF-8 scalars expand.
		static int GetExtraByteCount (ReadOnlySpan<byte> value, string paramName)
		{
			int extra = 0;
			int position = 0;
			int index;
			while ((index = value.IndexOfAnyExceptInRange (MinNonNullAsciiByte, MaxAsciiByte)) >= 0) {
				position += index;
				value = value.Slice (index);
				int length = value [0] switch {
					>= TwoByteLeadPrefix and <= MaxTwoByteLead => TwoByteSequenceLength,
					>= ThreeByteLeadPrefix and <= MaxThreeByteLead => ThreeByteSequenceLength,
					>= FourByteLeadPrefix and <= MaxArtFourByteLead => FourByteSequenceLength,
					_ => 0,
				};
				if (length == 0 || value.Length < length ||
						value.Slice (1, length - 1).IndexOfAnyExceptInRange (ContinuationBytePrefix, MaxContinuationByte) >= 0)
					throw new ArgumentException ($"Invalid JNI modified UTF-8 sequence at byte offset {position}.", paramName);
				if (length == FourByteSequenceLength && Rune.DecodeFromUtf8 (value.Slice (0, FourByteSequenceLength), out _, out _) == OperationStatus.Done)
					extra = checked (extra + SupplementaryConversionExtraBytes);
				position += length;
				value = value.Slice (length);
			}
			return extra;
		}

		static int IndexOfSupplementary (ReadOnlySpan<byte> value, out Rune rune)
		{
			int offset = 0;
			int index;
			// The lead-byte range is necessary but not sufficient; Rune validates the complete sequence.
			while ((index = value.IndexOfAnyInRange (FourByteLeadPrefix, MaxUtf8FourByteLead)) >= 0) {
				offset += index;
				value = value.Slice (index);
				if (Rune.DecodeFromUtf8 (value, out rune, out _) == OperationStatus.Done)
					return offset;
				// Preserve structurally accepted sequences that are not Unicode scalars.
				offset++;
				value = value.Slice (1);
			}
			rune = default;
			return -1;
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
		/// The address must only be consumed while this wrapper is alive. For caller-provided
		/// source storage, the constructor's lifetime and immutability requirements also apply.
		/// </safety>
		internal unsafe IntPtr Pointer { get; }

		internal JniModifiedUtf8String (string value)
		{
			owned = JniModifiedUtf8.Alloc (value);
			unsafe {
				Pointer = owned;
			}
		}

		/// <safety>
		/// A non-null value must remain readable and unchanged through its NUL terminator
		/// until this instance is disposed. The source storage is never freed.
		/// </safety>
		internal unsafe JniModifiedUtf8String (byte* value)
		{
			// SAFETY: The caller retains the source bytes through conversion and any subsequent JNI call.
			owned = unsafe (JniModifiedUtf8.AllocConvertedUtf8 (value, nameof (value)));
			Pointer = owned == IntPtr.Zero ? (IntPtr) value : owned;
		}

		public void Dispose () => JniModifiedUtf8.Free (owned);
	}
}
