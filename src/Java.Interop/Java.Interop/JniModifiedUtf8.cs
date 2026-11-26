#nullable enable

using System;
using System.Buffers;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Security.Cryptography;
using System.Text;

namespace Java.Interop {

	static class JniModifiedUtf8 {

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
						var twoBytes = Vector128.GreaterThan (chars, Vector128.Create ((ushort) 0x7f)) |
							Vector128.Equals (chars, Vector128<ushort>.Zero);
						var threeBytes = Vector128.GreaterThan (chars, Vector128.Create ((ushort) 0x7ff));
						count += Vector128<ushort>.Count + Vector128.Sum (Vector128<ushort>.Zero - twoBytes - threeBytes);
						value = value.Slice (Vector128<ushort>.Count);
					}
				}
				foreach (char c in value)
					count += c is > '\0' and <= '\u007f' ? 1 : c <= '\u07ff' ? 2 : 3;
			}
			return count;
		}

		internal static void GetBytes (ReadOnlySpan<char> value, Span<byte> destination)
		{
			int offset = 0;
			if (!value.IsEmpty && value [0] is > '\0' and <= '\u007f') {
				int asciiLength = value.IndexOfAnyExceptInRange ('\u0001', '\u007f');
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
						int scalar = rune.Value - 0x10000;
						WriteChar ((char) (0xd800 + (scalar >> 10)), destination, ref offset);
						WriteChar ((char) (0xdc00 + (scalar & 0x3ff)), destination, ref offset);
						source = source.Slice (index + 4);
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
			while ((index = value.IndexOfAnyExceptInRange ((byte) 0x01, (byte) 0x7f)) >= 0) {
				position += index;
				value = value.Slice (index);
				int length = value [0] switch {
					>= 0xc0 and <= 0xdf => 2,
					>= 0xe0 and <= 0xef => 3,
					>= 0xf0 and <= 0xf7 => 4,
					_ => 0,
				};
				if (length == 0 || value.Length < length ||
						value.Slice (1, length - 1).IndexOfAnyExceptInRange ((byte) 0x80, (byte) 0xbf) >= 0)
					throw new ArgumentException ($"Invalid JNI modified UTF-8 sequence at byte offset {position}.", paramName);
				if (length == 4 && Rune.DecodeFromUtf8 (value.Slice (0, 4), out _, out _) == OperationStatus.Done)
					extra = checked (extra + 2);
				position += length;
				value = value.Slice (length);
			}
			return extra;
		}

		static int IndexOfSupplementary (ReadOnlySpan<byte> value, out Rune rune)
		{
			int offset = 0;
			int index;
			// Only F0..F4 start four-byte scalars; let Rune validate the complete sequence.
			while ((index = value.IndexOfAnyInRange ((byte) 0xf0, (byte) 0xf4)) >= 0) {
				offset += index;
				value = value.Slice (index);
				if (Rune.DecodeFromUtf8 (value, out rune, out _) == OperationStatus.Done)
					return offset;
				// Preserve malformed bytes and existing modified UTF-8 for JNI to handle.
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

		internal IntPtr Pointer { get; }

		internal JniModifiedUtf8String (string value)
		{
			Pointer = owned = JniModifiedUtf8.Alloc (value);
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
