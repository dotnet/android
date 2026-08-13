#!/usr/bin/env dotnet
// Helper for the update-androidsdk-packages skill.
//
// Fetches Google's Android SDK repository manifests and prints the
// <remotePackage>/<localPackage> entries whose `path` contains a given
// substring, sorted by revision (newest first) so the current stable
// release is easy to spot. Read-only research tooling: it never edits repo
// files. Use it to answer "what is the current stable revision/URL/SHA-256
// for package X" before hand-editing Configuration.props / androidsdk.targets.
//
// Usage:
//   dotnet run fetch_repo_package.cs -- --path build-tools
//   dotnet run fetch_repo_package.cs -- --path "platforms;android-37" --archives
//   dotnet run fetch_repo_package.cs -- --path emulator --archives --manifest https://dl.google.com/android/repository/sys-img/android/sys-img2-3.xml
//
// Notes:
// - Google's channelRef in repository2-3.xml is NOT a reliable stable/preview
//   signal by itself (some legitimately-stable packages carry a non-zero
//   channel id, and freshly-promoted stable packages can briefly still show
//   old channel numbers). This tool instead flags a package as
//   "preview-looking" when its <revision><preview> element is set (nonzero)
//   or its path/display-name contains an obvious marker (rc, alpha, beta,
//   canary, preview) matched on word boundaries so e.g. "sources" isn't
//   mistaken for "rc". It prints the channel id alongside the revision so a
//   human/agent can make the final call. Pass --all to see every match,
//   preview-looking or not.
// - Only reads data. It does not download archives or compute SHA-256; use
//   sha256_of_url.cs for that once you've picked the exact archive URL
//   (Google's manifests only publish SHA-1).

using System.Text.RegularExpressions;
using System.Xml.Linq;

const string DefaultManifest = "https://dl.google.com/android/repository/repository2-3.xml";
// Word-boundary matches so e.g. "sources;android-35" (contains "rc" inside
// "Sources") isn't mistaken for a release-candidate marker.
var previewMarkers = new Regex(@"\b(rc\d*|alpha\d*|beta\d*|canary|preview)\b", RegexOptions.IgnoreCase);

string? path = null;
string manifest = DefaultManifest;
bool showAll = false;
bool showArchives = false;

for (int i = 0; i < args.Length; i++) {
	switch (args[i]) {
		case "--path": path = ++i < args.Length ? args[i] : null; break;
		case "--manifest": manifest = ++i < args.Length ? args[i] : manifest; break;
		case "--all": showAll = true; break;
		case "--archives": showArchives = true; break;
		case "--help": case "-h":
			PrintUsage();
			return 0;
	}
}

if (string.IsNullOrEmpty(path)) {
	PrintUsage();
	return 1;
}

XDocument doc;
try {
	using var http = new HttpClient();
	http.DefaultRequestHeaders.UserAgent.ParseAdd("dotnet-android-skill/1.0");
	var xml = await http.GetStringAsync(manifest);
	doc = XDocument.Parse(xml);
} catch (Exception ex) {
	Console.Error.WriteLine($"error: failed to fetch/parse {manifest}: {ex.Message}");
	return 1;
}

var matches = new List<PackageMatch>();
foreach (var pkg in doc.Descendants().Where(e => e.Name.LocalName is "remotePackage" or "localPackage")) {
	var pkgPath = (string?)pkg.Attribute("path") ?? "";
	if (!pkgPath.Contains(path, StringComparison.Ordinal))
		continue;

	var revisionElem = pkg.Elements().FirstOrDefault(e => e.Name.LocalName == "revision");
	var (revisionText, revisionKey) = ParseRevision(revisionElem);
	bool hasPreviewRevision = revisionElem?.Elements().FirstOrDefault(e => e.Name.LocalName == "preview") is { } previewElem
		&& int.TryParse(previewElem.Value, out var previewNum0) && previewNum0 != 0;
	var displayName = pkg.Elements().FirstOrDefault(e => e.Name.LocalName == "display-name")?.Value ?? "";
	// <channelRef> is a direct child of <remotePackage>/<localPackage>, not of <type-details>.
	var channelRef = pkg.Elements().FirstOrDefault(e => e.Name.LocalName == "channelRef")?.Attribute("ref")?.Value ?? "";
	bool preview = hasPreviewRevision || previewMarkers.IsMatch($"{pkgPath} {displayName}");
	if (preview && !showAll)
		continue;

	var archives = showArchives ? CollectArchives(pkg, manifest) : new List<ArchiveInfo>();
	matches.Add(new PackageMatch(pkgPath, revisionText, revisionKey, displayName, channelRef, preview, archives));
}

if (matches.Count == 0) {
	Console.Error.WriteLine($"No packages matched --path '{path}' in {manifest}");
	return 1;
}

foreach (var m in matches.OrderByDescending(m => m.RevisionKey)) {
	string flag = m.PreviewGuess ? " [PREVIEW-LOOKING]" : "";
	Console.WriteLine($"{m.Path}  rev={m.Revision}  channel={m.ChannelRef}{flag}  {m.DisplayName}");
	foreach (var a in m.Archives) {
		string tag = string.Join("/", new[] { a.HostOs, a.HostArch }.Where(s => !string.IsNullOrEmpty(s)));
		if (tag.Length == 0)
			tag = "generic";
		Console.WriteLine($"    [{tag}] {a.Url}  sha1={a.Sha1}  size={a.Size}");
	}
}

return 0;

static void PrintUsage()
{
	Console.Error.WriteLine("usage: dotnet run fetch_repo_package.cs -- --path <substring> [--manifest <url>] [--all] [--archives]");
	Console.Error.WriteLine($"  --path      required substring to match against each package's `path` attribute (e.g. 'build-tools', 'platforms;android-37', 'emulator')");
	Console.Error.WriteLine($"  --manifest  manifest URL (default: {DefaultManifest}); use a sys-img*.xml URL for system images");
	Console.Error.WriteLine("  --all       show every match, including ones that look like previews (default: stable-looking only)");
	Console.Error.WriteLine("  --archives  print archive URLs + SHA-1 + size for each match (SHA-1 only; recompute SHA-256 from the actual download)");
}

static (string Text, (int, int, int, int) Key) ParseRevision(XElement? revisionElem)
{
	if (revisionElem is null)
		return ("", (0, 0, 0, 0));

	int Int(string name) {
		var s = revisionElem.Elements().FirstOrDefault(e => e.Name.LocalName == name)?.Value;
		return int.TryParse(s, out var v) ? v : 0;
	}

	string? Str(string name) => revisionElem.Elements().FirstOrDefault(e => e.Name.LocalName == name)?.Value;

	int major = Int("major");
	string? minor = Str("minor");
	string? micro = Str("micro");
	string? preview = Str("preview");
	int previewNum = int.TryParse(preview, out var p) ? p : 0;
	var parts = new List<string> { major.ToString() };
	if (!string.IsNullOrEmpty(minor)) parts.Add(minor);
	if (!string.IsNullOrEmpty(micro)) parts.Add(micro);
	if (!string.IsNullOrEmpty(preview) && previewNum != 0) parts.Add($"rc{preview}");
	// Sort key includes preview as its own component so e.g. 36.0.0-preview1 and
	// 36.0.0-preview2 don't collapse to the same key, and so a stable release
	// (previewNum == 0) always sorts *after* any preview of the same
	// major.minor.micro (Google publishes previews before promoting to stable).
	return (string.Join(".", parts), (major, Int("minor"), Int("micro"), previewNum == 0 ? int.MaxValue : previewNum));
}

static List<ArchiveInfo> CollectArchives(XElement pkg, string manifestUrl)
{
	var result = new List<ArchiveInfo>();
	Uri? baseUri = Uri.TryCreate(manifestUrl, UriKind.Absolute, out var u) ? u : null;
	foreach (var archivesElem in pkg.Elements().Where(e => e.Name.LocalName == "archives")) {
		foreach (var archive in archivesElem.Elements().Where(e => e.Name.LocalName == "archive")) {
			var complete = archive.Elements().FirstOrDefault(e => e.Name.LocalName == "complete");
			if (complete is null)
				continue;
			string hostOs = archive.Elements().FirstOrDefault(e => e.Name.LocalName == "host-os")?.Value ?? "";
			string hostArch = archive.Elements().FirstOrDefault(e => e.Name.LocalName == "host-arch")?.Value ?? "";
			string rawUrl = complete.Elements().FirstOrDefault(e => e.Name.LocalName == "url")?.Value ?? "";
			// Archive <url> values in the manifest are relative to the manifest's own
			// location (e.g. "build-tools_r30.0.3-linux.zip"), not absolute. Resolve
			// against the manifest URI so the printed URL can be fed straight into
			// sha256_of_url.cs / DownloadFile without the caller having to guess the
			// base path.
			string url = rawUrl;
			if (baseUri is not null && Uri.TryCreate(baseUri, rawUrl, out var resolved))
				url = resolved.AbsoluteUri;
			string sha1 = complete.Elements().FirstOrDefault(e => e.Name.LocalName == "checksum")?.Value ?? "";
			string size = complete.Elements().FirstOrDefault(e => e.Name.LocalName == "size")?.Value ?? "";
			result.Add(new ArchiveInfo(hostOs, hostArch, url, sha1, size));
		}
	}
	return result;
}

record PackageMatch(string Path, string Revision, (int, int, int, int) RevisionKey, string DisplayName, string ChannelRef, bool PreviewGuess, List<ArchiveInfo> Archives);
record ArchiveInfo(string HostOs, string HostArch, string Url, string Sha1, string Size);
