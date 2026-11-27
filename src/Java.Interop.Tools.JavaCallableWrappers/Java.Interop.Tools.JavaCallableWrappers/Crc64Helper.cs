using System;
using System.IO.Hashing;

namespace Java.Interop.Tools.JavaCallableWrappers
{
	/// <summary>
	///  CRC64 variant: crc-64-jones 64-bit
	///    * Poly: 0xad93d23594c935a9
	///
	///  Changes beyond initial implementation:
	///    * Starting Value: ulong.MaxValue
	///    * XOR length in HashFinal()
	///    * System.IO.Hashing implementation with custom Jones parameters
	/// </summary>
	internal sealed class Crc64Helper
	{
		static readonly Crc64ParameterSet parameters = Crc64ParameterSet.Create (
			polynomial: 0xad93d23594c935a9UL,
			initialValue: ulong.MaxValue,
			finalXorValue: 0,
			reflectValues: true);

		readonly System.IO.Hashing.Crc64 crc = new System.IO.Hashing.Crc64 (parameters);
		ulong length;

		internal void Initialize ()
		{
			crc.Reset ();
			length = 0;
		}

		internal void HashCoreJones (byte [] array, int ibStart, int cbSize)
		{
			if (array == null)
				throw new ArgumentNullException (nameof (array));
			if (ibStart < 0 || ibStart > array.Length)
				throw new ArgumentOutOfRangeException (nameof (ibStart));
			if (cbSize < 0 || cbSize > array.Length - ibStart)
				throw new ArgumentOutOfRangeException (nameof (cbSize));

			crc.Append (array.AsSpan (ibStart, cbSize));
			length += (ulong) cbSize;
		}

		internal byte [] GetCurrentHash () => BitConverter.GetBytes (crc.GetCurrentHashAsUInt64 () ^ length);

		internal static byte [] ComputeJones (byte [] array)
		{
			if (array == null)
				throw new ArgumentNullException (nameof (array));
			return BitConverter.GetBytes (HashToUInt64Jones (array));
		}

		internal static ulong HashToUInt64Jones (ReadOnlySpan<byte> input) =>
			System.IO.Hashing.Crc64.HashToUInt64 (parameters, input) ^ (ulong) input.Length;
	}
}
