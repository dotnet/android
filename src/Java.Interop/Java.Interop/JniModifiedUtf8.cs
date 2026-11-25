#nullable enable

using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Java.Interop {

	static class JniModifiedUtf8 {

		// JNI encodes each UTF-16 code unit, including NUL and both halves of a surrogate pair.
		internal static int GetByteCount (ReadOnlySpan<char> value)
		{
			int count = 0;
			checked {
				foreach (char c in value)
					count += c is > '\0' and <= '\u007f' ? 1 : c <= '\u07ff' ? 2 : 3;
			}
			return count;
		}

		internal static void GetBytes (ReadOnlySpan<char> value, Span<byte> destination)
		{
			int offset = 0;
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
		internal static unsafe bool RequiresConversion (byte* value)
		{
			if (value == null)
				return false;
			// SAFETY: The caller keeps value readable through its terminator for this scan.
			return GetExtraByteCount (unsafe (MemoryMarshal.CreateReadOnlySpanFromNullTerminated (value))) != 0;
		}

		/// <safety>
		/// A non-null value must remain readable and unchanged through its NUL terminator.
		/// The caller owns a nonzero result and must release it with Free.
		/// </safety>
		internal static unsafe IntPtr AllocConvertedUtf8 (byte* value)
		{
			if (value == null)
				return IntPtr.Zero;
			// SAFETY: The caller keeps value readable through its terminator while the bytes are copied.
			var source = unsafe (MemoryMarshal.CreateReadOnlySpanFromNullTerminated (value));
			int extra = GetExtraByteCount (source);
			if (extra == 0)
				return IntPtr.Zero;

			int length = checked (source.Length + extra + 1);
			IntPtr result = Marshal.AllocCoTaskMem (length);
			try {
				unsafe {
					// SAFETY: result owns length writable bytes; source remains readable throughout the copy.
					var destination = new Span<byte> ((void*) result, length);
					int offset = 0;
					for (int i = 0; i < source.Length; i++) {
						if (TryReadSupplementary (source, i, out int scalar)) {
							scalar -= 0x10000;
							WriteChar ((char) (0xd800 + (scalar >> 10)), destination, ref offset);
							WriteChar ((char) (0xdc00 + (scalar & 0x3ff)), destination, ref offset);
							i += 3;
						} else {
							destination [offset++] = source [i];
						}
					}
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

		static int GetExtraByteCount (ReadOnlySpan<byte> value)
		{
			// Existing modified UTF-8 needs no conversion; only valid four-byte UTF-8 scalars expand.
			int extra = 0;
			for (int i = 0; i < value.Length; i++) {
				if (TryReadSupplementary (value, i, out _)) {
					extra = checked (extra + 2);
					i += 3;
				}
			}
			return extra;
		}

		static bool TryReadSupplementary (ReadOnlySpan<byte> value, int offset, out int scalar)
		{
			scalar = 0;
			int first = value [offset];
			if (first < 0xf0 || first > 0xf4 || offset > value.Length - 4)
				return false;

			int second = value [offset + 1];
			int third = value [offset + 2];
			int fourth = value [offset + 3];
			if ((second & 0xc0) != 0x80 || (third & 0xc0) != 0x80 || (fourth & 0xc0) != 0x80)
				return false;

			scalar = ((first & 0x07) << 18) | ((second & 0x3f) << 12) | ((third & 0x3f) << 6) | (fourth & 0x3f);
			return scalar is >= 0x10000 and <= 0x10ffff;
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
			owned = unsafe (JniModifiedUtf8.AllocConvertedUtf8 (value));
			Pointer = owned == IntPtr.Zero ? (IntPtr) value : owned;
		}

		public void Dispose () => JniModifiedUtf8.Free (owned);
	}
}
