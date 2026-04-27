using System;
using Java.Interop.Tools.JavaCallableWrappers;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

internal static class ScannerHashingHelper
{
	internal static string ToLegacyCrc64 (string ns, string assemblyName)
	{
		int byteCount = GetNamespaceAssemblyUtf8ByteCount (ns, assemblyName);
		byte[] utf8Buffer = new byte [byteCount];
		int bytesWritten = GetNamespaceAssemblyUtf8Bytes (ns, assemblyName, utf8Buffer);
		ulong crc = ulong.MaxValue;
		ulong length = 0;
		Crc64Helper.HashCore (utf8Buffer, 0, bytesWritten, ref crc, ref length);
		Span<byte> hash = stackalloc byte [8];
		WriteUInt64LittleEndian (hash, crc ^ length);
		return ToHexString (hash, lowercase: true);
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
		return ToHexString (hash, lowercase: true);
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

	static string ToHexString (ReadOnlySpan<byte> hash, bool lowercase)
	{
		const int maxStackCharLength = 128;
		int charLength = hash.Length * 2;
		Span<char> chars = charLength <= maxStackCharLength
			? stackalloc char [charLength]
			: new char [charLength];

		for (int i = 0, j = 0; i < hash.Length; i += 1, j += 2) {
			byte b = hash [i];
			chars [j] = GetHexValue (b / 16, lowercase);
			chars [j + 1] = GetHexValue (b % 16, lowercase);
		}

		return ((ReadOnlySpan<char>) chars).ToString ();
	}

	static void WriteUInt64LittleEndian (Span<byte> destination, ulong value)
	{
		destination [0] = (byte) value;
		destination [1] = (byte) (value >> 8);
		destination [2] = (byte) (value >> 16);
		destination [3] = (byte) (value >> 24);
		destination [4] = (byte) (value >> 32);
		destination [5] = (byte) (value >> 40);
		destination [6] = (byte) (value >> 48);
		destination [7] = (byte) (value >> 56);
	}

	static char GetHexValue (int value, bool lowercase)
	{
		return (char) (value < 10
			? value + '0'
			: value - 10 + (lowercase ? 'a' : 'A'));
	}
}
