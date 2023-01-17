// Original source can be found at
// https://github.com/brutaldev/StrongNameSigner/blob/c38d42ab8d1444504720a62736b310303236cd85/src/Brutal.Dev.StrongNameSigner/SigningHelper.cs#L437
using System;
using Mono.Security.Cryptography;


namespace Brutal.Dev.StrongNameSigner {
	/// <summary>
	/// Static helper class for easily getting assembly information and strong-name signing .NET assemblies.
	/// </summary>
	public static class SigningHelper
	{
		internal static byte[] GetPublicKey (byte[] keyBlob)
		{
			using var rsa = CryptoConvert.FromCapiKeyBlob(keyBlob);
			var cspBlob = CryptoConvert.ToCapiPublicKeyBlob(rsa);
			var publicKey = new byte[12 + cspBlob.Length];
			Buffer.BlockCopy(cspBlob, 0, publicKey, 12, cspBlob.Length);
			// The first 12 bytes are documented at:
			// http://msdn.microsoft.com/library/en-us/cprefadd/html/grfungethashfromfile.asp
			// ALG_ID - Signature
			publicKey[1] = 36;
			// ALG_ID - Hash
			publicKey[4] = 4;
			publicKey[5] = 128;
			// Length of Public Key (in bytes)
			publicKey[8] = (byte)(cspBlob.Length >> 0);
			publicKey[9] = (byte)(cspBlob.Length >> 8);
			publicKey[10] = (byte)(cspBlob.Length >> 16);
			publicKey[11] = (byte)(cspBlob.Length >> 24);

			return publicKey;
		}
	}
}
