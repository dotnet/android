using System;

namespace tmt;

static class Helpers
{
	public static uint ReadUInt32 (byte[] data, ref ulong offset, bool is64Bit, bool packed = false)
	{
		const ulong DataSize = 4;

		if ((ulong)data.Length < (offset + DataSize))
		throw new InvalidOperationException ("Not enough data to read a 32-bit integer");

		uint ret = BitConverter.ToUInt32 (data, (int)offset);
		offset += packed ? DataSize : GetPaddedSize<uint> (offset, is64Bit);

		return ret;
	}

	public static ulong ReadUInt64 (byte[] data, ref ulong offset, bool is64Bit, bool packed = false)
	{
		const ulong DataSize = 8;

		if ((ulong)data.Length < (offset + DataSize))
		throw new InvalidOperationException ("Not enough data to read a 64-bit integer");

		ulong ret = BitConverter.ToUInt64 (data, (int)offset);
		offset += packed ? DataSize : GetPaddedSize<ulong> (offset, is64Bit);

		return ret;
	}

	public static long ReadInt64 (byte[] data, ref ulong offset, bool is64Bit, bool packed = false)
	{
		const ulong DataSize = 8;

		if ((ulong)data.Length < (offset + DataSize))
		throw new InvalidOperationException ("Not enough data to read a 64-bit integer");

		long ret = BitConverter.ToInt64 (data, (int)offset);
		offset += packed ? DataSize : GetPaddedSize<long> (offset, is64Bit);

		return ret;
	}

	public static ulong ReadPointer (byte[] data, ref ulong offset, bool is64Bit, bool packed = false)
	{
		ulong ret;

		if (is64Bit) {
			ret = ReadUInt64 (data, ref offset, packed);
		} else {
			ret = (ulong)ReadUInt32 (data, ref offset, packed);
		}

		return ret;
	}

	public static ulong GetPaddedSize<S> (ulong sizeSoFar, bool is64Bit)
	{
		ulong typeSize = GetTypeSize<S> (is64Bit);

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

	static ulong GetTypeSize<S> (bool is64Bit)
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
}
