using System;
using System.IO.Hashing;

#if MICROSOFT_ANDROID_BUILD_BASETASKS
namespace Microsoft.Android.Build.Tasks;
#else // !MICROSOFT_ANDROID_BUILD_BASETASKS
namespace Java.Interop.Tools.JavaCallableWrappers;
#endif // !MICROSOFT_ANDROID_BUILD_BASETASKS

// Legacy Android naming uses Jones parameters, little-endian output, and an extra length XOR.
internal sealed class Crc64JonesCompat
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

	internal void Append (byte [] array, int ibStart, int cbSize)
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

	internal static byte [] Compute (byte [] array)
	{
		if (array == null)
			throw new ArgumentNullException (nameof (array));
		return BitConverter.GetBytes (HashToUInt64 (array));
	}

	internal static ulong HashToUInt64 (ReadOnlySpan<byte> input) =>
		System.IO.Hashing.Crc64.HashToUInt64 (parameters, input) ^ (ulong) input.Length;
}
