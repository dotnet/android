#nullable enable

using System;
using System.Security.Cryptography;
using System.Text;

namespace Microsoft.Android.Tasks;

static class HashingHelper
{
	internal static void Sha256Utf8 (string text, Span<byte> destination)
	{
		ArgumentOutOfRangeException.ThrowIfNotEqual (destination.Length, SHA256.HashSizeInBytes, nameof (destination));
		const int stackallocThresholdBytes = 256;
		int byteCount = Encoding.UTF8.GetByteCount (text);
		Span<byte> input = byteCount <= stackallocThresholdBytes
			? stackalloc byte [byteCount]
			: new byte [byteCount];
		Encoding.UTF8.GetBytes (text.AsSpan (), input);
		SHA256.HashData (input, destination);
	}
}
