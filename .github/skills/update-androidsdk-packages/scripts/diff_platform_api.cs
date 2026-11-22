#!/usr/bin/env dotnet
// Print a Markdown diff of the public/protected API in android.jar between
// two revisions of one platform package, for the PR description.
//
// Usage:
//   dotnet run diff_platform_api.cs -- <old-url> <old-sha256> <new-url> <new-sha1> <new-size>
//
// <old-sha256> is the Hash pinned in androidsdk.targets. <new-sha1> and
// <new-size> come from the manifest. Requires javap (JAVA_HOME or PATH).

using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;

if (args.Length != 5 || !long.TryParse(args [4], out long newSize)) {
	Console.Error.WriteLine("usage: dotnet run diff_platform_api.cs -- <old-url> <old-sha256> <new-url> <new-sha1> <new-size>");
	return 1;
}

string workDir = Directory.CreateTempSubdirectory("androidsdk-skill-").FullName;
try {
	using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };

	byte[] oldZip = await http.GetByteArrayAsync(args [0]);
	Check("SHA-256", args [0], args [1], Convert.ToHexString(SHA256.HashData(oldZip)));

	byte[] newZip = await http.GetByteArrayAsync(args [2]);
	using var sha1 = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
	sha1.AppendData(newZip);
	Check("SHA-1", args [2], args [3], Convert.ToHexString(sha1.GetHashAndReset()));
	Check("size", args [2], newSize.ToString(), newZip.Length.ToString());

	string oldJar = ExtractAndroidJar(oldZip, Path.Combine(workDir, "old.jar"));
	string newJar = ExtractAndroidJar(newZip, Path.Combine(workDir, "new.jar"));
	var oldClasses = HashClasses(oldJar);
	var newClasses = HashClasses(newJar);

	var removed = oldClasses.Keys.Except(newClasses.Keys).ToList();
	var added = newClasses.Keys.Except(oldClasses.Keys).ToList();
	var changed = oldClasses.Keys.Where(k => newClasses.TryGetValue(k, out var h) && h != oldClasses [k]).ToList();

	// javap omits non-public members; non-public types are filtered in Javap ().
	var oldApi = Javap(oldJar, removed.Concat(changed));
	var newApi = Javap(newJar, added.Concat(changed));

	var removedTypes = removed.Where(oldApi.ContainsKey).Order(StringComparer.Ordinal).ToList();
	var addedTypes = added.Where(newApi.ContainsKey).Order(StringComparer.Ordinal).ToList();
	var diffs = new List<string>();
	bool memberRemoved = false;
	foreach (var type in changed.Order(StringComparer.Ordinal)) {
		var before = oldApi.GetValueOrDefault(type) ?? [];
		var after = newApi.GetValueOrDefault(type) ?? [];
		var lines = before.Except(after).Select(l => "- " + l).Concat(after.Except(before).Select(l => "+ " + l)).ToList();
		if (lines.Count == 0)
			continue;
		memberRemoved |= lines [0].StartsWith('-');
		diffs.Add(type + "\n" + string.Join("\n", lines));
	}

	Console.WriteLine($"### Platform API changes: `{Path.GetFileNameWithoutExtension(args [0])}` -> `{Path.GetFileNameWithoutExtension(args [2])}`");
	Console.WriteLine();
	Console.WriteLine($"New archive SHA-256: `{Convert.ToHexString(SHA256.HashData(newZip))}`");
	Console.WriteLine();
	Console.WriteLine(removedTypes.Count > 0 || memberRemoved
		? "**Binding follow-up required:** public/protected API was removed or changed."
		: "Binding follow-up required: no.");
	PrintTypes("Removed types", removedTypes);
	PrintTypes("Added types", addedTypes);
	if (diffs.Count > 0) {
		Console.WriteLine($"\n#### Changed types ({diffs.Count})\n\n```diff");
		Console.WriteLine(string.Join("\n\n", diffs));
		Console.WriteLine("```");
	}
	return 0;
} catch (Exception ex) {
	Console.Error.WriteLine($"error: {ex.Message}");
	return 1;
} finally {
	try {
		Directory.Delete(workDir, recursive: true);
	} catch (Exception ex) {
		Console.Error.WriteLine($"warning: failed to delete '{workDir}': {ex.Message}");
	}
}

static void Check(string what, string url, string expected, string actual)
{
	if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
		throw new InvalidDataException($"{what} mismatch for {url}: expected {expected}, got {actual}");
}

static void PrintTypes(string title, List<string> types)
{
	if (types.Count == 0)
		return;
	Console.WriteLine($"\n#### {title} ({types.Count})\n");
	foreach (var t in types)
		Console.WriteLine($"- `{t}`");
}

static string ExtractAndroidJar(byte[] zipBytes, string destination)
{
	using var zip = new ZipArchive(new MemoryStream(zipBytes));
	var entry = zip.Entries.Single(e => e.Name == "android.jar" && e.FullName.Count(c => c == '/') == 1);
	entry.ExtractToFile(destination);
	return destination;
}

static Dictionary<string, string> HashClasses(string jarPath)
{
	using var jar = ZipFile.OpenRead(jarPath);
	return jar.Entries
		.Where(e => e.FullName.EndsWith(".class", StringComparison.Ordinal) && !e.FullName.StartsWith("META-INF/", StringComparison.Ordinal))
		.ToDictionary(e => e.FullName [..^".class".Length].Replace('/', '.'), e => {
			using var s = e.Open();
			return Convert.ToHexString(SHA256.HashData(s));
		});
}

// Returns type name -> javap lines (type header and members).
static Dictionary<string, HashSet<string>> Javap(string jar, IEnumerable<string> classes)
{
	string? javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
	string javap = string.IsNullOrEmpty(javaHome) ? "javap" : Path.Combine(javaHome, "bin", "javap");
	var result = new Dictionary<string, HashSet<string>>();
	// Use jar: URLs; with -cp, javap reads java.* classes from the host JDK instead of android.jar.
	string jarUrl = "jar:" + new Uri(jar).AbsoluteUri + "!/";
	foreach (var chunk in classes.Chunk(100)) {
		var output = Process.RunAndCaptureText(javap, ["-protected", "-constants", .. chunk.Select(c => jarUrl + c.Replace('.', '/') + ".class")]);
		if (output.ExitStatus.ExitCode != 0)
			throw new InvalidOperationException($"javap exited with code {output.ExitStatus.ExitCode}: {output.StandardError}");

		HashSet<string>? current = null;
		foreach (var line in output.StandardOutput.Split('\n').Select(l => l.TrimEnd('\r'))) {
			if (line.Length == 0 || line.StartsWith("Compiled from ", StringComparison.Ordinal))
				continue;
			if (current is null) {
				current = [line.TrimEnd(' ', '{')];
				// javap still prints package-private types that are named on the command line.
				if (line.StartsWith("public ", StringComparison.Ordinal) || line.StartsWith("protected ", StringComparison.Ordinal))
					result [TypeName(line)] = current;
			} else if (line == "}") {
				current = null;
			} else {
				current.Add(line.Trim());
			}
		}
	}
	return result;
}

// "public final class a.b.C$D<T> extends ... {" -> "a.b.C$D"
static string TypeName(string header)
{
	var tokens = header.Split(' ');
	int i = Array.FindIndex(tokens, t => t is "class" or "interface" or "enum");
	return tokens [i + 1].Split('<') [0];
}
