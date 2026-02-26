// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Xamarin.Android.Tools
{
	/// <summary>
	/// Shared helpers for downloading files, verifying checksums, and extracting archives.
	/// </summary>
	static class DownloadUtils
	{
		const int BufferSize = 81920;
		const long BytesPerMB = 1024 * 1024;
		static readonly char[] WhitespaceChars = [' ', '\t', '\n', '\r'];

		static Task<Stream> ReadAsStreamAsync (HttpContent content, CancellationToken cancellationToken)
		{
#if NET5_0_OR_GREATER
			return content.ReadAsStreamAsync (cancellationToken);
#else
			return content.ReadAsStreamAsync ();
#endif
		}

		static Task<string> ReadAsStringAsync (HttpContent content, CancellationToken cancellationToken)
		{
#if NET5_0_OR_GREATER
			return content.ReadAsStringAsync (cancellationToken);
#else
			return content.ReadAsStringAsync ();
#endif
		}

		/// <summary>Downloads a file from the given URL with optional progress reporting.</summary>
		public static async Task DownloadFileAsync (HttpClient client, string url, string destinationPath, long expectedSize, IProgress<(double percent, string message)>? progress, CancellationToken cancellationToken)
		{
			using var response = await client.GetAsync (url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait (false);
			response.EnsureSuccessStatusCode ();

			var totalBytes = response.Content.Headers.ContentLength ?? expectedSize;

			using var contentStream = await ReadAsStreamAsync (response.Content, cancellationToken).ConfigureAwait (false);

			var dirPath = Path.GetDirectoryName (destinationPath);
			if (!string.IsNullOrEmpty (dirPath))
				Directory.CreateDirectory (dirPath);

			using var fileStream = new FileStream (destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, useAsync: true);

			var buffer = ArrayPool<byte>.Shared.Rent (BufferSize);
			try {
				long totalRead = 0;
				int bytesRead;

				while ((bytesRead = await contentStream.ReadAsync (buffer, 0, buffer.Length, cancellationToken).ConfigureAwait (false)) > 0) {
					await fileStream.WriteAsync (buffer, 0, bytesRead, cancellationToken).ConfigureAwait (false);
					totalRead += bytesRead;

					if (progress is not null && totalBytes > 0) {
						var pct = (double) totalRead / totalBytes * 100;
						progress.Report ((pct, $"Downloaded {totalRead / BytesPerMB} MB / {totalBytes / BytesPerMB} MB"));
					}
				}
			}
			finally {
				ArrayPool<byte>.Shared.Return (buffer);
			}
		}

		/// <summary>Verifies a file's SHA-256 hash against an expected value.</summary>
		public static void VerifyChecksum (string filePath, string expectedChecksum)
		{
			using var sha256 = SHA256.Create ();
			using var stream = File.OpenRead (filePath);

			var hash = sha256.ComputeHash (stream);
			var actual = BitConverter.ToString (hash).Replace ("-", "").ToLowerInvariant ();

			if (!string.Equals (actual, expectedChecksum, StringComparison.OrdinalIgnoreCase))
				throw new InvalidOperationException ($"Checksum verification failed. Expected: {expectedChecksum}, Actual: {actual}");
		}

		/// <summary>Extracts a ZIP archive with Zip Slip protection.</summary>
		public static void ExtractZipSafe (string archivePath, string destinationPath, CancellationToken cancellationToken)
		{
			using var archive = ZipFile.OpenRead (archivePath);
			var fullExtractRoot = Path.GetFullPath (destinationPath);

			foreach (var entry in archive.Entries) {
				cancellationToken.ThrowIfCancellationRequested ();

				if (string.IsNullOrEmpty (entry.Name))
					continue;

				var destinationFile = Path.GetFullPath (Path.Combine (fullExtractRoot, entry.FullName));

				// Zip Slip protection
				if (!FileUtil.IsUnderDirectory (destinationFile, fullExtractRoot)) {
					throw new InvalidOperationException ($"Archive entry '{entry.FullName}' would extract outside target directory.");
				}

				var entryDir = Path.GetDirectoryName (destinationFile);
				if (!string.IsNullOrEmpty (entryDir))
					Directory.CreateDirectory (entryDir);

				entry.ExtractToFile (destinationFile, overwrite: true);
			}
		}

		/// <summary>Extracts a tar.gz archive using the system tar command.</summary>
		public static async Task ExtractTarGzAsync (string archivePath, string destinationPath, Action<TraceLevel, string> logger, CancellationToken cancellationToken)
		{
			var psi = ProcessUtils.CreateProcessStartInfo ("/usr/bin/tar", "-xzf", archivePath, "-C", destinationPath);

			using var stdout = new StringWriter ();
			using var stderr = new StringWriter ();
			var exitCode = await ProcessUtils.StartProcess (psi, stdout: stdout, stderr: stderr, cancellationToken).ConfigureAwait (false);

			if (exitCode != 0) {
				var errorOutput = stderr.ToString ();
				logger (TraceLevel.Error, $"tar extraction failed (exit code {exitCode}): {errorOutput}");
				throw new IOException ($"Failed to extract archive '{archivePath}': {errorOutput}");
			}
		}

		/// <summary>Fetches a SHA-256 checksum from a remote URL, returning null on failure.</summary>
		public static async Task<string?> FetchChecksumAsync (HttpClient httpClient, string checksumUrl, string label, Action<TraceLevel, string> logger, CancellationToken cancellationToken)
		{
			try {
				using var response = await httpClient.GetAsync (checksumUrl, cancellationToken).ConfigureAwait (false);
				response.EnsureSuccessStatusCode ();
				var content = await ReadAsStringAsync (response.Content, cancellationToken).ConfigureAwait (false);
				var checksum = ParseChecksumFile (content);
				logger (TraceLevel.Verbose, $"{label}: checksum={checksum}");
				return checksum;
			}
			catch (OperationCanceledException) {
				throw;
			}
			catch (Exception ex) {
				logger (TraceLevel.Warning, $"Could not fetch checksum for {label}: {ex.Message}");
				return null;
			}
		}

		/// <summary>Parses "hash  filename" or just "hash" from .sha256sum.txt content.</summary>
		public static string? ParseChecksumFile (string content)
		{
			if (string.IsNullOrWhiteSpace (content))
				return null;

			var trimmed = content.Trim ();
			var end = trimmed.IndexOfAny (WhitespaceChars);
			return end >= 0 ? trimmed.Substring (0, end) : trimmed;
		}
	}
}
