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
	internal static bool TryGetType (string jniName, [NotNullWhen (true)] out Type? type)
	{
		type = null;

		// the hashes array is sorted and all the hashes are unique
		ulong jniNameHash = Hash (jniName);
		int jniNameHashIndex = MemoryExtensions.BinarySearch (JniNameHashes, jniNameHash);
		if (jniNameHashIndex < 0) {
			return false;
		}

		// we need to make sure if this is the right match or if it is a hash collision
		if (jniName != GetJniNameByJniNameHashIndex (jniNameHashIndex)) {
			return false;
		}

		type = GetTypeByJniNameHashIndex (jniNameHashIndex);
		if (type is null) {
			throw new InvalidOperationException ($"Type for {jniName} (hash: {jniNameHash}, index: {jniNameHashIndex}) not found.");
		}

		return true;
	}

	internal static bool TryGetJniName (Type type, [NotNullWhen (true)] out string? jniName)
	{
		jniName = null;

		string? assemblyQualifiedName = type.AssemblyQualifiedName;
		if (assemblyQualifiedName is null) {
			jniName = null;
			return false;
		}

		ReadOnlySpan<char> typeName = GetSimplifiedAssemblyQualifiedTypeName (assemblyQualifiedName);

		// the hashes array is sorted and all the hashes are unique
		ulong typeNameHash = Hash (typeName);
		int typeNameHashIndex = MemoryExtensions.BinarySearch (TypeNameHashes, typeNameHash);
		if (typeNameHashIndex < 0) {
			return false;
		}

		// we need to make sure if this is the match or if it is a hash collision
		if (!typeName.SequenceEqual (GetTypeNameByTypeNameHashIndex (typeNameHashIndex))) {
			return false;
		}

		jniName = GetJniNameByTypeNameHashIndex (typeNameHashIndex);
		if (jniName is null) {
			throw new InvalidOperationException ($"JNI name for {typeName} (hash: {typeNameHash}, index: {typeNameHashIndex}) not found.");
		}

		return true;
	}

	private static ulong Hash (ReadOnlySpan<char> value)
	{
		ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes (value);
		ulong hash = XxHash3.HashToUInt64 (bytes);

		// The bytes in the hashes array are stored as little endian. If the target platform is big endian,
		// we need to reverse the endianness of the hash.
		if (!BitConverter.IsLittleEndian) {
			hash = BinaryPrimitives.ReverseEndianness (hash);
		}

		return hash;
	}

	// This method keeps only the full type name and the simple assembly name.
	// It drops the version, culture, and public key information.
	//
	// For example: "System.Int32, System.Private.CoreLib, Version=9.0.0.0, Culture=neutral, PublicKeyToken=7cec85d7bea7798e"
	//     becomes: "System.Int32, System.Private.CoreLib"
	private static ReadOnlySpan<char> GetSimplifiedAssemblyQualifiedTypeName(string assemblyQualifiedName)
	{
		var commaIndex = assemblyQualifiedName.IndexOf(',');
		var secondCommaIndex = assemblyQualifiedName.IndexOf(',', startIndex: commaIndex + 1);
		return secondCommaIndex < 0
			? assemblyQualifiedName
			: assemblyQualifiedName.AsSpan(0, secondCommaIndex);
	}

	// Replaced by src/Microsoft.Android.Sdk.ILLink/TypeMappingStep.cs
	private static ReadOnlySpan<ulong> TypeNameHashes => throw new NotImplementedException ();
	private static Type? GetTypeByJniNameHashIndex (int jniNameHashIndex) => throw new NotImplementedException ();
	private static string? GetJniNameByJniNameHashIndex (int jniNameHashIndex) => throw new NotImplementedException ();

	private static ReadOnlySpan<ulong> JniNameHashes => throw new NotImplementedException ();
	private static string? GetJniNameByTypeNameHashIndex (int typeNameHashIndex) => throw new NotImplementedException ();
	private static string? GetTypeNameByTypeNameHashIndex (int typeNameHashIndex) => throw new NotImplementedException ();
}
