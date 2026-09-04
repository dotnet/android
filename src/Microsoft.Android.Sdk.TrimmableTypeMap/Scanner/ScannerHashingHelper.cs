using System;
using System.Buffers;
using System.Buffers.Binary;
using Java.Interop.Tools.JavaCallableWrappers;
using Microsoft.Android.Build.Tasks;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

internal static class ScannerHashingHelper
{
	const ulong Crc64Polynomial = 0x42F0E1EBA9EA3693;
	static readonly ulong [] Crc64Lookup = CreateCrc64Lookup ();

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
			return HexUtilities.ToHexString (hash, upperCase: false);
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
		ulong hashValue = BinaryPrimitives.ReverseEndianness (ComputeCrc64 (utf8Buffer.Slice (0, bytesWritten)));
		Span<byte> hash = stackalloc byte [8];
		BinaryPrimitives.WriteUInt64LittleEndian (hash, hashValue ^ (ulong) bytesWritten);
		return HexUtilities.ToHexString (hash, upperCase: false);
	}

	static ulong ComputeCrc64 (ReadOnlySpan<byte> source)
	{
		ulong crc = 0;
		foreach (byte value in source) {
			int index = (byte) ((crc >> 56) ^ value);
			crc = Crc64Lookup [index] ^ (crc << 8);
		}
		return crc;
	}

	static ulong [] CreateCrc64Lookup ()
	{
		var lookup = new ulong [256];
		for (int i = 0; i < lookup.Length; i++) {
			ulong value = (ulong) i << 56;
			for (int bit = 0; bit < 8; bit++) {
				value = (value & 0x8000000000000000) != 0
					? (value << 1) ^ Crc64Polynomial
					: value << 1;
			}
			lookup [i] = value;
		}
		return lookup;
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
}
