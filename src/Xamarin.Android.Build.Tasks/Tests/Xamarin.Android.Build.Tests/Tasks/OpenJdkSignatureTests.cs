using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using NUnit.Framework;
using Xamarin.Installer.AndroidSDK.Common;
using Xamarin.Installer.Build.Tasks;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	public class OpenJdkSignatureTests
	{
		static readonly byte [] archiveBytes = Encoding.UTF8.GetBytes ("OpenJDK archive test contents");
		RSA signingKey;
		byte [] publicKey;
		string publicKeyHash;

		[OneTimeSetUp]
		public void CreateSigningKey ()
		{
			signingKey = RSA.Create ();
			signingKey.KeySize = 2048;
			var parameters = signingKey.ExportParameters (false);
			publicKey = Packet (6, Join ([4, 0, 0, 0, 0, 1], Integer (parameters.Modulus), Integer (parameters.Exponent)));
			using (var hash = SHA256.Create ())
				publicKeyHash = Convert.ToHexString (hash.ComputeHash (publicKey));
		}

		[OneTimeTearDown]
		public void DisposeSigningKey () => signingKey.Dispose ();

		byte [] Sign (int algorithm = 8, bool oldFormat = false, byte [] hashedSubpackets = null)
		{
			var hashName = algorithm == 10 ? HashAlgorithmName.SHA512 : algorithm == 9 ? HashAlgorithmName.SHA384 : HashAlgorithmName.SHA256;
			hashedSubpackets ??= [5, 2, 0, 0, 0, 0];
			var header = Join ([4, 0, 1, (byte) algorithm, (byte) (hashedSubpackets.Length >> 8), (byte) hashedSubpackets.Length], hashedSubpackets);
			using (var hash = IncrementalHash.CreateHash (hashName)) {
				hash.AppendData (archiveBytes);
				hash.AppendData (header);
				hash.AppendData ([4, 255, 0, 0, (byte) (header.Length >> 8), (byte) header.Length]);
				var digest = hash.GetHashAndReset ();
				var rsaSignature = signingKey.SignHash (digest, hashName, RSASignaturePadding.Pkcs1);
				return Packet (2, Join (header, [0, 0, digest [0], digest [1]], Integer (rsaSignature)), oldFormat);
			}
		}

		void Verify (byte [] contents, byte [] signature, byte [] key = null, string trustedHash = null, CancellationToken cancellationToken = default)
		{
			using (var archive = new MemoryStream (contents))
				OpenJdkSignatureVerifier.Verify (archive, signature, key ?? publicKey, trustedHash ?? publicKeyHash, cancellationToken);
		}

		[TestCase (8, false)]
		[TestCase (9, false)]
		[TestCase (10, false)]
		[TestCase (8, true)]
		public void ValidSignature (int algorithm, bool oldFormat)
		{
			Assert.DoesNotThrow (() => Verify (archiveBytes, Sign (algorithm, oldFormat)));
		}

		[Test]
		public void ArmoredSignatureAndKey ()
		{
			Assert.DoesNotThrow (() => Verify (archiveBytes, Armor (Sign (), "SIGNATURE"), Armor (publicKey, "PUBLIC KEY BLOCK")));
		}

		[Test]
		public void ChangedArchive ()
		{
			var changed = (byte []) archiveBytes.Clone ();
			changed [0] ^= 1;
			Assert.Throws<InvalidDataException> (() => Verify (changed, Sign ()));
		}

		[Test]
		public void ChangedSignature ()
		{
			var changed = Sign ();
			changed [changed.Length - 1] ^= 1;
			Assert.Throws<InvalidDataException> (() => Verify (archiveBytes, changed));
		}

		[Test]
		public void ChangedSignedMetadata ()
		{
			var changed = Sign ();
			changed [14] ^= 1;
			Assert.Throws<InvalidDataException> (() => Verify (archiveBytes, changed));
		}

		[Test]
		public void UntrustedKey ()
		{
			Assert.Throws<InvalidDataException> (() => Verify (archiveBytes, Sign (), trustedHash: OpenJdkSignatureVerifier.PublicKeySha256));
		}

		[Test]
		public void WrongRsaKey ()
		{
			using (var other = RSA.Create ()) {
				other.KeySize = 2048;
				var parameters = other.ExportParameters (false);
				var key = Packet (6, Join ([4, 0, 0, 0, 0, 1], Integer (parameters.Modulus), Integer (parameters.Exponent)));
				using (var hash = SHA256.Create ())
					Assert.Throws<InvalidDataException> (() => Verify (archiveBytes, Sign (), key, Convert.ToHexString (hash.ComputeHash (key))));
			}
		}

		[Test]
		public void MissingSignature ()
		{
			Assert.Throws<InvalidDataException> (() => Verify (archiveBytes, []));
		}

		[Test]
		public void WeakHash ()
		{
			Assert.Throws<InvalidDataException> (() => Verify (archiveBytes, Sign (algorithm: 2)));
		}

		[Test]
		public void CriticalSubpacket ()
		{
			Assert.Throws<InvalidDataException> (() => Verify (archiveBytes, Sign (hashedSubpackets: [1, 255])));
		}

		[Test]
		public void MultipleSignatures ()
		{
			var signature = Sign ();
			Assert.Throws<InvalidDataException> (() => Verify (archiveBytes, Join (signature, signature)));
		}

		[Test]
		public void RejectTruncatedSignatures ()
		{
			var signature = Sign ();
			for (int length = 0; length < signature.Length; length++) {
				var truncated = new byte [length];
				Buffer.BlockCopy (signature, 0, truncated, 0, length);
				Assert.Throws<InvalidDataException> (() => Verify (archiveBytes, truncated), $"Length: {length}");
			}
		}

		[Test]
		public void InvalidPacketLengthsAndTypes ()
		{
			byte [] [] malformed = [
				[194, 224],
				[194, 255, 255, 255, 255, 255],
				[139],
				[195, 0],
				[194, 0],
				[194, 1, 5],
			];
			foreach (var signature in malformed)
				Assert.Throws<InvalidDataException> (() => Verify (archiveBytes, signature));
		}

		[Test]
		public void InvalidArmoredSignatures ()
		{
			string [] malformed = [
				"",
				"-----BEGIN PGP SIGNATURE-----",
				"-----BEGIN PGP SIGNATURE-----\nInvalid metadata\n",
				"-----BEGIN PGP SIGNATURE-----\n\nAA==",
				Encoding.ASCII.GetString (Armor (Sign (), "SIGNATURE")) + "extra",
			];
			foreach (var signature in malformed)
				Assert.Throws<InvalidDataException> (() => Verify (archiveBytes, Encoding.ASCII.GetBytes (signature)));
		}

		[TestCase (0, 5)]
		[TestCase (1, 1)]
		[TestCase (2, 17)]
		public void UnsupportedSignatureFields (int offset, int value)
		{
			var signature = Sign ();
			signature [6 + offset] = (byte) value;
			Assert.Throws<InvalidDataException> (() => Verify (archiveBytes, signature));
		}

		[TestCase (false)]
		[TestCase (true)]
		public void CancelledVerification (bool skip)
		{
			Assert.Throws<OperationCanceledException> (() => Verify (archiveBytes, Sign (), cancellationToken: new CancellationToken (true)));
			using (var client = new HttpClient (new SignatureHandler ([], []))) {
				var task = new InstallAndroidDependencies { SkipSignCheck = skip };
				Assert.ThrowsAsync<OperationCanceledException> (async () => await task.VerifyJavaArchiveAsync (client, new Archive ("windows"), new CancellationToken (true)));
			}
		}

		[Test]
		public void OversizedRentedArchiveBufferUsesFullCapacity ()
		{
			var pool = new TrackingBufferPool ();
			using (var archive = new FullBufferReadCheckingStream (archiveBytes))
				OpenJdkSignatureVerifier.Verify (archive, Sign (), publicKey, publicKeyHash, CancellationToken.None, pool);
			Assert.IsTrue (pool.Returned);
		}

		[TestCase (false)]
		[TestCase (true)]
		public void HttpTimeoutReportsVerificationError (bool keyRequest)
		{
			var handler = new BlockingVerificationHandler (keyRequest);
			using (var client = new HttpClient (handler) { Timeout = TimeSpan.FromMilliseconds (100) }) {
				var engine = CreateBuildEngine ();
				var task = new VerificationTask {
					BuildEngine = engine,
					Client = client,
					Archive = new Archive ("windows") { Url = new Uri ("https://example.test/jdk.zip") },
				};
				Assert.IsFalse (task.Execute ());
				Assert.IsTrue (handler.Blocked);
				Assert.IsFalse (task.CancellationToken.IsCancellationRequested, "The HTTP timeout must not cancel the caller's token.");
				Assert.AreEqual (1, engine.Errors.Count);
				Assert.AreEqual ("XA5305", engine.Errors [0].Code);
				Assert.IsEmpty (engine.Warnings);
			}
		}

		[TestCase (false)]
		[TestCase (true)]
		public void CallerCancellationDuringVerificationPropagates (bool keyRequest)
		{
			using (var source = new CancellationTokenSource ()) {
				var handler = new BlockingVerificationHandler (keyRequest, source.Cancel);
				using (var client = new HttpClient (handler) { Timeout = Timeout.InfiniteTimeSpan }) {
					var engine = CreateBuildEngine ();
					var task = new InstallAndroidDependencies { BuildEngine = engine };
					var archive = new Archive ("windows") { Url = new Uri ("https://example.test/jdk.zip") };
					Assert.CatchAsync<OperationCanceledException> (async () => await task.VerifyJavaArchiveAsync (client, archive, source.Token));
					Assert.IsTrue (handler.Blocked);
					Assert.IsTrue (source.IsCancellationRequested);
					Assert.IsEmpty (engine.Errors);
				}
			}
		}

		[TestCase (false)]
		[TestCase (true)]
		public void RentedArchiveBufferIsReturnedOnFailureOrCancellation (bool cancel)
		{
			var contents = (byte []) archiveBytes.Clone ();
			contents [0] ^= 1;
			var pool = new TrackingBufferPool ();
			using (var source = new CancellationTokenSource ())
			using (var archive = new FullBufferReadCheckingStream (contents, cancel ? source.Cancel : null)) {
				if (cancel)
					Assert.Throws<OperationCanceledException> (() => OpenJdkSignatureVerifier.Verify (archive, Sign (), publicKey, publicKeyHash, source.Token, pool));
				else
					Assert.Throws<InvalidDataException> (() => OpenJdkSignatureVerifier.Verify (archive, Sign (), publicKey, publicKeyHash, source.Token, pool));
			}
			Assert.IsTrue (pool.Returned);
		}

		[TestCase (0)]
		[TestCase (4096 + 19)]
		[TestCase (64 * 1024)]
		public async Task OversizedRentedDownloadBufferReturnsOnlyContentBytes (int length)
		{
			var contents = new byte [length];
			for (int i = 0; i < contents.Length; i++)
				contents [i] = (byte) i;
			var pool = new TrackingBufferPool ();
			var handler = new SignatureHandler ([], []) {
				ResponseStream = new FullBufferReadCheckingStream (contents, maximumBytesPerRead: 2048),
			};
			using (var client = new HttpClient (handler)) {
				var result = await InstallAndroidDependencies.DownloadVerificationFileAsync (client, new Uri ("https://example.test/jdk.zip.sig"), CancellationToken.None, pool);
				Assert.AreEqual (contents, result);
			}
			Assert.IsTrue (pool.Returned);
		}

		[TestCase (false)]
		[TestCase (true)]
		public void RentedDownloadBufferIsReturnedOnFailureOrCancellation (bool cancel)
		{
			var pool = new TrackingBufferPool ();
			using (var source = new CancellationTokenSource ()) {
				var handler = new SignatureHandler ([], []) {
					ResponseStream = new FullBufferReadCheckingStream (new byte [64 * 1024 + 1], cancel ? source.Cancel : null, maximumBytesPerRead: 2048),
				};
				using (var client = new HttpClient (handler)) {
					if (cancel)
						Assert.ThrowsAsync<OperationCanceledException> (async () => await InstallAndroidDependencies.DownloadVerificationFileAsync (client, new Uri ("https://example.test/jdk.zip.sig"), source.Token, pool));
					else
						Assert.ThrowsAsync<InvalidDataException> (async () => await InstallAndroidDependencies.DownloadVerificationFileAsync (client, new Uri ("https://example.test/jdk.zip.sig"), source.Token, pool));
				}
			}
			Assert.IsTrue (pool.Returned);
		}

		[TestCase (HttpStatusCode.NotFound, false)]
		[TestCase (HttpStatusCode.OK, false)]
		[TestCase (HttpStatusCode.NotFound, true)]
		public void VerificationRequiredUnlessExplicitlySkipped (HttpStatusCode status, bool skip)
		{
			var handler = new SignatureHandler ([], publicKey) { SignatureStatus = status };
			var engine = CreateBuildEngine ();
			bool result = RunVerificationTask (handler, engine, skip);
			Assert.AreEqual (skip, result);
			Assert.AreEqual (skip ? 0 : 1, engine.Errors.Count);
			Assert.IsEmpty (engine.Warnings);
			if (skip) {
				var message = engine.Messages.Single (entry => entry.Message.Contains ("_SkipSignCheck=true"));
				Assert.AreEqual (MessageImportance.Normal, message.Importance);
				StringAssert.Contains ("signature verification was skipped", message.Message);
				Assert.IsEmpty (handler.Requests, "The bypass must not request a signature or key.");
			} else {
				Assert.AreEqual ("XA5305", engine.Errors [0].Code);
				Assert.AreEqual ("https://example.test/jdk.tar.gz.sig?token=test", handler.Requests [0].AbsoluteUri);
			}
		}

		[TestCase (false)]
		[TestCase (true)]
		public void KeyUnavailableOrUntrusted (bool missing)
		{
			var handler = new SignatureHandler (Sign (), publicKey) {
				KeyStatus = missing ? HttpStatusCode.NotFound : HttpStatusCode.OK,
			};
			var engine = CreateBuildEngine ();
			Assert.IsFalse (RunVerificationTask (handler, engine));
			Assert.AreEqual (2, handler.Requests.Count);
			Assert.AreEqual (OpenJdkSignatureVerifier.PublicKeyUrl, handler.Requests [1].AbsoluteUri);
			Assert.AreEqual ("XA5305", engine.Errors [0].Code);
			Assert.IsEmpty (engine.Warnings);
		}

		[TestCase (null)]
		[TestCase ("")]
		[TestCase ("https://example.test/openjdk.asc")]
		public void PublicKeyUrlUsesDefaultOrOverrideWithoutChangingTrust (string publicKeyUrl)
		{
			var expectedUrl = string.IsNullOrEmpty (publicKeyUrl) ? OpenJdkSignatureVerifier.PublicKeyUrl : publicKeyUrl;
			var handler = new SignatureHandler (Sign (), publicKey) { KeyUrl = expectedUrl };
			var engine = CreateBuildEngine ();
			Assert.IsFalse (RunVerificationTask (handler, engine, publicKeyUrl: publicKeyUrl));
			Assert.AreEqual (2, handler.Requests.Count);
			Assert.AreEqual (expectedUrl, handler.Requests [1].AbsoluteUri);
			Assert.AreEqual ("XA5305", engine.Errors [0].Code);
			StringAssert.Contains ("does not match the trusted SHA-256 hash", engine.Errors [0].Message);
			Assert.IsEmpty (engine.Warnings);
		}

		[TestCase ("relative/key.asc")]
		[TestCase ("file:///tmp/key.asc")]
		public void InvalidPublicKeyUrlFailsWithoutFallback (string publicKeyUrl)
		{
			var handler = new SignatureHandler (Sign (), publicKey);
			var engine = CreateBuildEngine ();
			Assert.IsFalse (RunVerificationTask (handler, engine, publicKeyUrl: publicKeyUrl));
			Assert.AreEqual ("XA5305", engine.Errors [0].Code);
			Assert.IsEmpty (handler.Requests);
		}

		[TestCase (null)]
		[TestCase ("")]
		public void BlankSigningKeyHashUsesBuiltInDefault (string hash)
		{
			var handler = new SignatureHandler (Sign (), publicKey);
			var engine = CreateBuildEngine ();
			Assert.IsFalse (RunVerificationTask (handler, engine, publicKeySha256: hash));
			Assert.AreEqual ("XA5305", engine.Errors [0].Code);
			StringAssert.Contains ("does not match the trusted SHA-256 hash", engine.Errors [0].Message);
		}

		[TestCase (null, false)]
		[TestCase ("https://example.test/openjdk.asc", true)]
		public void SigningKeyHashOverrideAuthenticatesConfiguredKey (string publicKeyUrl, bool lowercaseHash)
		{
			var expectedUrl = publicKeyUrl ?? OpenJdkSignatureVerifier.PublicKeyUrl;
			var handler = new SignatureHandler (Sign (), publicKey) { KeyUrl = expectedUrl };
			var engine = CreateBuildEngine ();
			var hash = lowercaseHash ? publicKeyHash.ToLowerInvariant () : publicKeyHash;
			Assert.IsTrue (RunVerificationTask (handler, engine, publicKeyUrl: publicKeyUrl, publicKeySha256: hash));
			Assert.AreEqual (2, handler.Requests.Count);
			Assert.AreEqual (expectedUrl, handler.Requests [1].AbsoluteUri);
			Assert.IsEmpty (engine.Errors);
			Assert.IsEmpty (engine.Warnings);
		}

		[Test]
		public void WrongSigningKeyHashOverrideFailsWithoutFallback ()
		{
			var handler = new SignatureHandler (Sign (), publicKey);
			var engine = CreateBuildEngine ();
			Assert.IsFalse (RunVerificationTask (handler, engine, publicKeySha256: new string ('0', 64)));
			Assert.AreEqual ("XA5305", engine.Errors [0].Code);
			StringAssert.Contains ("does not match the trusted SHA-256 hash", engine.Errors [0].Message);
		}

		[TestCase (false)]
		[TestCase (true)]
		public void MalformedSigningKeyHashOverrideFailsBeforeDownload (bool validLength)
		{
			var handler = new SignatureHandler (Sign (), publicKey);
			var engine = CreateBuildEngine ();
			var hash = validLength ? new string ('g', 64) : "ABC";
			Assert.IsFalse (RunVerificationTask (handler, engine, publicKeySha256: hash));
			Assert.AreEqual ("XA5305", engine.Errors [0].Code);
			StringAssert.Contains ("exactly 64 hexadecimal characters", engine.Errors [0].Message);
			Assert.IsEmpty (handler.Requests);
		}

		[Test]
		public void SigningKeyHashOverrideStillRequiresValidArchiveSignature ()
		{
			var signature = Sign ();
			signature [signature.Length - 1] ^= 1;
			var handler = new SignatureHandler (signature, publicKey);
			var engine = CreateBuildEngine ();
			Assert.IsFalse (RunVerificationTask (handler, engine, publicKeySha256: publicKeyHash));
			Assert.AreEqual ("XA5305", engine.Errors [0].Code);
			StringAssert.Contains ("archive signature is invalid", engine.Errors [0].Message);
		}

		[TestCase (false)]
		[TestCase (true)]
		public void VerificationDownloadSizeLimit (bool oversizedKey)
		{
			var oversized = new byte [64 * 1024 + 1];
			var handler = new SignatureHandler (oversizedKey ? Sign () : oversized, oversizedKey ? oversized : publicKey);
			var engine = CreateBuildEngine ();
			Assert.IsFalse (RunVerificationTask (handler, engine));
			Assert.AreEqual ("XA5305", engine.Errors [0].Code);
			StringAssert.Contains ("64 KiB", engine.Errors [0].Message);
		}

		[Test]
		public void BypassDefaultsToFalseAndTargetForwardsPrivateProperty ()
		{
			Assert.IsFalse (new InstallAndroidDependencies ().SkipSignCheck);
			Assert.AreEqual ("", new InstallAndroidDependencies ().OpenJdkPublicKeyUrl);
			Assert.AreEqual ("", new InstallAndroidDependencies ().OpenJdkPublicKeySha256);
			var targets = XDocument.Load (Path.Combine (XABuildPaths.TopDirectory, "src", "Xamarin.Installer.Build.Tasks", "Xamarin.Installer.Common.targets"));
			XNamespace ns = "http://schemas.microsoft.com/developer/msbuild/2003";
			var task = targets.Root.Elements (ns + "Target").Single (target => (string) target.Attribute ("Name") == "InstallAndroidDependencies");
			Assert.AreEqual ("$(_SkipSignCheck)", (string) task.Element (ns + "InstallAndroidDependencies").Attribute ("SkipSignCheck"));
			Assert.AreEqual ("$(_AndroidOpenJdkPublicKeyUrl)", (string) task.Element (ns + "InstallAndroidDependencies").Attribute ("OpenJdkPublicKeyUrl"));
			Assert.AreEqual ("$(_AndroidOpenJdkPublicKeySha256)", (string) task.Element (ns + "InstallAndroidDependencies").Attribute ("OpenJdkPublicKeySha256"));
			Assert.IsFalse (targets.Descendants (ns + "_AndroidOpenJdkPublicKeyUrl").Any (), "The private property must remain blank by default.");
			Assert.IsFalse (targets.Descendants (ns + "_AndroidOpenJdkPublicKeySha256").Any (), "The private hash property must remain blank by default.");
		}

		[Test]
		[Explicit ("Downloads a Microsoft OpenJDK archive to verify the production key hash and format reader.")]
		public async Task PublishedMicrosoftArchive ()
		{
			using (var client = new HttpClient { Timeout = TimeSpan.FromMinutes (10) }) {
				var archive = new Archive ("windows") {
					Url = new Uri ("https://aka.ms/download-jdk/microsoft-jdk-21.0.8-windows-x64.zip"),
					DownloadedFilePath = Path.GetTempFileName (),
				};
				try {
					using (var response = await client.GetAsync (archive.Url, HttpCompletionOption.ResponseHeadersRead)) {
						response.EnsureSuccessStatusCode ();
						using (var output = File.Create (archive.DownloadedFilePath))
							await response.Content.CopyToAsync (output);
					}
					var engine = CreateBuildEngine ();
					var task = new VerificationTask { BuildEngine = engine, Client = client, Archive = archive };
					Assert.IsTrue (task.Execute (), string.Join (Environment.NewLine, engine.Errors.Select (error => error.Message)));
					Assert.IsTrue (task.Verified);
					Assert.IsEmpty (engine.Errors);
					Assert.IsEmpty (engine.Warnings);
					var key = await client.GetByteArrayAsync (OpenJdkSignatureVerifier.PublicKeyUrl);
					var signature = await client.GetByteArrayAsync (archive.Url.AbsoluteUri + ".sig");
					var handler = new SignatureHandler (signature, key) { KeyUrl = "https://example.test/openjdk.asc" };
					using (var customClient = new HttpClient (handler)) {
						var customUrlTask = new VerificationTask {
							BuildEngine = engine,
							Client = customClient,
							Archive = archive,
							OpenJdkPublicKeyUrl = handler.KeyUrl,
							OpenJdkPublicKeySha256 = OpenJdkSignatureVerifier.PublicKeySha256.ToLowerInvariant (),
						};
						Assert.IsTrue (customUrlTask.Execute (), "The same trusted key must verify when returned by a custom URL.");
						Assert.AreEqual (handler.KeyUrl, handler.Requests [1].AbsoluteUri);
					}
					using (var file = File.OpenWrite (archive.DownloadedFilePath))
						file.WriteByte (0);
					var changed = new VerificationTask { BuildEngine = engine, Client = client, Archive = archive };
					Assert.IsFalse (changed.Execute (), "Changed Microsoft archive contents must be rejected.");
					Assert.AreEqual ("XA5305", engine.Errors [0].Code);
				} finally {
					File.Delete (archive.DownloadedFilePath);
				}
			}
		}

		[Test]
		[NonParallelizable]
		[Explicit ("Downloads and installs Microsoft OpenJDK through the production dependency task.")]
		public void InstallMicrosoftJdk ()
		{
			var root = Path.Combine (Path.GetTempPath (), "android-jdk-signature-test-" + Guid.NewGuid ().ToString ("N"));
			try {
				var sdkPath = Directory.CreateDirectory (Path.Combine (root, "android-sdk")).FullName;
				var jdkPath = Path.Combine (root, "jdk");
				var dependency = new Microsoft.Build.Utilities.TaskItem ("jdk");
				dependency.SetMetadata ("Version", "21.0.8");
				var engine = CreateBuildEngine ();
				var task = new InstallAndroidDependencies {
					BuildEngine = engine,
					Dependencies = [],
					JavaDependencies = [dependency],
					AndroidSdkPath = sdkPath,
					JavaSdkPath = jdkPath,
				};
				Assert.IsTrue (task.Execute (), string.Join (Environment.NewLine, engine.Errors.Select (error => error.Message)));
				Assert.IsEmpty (engine.Errors);
				Assert.IsEmpty (engine.Warnings);
				Assert.IsTrue (File.Exists (Path.Combine (jdkPath, "release")));
				StringAssert.Contains ("JAVA_VERSION=\"21.0.8\"", File.ReadAllText (Path.Combine (jdkPath, "release")));
			} finally {
				if (Directory.Exists (root))
					Directory.Delete (root, recursive: true);
			}
		}

		static MockBuildEngine CreateBuildEngine () => new MockBuildEngine (TestContext.Out, [], [], []);

		static bool RunVerificationTask (SignatureHandler handler, MockBuildEngine engine, bool skip = false, string publicKeyUrl = null, string publicKeySha256 = null)
		{
			using (var client = new HttpClient (handler)) {
				var path = Path.GetTempFileName ();
				try {
					File.WriteAllBytes (path, archiveBytes);
					var task = new VerificationTask {
						BuildEngine = engine,
						Client = client,
						Archive = new Archive ("windows") {
							Url = new Uri ("https://example.test/jdk.tar.gz?token=test"),
							DownloadedFilePath = path,
						},
						SkipSignCheck = skip,
						OpenJdkPublicKeyUrl = publicKeyUrl,
						OpenJdkPublicKeySha256 = publicKeySha256,
					};
					bool result = task.Execute ();
					Assert.AreEqual (result, task.Verified);
					return result;
				} finally {
					File.Delete (path);
				}
			}
		}

		static byte [] Integer (byte [] value)
		{
			int first = 0;
			while (value [first] == 0)
				first++;
			int bits = (value.Length - first - 1) * 8;
			for (int high = value [first]; high > 0; high >>= 1)
				bits++;
			var result = new byte [value.Length - first + 2];
			result [0] = (byte) (bits >> 8);
			result [1] = (byte) bits;
			Buffer.BlockCopy (value, first, result, 2, value.Length - first);
			return result;
		}

		static byte [] Packet (int tag, byte [] contents, bool oldFormat = false)
		{
			if (oldFormat)
				return Join ([(byte) (128 | (tag << 2) | 1), (byte) (contents.Length >> 8), (byte) contents.Length], contents);
			return Join ([(byte) (192 | tag), 255, 0, 0, (byte) (contents.Length >> 8), (byte) contents.Length], contents);
		}

		static byte [] Join (params byte [] [] arrays)
		{
			var result = new byte [arrays.Sum (array => array.Length)];
			var destination = result.AsSpan ();
			foreach (var array in arrays) {
				array.AsSpan ().CopyTo (destination);
				destination = destination.Slice (array.Length);
			}
			return result;
		}

		static byte [] Armor (byte [] bytes, string label)
			=> Encoding.ASCII.GetBytes ($"-----BEGIN PGP {label}-----\nVersion: Test\n\n{Convert.ToBase64String (bytes)}\n-----END PGP {label}-----\n");

		sealed class VerificationTask : InstallAndroidDependencies
		{
			public HttpClient Client { get; set; }
			public Archive Archive { get; set; }
			public bool Verified { get; private set; }

			public override async Task RunTaskAsync ()
			{
				Verified = await VerifyJavaArchiveAsync (Client, Archive, CancellationToken);
			}
		}

		sealed class SignatureHandler : HttpMessageHandler
		{
			readonly byte [] signature;
			readonly byte [] key;
			public List<Uri> Requests { get; } = [];
			public HttpStatusCode SignatureStatus { get; set; } = HttpStatusCode.OK;
			public HttpStatusCode KeyStatus { get; set; } = HttpStatusCode.OK;
			public string KeyUrl { get; set; } = OpenJdkSignatureVerifier.PublicKeyUrl;
			public Stream ResponseStream { get; set; }

			public SignatureHandler (byte [] signature, byte [] key)
			{
				this.signature = signature;
				this.key = key;
			}

			protected override Task<HttpResponseMessage> SendAsync (HttpRequestMessage request, CancellationToken cancellationToken)
			{
				Requests.Add (request.RequestUri);
				bool isKey = request.RequestUri.AbsoluteUri == KeyUrl;
				return Task.FromResult (new HttpResponseMessage (isKey ? KeyStatus : SignatureStatus) {
					Content = ResponseStream == null ? new ByteArrayContent (isKey ? key : signature) : new StreamContent (ResponseStream),
				});
			}
		}

		sealed class BlockingVerificationHandler : HttpMessageHandler
		{
			readonly bool keyRequest;
			readonly Action onBlocked;
			public bool Blocked { get; private set; }

			public BlockingVerificationHandler (bool keyRequest, Action onBlocked = null)
			{
				this.keyRequest = keyRequest;
				this.onBlocked = onBlocked;
			}

			protected override async Task<HttpResponseMessage> SendAsync (HttpRequestMessage request, CancellationToken cancellationToken)
			{
				if ((request.RequestUri.AbsoluteUri == OpenJdkSignatureVerifier.PublicKeyUrl) == keyRequest) {
					Blocked = true;
					onBlocked?.Invoke ();
					await Task.Delay (Timeout.Infinite, cancellationToken);
				}
				return new HttpResponseMessage (HttpStatusCode.OK) { Content = new ByteArrayContent ([]) };
			}
		}

		sealed class TrackingBufferPool : ArrayPool<byte>
		{
			byte [] rented;
			public bool Returned { get; private set; }

			public override byte [] Rent (int minimumLength)
			{
				Assert.IsNull (rented);
				rented = new byte [minimumLength + 17];
				Array.Fill (rented, (byte) 0xA5);
				return rented;
			}

			public override void Return (byte [] array, bool clearArray = false)
			{
				Assert.AreSame (rented, array);
				Assert.IsFalse (Returned);
				Returned = true;
				Array.Fill (array, (byte) 0xDD);
			}
		}

		sealed class FullBufferReadCheckingStream : MemoryStream
		{
			readonly Action onRead;
			readonly int maximumBytesPerRead;

			public FullBufferReadCheckingStream (byte [] contents, Action onRead = null, int maximumBytesPerRead = int.MaxValue) : base (contents, writable: false)
			{
				this.onRead = onRead;
				this.maximumBytesPerRead = maximumBytesPerRead;
			}

			public override int Read (byte [] buffer, int offset, int count)
			{
				Assert.AreEqual (buffer.Length - offset, count, "Use the rented buffer's remaining capacity for reads.");
				int read = base.Read (buffer, offset, Math.Min (count, maximumBytesPerRead));
				if (read > 0)
					onRead?.Invoke ();
				return read;
			}

			public override Task<int> ReadAsync (byte [] buffer, int offset, int count, CancellationToken cancellationToken)
			{
				cancellationToken.ThrowIfCancellationRequested ();
				return Task.FromResult (Read (buffer, offset, count));
			}
		}

	}
}
