using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using NUnit.Framework;

namespace Xamarin.ProjectTools
{
	public class DownloadedCache
	{
		static readonly ConcurrentDictionary<string, object> locks = new ConcurrentDictionary<string, object> ();
		static readonly HttpClient httpClient = new HttpClient ();

		public DownloadedCache ()
			: this (Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.LocalApplicationData), "Xamarin.ProjectTools"))
		{
		}

		public DownloadedCache (string cacheDirectory)
		{
			if (cacheDirectory == null)
				throw new ArgumentNullException ("cacheDirectory");
			CacheDirectory = cacheDirectory;
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
					TestContext.WriteLine ($"Transient network error downloading '{url}':");
					TestContext.WriteLine ($"  Message: {ex.Message}");
					if (ex.StatusCode.HasValue) {
						TestContext.WriteLine ($"  HTTP Status Code: {(int)ex.StatusCode.Value} ({ex.StatusCode.Value})");
					}
					TestContext.WriteLine ($"  URL: {url}");
					TestContext.WriteLine ($"  Stack Trace: {ex.StackTrace}");
					Assert.Inconclusive ($"Test skipped due to transient network error: {ex.Message}");
					try {
						File.Delete (filename);
					} catch {
						// Ignore any errors cleaning up the partially written file.
					}
				}
				return filename;
			}
		}

		static bool IsTransientError (HttpRequestException ex)
		{
			// Check for common transient errors
			if (ex.StatusCode is HttpStatusCode statusCode) {
				return statusCode == HttpStatusCode.RequestTimeout ||
						statusCode == HttpStatusCode.GatewayTimeout ||
						statusCode == HttpStatusCode.ServiceUnavailable ||
						statusCode == HttpStatusCode.BadGateway;
			}
			return false;
		}
	}
}

