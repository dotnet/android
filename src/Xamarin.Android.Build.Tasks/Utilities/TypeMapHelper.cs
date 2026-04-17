using System;
using System.Buffers;
using System.IO.Hashing;
using System.Text;

namespace Xamarin.Android.Tasks;

static class TypeMapHelper
{
	const int MaxStackallocBytes = 512;
	/// <summary>
	/// Hash the given Java type name for use in java-to-managed typemap array (MonoVM version)
	/// </summary>
	public static ulong HashJavaName (string name, bool is64Bit)
	{
		if (name.Length == 0) {
			return UInt64.MaxValue;
		}

		// Native code (EmbeddedAssemblies::typemap_java_to_managed in embedded-assemblies.cc) will operate on wchar_t cast to a byte array, we need to do
		// the same
		return HashString (name, Encoding.Unicode, is64Bit);
	}

	/// <summary>
	/// Hash the given Java type name for use in java-to-managed typemap array (CoreCLR version)
	/// </summary>
	public static ulong HashJavaNameForCLR (string name, bool is64Bit)
	{
		if (name.Length == 0) {
			return UInt64.MaxValue;
		}

		return HashString (name, Encoding.UTF8, is64Bit);
	}

	// Java type names are always ASCII and typically 20-100 characters,
	// so the encoded byte count is well within stackalloc limits.
	// The unsafe Encoding.GetBytes(char*, int, byte*, int) overload is
	// used because the Span-based overload requires netstandard2.1+.
	static unsafe ulong HashString (string name, Encoding encoding, bool is64Bit)
	{
		int byteCount = encoding.GetByteCount (name);
		byte[]? rented = null;
		Span<byte> buffer = byteCount <= MaxStackallocBytes
			? stackalloc byte [byteCount]
			: (rented = ArrayPool<byte>.Shared.Rent (byteCount)).AsSpan (0, byteCount);
		try {
			fixed (char* pChars = name)
			fixed (byte* pBuffer = buffer) {
				encoding.GetBytes (pChars, name.Length, pBuffer, byteCount);
			}
			return HashBytes (buffer, is64Bit);
		} finally {
			if (rented != null) {
				ArrayPool<byte>.Shared.Return (rented);
			}
		}
	}

	static ulong HashBytes (ReadOnlySpan<byte> bytes, bool is64Bit)
	{
		if (is64Bit) {
			return XxHash3.HashToUInt64 (bytes);
		}

		return (ulong)XxHash32.HashToUInt32 (bytes);
	}
}
