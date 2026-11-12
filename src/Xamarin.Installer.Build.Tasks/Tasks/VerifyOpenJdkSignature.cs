#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Xamarin.Installer.Build.Tasks.Properties;

namespace Xamarin.Installer.Build.Tasks
{
	/// <summary>
	/// Verifies the detached OpenPGP signature of a Microsoft OpenJDK archive against a pinned public key.
	/// </summary>
	[MSBuildMultiThreadableTask]
	public class VerifyOpenJdkSignature : MultithreadedAndroidTask
	{
		public override string TaskPrefix => "VOJ";

		[Required]
		public string ArchiveFile { get; set; } = "";

		[Required]
		public string SignatureFile { get; set; } = "";

		[Required]
		public string PublicKeyFile { get; set; } = "";

		[Required]
		public string PublicKeySha256 { get; set; } = "";

		[Required]
		public string Url { get; set; } = "";

		public override bool RunTask ()
		{
			try {
				if (PublicKeySha256.Length != 64 || !PublicKeySha256.All (Uri.IsHexDigit))
					throw new InvalidDataException ("The OpenJDK public key SHA-256 hash must contain exactly 64 hexadecimal characters.");
				var signature = File.ReadAllBytes (TaskEnvironment.GetAbsolutePath (SignatureFile));
				var publicKey = File.ReadAllBytes (TaskEnvironment.GetAbsolutePath (PublicKeyFile));
				using (var archive = File.OpenRead (TaskEnvironment.GetAbsolutePath (ArchiveFile)))
					OpenJdkSignatureVerifier.Verify (archive, signature, publicKey, PublicKeySha256, CancellationToken.None);
			} catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is InvalidDataException ||
				ex is CryptographicException || ex is FormatException) {
				Log.LogCodedError ("XA5305", Resources.XA5305, Url, ex.Message);
			}
			return !Log.HasLoggedErrors;
		}
	}
}
