using System;
using System.Buffers;
using System.Buffers.Binary;
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
		int byteCount = GetNamespaceAssemblyUtf8ByteCount (ns, assemblyName);
		byte[] utf8Buffer = new byte [byteCount];

		int bytesWritten = GetNamespaceAssemblyUtf8Bytes (ns, assemblyName, utf8Buffer);
		// Avoid Span overloads here: their assembly identity differs between netstandard and netCore System.IO.Hashing assets.
		byte[] hash = System.IO.Hashing.Crc64.Hash (utf8Buffer);
		ulong hashValue = BinaryPrimitives.ReadUInt64LittleEndian (hash);
		BinaryPrimitives.WriteUInt64LittleEndian (hash, hashValue ^ (ulong) bytesWritten);
		return ToHexString (hash);
	}

	static int GetNamespaceAssemblyUtf8ByteCount (string ns, string assemblyName)
	{
		return System.Text.Encoding.UTF8.GetByteCount (ns) + 1 + System.Text.Encoding.UTF8.GetByteCount (assemblyName);
	}

	static unsafe int GetNamespaceAssemblyUtf8Bytes (string ns, string assemblyName, Span<byte> destination)
	{
		int bytesWritten = 0;
		fixed (char* nsPtr = ns)
		fixed (byte* destinationPtr = destination) {
			bytesWritten += System.Text.Encoding.UTF8.GetBytes (nsPtr, ns.Length, destinationPtr, destination.Length);
		}

		destination [bytesWritten++] = (byte) ':';

		fixed (char* assemblyNamePtr = assemblyName)
		fixed (byte* destinationPtr = destination) {
			bytesWritten += System.Text.Encoding.UTF8.GetBytes (assemblyNamePtr, assemblyName.Length, destinationPtr + bytesWritten, destination.Length - bytesWritten);
		}

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
