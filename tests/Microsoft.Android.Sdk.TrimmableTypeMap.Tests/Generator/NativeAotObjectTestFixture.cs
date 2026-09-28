using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.Tests;

internal static partial class NativeAotObjectTestFixture
{
	internal static byte [] CreateBlob (params string [] keys) => CreateGroups (keys);

	internal static byte [] CreateGroups (params string [] [] groups) =>
		CreateTable (groups.Select ((keys, index) =>
			CreateGroup (CreateTable (keys.Select (key => CreateKey (key)).ToArray ()), typeIndex: (uint) index)).ToArray ());

	internal static byte [] CreateGroup (byte [] inner, uint state = 1, uint typeIndex = 0) =>
		[.. EncodeUnsigned (typeIndex), .. EncodeUnsigned (state), .. inner];

	internal static byte [] CreateKey (string key, uint typeIndex = 0)
	{
		byte [] utf8 = new UTF8Encoding (false, true).GetBytes (key);
		return [.. EncodeUnsigned ((uint) utf8.Length), .. utf8, .. EncodeUnsigned (typeIndex)];
	}

	internal static byte [] CreateTable (byte [] [] payloads, int indexWidth = 1, int bucketShift = 0, int relativeWidth = 5, bool backwards = false)
	{
		int bucketCount = 1 << bucketShift;
		int indexTag = indexWidth switch { 1 => 0, 2 => 1, 4 => 2, _ => throw new ArgumentOutOfRangeException (nameof (indexWidth)) };
		var bytes = new List<byte> { (byte) ((bucketShift << 2) | indexTag) };
		bytes.AddRange (new byte [(bucketCount + 1) * indexWidth]);
		var targets = new int [payloads.Length];
		var pointers = new List<(int address, int target)> ();
		if (backwards) {
			AppendPayloads ();
		}
		for (int bucket = 0; bucket <= bucketCount; bucket++) {
			uint index = checked ((uint) (bytes.Count - 1));
			if (indexWidth < 4 && index >= 1u << (8 * indexWidth)) {
				throw new ArgumentOutOfRangeException (nameof (indexWidth), "Fixture bucket offset does not fit.");
			}
			for (int i = 0; i < indexWidth; i++) {
				bytes [1 + bucket * indexWidth + i] = (byte) (index >> (8 * i));
			}
			if (bucket == bucketCount) {
				break;
			}
			for (int entry = bucket; entry < payloads.Length; entry += bucketCount) {
				bytes.Add ((byte) entry);
				pointers.Add ((bytes.Count, entry));
				bytes.AddRange (new byte [relativeWidth]);
			}
		}
		if (!backwards) {
			AppendPayloads ();
		}
		foreach (var (address, target) in pointers) {
			// NativeFormat offsets are relative to the integer's address, not its end.
			int relative = targets [target] - address;
			int bits = relativeWidth == 5 ? 32 : relativeWidth * 7;
			if (bits < 32 && (relative < -(1L << (bits - 1)) || relative >= 1L << (bits - 1))) {
				throw new ArgumentOutOfRangeException (nameof (relativeWidth), "Fixture relative offset does not fit.");
			}
			byte [] encoded = EncodeInteger (unchecked ((uint) relative), relativeWidth);
			for (int i = 0; i < encoded.Length; i++) {
				bytes [address + i] = encoded [i];
			}
		}
		return bytes.ToArray ();

		void AppendPayloads ()
		{
			for (int i = 0; i < payloads.Length; i++) {
				targets [i] = bytes.Count;
				bytes.AddRange (payloads [i]);
			}
		}
	}

	internal static byte [] EncodeUnsigned (uint value)
	{
		int width = value < 1u << 7 ? 1 : value < 1u << 14 ? 2 : value < 1u << 21 ? 3 : value < 1u << 28 ? 4 : 5;
		return EncodeInteger (value, width);
	}

	static byte [] EncodeInteger (uint value, int width)
	{
		var result = new byte [width];
		if (width == 5) {
			result [0] = 15;
			for (int i = 0; i < 4; i++) {
				result [i + 1] = (byte) (value >> (8 * i));
			}
		} else {
			uint encoded = (value << width) | ((1u << (width - 1)) - 1);
			for (int i = 0; i < width; i++) {
				result [i] = (byte) (encoded >> (8 * i));
			}
		}
		return result;
	}
}
