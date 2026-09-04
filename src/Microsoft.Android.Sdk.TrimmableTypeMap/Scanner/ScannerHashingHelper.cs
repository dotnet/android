using System;
using System.Buffers;
using System.Buffers.Binary;
using Java.Interop.Tools.JavaCallableWrappers;
using Microsoft.Android.Build.Tasks;

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
			return HexUtilities.ToHexString (hash, upperCase: false);
		} finally {
			ArrayPool<byte>.Shared.Return (rented);
		}
	}

	internal static string ToCrc64 (string ns, string assemblyName)
	{
		int byteCount = GetNamespaceAssemblyUtf8ByteCount (ns, assemblyName);
		var utf8Buffer = new byte [byteCount];
		int bytesWritten = GetNamespaceAssemblyUtf8Bytes (ns, assemblyName, utf8Buffer);
		var hasher = new System.IO.Hashing.Crc64 ();
		// MSBuild can load the netstandard2.0 hashing assembly first, whose Span signatures
		// cannot bind from this net11.0 assembly. The array API is compatible in both contexts.
		hasher.Append (utf8Buffer);
		ulong hashValue = BinaryPrimitives.ReverseEndianness (hasher.GetCurrentHashAsUInt64 ());
		Span<byte> hash = stackalloc byte [8];
		BinaryPrimitives.WriteUInt64LittleEndian (hash, hashValue ^ (ulong) bytesWritten);
		return HexUtilities.ToHexString (hash, upperCase: false);
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
