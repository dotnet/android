using System;
using System.Buffers;
using System.IO;

namespace Xamarin.Android.Application.Utilities;

static class Util
{
	public static readonly ArrayPool<byte> BytePool = ArrayPool<byte>.Shared;

	public static void CreateFileDirectory (string filePath)
	{
		string fileDir = Path.GetDirectoryName (filePath) ?? String.Empty;
		if (fileDir.Length > 0) {
			Directory.CreateDirectory (fileDir);
		}
	}

	public static ulong GetPaddedSize<S> (ulong sizeSoFar, bool is64Bit)
	{
		ulong typeSize = Util.GetNativeTypeSize<S> (is64Bit);
		if (typeSize == 1) {
			return 1;
		}

		ulong modulo;
		if (is64Bit) {
			modulo = typeSize < 8 ? 4u : 8u;
		} else {
			modulo = 4u;
		}

		ulong alignment = sizeSoFar % modulo;
		if (alignment == 0)
			return typeSize;

		return typeSize + (modulo - alignment);
	}

	public static ulong GetNativeTypeSize<S> (bool is64Bit)
	{
		Type type = typeof(S);

		if (type == typeof(string)) {
			// We treat `string` as a generic pointer
			return is64Bit ? 8u : 4u;
		}

		if (type == typeof(byte)) {
			return 1u;
		}

		if (type == typeof(bool)) {
			return 1u;
		}

		if (type == typeof(Int32) || type == typeof(UInt32)) {
			return 4u;
		}

		if (type == typeof(Int64) || type == typeof(UInt64)) {
			return 8u;
		}

		throw new InvalidOperationException ($"Unable to map managed type {type} to native assembler type");
	}

	public static string ToStringOrNull<T> (T? reference) => reference == null ? "<NULL>" : reference.ToString () ?? "[unknown]";

	public static string YesNo (bool yes) => yes ? "yes" : "no";
}
