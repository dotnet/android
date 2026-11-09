using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;

namespace Xamarin.ProjectTools
{
	public partial class DownloadedCache
	{
		static readonly ConcurrentDictionary<string, object> locks = new ConcurrentDictionary<string, object> ();
		static readonly HttpClient httpClient = new HttpClient ();

		readonly Action<string> writeLine;
		readonly Action<string> inconclusive;

		/// <summary>
		/// Routes diagnostics and transient failures through the calling test's framework.
		/// The inconclusive callback must throw the framework's inconclusive exception.
		/// </summary>
		public DownloadedCache (string cacheDirectory, Action<string> writeLine, Action<string> inconclusive)
		{
			if (cacheDirectory == null)
				throw new ArgumentNullException ("cacheDirectory");
			CacheDirectory = cacheDirectory;
			this.writeLine = writeLine ?? throw new ArgumentNullException (nameof (writeLine));
			this.inconclusive = inconclusive ?? throw new ArgumentNullException (nameof (inconclusive));
		}

		public string CacheDirectory { get; private set; }

		public string GetAsFile (string url, string filename = "")
		{
			Directory.CreateDirectory (CacheDirectory);

			filename = Path.Combine (CacheDirectory, string.IsNullOrEmpty (filename) ? Path.GetFileName (new Uri (url).LocalPath) : filename);
			lock (locks.GetOrAdd (filename, _ => new object ())) {
				if (File.Exists (filename))
					return filename;
				// FIXME: should be clever enough to resolve name conflicts.
				try {
					using (var response = httpClient.GetAsync (url).GetAwaiter ().GetResult ()) {
						response.EnsureSuccessStatusCode ();
						using (var fileStream = File.Create (filename))
						using (var httpStream = response.Content.ReadAsStreamAsync ().GetAwaiter ().GetResult ()) {
							httpStream.CopyTo (fileStream);
						}
					}
				} catch (HttpRequestException ex) when (IsTransientError (ex)) {
					writeLine ($"Transient network error downloading '{url}':");
					writeLine ($"  Message: {ex.Message}");
					if (ex.StatusCode.HasValue) {
						writeLine ($"  HTTP Status Code: {(int)ex.StatusCode.Value} ({ex.StatusCode.Value})");
					}
					writeLine ($"  URL: {url}");
					writeLine ($"  Stack Trace: {ex.StackTrace}");
					inconclusive ($"Test skipped due to transient network error: {ex.Message}");
					try {
						File.Delete (filename);
					} catch {
						// Ignore any errors cleaning up the partially written file.
					}
					throw new InvalidOperationException ("The inconclusive callback must throw to stop the test.");
				}
				return filename;
			}
		}

		static bool IsTransientError (HttpRequestException ex)
		{
			// Check for common transient HTTP status codes
			if (ex.StatusCode is HttpStatusCode statusCode) {
				return statusCode == HttpStatusCode.RequestTimeout ||
						statusCode == HttpStatusCode.GatewayTimeout ||
						statusCode == HttpStatusCode.ServiceUnavailable ||
						statusCode == HttpStatusCode.BadGateway;
			}

			// Check for socket/DNS errors (e.g., "nodename nor servname provided, or not known")
			if (ex.InnerException is SocketException) {
				return true;
			}

			// Check for SSL/TLS errors (e.g., "Received an unexpected EOF or 0 bytes from the transport stream")
			if (ex.InnerException is IOException) {
				return true;
			}

			return false;
		}
	}
}
