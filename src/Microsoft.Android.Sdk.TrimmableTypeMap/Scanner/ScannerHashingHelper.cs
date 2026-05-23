using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using Java.Interop.Tools.JavaCallableWrappers;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

internal static class ScannerHashingHelper
{
	internal static string ToLegacyCrc64 (string ns, string assemblyName)
	{
		int byteCount = GetNamespaceAssemblyUtf8ByteCount (ns, assemblyName);
		byte[] rented = ArrayPool<byte>.Shared.Rent (byteCount);
		try {
			int bytesWritten = GetNamespaceAssemblyUtf8Bytes (ns, assemblyName, rented.AsSpan (0, byteCount));
			ulong crc = ulong.MaxValue;
			ulong length = 0;
			Crc64Helper.HashCore (rented, 0, bytesWritten, ref crc, ref length);
			Span<byte> hash = stackalloc byte [8];
			BinaryPrimitives.WriteUInt64LittleEndian (hash, crc ^ length);
			return ToHexString (hash);
		} finally {
			ArrayPool<byte>.Shared.Return (rented);
		}
	}

	internal static string ToCrc64 (string ns, string assemblyName)
	{
		const int stackallocThresholdBytes = 256;
		int byteCount = GetNamespaceAssemblyUtf8ByteCount (ns, assemblyName);
		Span<byte> utf8Buffer = byteCount <= stackallocThresholdBytes
			? stackalloc byte [stackallocThresholdBytes]
			: new byte [byteCount];

		int bytesWritten = GetNamespaceAssemblyUtf8Bytes (ns, assemblyName, utf8Buffer.Slice (0, byteCount));
		Span<byte> hash = stackalloc byte [8];
		System.IO.Hashing.Crc64.Hash (utf8Buffer.Slice (0, bytesWritten), hash);
		ulong hashValue = BinaryPrimitives.ReadUInt64LittleEndian (hash);
		BinaryPrimitives.WriteUInt64LittleEndian (hash, hashValue ^ (ulong) bytesWritten);
		return ToHexString (hash);
	}

	static int GetNamespaceAssemblyUtf8ByteCount (string ns, string assemblyName)
	{
		return System.Text.Encoding.UTF8.GetByteCount (ns) + 1 + System.Text.Encoding.UTF8.GetByteCount (assemblyName);
	}

	static int GetNamespaceAssemblyUtf8Bytes (string ns, string assemblyName, Span<byte> destination)
	{
		byte[] nsBytes = Encoding.UTF8.GetBytes (ns);
		nsBytes.AsSpan ().CopyTo (destination);
		int bytesWritten = nsBytes.Length;

		destination [bytesWritten++] = (byte) ':';

		byte[] assemblyNameBytes = Encoding.UTF8.GetBytes (assemblyName);
		assemblyNameBytes.AsSpan ().CopyTo (destination.Slice (bytesWritten));
		bytesWritten += assemblyNameBytes.Length;

		return bytesWritten;
	}

	static string ToHexString (ReadOnlySpan<byte> hash)
	{
		const int maxStackCharLength = 128;
		int charLength = hash.Length * 2;
		Span<char> chars = charLength <= maxStackCharLength
			? stackalloc char [charLength]
			: new char [charLength];

		for (int i = 0, j = 0; i < hash.Length; i += 1, j += 2) {
			byte b = hash [i];
			chars [j] = GetHexValue (b / 16);
			chars [j + 1] = GetHexValue (b % 16);
		}

		return ((ReadOnlySpan<char>) chars).ToString ();
	}

	static char GetHexValue (int value) => (char) (value < 10 ? value + '0' : value - 10 + 'a');
}
