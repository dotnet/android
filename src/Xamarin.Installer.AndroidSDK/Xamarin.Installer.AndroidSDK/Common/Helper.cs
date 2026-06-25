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
using System.Net;
using System.Net.Http;
using ICSharpCode.SharpZipLib.Zip;
using Xamarin.Installer.Common;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Xamarin.Installer.AndroidSDK.Common;
using System.Threading;
using System.Linq;
using Polly;
using Polly.Retry;

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

		static ConcurrentDictionary<Uri, Tuple<DateTimeOffset, string>> stringDownloadCache = new ConcurrentDictionary<Uri, Tuple<DateTimeOffset, string>> ();

		public Helper (string cacheFolder = null)
		{
			CacheFolder = cacheFolder;
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
            // ships alongside this assembly (XamarinRepository.GetFallbackManifestUrl).
            if (url.Scheme == "file") {
                var assemblyDir = Path.GetDirectoryName (typeof (Helper).Assembly.Location);
                var requestedDir = Path.GetDirectoryName (url.LocalPath);
                if (!string.IsNullOrEmpty (assemblyDir) &&
                    string.Equals (assemblyDir, requestedDir, StringComparison.OrdinalIgnoreCase)) {
                    output = File.ReadAllText (url.LocalPath);
                    return true;
                }
                Logger.Warning ($"Ignoring file:// manifest URL '{url}': file:// is only honored for the in-box fallback manifest in shipped builds.");
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
			ZipFile zip = null;

			try
			{
				zip = new ZipFile (archivePath);

				float minProgressDelta = 5f; // percents
				float lastProgress = 0f;
				float progress;

				ulong totalSize = DirectorySizeMonitoringTimer.CalculateTotalSize (zip);
				ulong currentSize = 0, gradualSize;
				try {
					progressCallback?.Invoke (0f);
				} catch (Exception ex) {
					Logger.Error ($"[Unzip] progress callback exception: {ex}");
				}

				string fullBaseDirectory = Path.GetFullPath (baseDirectory).TrimEnd (Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

				foreach (ZipEntry entry in zip) {
					string destPath = Path.Combine (baseDirectory, entry.Name);
					lastDestPath = destPath;

					if (!Path.GetFullPath (destPath).StartsWith (fullBaseDirectory, StringComparison.OrdinalIgnoreCase)) {
						throw new Exception ($"Archive file '{archivePath}' has parent traversal in paths");
					}

					if (entry.IsDirectory) {
						Directory.CreateDirectory (destPath);
						SafeSetLastWriteTime (destPath, entry.DateTime);
						continue;
					}

					Directory.CreateDirectory (Path.GetDirectoryName (destPath));

					byte [] buf = new byte [4096];
					int n = 0;
					gradualSize = 0;
					ulong iteration = 0;
					using (var fs = new FileStream (destPath, FileMode.Create, FileAccess.Write, FileShare.None)) {
						using (var inZip = zip.GetInputStream (entry)) {
							while ((n = inZip.Read (buf, 0, buf.Length)) > 0) {
								fs.Write (buf, 0, n);
								gradualSize += (ulong) n;
								iteration++;

								if (iteration % 1000 == 0 && progressCallback != null) {
									progress = (currentSize + gradualSize) * 100f / totalSize;
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
						}
						currentSize += (ulong) fs.Length;
					}
					SafeSetLastWriteTime (destPath, entry.DateTime);
					SetFileAttributes (destPath, entry.IsDOSEntry, entry.ExternalFileAttributes);
					// TODO: set owner

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
			} finally {
				zip?.Close ();
			}

			string topDirectory = string.Empty;
			string [] entries = Directory.GetDirectories (baseDirectory);

			if (entries.Length == 1)
				topDirectory = entries [0];

			string ret = Path.GetFullPath (Path.Combine (baseDirectory, topDirectory));
			Logger.Info ($"Archive unzipped to '{ret}'");

			return ret;
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

		static void SetFileAttributes (string filePath, bool dosAttributes, int zipAttributes)
		{
			if (String.IsNullOrEmpty(filePath) || !File.Exists(filePath))
				return;
			if (dosAttributes && zipAttributes != -1) {
				var fileAttributes = (FileAttributes)zipAttributes;
				fileAttributes &= (FileAttributes.Archive | FileAttributes.Normal | FileAttributes.ReadOnly | FileAttributes.Hidden);
				File.SetAttributes(filePath, fileAttributes);
			} else
				File.SetAttributes(filePath, FileAttributes.Normal);

			#if !WINDOWS
			if (Platform.IsWindows)
				return;
			
			zipAttributes = (zipAttributes >> 16);

			if (zipAttributes == -1)
				return;

			FilePermissions permissions = unchecked((FilePermissions)zipAttributes);
			if (permissions == 0) {
				// 644 octal
				permissions = FilePermissions.S_IRUSR |
					      FilePermissions.S_IWUSR |
					      FilePermissions.S_IRGRP |
					      FilePermissions.S_IROTH;
			} else if ((permissions & FilePermissions.S_IRUSR) == 0)
				permissions |= FilePermissions.S_IRUSR; // owner must be able to read the file

			if (Syscall.chmod(filePath, permissions) == -1)
				throw new InvalidOperationException (String.Format("Failed to set file attributes for '{0}'. {1}",
				                                                   filePath, Stdlib.strerror(Stdlib.GetLastError())));
			#endif
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

		public async Task<bool> CheckIfNetworkIsAvailableAsync ()
		{
			var policyResult  = await ResiliencyPolicies.AsyncRetryIfTimeout (3)
				.ExecuteAndCaptureAsync (() => CheckIfNetworkAvailable ());
			if (policyResult.Outcome == OutcomeType.Successful)
				return policyResult.Result;

			return false;
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

		public static class ResiliencyPolicies
		{
			public static AsyncRetryPolicy AsyncRetryIfTimeout (int numberOfRetries, Action actionToPerformOnRetry = null)
			{
				return Policy
					.Handle<TimeoutException> ()
					.Or<TaskCanceledException>()
					.WaitAndRetryAsync (numberOfRetries,
						(x) => TimeSpan.FromSeconds (1),
							(exception, timespan) => {
								Logger.Warning ($"[Resiliency.Polly.Policies]: Delegate has thrown {exception}, retrying...");
								actionToPerformOnRetry?.Invoke ();
							});
			}
		}
	}
}
