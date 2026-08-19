#nullable enable
using System;
using System.Diagnostics;
using System.IO;

namespace Microsoft.Android.Build.Tasks
{
	/// <summary>
	/// Allocation-free helpers for rendering bytes as hexadecimal.
	/// </summary>
	/// <remarks>
	/// This file is also linked into <c>Microsoft.Android.Sdk.TrimmableTypeMap</c>, which
	/// deliberately does not reference <c>Microsoft.Android.Build.BaseTasks</c> (that would drag
	/// Microsoft.Build.*, System.IO.Hashing, K4os.LZ4 and Mono.Unix into it).  Only the copy compiled
	/// into <c>Microsoft.Android.Build.BaseTasks</c> is <c>public</c>; the linked copy stays
	/// <c>internal</c>, otherwise <c>Xamarin.Android.Build.Tasks</c> — which references both
	/// assemblies — fails with <c>CS0433</c>.
	/// </remarks>
#if MICROSOFT_ANDROID_BUILD_BASETASKS
	public
#endif
	static class HexUtilities
	{
		/// <summary>
		/// Convert a value in the <c>0..15</c> range to its hexadecimal digit.
		/// </summary>
		/// <remarks>
		/// Values outside <c>0..15</c> produce meaningless characters.
		/// </remarks>
		public static char GetHexValue (int value, bool upperCase = true)
		{
			Debug.Assert ((uint) value < 16, $"Value must be in the 0..15 range, was {value}.");

			if (value < 10)
				return (char) (value + '0');
			return (char) (value - 10 + (upperCase ? 'A' : 'a'));
		}

		/// <summary>
		/// Write <paramref name="value"/> into <paramref name="destination"/> as exactly two
		/// hexadecimal digits.
		/// </summary>
		/// <exception cref="ArgumentException">
		/// <paramref name="destination"/> is shorter than two characters.
		/// </exception>
		public static void WriteHex (Span<char> destination, byte value, bool upperCase = true)
		{
			if (destination.Length < 2)
				throw new ArgumentException ("Destination must be at least 2 characters long.", nameof (destination));

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

			writer.Write (GetHexValue (value >> 4, upperCase));
			writer.Write (GetHexValue (value & 0x0f, upperCase));
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
				? stackalloc char [charLength]
				: new char [charLength];
			for (int i = 0, j = 0; i < bytes.Length; i += 1, j += 2) {
				WriteHex (chars.Slice (j, 2), bytes [i], upperCase);
			}
			return ((ReadOnlySpan<char>) chars).ToString ();
		}
	}
}
