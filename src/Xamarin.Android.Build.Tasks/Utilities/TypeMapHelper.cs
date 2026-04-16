using System;
using System.Buffers;
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
	/// Hash the given Java type name for use in java-to-managed typemap array (CoreCLR version)
	/// </summary>
	public static ulong HashJavaNameForCLR (string name, bool is64Bit)
	{
		if (name.Length == 0) {
			return UInt64.MaxValue;
		}

		return HashString (name, Encoding.UTF8, is64Bit);
	}

	static ulong HashString (string name, Encoding encoding, bool is64Bit)
	{
		int byteCount = encoding.GetByteCount (name);
		byte[] buffer = ArrayPool<byte>.Shared.Rent (byteCount);
		try {
			encoding.GetBytes (name, 0, name.Length, buffer, 0);
			// Rent may return a larger array, so only hash the actual bytes
			return HashBytes (new ReadOnlySpan<byte> (buffer, 0, byteCount), is64Bit);
		} finally {
			ArrayPool<byte>.Shared.Return (buffer);
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
