using System;
using System.Buffers;
using System.Security.Cryptography;
using System.Text;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

sealed class DeterministicHashBuilder : IDisposable
{
	readonly HashAlgorithm hash = SHA256.Create ();
	readonly byte [] intBuffer = new byte [4];
	readonly byte [] byteBuffer = new byte [1];
	bool finished;

	public void AddString (string value)
	{
		var bytes = Encoding.UTF8.GetBytes (value);
		AddInt32 (bytes.Length);
		AddBytes (bytes);
	}

	public void AddVersion (Version version)
	{
		AddInt32 (version.Major);
		AddInt32 (version.Minor);
		AddInt32 (version.Build);
		AddInt32 (version.Revision);
	}

	public void AddInt32 (int value)
	{
		intBuffer [0] = (byte) value;
		intBuffer [1] = (byte) (value >> 8);
		intBuffer [2] = (byte) (value >> 16);
		intBuffer [3] = (byte) (value >> 24);
		AddBytes (intBuffer);
	}

	public void AddByte (byte value)
	{
		byteBuffer [0] = value;
		AddBytes (byteBuffer);
	}

	public void AddBytes (byte [] bytes)
	{
		EnsureNotFinished ();
		if (bytes.Length != 0) {
			hash.TransformBlock (bytes, 0, bytes.Length, null, 0);
		}
	}

	public void AddBytes (ReadOnlySpan<byte> bytes)
	{
		EnsureNotFinished ();
		if (bytes.IsEmpty) {
			return;
		}

		var buffer = ArrayPool<byte>.Shared.Rent (Math.Min (bytes.Length, 4096));
		try {
			while (!bytes.IsEmpty) {
				var count = Math.Min (bytes.Length, buffer.Length);
				bytes.Slice (0, count).CopyTo (buffer);
				hash.TransformBlock (buffer, 0, count, null, 0);
				bytes = bytes.Slice (count);
			}
		} finally {
			ArrayPool<byte>.Shared.Return (buffer);
		}
	}

	public byte [] ToHash ()
	{
		if (!finished) {
			hash.TransformFinalBlock ([], 0, 0);
			finished = true;
		}
		if (hash.Hash is not null) {
			return hash.Hash;
		}
		throw new InvalidOperationException ("SHA256 did not produce a hash.");
	}

	void EnsureNotFinished ()
	{
		if (finished) {
			throw new InvalidOperationException ("Cannot add data after finalizing the hash.");
		}
	}

	public void Dispose ()
	{
		hash.Dispose ();
	}
}
