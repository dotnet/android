using System;
using System.Security.Cryptography;
using System.Text;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

sealed class DeterministicHashBuilder : IDisposable
{
	readonly HashAlgorithm hash = SHA256.Create ();
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
		byte [] bytes = [
			(byte) value,
			(byte) (value >> 8),
			(byte) (value >> 16),
			(byte) (value >> 24),
		];
		AddBytes (bytes);
	}

	public void AddByte (byte value)
	{
		AddBytes ([value]);
	}

	public void AddBytes (byte [] bytes)
	{
		if (finished) {
			throw new InvalidOperationException ("Cannot add data after finalizing the hash.");
		}
		if (bytes.Length != 0) {
			hash.TransformBlock (bytes, 0, bytes.Length, null, 0);
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

	public void Dispose ()
	{
		hash.Dispose ();
	}
}
