//
// This is a quick and dirty utility to update LLVM sources we copied locally
// for use with the NativeAOT runtime in order to avoid dependency on libc++, which
// NativeAOT cannot use on NDK r29 and newer, because of symbol conflicts (see https://github.com/dotnet/runtime/issues/121172)
//
// The utility is supposed to die fast and loud if anything goes wrong.
//
using System;
using System.IO;
using System.Net.Http;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Xamarin.Android.Tools;

class App
{
	static readonly string LocalSourcesPath = Path.Combine ("..", "..", "src-ThirdParty", "llvm");
	static readonly TimeSpan ExceptionRetryInitialDelay = TimeSpan.FromSeconds (30);
	static readonly TimeSpan WebRequestTimeout = TimeSpan.FromMinutes (60);
        static readonly int ExceptionRetries = 5;

	// URLs finally look like: https://android.googlesource.com/toolchain/llvm-project/+/5e96669f06077099aa41290cdb4c5e6fa0f59349/libcxx/src/hash.cpp?format=TEXT
	// Returned text is base64-encoded
	static int Main ()
	{
		try {
			Console.WriteLine ("Updating LLVM sources for NativeAOT runtime.");
			Console.WriteLine ();
			Console.WriteLine ($"NDK release: {XABuildConfig.NDKRelease}");
			Console.WriteLine ($"NDK revision: {XABuildConfig.NDKRevision}");
			Console.WriteLine ($"LLVM version: {LlvmUpdateInfo.Version}");
			Console.WriteLine ($"LLVM GIT revision: {LlvmUpdateInfo.Revision}");
			Console.WriteLine ($"LLVM GIT URL: {LlvmUpdateInfo.BaseUrl}");

			return UpdateSources () ? 0 : 1;
		} catch (Exception ex) {
			Console.Error.WriteLine ("Failed to update LLVM sources. Exception was thrown:");
			Console.Error.WriteLine (ex.ToString ());
			Console.Error.WriteLine ();
			return 1;
		}
	}

	static bool UpdateSources ()
	{
		Console.WriteLine ();
		Console.WriteLine ("Checking for updates:");
		var options = new EnumerationOptions {
			RecurseSubdirectories = true,
		};

		foreach (string file in Directory.EnumerateFiles (LocalSourcesPath, "*.*", options)) {
			UpdateSource (file);
		}

		return true;
	}

	static void UpdateSource (string localPath)
	{
		Console.WriteLine ($" * {localPath}");

		var uriBuilder = new UriBuilder (LlvmUpdateInfo.BaseUrl);
		uriBuilder.Path += "/" + Path.GetRelativePath (LocalSourcesPath, localPath);
		uriBuilder.Query = "format=TEXT";

		var fileUrl = uriBuilder.Uri;
		string? tempFilePath = null;
		string? decodedFilePath = null;

		try {
			tempFilePath = DownloadFile (fileUrl);
			decodedFilePath = UpdateIfNecessary (localPath, tempFilePath);
		} finally {
			DeleteFile (tempFilePath);
			DeleteFile (decodedFilePath);
		}
	}

	static string UpdateIfNecessary (string localPath, string? remotePath)
	{
		if (String.IsNullOrEmpty (remotePath)) {
			throw new InvalidOperationException ("Remote file not downloaded properly.");
		}

		var fi = new FileInfo (remotePath);
		if (!fi.Exists) {
			throw new InvalidOperationException ($"Remote file '{remotePath}' does not exist.");
		}

		if (fi.Length == 0) {
			throw new InvalidOperationException ($"Remove file '{remotePath}' is empty.");
		}

		// Remote files are base64-encoded
		string decodedFilePath = DecodeFile (remotePath);

		byte[] localHash = GetFileHash (localPath);
		byte[] remoteHash = GetFileHash (decodedFilePath);

		if (ArraysAreEqual (localHash, remoteHash)) {
			Console.WriteLine ($"   Local file is identical to the remote one. No need to update.");
			return decodedFilePath;
		}

		Console.WriteLine ($"   Local file is different to the remote one. Updating.");
		File.Copy (decodedFilePath, localPath, overwrite: true);

		return decodedFilePath;
	}

	static bool ArraysAreEqual (byte[] a, byte[] b)
	{
		if (a.Length != b.Length) {
			return false;
		}

		for (int i = 0; i < a.Length; i++) {
			if (a[i] != b[i]) {
				return false;
			}
		}

		return true;
	}

	static string DecodeFile (string remotePath)
	{
		string decodedFilePath = Path.GetTempFileName ();
		using var inFile = File.OpenRead (remotePath);
		using var outFile = File.OpenWrite (decodedFilePath);
		using var transform = new FromBase64Transform (FromBase64TransformMode.IgnoreWhiteSpaces);
		byte[] outputBytes = new byte[transform.OutputBlockSize];

		// Input sources are small, no need to do anything fancy here.
		byte[] inputBytes = File.ReadAllBytes (remotePath);

		// Transform the data in chunks the size of InputBlockSize.
		int i = 0;
		while (inputBytes.Length - i > transform.InputBlockSize) {
			int bytesWritten = transform.TransformBlock (inputBytes, i, transform.InputBlockSize, outputBytes, 0);
			i += transform.InputBlockSize;
			outFile.Write (outputBytes, 0, bytesWritten);
		}

		// Transform the final block of data.
		outputBytes = transform.TransformFinalBlock (inputBytes, i, inputBytes.Length - i);
		outFile.Write (outputBytes, 0, outputBytes.Length);
		outFile.Flush ();
		outFile.Close ();

		// Free up any used resources.
		transform.Clear ();
		return decodedFilePath;
	}

	static string DownloadFile (Uri url)
	{
		string targetFile = Path.GetTempFileName ();
		Console.WriteLine ($"   Downloading: {url}");

		TimeSpan delay = ExceptionRetryInitialDelay;
                for (int i = 0; i < ExceptionRetries; i++) {
                        try {
                                DoDownload (url, targetFile);
                                break;
                        } catch (Exception ex) {
                                Console.Error.WriteLine ($"    Download of '{url}', attempt {i} failed: {ex.Message}");
                                if (i < ExceptionRetries - 1) {
					Console.Error.WriteLine ($"    Retrying after delay ({delay})");
                                        WaitAWhile ($"Download {url}", i, ref delay);
                                } else {
					throw;
				}
                        }
                }

		return targetFile;
	}

	static void DoDownload (Uri url, string targetFile)
	{
		using var httpClient = CreateHttpClient ();
		httpClient.Timeout = WebRequestTimeout;
		HttpResponseMessage resp = httpClient.GetAsync (url, HttpCompletionOption.ResponseHeadersRead).Result;
		resp.EnsureSuccessStatusCode ();

		using var fs = File.Open (targetFile, FileMode.Create, FileAccess.Write);
		using var webStream = resp.Content.ReadAsStreamAsync ().Result;
		var buf = new byte [16384];
                int nread;

		while ((nread = webStream.Read (buf, 0, buf.Length)) > 0) {
                        fs.Write (buf, 0, nread);
                }

                fs.Flush ();
		fs.Close ();
	}

	static HttpClient CreateHttpClient ()
        {
                // Originally from: https://github.com/dotnet/arcade/pull/15546
                // Configure the cert revocation check in a fail-open state to avoid intermittent failures
                // on Mac if the endpoint is not available. This is only available on .NET Core, but has only been
                // observed on Mac anyway.

                var handler = new SocketsHttpHandler ();
                handler.SslOptions.CertificateChainPolicy = new X509ChainPolicy {
                        // Yes, check revocation.
                        // Yes, allow it to be downloaded if needed.
                        // Online is the default, but it doesn't hurt to be explicit.
                        RevocationMode = X509RevocationMode.Online,
                        // Roots never bother with revocation.
                        // ExcludeRoot is the default, but it doesn't hurt to be explicit.
                        RevocationFlag = X509RevocationFlag.ExcludeRoot,
                        // RevocationStatusUnknown at the EndEntity/Leaf certificate will not fail the chain build.
                        // RevocationStatusUnknown for any intermediate CA will not fail the chain build.
                        // IgnoreRootRevocationUnknown could also be specified, but it won't apply given ExcludeRoot above.
                        // The default is that all status codes are bad, this is not the default.
                        VerificationFlags =
                                        X509VerificationFlags.IgnoreCertificateAuthorityRevocationUnknown |
                                        X509VerificationFlags.IgnoreEndRevocationUnknown,
                        // Always use the "now" when building the chain, rather than the "now" of when this policy object was constructed.
                        VerificationTimeIgnored = true,
                };

                return new HttpClient (handler);
        }

	static void DeleteFile (string? path)
	{
		if (String.IsNullOrEmpty (path) || !File.Exists (path)) {
			return;
		}

		try {
			File.Delete (path);
		} catch (Exception) {
			// Swallow, doesn't really matter
		}
	}
	static string HashToString (byte[] hash)
	{
		var sb = new StringBuilder ();

		foreach (byte b in hash) {
			sb.Append ($"{b:x02}");
		}
		return sb.ToString ();
	}

	static byte[] GetFileHash (string path)
	{
		using Stream fs = File.OpenRead (path);
		using var sha256 = SHA256.Create ();

		return sha256.ComputeHash (fs);
	}

	static void WaitAWhile (string what, int which, ref TimeSpan delay)
        {
                Thread.Sleep (delay);
                delay = TimeSpan.FromMilliseconds (delay.TotalMilliseconds * 2);
        }

}
