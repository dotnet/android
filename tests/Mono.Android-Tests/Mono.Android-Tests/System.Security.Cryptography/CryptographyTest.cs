using System.Security.Cryptography;
using System.Text;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace System.Security.CryptographyTests
{
	[TestClass]
	public class CryptographyTest
	{
		[TestMethod]
		[TestCategory ("NativeAOTCrypto")]
		public void AesEncryptDecryptRoundTrip ()
		{
			const string plaintext = "NativeAOT crypto smoke test";
			byte [] plaintextBytes = Encoding.UTF8.GetBytes (plaintext);

			using Aes aes = Aes.Create ();
			aes.GenerateKey ();
			aes.GenerateIV ();

			byte [] ciphertext;
			using (ICryptoTransform encryptor = aes.CreateEncryptor ()) {
				ciphertext = encryptor.TransformFinalBlock (plaintextBytes, 0, plaintextBytes.Length);
			}

			byte [] decrypted;
			using (ICryptoTransform decryptor = aes.CreateDecryptor ()) {
				decrypted = decryptor.TransformFinalBlock (ciphertext, 0, ciphertext.Length);
			}

			Assert.AreEqual (plaintext, Encoding.UTF8.GetString (decrypted));
		}
	}
}
