using System;
using System.IO.Hashing;
using System.Text;

namespace Xamarin.Android.Tasks;

static class TypeMapHelper
{
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
	/// Hash the given type name for use in CoreCLR native typemap arrays.
	/// </summary>
	public static unsafe uint HashNameForCLR (string name)
	{
		if (name.Length == 0) {
			return UInt32.MaxValue;
		}

		int byteCount = Encoding.UTF8.GetByteCount (name);
		Span<byte> buffer = byteCount <= 256
			? stackalloc byte [byteCount]
			: new byte [byteCount];
		fixed (char* pChars = name)
		fixed (byte* pBuffer = buffer) {
			Encoding.UTF8.GetBytes (pChars, name.Length, pBuffer, byteCount);
		}
		return Crc32.HashToUInt32 (buffer);
	}

	/// <summary>
	/// Hash the given bytes for use in CoreCLR native lookup tables.
	/// </summary>
	public static uint HashBytesForCLR (ReadOnlySpan<byte> bytes)
	{
		if (bytes.Length == 0) {
			return UInt32.MaxValue;
		}

		return Crc32.HashToUInt32 (bytes);
	}

	// Java type names are always ASCII and typically 20-100 characters,
	// so the encoded byte count is well within stackalloc limits.
	// The unsafe Encoding.GetBytes(char*, int, byte*, int) overload is
	// used because the Span-based overload requires netstandard2.1+.
	static unsafe ulong HashString (string name, Encoding encoding, bool is64Bit)
	{
		int byteCount = encoding.GetByteCount (name);
		Span<byte> buffer = byteCount <= 256
			? stackalloc byte [byteCount]
			: new byte [byteCount];
		fixed (char* pChars = name)
		fixed (byte* pBuffer = buffer) {
			encoding.GetBytes (pChars, name.Length, pBuffer, byteCount);
		}
		return HashBytes (buffer, is64Bit);
	}

	static ulong HashBytes (ReadOnlySpan<byte> bytes, bool is64Bit)
	{
		if (is64Bit) {
			return XxHash3.HashToUInt64 (bytes);
		}

		return (ulong)XxHash32.HashToUInt32 (bytes);
	}
}
