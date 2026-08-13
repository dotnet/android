#!/usr/bin/env dotnet
// Download a URL into a scratch temp file (NOT the Android archive cache at
// $HOME/android-archives) and print its SHA-256 in the uppercase hex format
// Configuration.props expects. Use this only when Google's manifest doesn't
// publish an authoritative SHA-256 for an archive you need to pin (Google's
// repository manifests only carry SHA-1). Never hand-guess a hash.
//
// Usage:
//   dotnet run sha256_of_url.cs -- https://dl.google.com/android/repository/build-tools_r37.0.0_linux.zip
//
// Deletes the downloaded file after hashing unless --keep is passed, so it
// never leaves stray files behind for the diff/cache-cleanliness check.

using System.Security.Cryptography;

string? url = null;
bool keep = false;

foreach (var a in args) {
	if (a == "--keep")
		keep = true;
	else if (!a.StartsWith("--", StringComparison.Ordinal))
		url = a;
}

if (string.IsNullOrEmpty(url)) {
	Console.Error.WriteLine("usage: dotnet run sha256_of_url.cs -- <url> [--keep]");
	return 1;
}

string? tmpPath = null;

try {
	if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)) {
		Console.Error.WriteLine($"error: '{url}' is not a valid absolute http(s) URL");
		return 1;
	}

	tmpPath = Path.Combine(Path.GetTempPath(), $"androidsdk-skill-{Guid.NewGuid():N}{Path.GetExtension(uri.AbsolutePath)}");

	using var http = new HttpClient();
	http.DefaultRequestHeaders.UserAgent.ParseAdd("dotnet-android-skill/1.0");
	http.Timeout = TimeSpan.FromMinutes(10);

	long size = 0;
	using (var sha256 = SHA256.Create())
	using (var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead)) {
		response.EnsureSuccessStatusCode();
		await using var httpStream = await response.Content.ReadAsStreamAsync();
		await using var fileStream = File.Create(tmpPath);
		await using var cryptoStream = new CryptoStream(fileStream, sha256, CryptoStreamMode.Write);
		await httpStream.CopyToAsync(cryptoStream);
		await cryptoStream.FlushFinalBlockAsync();
		size = new FileInfo(tmpPath).Length;

		string digest = Convert.ToHexString(sha256.Hash ?? throw new InvalidOperationException("SHA256 hash was not computed."));
		Console.WriteLine($"url:    {url}");
		Console.WriteLine($"size:   {size} bytes");
		Console.WriteLine($"sha256: {digest}");
	}

	if (keep)
		Console.WriteLine($"kept at: {tmpPath}");

	return 0;
} catch (Exception ex) {
	Console.Error.WriteLine($"error: failed to download/hash {url}: {ex.Message}");
	return 1;
} finally {
	// Cleanup failures (e.g. a transient file lock) must not mask the real
	// download/hash outcome above, so report them separately instead of
	// letting an exception escape the finally block.
	if (!keep && tmpPath is not null && File.Exists(tmpPath)) {
		try {
			File.Delete(tmpPath);
		} catch (Exception cleanupEx) {
			Console.Error.WriteLine($"warning: failed to delete temp file '{tmpPath}': {cleanupEx.Message}");
		}
	}
}
