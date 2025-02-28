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
	private static Dictionary<Type, string[]> s_javaClassNames = new ();

	internal static bool TryGetType (string javaClassName, [NotNullWhen (true)] out Type? type)
	{
		ulong hash = Hash (javaClassName);

		// the hashes array is sorted and all the hashes are unique
		int index = MemoryExtensions.BinarySearch (Hashes, hash);
		if (index < 0) {
			type = null;
			return false;
		}

		// ensure this is not a hash collision
		string resolvedJavaClassName = GetJavaClassNameByIndex (index);
		if (!resolvedJavaClassName.Equals (javaClassName, StringComparison.Ordinal)) {
			type = null;
			return false;
		}

		type = GetTypeByIndex (index);
		return type is not null;
	}

	internal static IEnumerable<string> GetJavaClassNames (Type type)
	{
		if (!s_javaClassNames.TryGetValue (type, out var javaClassNames)) {
			javaClassNames = GetJavaClassNamesSlow (type).ToArray ();
			_ = s_javaClassNames.TryAdd (type, javaClassNames);
		}

		return javaClassNames;
	}

	private static IEnumerable<string> GetJavaClassNamesSlow (Type type)
	{
		for (int i = 0; i < Hashes.Length; i++) {
			if (GetTypeByIndex (i) == type) {
				var javaClassName = TypeMapping.GetJavaClassNameByIndex (i);
				if (javaClassName is not null) {
					yield return javaClassName;
				}
			}
		}
	}

	private static ulong Hash (string javaClassName)
	{
		ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes(javaClassName.AsSpan ());
		ulong hash = XxHash3.HashToUInt64 (bytes);

		// The bytes in the hashes array are stored as little endian. If the target platform is big endian,
		// we need to reverse the endianness of the hash.
		if (!BitConverter.IsLittleEndian) {
			hash = BinaryPrimitives.ReverseEndianness (hash);
		}

		return hash;
	}

	// Replaced by src/Microsoft.Android.Sdk.ILLink/TypeMappingStep.cs
	private static ReadOnlySpan<ulong> Hashes => throw new NotImplementedException ();
	private static Type? GetTypeByIndex (int index) => throw new NotImplementedException ();
	private static string? GetJavaClassNameByIndex (int index) => throw new NotImplementedException ();
}
