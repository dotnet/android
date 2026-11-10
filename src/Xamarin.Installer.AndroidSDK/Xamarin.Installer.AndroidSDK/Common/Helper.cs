//
// Helper.cs
//
// Author:
//       Vsevolod Kukol <sevoku@microsoft.com>
//
//  Copyright (c) 2017, Microsoft, Inc
//

using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using Xamarin.Installer.Common;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Xamarin.Installer.AndroidSDK.Common;
using System.Threading;
using System.Linq;

#if !WINDOWS
using Mono.Unix.Native;
#endif

using System.Runtime.InteropServices;

namespace Xamarin.Installer.AndroidSDK.Manager
{
	public class Helper : IHelpers
	{
		public static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds (Platform.IsWindows ? 60 : 10);
		static string homeDirectory;
		ConfigManager config = new ConfigManager ();

		public string CacheFolder { get; set; }

		readonly string manifestFilePath;

		static ConcurrentDictionary<Uri, Tuple<DateTimeOffset, string>> stringDownloadCache = new ConcurrentDictionary<Uri, Tuple<DateTimeOffset, string>> ();

		public Helper (string cacheFolder = null, string manifestFilePath = null)
		{
			CacheFolder = cacheFolder;
			this.manifestFilePath = manifestFilePath;
		}

		public static string HomeDirectory {
			get
			{
				if (string.IsNullOrEmpty(homeDirectory)) {
					homeDirectory = Environment.GetEnvironmentVariable("HOME");
					if (String.IsNullOrEmpty(homeDirectory) || !Directory.Exists(homeDirectory)) {
						homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
					}
				}
				return homeDirectory;
			}
		}

		string IHelpers.HomeDirectory {
			get {
				return HomeDirectory;
			}
		}

		public bool IsArm64
		{
			get {
				return RuntimeInformation.ProcessArchitecture == Architecture.Arm64;
			}
		}

		public bool Is64BitOS {
			get {
				return Environment.Is64BitOperatingSystem;
			}
		}

		public bool IsCaseSensitiveFileSystem {
			get { return true; } // TODO
		}

		public string UserName {
			get {
				if (Platform.IsWindows)
					return Environment.UserName;
				return Environment.GetEnvironmentVariable ("USER");
			}
		}

		public bool DownloadToString (Uri url, out string output)
		{
#if DEBUG
            // DEBUG: support setting XAMARIN_ANDROID_MANIFEST_URL=file:///… for local manifest authoring.
            if (url.Scheme == "file")
            {
                output = File.ReadAllText(url.LocalPath);
                return true;
            }
#else
            // Release: only allow https://, plus the in-box file:// fallback manifest that
            // ships alongside this assembly (XamarinRepository.GetFallbackManifestUrl), or a
            // local manifest path explicitly supplied by InstallAndroidDependencies.
            if (url.Scheme == "file") {
                var assemblyDir = Path.GetDirectoryName (typeof (Helper).Assembly.Location);
                var requestedDir = Path.GetDirectoryName (url.LocalPath);
                if ((!string.IsNullOrEmpty (manifestFilePath) && string.Equals (manifestFilePath, url.LocalPath, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrEmpty (assemblyDir) && string.Equals (assemblyDir, requestedDir, StringComparison.OrdinalIgnoreCase))) {
                    output = File.ReadAllText (url.LocalPath);
                    return true;
                }
                Logger.Warning ($"Ignoring file:// manifest URL '{url}': file:// is only honored for an explicit local manifest path or the in-box fallback manifest in shipped builds.");
                output = null;
                return false;
            }
            if (url.Scheme != "https") {
                Logger.Warning ($"Ignoring manifest URL '{url}': only https:// URLs are honored in shipped builds.");
                output = null;
                return false;
            }
#endif

			// FIXME: returning false on failure doesn't make Installer.Discover return false.
			//        hence we need a hard throw on failure to detect it on higher levels
			//try {

			using (var client = HttpClientProvider.CreateHttpClient (url)) {
				client.Timeout = HttpTimeout;
				var req = new HttpRequestMessage (HttpMethod.Get, url);

				if (stringDownloadCache.TryGetValue(url, out Tuple<DateTimeOffset, string> cache)) {
					req.Headers.IfModifiedSince = cache.Item1;
					output = cache.Item2;
				}

				//try {
					using (var res = client.SendAsync (req, HttpCompletionOption.ResponseHeadersRead).Result) {
						if (res.StatusCode == HttpStatusCode.NotModified) {
							output = cache.Item2;
							return true;
						}
						if (res.StatusCode != HttpStatusCode.OK) {
							Logger.Warning ($"Unable to download manifest xml from {url}. Status Code: {res.StatusCode} - {(int)res.StatusCode}");
							output = null;
							return false;
						}
						output = res.Content.ReadAsStringAsync ().Result;
						DateTimeOffset? lastModified = res.Content.Headers.LastModified;
						if (lastModified.HasValue)
							stringDownloadCache[url] = Tuple.Create (lastModified.Value, output);
						return true;
					}
				//} catch (Exception ex) {
				//	Logger.Exception ($"Downloading '{url}' failed", ex);
				//	output = null;
				//	return false;
				//}
			}
		}

		public string GetPathForDownloadId (Guid id)
		{
			return Path.Combine (Path.GetTempPath (), "xamarin-android-sdk", id.ToString () + ".zip");
		}

		public ulong GetUrlContentLength (Uri url)
		{
			using (var client = HttpClientProvider.CreateHttpClient (url)) {
				client.Timeout = HttpTimeout;
				var request = new HttpRequestMessage(HttpMethod.Head, url);
				var resp = client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).Result;
				if (resp.IsSuccessStatusCode)
					return (ulong)(resp.Content.Headers.ContentLength ?? 0);
				return 0;
			}
		}

		public bool IsSpecialFile (string filePath)
		{
			return false;
		}

		public void CopySpecialFile (string source, string target)
		{
			throw new NotImplementedException ();
		}

		public class CustomTooLongPathException : Exception
		{
			public Exception OriginalException { get; set; }
		}

		public string Unzip (string baseDirectory, string archivePath, string fileOwnerName = null)
		{
			return Unzip (baseDirectory, archivePath, fileOwnerName, progressCallback: null);
		}

		public string Unzip (string baseDirectory, string archivePath, string fileOwnerName = null,
			InstallationProgressEventArgs.InstallationProgressActionDelegate progressCallback = null)
		{
			if (string.IsNullOrEmpty (baseDirectory))
				throw new ArgumentNullException (nameof (baseDirectory), "No base directory given, unable to unzip.");
			if (string.IsNullOrEmpty (archivePath))
				throw new ArgumentNullException (nameof (archivePath), "No ZIP archive specified.");
			Logger.Info ($"Unzipping file '{archivePath}' to directory '{baseDirectory}'");

			if (!Directory.Exists (baseDirectory)) {
				try {
					Directory.CreateDirectory (baseDirectory);
					// TODO: set directory owner and permissions
				} catch (Exception ex) {
					Logger.Exception ($"Failed to create base directory '{baseDirectory}' to unzip archive.", ex);
					return null;
				}
			}

			string lastDestPath = String.Empty;

			try
			{
				using (var zipFile = ZipFile.OpenRead (archivePath)) {
					float minProgressDelta = 5f; // percents
					float lastProgress = 0f;
					float progress;

					ulong totalSize = DirectorySizeMonitoringTimer.CalculateTotalSize (zipFile);
					ulong currentSize = 0;
					try {
						progressCallback?.Invoke (0f);
					} catch (Exception ex) {
						Logger.Error ($"[Unzip] progress callback exception: {ex}");
					}

					string fullBaseDirectory = Path.GetFullPath (baseDirectory).TrimEnd (Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

					foreach (var entry in zipFile.Entries) {
						string destPath = Path.Combine (baseDirectory, entry.FullName);
						lastDestPath = destPath;

						if (!Path.GetFullPath (destPath).StartsWith (fullBaseDirectory, StringComparison.OrdinalIgnoreCase)) {
							throw new Exception ($"Archive file '{archivePath}' has parent traversal in paths");
						}

						if (entry.FullName.EndsWith ("/", StringComparison.Ordinal) || entry.FullName.EndsWith ("\\", StringComparison.Ordinal)) {
							Directory.CreateDirectory (destPath);
							SafeSetLastWriteTime (destPath, entry.LastWriteTime.LocalDateTime);
							continue;
						}

						Directory.CreateDirectory (Path.GetDirectoryName (destPath));
						// Delete first: the create mode applies only to new files, so an overwrite would keep the old permissions.
						if (File.Exists (destPath))
							File.Delete (destPath);
						entry.ExtractToFile (destPath, overwrite: false);
						SetUnixPermissions (destPath, entry);
						// TODO: set owner

						currentSize += (ulong) entry.Length;
						progress = currentSize * 100f / totalSize;
						if (progress - lastProgress > minProgressDelta) {
							try {
								progressCallback?.Invoke (progress);
							} catch (Exception ex) {
								Logger.Error ($"[Unzip] progress callback exception: {ex}");
							}
							lastProgress = progress;
						}
					}
				}

				try {
					progressCallback?.Invoke (100f);
				} catch (Exception ex) {
					Logger.Error ($"[Unzip] progress callback exception: {ex}");
				}
			} catch (DirectoryNotFoundException ex) {
				Logger.Error ($"Unzip failed with DirectoryNotFoundException error: {ex}");
				if (lastDestPath.Length >= 240) {
					Logger.Error ($"Try enabling long paths support to resolve this issue: https://blogs.msdn.microsoft.com/jeremykuhne/2016/07/30/net-4-6-2-and-long-paths-on-windows-10/");
					throw new CustomTooLongPathException { OriginalException = ex };
				}
				throw;
			}

			string topDirectory = string.Empty;
			string [] entries = Directory.GetDirectories (baseDirectory);

			if (entries.Length == 1)
				topDirectory = entries [0];

			string ret = Path.GetFullPath (Path.Combine (baseDirectory, topDirectory));
			Logger.Info ($"Archive unzipped to '{ret}'");

			return ret;
		}

		// ZipArchiveEntry.ExternalAttributes exists at runtime on .NET and .NET Framework 4.7.2+, but not in the netstandard2.0 reference assemblies.
		static readonly System.Reflection.PropertyInfo ExternalAttributesProperty = typeof (ZipArchiveEntry).GetProperty ("ExternalAttributes");

		static void SetUnixPermissions (string filePath, ZipArchiveEntry entry)
		{
#if !WINDOWS
			if (Platform.IsWindows)
				return;
			if (ExternalAttributesProperty == null)
				throw new PlatformNotSupportedException ("ZIP entry permissions are not available on this runtime.");

			int mode = (int) ExternalAttributesProperty.GetValue (entry) >> 16;
			if (mode == -1)
				return;

			var permissions = mode == 0
				? FilePermissions.S_IRUSR | FilePermissions.S_IWUSR | FilePermissions.S_IRGRP | FilePermissions.S_IROTH // 0644
				: (FilePermissions) (mode & 0x1FF) | FilePermissions.S_IRUSR; // drop setuid/setgid/sticky; owner must be able to read

			if (Syscall.chmod (filePath, permissions) == -1)
				throw new InvalidOperationException ($"Failed to set file attributes for '{filePath}'. {Stdlib.strerror (Stdlib.GetLastError ())}");
#endif
		}

		static void SafeSetLastWriteTime (string path, DateTime stamp)
		{
			try {
				if (Directory.Exists (path))
					Directory.SetLastWriteTime (path, stamp);
				else if (File.Exists (path))
					File.SetLastWriteTime (path, stamp);
			} catch {
				// Just ignoring this exception, time stamp is not THAT important...
			}
		}

		public bool URLExists (Uri url)
		{
			throw new NotImplementedException ();
		}

		public string GetRegistryKeyValue (string subKeyPath, string keyName, bool check64Node)
		{
			throw new NotImplementedException ();
		}

		public string GetPluralString (string s, string p, int n)
		{
			return n == 1 ? s : p;
		}

		public string GetString (string s)
		{
			return s;
		}

		public string GetProperty (string key, string defaultValue = "")
		{
			return config.GetProperty (key, defaultValue);
		}

		public void SetProperty (string key, string value)
		{
			config.SetProperty (key, value);
			config.SaveProperties ();
		}

		const int NetworkCheckRetryCount = 3;
		static readonly TimeSpan NetworkCheckRetryDelay = TimeSpan.FromSeconds (1);

		public async Task<bool> CheckIfNetworkIsAvailableAsync ()
		{
			try {
				return await RetryIfTimeoutAsync (CheckIfNetworkAvailable, NetworkCheckRetryCount, NetworkCheckRetryDelay);
			} catch (TimeoutException) {
				return false;
			} catch (TaskCanceledException) {
				return false;
			}
		}

		static async Task<T> RetryIfTimeoutAsync<T> (Func<Task<T>> action, int retryCount, TimeSpan retryDelay)
		{
			for (int attempt = 0; ; attempt++) {
				try {
					return await action ().ConfigureAwait (false);
				} catch (Exception exception) when (attempt < retryCount && (exception is TimeoutException || exception is TaskCanceledException)) {
					Logger.Warning ($"[Resiliency.Polly.Policies]: Delegate has thrown {exception}, retrying...");
				}
				await Task.Delay (retryDelay).ConfigureAwait (false);
			}
		}

		private Uri GetUrlToCheckIfNetworkAvailable()
		{
			var customURL = Environment.GetEnvironmentVariable("ANDROID_SDK_INSTALLER_CHECK_NETWORK_URL");
			if (!string.IsNullOrEmpty(customURL) && Uri.TryCreate(customURL, UriKind.Absolute, out Uri url))	
			{
				return url;
			} else
			{
				return new Uri("https://dotnet.microsoft.com/");
			}
		}

		async Task<bool> CheckIfNetworkAvailable ()
		{
			try {
				var uri = GetUrlToCheckIfNetworkAvailable();
				using (var client = HttpClientProvider.CreateHttpClient (uri)) {
					client.Timeout = HttpTimeout;
					var request = new HttpRequestMessage (HttpMethod.Get, uri);
					using (var response = await client.SendAsync (request, HttpCompletionOption.ResponseHeadersRead))
						return response.IsSuccessStatusCode;
				}
			} catch (TimeoutException) {
				throw;
			} catch (TaskCanceledException) {
				throw;
			} catch (HttpRequestException) {
				return false;
			} catch (AggregateException ex) {
				foreach (var innerEx in ex.Flatten ().InnerExceptions)
					if (!(innerEx is HttpRequestException))
						Logger.Warning ($"An exception occurred while checking network availability.\n{innerEx}");

				if (ex.Flatten ().InnerException is TaskCanceledException)
					throw ex.Flatten ().InnerException;

				return false;
			} catch (Exception ex) {
				Logger.Warning ($"An exception occurred while checking network availability.\n{ex}");
				return false;
			}
		}
	}
}
