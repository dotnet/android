using System;
using System.IO.Hashing;
using System.Text;

namespace Xamarin.Android.Tasks;

static class TypeMapHelper
{
	/// <summary>
	/// The largest buffer callers of <see cref="GetBytes"/> should <c>stackalloc</c> before falling
	/// back to the heap.  Matches the threshold used elsewhere in the SDK, e.g.
	/// <c>ScannerHashingHelper</c> in Microsoft.Android.Sdk.TrimmableTypeMap.
	/// </summary>
	public const int StackallocThresholdBytes = 256;

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
	public static uint HashNameForCLR (string name)
	{
		if (name.Length == 0) {
			return UInt32.MaxValue;
		}

		int byteCount = Encoding.UTF8.GetByteCount (name);
		Span<byte> buffer = byteCount <= StackallocThresholdBytes
			? stackalloc byte [byteCount]
			: new byte [byteCount];
		GetBytes (name, Encoding.UTF8, buffer);
		return Crc32.HashToUInt32 (buffer);
	}

	/// <summary>
	/// Encodes <paramref name="value"/> into <paramref name="buffer"/>, which must be at least
	/// <c>encoding.GetByteCount (value)</c> bytes long.  Callers allocate the buffer themselves so
	/// that short strings can use <c>stackalloc</c>.
	/// </summary>
	// The unsafe Encoding.GetBytes(char*, int, byte*, int) overload is used because the
	// Span-based overload requires netstandard2.1+.
	public static unsafe void GetBytes (string value, Encoding encoding, Span<byte> buffer)
	{
		if (value.Length == 0) {
			return;
		}

		fixed (char* pChars = value)
		fixed (byte* pBuffer = buffer) {
			encoding.GetBytes (pChars, value.Length, pBuffer, buffer.Length);
		}
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
	static ulong HashString (string name, Encoding encoding, bool is64Bit)
	{
		int byteCount = encoding.GetByteCount (name);
		Span<byte> buffer = byteCount <= StackallocThresholdBytes
			? stackalloc byte [byteCount]
			: new byte [byteCount];
		GetBytes (name, encoding, buffer);
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
