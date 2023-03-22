using System;
using System.Buffers;
using System.IO;

namespace Xamarin.Android.Application.Utilities;

static class Util
{
	public static readonly ArrayPool<byte> BytePool = ArrayPool<byte>.Shared;
	public static ILogger? Log;

	public static void CreateFileDirectory (string filePath)
	{
		string fileDir = Path.GetDirectoryName (filePath) ?? String.Empty;
		if (fileDir.Length > 0) {
			Directory.CreateDirectory (fileDir);
		}
	}

	static ulong GetPadding<S> (ulong sizeSoFar, bool is64Bit, out ulong typeSize)
	{
		typeSize = Util.GetNativeTypeSize<S> (is64Bit);
		if (typeSize == 1) {
			return 0;
		}

		ulong modulo;
		if (is64Bit) {
			modulo = typeSize < 8 ? 4u : 8u;
		} else {
			modulo = 4u;
		}

		ulong alignment = sizeSoFar % modulo;
		if (alignment == 0) {
			return 0;
		}

		return modulo - alignment;
	}

	public static ulong GetPadding<S> (ulong sizeSoFar, bool is64Bit)
	{
		return GetPadding<S> (sizeSoFar, is64Bit, out ulong _);
	}

	public static ulong GetPaddedSize<S> (ulong sizeSoFar, bool is64Bit)
	{
		ulong padding = GetPadding<S> (sizeSoFar, is64Bit, out ulong typeSize);

		if (padding == 0) {
			return typeSize;
		}

		return typeSize + padding;
	}

	public static ulong GetNativeTypeSize<S> (bool is64Bit)
	{
		Type type = typeof(S);

		if (type == typeof(string) || type == typeof(IntPtr)) {
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

	/// <summary>
	/// When reading binary data for C++ structures from ELF images, we need to account for field alignment.
	/// This method calculates the number of actual bytes read from the data stream, as well as properly
	/// adjusts the stream position to read the next field correctly.
	/// </summary>
	public static ulong GetSizeAndAdjustPosition <T> (BinaryReader reader, ulong sizeSoFar, bool is64Bit)
	{
		ulong typeSize = GetNativeTypeSize<T> (is64Bit);
		ulong paddedSize = GetPaddedSize<T> (sizeSoFar, is64Bit);

		if (paddedSize == 0) {
			throw new InvalidOperationException ("Padded size must not be 0");
		}

		if (paddedSize < typeSize) {
			throw new InvalidOperationException ("Padded size must not be smaller than type size");
		}

		if (paddedSize == typeSize) {
			return typeSize;
		}

		ulong seekOffset = paddedSize - typeSize;
		reader.BaseStream.Seek ((long)seekOffset, SeekOrigin.Current);

		return paddedSize;
	}

	/// <summary>
	/// Read data from a C++ structure <c>bool</c> field and adjust stream position accordingly.
	/// </summary>
	/// <seealso cref="GetSizeAndAdjustPosition"/>
	/// <returns>Number of actual bytes read (including padding)</returns>
	public static ulong ReadField (BinaryReader reader, ref bool field, ulong sizeSoFar, bool is64Bit)
	{
		ulong ret = GetSizeAndAdjustPosition<bool> (reader, sizeSoFar, is64Bit);
		field = reader.ReadBoolean ();
		return ret;
	}

	/// <summary>
	/// Read data from a C++ structure <c>byte</c> field and adjust stream position accordingly.
	/// </summary>
	/// <seealso cref="GetSizeAndAdjustPosition"/>
	/// <returns>Number of actual bytes read (including padding)</returns>
	public static ulong ReadField (BinaryReader reader, ref byte field, ulong sizeSoFar, bool is64Bit)
	{
		ulong ret = GetSizeAndAdjustPosition<byte> (reader, sizeSoFar, is64Bit);
		field = reader.ReadByte ();
		return ret;
	}

	/// <summary>
	/// Read data from a C++ structure <c>uint</c> field and adjust stream position accordingly.
	/// </summary>
	/// <seealso cref="GetSizeAndAdjustPosition"/>
	/// <returns>Number of actual bytes read (including padding)</returns>
	public static ulong ReadField (BinaryReader reader, ref uint field, ulong sizeSoFar, bool is64Bit)
	{
		ulong ret = GetSizeAndAdjustPosition<uint> (reader, sizeSoFar, is64Bit);
		field = reader.ReadUInt32 ();
		return ret;
	}

	/// <summary>
	/// Read data from a C++ structure <c>ulong</c> field and adjust stream position accordingly.
	/// </summary>
	/// <seealso cref="GetSizeAndAdjustPosition"/>
	/// <returns>Number of actual bytes read (including padding)</returns>
	public static ulong ReadField (BinaryReader reader, ref ulong field, ulong sizeSoFar, bool is64Bit)
	{
		ulong ret = GetSizeAndAdjustPosition<ulong> (reader, sizeSoFar, is64Bit);
		field = reader.ReadUInt64 ();
		return ret;
	}

	/// <summary>
	/// Read data from a C++ structure <c>pointer</c> field and adjust stream position accordingly.  Nothing is
	/// actually stored in the <param ref="field"/> parameter, as the pointer will have a value of <c>0</c>. Instead,
	/// appropriate number of bytes is skipped in the data stream.
	/// </summary>
	/// <seealso cref="GetSizeAndAdjustPosition"/>
	/// <returns>Number of actual bytes read (including padding)</returns>
	public static ulong ReadField (BinaryReader reader, ref string field, ulong sizeSoFar, bool is64Bit)
	{
		ulong ret = GetSizeAndAdjustPosition<string> (reader, sizeSoFar, is64Bit);
		var _ = is64Bit ? reader.ReadUInt64 () : reader.ReadUInt32 ();
		return ret;
	}

	/// <summary>
	/// Read data from a C++ structure <c>pointer</c> field and adjust stream position accordingly.  Nothing is
	/// actually stored in the <param ref="field"/> parameter, as the pointer will have a value of <c>0</c>. Instead,
	/// appropriate number of bytes is skipped in the data stream.
	/// </summary>
	/// <seealso cref="GetSizeAndAdjustPosition"/>
	/// <returns>Number of actual bytes read (including padding)</returns>
	public static ulong ReadField (BinaryReader reader, ref IntPtr field, ulong sizeSoFar, bool is64Bit)
	{
		ulong ret = GetSizeAndAdjustPosition<IntPtr> (reader, sizeSoFar, is64Bit);
		var _ = is64Bit ? reader.ReadUInt64 () : reader.ReadUInt32 ();
		return ret;
	}

	public static string ToStringOrNull<T> (T? reference) => reference == null ? "<NULL>" : reference.ToString () ?? "[unknown]";

	public static string YesNo (bool yes) => yes ? "yes" : "no";
	public static string AreOrNot (bool are) => are ? "are" : "are not";
}
