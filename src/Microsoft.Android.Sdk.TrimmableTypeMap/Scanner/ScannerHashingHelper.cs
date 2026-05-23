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
			int bytesWritten = GetNamespaceAssemblyUtf8Bytes (ns, assemblyName, rented);
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
		int byteCount = GetNamespaceAssemblyUtf8ByteCount (ns, assemblyName);
		byte[] rented = ArrayPool<byte>.Shared.Rent (byteCount);
		try {
			int bytesWritten = GetNamespaceAssemblyUtf8Bytes (ns, assemblyName, rented);
			Span<byte> hash = stackalloc byte [8];
			System.IO.Hashing.Crc64.Hash (rented.AsSpan (0, bytesWritten), hash);
			ulong hashValue = BinaryPrimitives.ReadUInt64LittleEndian (hash);
			BinaryPrimitives.WriteUInt64LittleEndian (hash, hashValue ^ (ulong) bytesWritten);
			return ToHexString (hash);
		} finally {
			ArrayPool<byte>.Shared.Return (rented);
		}
	}

	static int GetNamespaceAssemblyUtf8ByteCount (string ns, string assemblyName)
	{
		return System.Text.Encoding.UTF8.GetByteCount (ns) + 1 + System.Text.Encoding.UTF8.GetByteCount (assemblyName);
	}

	static int GetNamespaceAssemblyUtf8Bytes (string ns, string assemblyName, byte[] destination)
	{
		int bytesWritten = Encoding.UTF8.GetBytes (ns, 0, ns.Length, destination, 0);

		destination [bytesWritten++] = (byte) ':';

		bytesWritten += Encoding.UTF8.GetBytes (assemblyName, 0, assemblyName.Length, destination, bytesWritten);

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
