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
		ulong hash = Hash (javaClassName);

		// the hashes array is sorted and all the hashes are unique
		int typeIndex = MemoryExtensions.BinarySearch (JavaClassNameHashes, hash);
		if (typeIndex < 0) {
			type = null;
			return false;
		}

		type = GetTypeByIndex (typeIndex);
		if (type is null) {
			throw new InvalidOperationException ($"Type with hash {hash} not found.");
		}

		// ensure this is not a hash collision
		var resolvedJavaClassName = GetJavaClassNameByIndex (TypeIndexToJavaClassNameIndex [typeIndex]);
		if (resolvedJavaClassName != javaClassName) {
			type = null;
			return false;
		}

		return true;
	}

	internal static bool TryGetJavaClassName (Type type, [NotNullWhen (true)] out string? className)
	{
		string name = $"{type.FullName}, {type.Assembly.GetName ().Name}";
		ulong hash = Hash (name);

		// the hashes array is sorted and all the hashes are unique
		int javaClassNameIndex = MemoryExtensions.BinarySearch (TypeNameHashes, hash);
		if (javaClassNameIndex < 0) {
			className = null;
			return false;
		}

		className = GetJavaClassNameByIndex (javaClassNameIndex);
		if (className is null) {
			throw new InvalidOperationException ($"Java class name with hash {hash} not found.");
		}

		// ensure this is not a hash collision
		var resolvedType = GetTypeByIndex (JavaClassNameIndexToTypeIndex [javaClassNameIndex]);
		if (resolvedType?.AssemblyQualifiedName != type.AssemblyQualifiedName) {
			className = null;
			return false;
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
	private static ReadOnlySpan<ulong> JavaClassNameHashes => throw new NotImplementedException ();
	private static ReadOnlySpan<ulong> TypeNameHashes => throw new NotImplementedException ();
	private static ReadOnlySpan<int> JavaClassNameIndexToTypeIndex => throw new NotImplementedException ();
	private static ReadOnlySpan<int> TypeIndexToJavaClassNameIndex => throw new NotImplementedException ();
	private static Type? GetTypeByIndex (int index) => throw new NotImplementedException ();
	private static string? GetJavaClassNameByIndex (int index) => throw new NotImplementedException ();
}
