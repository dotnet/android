using System.Buffers.Binary;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.Hashing;
using System.Runtime.InteropServices;
using System.Text;
using Android.Runtime;

namespace Microsoft.Android.Runtime;

internal static class TypeMapping
{
	internal static bool TryGetType (string javaClassName, [NotNullWhen (true)] out Type? type)
	{
		type = null;

		// the hashes array is sorted and all the hashes are unique
		ulong hash = Hash (javaClassName);
		int typeIndex = MemoryExtensions.BinarySearch (JavaClassNameHashes, hash);
		if (typeIndex < 0) {
			return false;
		}

		// we need to make sure if this is the right match or if it is a hash collision
		if (javaClassName != GetJavaClassNameByTypeIndex (typeIndex)) {
			return false;
		}

		type = GetTypeByIndex (typeIndex);
		if (type is null) {
			throw new InvalidOperationException ($"Type with hash {hash} not found.");
		}

		return true;
	}

	internal static bool TryGetJavaClassName (Type type, [NotNullWhen (true)] out string? className)
	{
		className = null;

		string? name = type.AssemblyQualifiedName;
		if (name is null) {
			className = null;
			return false;
		}

		// the hashes array is sorted and all the hashes are unique
		ulong hash = Hash (name);
		int javaClassNameIndex = MemoryExtensions.BinarySearch (TypeNameHashes, hash);
		if (javaClassNameIndex < 0) {
			return false;
		}

		// we need to make sure if this is the match or if it is a hash collision
		if (name != GetAssemblyQualifiedTypeNameByJavaClassNameIndex (javaClassNameIndex)) {
			return false;
		}

		className = GetJavaClassNameByIndex (javaClassNameIndex);
		if (className is null) {
			throw new InvalidOperationException ($"Java class name with hash {hash} not found.");
		}

		return true;
	}

	private static ulong Hash (string value)
	{
		ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes (value.AsSpan ());
		ulong hash = XxHash3.HashToUInt64 (bytes);

		// The bytes in the hashes array are stored as little endian. If the target platform is big endian,
		// we need to reverse the endianness of the hash.
		if (!BitConverter.IsLittleEndian) {
			hash = BinaryPrimitives.ReverseEndianness (hash);
		}

		return hash;
	}

	// Replaced by src/Microsoft.Android.Sdk.ILLink/TypeMappingStep.cs
	private static ReadOnlySpan<ulong> TypeNameHashes => throw new NotImplementedException ();
	private static Type? GetTypeByIndex (int index) => throw new NotImplementedException ();
	private static string? GetJavaClassNameByTypeIndex (int index) => throw new NotImplementedException ();

	private static ReadOnlySpan<ulong> JavaClassNameHashes => throw new NotImplementedException ();
	private static string? GetJavaClassNameByIndex (int index) => throw new NotImplementedException ();
	private static string? GetAssemblyQualifiedTypeNameByJavaClassNameIndex (int index) => throw new NotImplementedException ();
}
