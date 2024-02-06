using System;

namespace tmt;

static class Helpers
{
	static void PrepareForRead<T> (byte[] data, ref ulong offset, out ulong dataSize, bool is64Bit, bool packed)
	{
		if (!packed) {
			offset = AdjustOffset<T> (offset, is64Bit);
		}
		dataSize = GetTypeSize<T> (is64Bit);

		if ((ulong)data.Length < (offset + dataSize)) {
			throw new InvalidOperationException ($"Not enough data to read a {GetNiceName<T> (is64Bit)}");
		}
	}

	static uint ReadUInt32_NoPrep (byte[] data, ref ulong offset, ulong dataSize)
	{
		uint ret = BitConverter.ToUInt32 (data, (int)offset);
		offset += dataSize;

		return ret;
	}

	public static uint ReadUInt32 (byte[] data, ref ulong offset, bool is64Bit, bool packed = false)
	{
		ulong dataSize;
		PrepareForRead<uint> (data, ref offset, out dataSize, is64Bit, packed);
		return ReadUInt32_NoPrep (data, ref offset, dataSize);
	}

	static ulong ReadUInt64_NoPrep (byte[] data, ref ulong offset, ulong dataSize)
	{
		ulong ret = BitConverter.ToUInt64 (data, (int)offset);
		offset += dataSize;

		return ret;
	}

	public static ulong ReadUInt64 (byte[] data, ref ulong offset, bool is64Bit, bool packed = false)
	{
		ulong dataSize;
		PrepareForRead<ulong> (data, ref offset, out dataSize, is64Bit, packed);
		return ReadUInt64_NoPrep (data, ref offset, dataSize);
	}

	public static long ReadInt64 (byte[] data, ref ulong offset, bool is64Bit, bool packed = false)
	{
		ulong dataSize;
		PrepareForRead<long> (data, ref offset, out dataSize, is64Bit, packed);

		long ret = BitConverter.ToInt64 (data, (int)offset);
		offset += dataSize;

		return ret;
	}

	public static ulong ReadPointer (byte[] data, ref ulong offset, bool is64Bit, bool packed = false)
	{
		ulong dataSize;
		PrepareForRead<UIntPtr> (data, ref offset, out dataSize, is64Bit, packed);

		if (is64Bit) {
			return ReadUInt64_NoPrep (data, ref offset, dataSize);
		}

		return (ulong)ReadUInt32_NoPrep (data, ref offset, dataSize);
	}

	static (ulong modulo, ulong typeSize) GetPaddingModuloAndTypeSize<T> (bool is64Bit)
	{
		ulong typeSize = GetTypeSize<T> (is64Bit);
		ulong modulo;
		if (is64Bit) {
			modulo = typeSize < 8 ? 4u : 8u;
		} else {
			modulo = 4u;
		}

		return (modulo, typeSize);
	}

	public static ulong GetPaddedSize<S> (ulong sizeSoFar, bool is64Bit)
	{
		(ulong typeSize, ulong modulo) = GetPaddingModuloAndTypeSize<S> (is64Bit);
		ulong alignment = sizeSoFar % modulo;
		if (alignment == 0) {
			return typeSize;
		}

		return typeSize + (modulo - alignment);
	}

	static ulong AdjustOffset<T> (ulong currentOffset, bool is64Bit)
	{
		(ulong _, ulong modulo) = GetPaddingModuloAndTypeSize<T> (is64Bit);
		return currentOffset + (currentOffset % modulo);
	}

	static ulong GetTypeSize<S> (bool is64Bit)
	{
		Type type = typeof(S);

		if (type == typeof(string) || type == typeof(IntPtr) || type == typeof(UIntPtr)) {
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

	static string GetNiceName<T> (bool is64Bit)
	{
		Type type = typeof(T);

		if (type == typeof(string) || type == typeof(IntPtr) || type == typeof(UIntPtr)) {
			// We treat `string` as a generic pointer
			string bitness = is64Bit ? "64" : "32";
			return $"{bitness}-bit pointer";
		}

		if (type == typeof(byte)) {
			return "byte";
		}

		if (type == typeof(bool)) {
			return "boolean";
		}

		if (type == typeof(Int32) || type == typeof(UInt32)) {
			return "32-bit integer";
		}

		if (type == typeof(Int64) || type == typeof(UInt64)) {
			return "64-bit integer";
		}

		throw new NotSupportedException ($"Type {type} is not supported");;
	}
}
