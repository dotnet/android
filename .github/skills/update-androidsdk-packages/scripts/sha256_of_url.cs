#!/usr/bin/env dotnet
// Download a URL into a scratch temp file (NOT the Android archive cache at
// $HOME/android-archives) and print its SHA-256 in the uppercase hex format
// Configuration.props expects. Use this only when Google's manifest doesn't
// publish an authoritative SHA-256 for an archive you need to pin (Google's
// repository manifests only carry SHA-1). Never hand-guess a hash.
//
// Usage:
//   dotnet run sha256_of_url.cs -- https://dl.google.com/android/repository/build-tools_r37.0.0_linux.zip --sha1 <manifest-sha1> --size <manifest-size>
//
// Deletes the downloaded file after hashing unless --keep is passed, so it
// never leaves stray files behind for the diff/cache-cleanliness check.

using System.Security.Cryptography;

string? url = null;
string? expectedSha1 = null;
long? expectedSize = null;
bool keep = false;

for (int i = 0; i < args.Length; i++) {
	switch (args [i]) {
		case "--keep":
			keep = true;
			break;
		case "--sha1":
			expectedSha1 = ++i < args.Length ? args [i] : null;
			break;
		case "--size":
			if (++i >= args.Length || !long.TryParse(args [i], out var size) || size < 0) {
				Console.Error.WriteLine("error: --size must be a non-negative byte count");
				return 1;
			}
			expectedSize = size;
			break;
		default:
			if (!args [i].StartsWith("--", StringComparison.Ordinal))
				url = args [i];
			break;
	}
}

if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(expectedSha1) || expectedSize is null) {
	Console.Error.WriteLine("usage: dotnet run sha256_of_url.cs -- <url> --sha1 <manifest-sha1> --size <manifest-size> [--keep]");
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
	using (var sha1 = IncrementalHash.CreateHash(HashAlgorithmName.SHA1))
	using (var sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
	using (var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead)) {
		response.EnsureSuccessStatusCode();
		await using var httpStream = await response.Content.ReadAsStreamAsync();
		await using var fileStream = File.Create(tmpPath);
		var buffer = new byte [81920];
		int bytesRead;
		while ((bytesRead = await httpStream.ReadAsync(buffer)) != 0) {
			sha1.AppendData(buffer, 0, bytesRead);
			sha256.AppendData(buffer, 0, bytesRead);
			await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
			size += bytesRead;
		}

		string actualSha1 = Convert.ToHexString(sha1.GetHashAndReset());
		if (!string.Equals(actualSha1, expectedSha1, StringComparison.OrdinalIgnoreCase))
			throw new InvalidDataException($"SHA-1 mismatch: manifest={expectedSha1}, downloaded={actualSha1}");
		if (size != expectedSize.Value)
			throw new InvalidDataException($"size mismatch: manifest={expectedSize.Value}, downloaded={size}");

		string digest = Convert.ToHexString(sha256.GetHashAndReset());
		Console.WriteLine($"url:    {url}");
		Console.WriteLine($"size:   {size} bytes");
		Console.WriteLine($"sha1:   {actualSha1}");
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
