using System;

namespace ApplicationUtility;

class NativeUtils
{
	static ulong GetPadding<S> (ulong sizeSoFar, bool is64Bit, out ulong typeSize)
	{
		typeSize = GetNativeTypeSize<S> (is64Bit);
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
}
