#nullable enable
using System;
using System.IO;

namespace Microsoft.Android.Build.Tasks
{
	/// <summary>
	/// Allocation-free helpers for rendering bytes as hexadecimal.
	/// </summary>
	/// <remarks>
	/// This file is also linked into <c>Microsoft.Android.Sdk.TrimmableTypeMap</c>, which cannot
	/// reference <c>Microsoft.Android.Build.BaseTasks</c>.  It is therefore <c>internal</c>, so the
	/// two copies do not collide in assemblies referencing both.
	/// </remarks>
	static class HexUtilities	{
		/// <summary>
		/// Convert a value in the <c>0..15</c> range to its hexadecimal digit.
		/// </summary>
		public static char GetHexValue (int value, bool upperCase = true)
		{
			if (value < 10)
				return (char)(value + '0');
			return (char)(value - 10 + (upperCase ? 'A' : 'a'));
		}

		/// <summary>
		/// Write <paramref name="value"/> into <paramref name="destination"/> as exactly two
		/// hexadecimal digits.
		/// </summary>
		public static void WriteHex (Span<char> destination, byte value, bool upperCase = true)
		{
			destination [0] = GetHexValue (value >> 4, upperCase);
			destination [1] = GetHexValue (value & 0x0f, upperCase);
		}

		/// <summary>
		/// Write <paramref name="value"/> to <paramref name="writer"/> as exactly two hexadecimal
		/// digits, without allocating.
		/// </summary>
		public static void WriteHex (TextWriter writer, byte value, bool upperCase = true)
		{
			if (writer == null)
				throw new ArgumentNullException (nameof (writer));

			Span<char> chars = stackalloc char[2];
			WriteHex (chars, value, upperCase);
			writer.Write (chars [0]);
			writer.Write (chars [1]);
		}

		/// <summary>
		/// Convert <paramref name="bytes"/> to a hexadecimal string, without allocating
		/// intermediate strings.
		/// </summary>
		public static string ToHexString (ReadOnlySpan<byte> bytes, bool upperCase = true)
		{
			const int MaxStackCharLength = 128;

			int charLength = bytes.Length * 2;
			Span<char> chars = charLength <= MaxStackCharLength
				? stackalloc char[charLength]
				: new char[charLength];
			for (int i = 0, j = 0; i < bytes.Length; i += 1, j += 2) {
				WriteHex (chars.Slice (j, 2), bytes [i], upperCase);
			}
			return ((ReadOnlySpan<char>) chars).ToString ();
		}
	}
}
