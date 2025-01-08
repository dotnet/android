using System.Net.Http;
using System.Xml.Linq;

using Mono.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

const string AppName = "workload-dependencies";

var help                    = false;
var feed                    = (string?) null;
var output                  = (string?) null;
int Verbosity               = 0;
var CmdlineToolsVersion     = (string?) null;
var BuildToolsVersion       = (string?) null;
var JdkVersion              = (string?) null;
var JdkMaxVersion           = (string?) null;
var NdkVersion              = (string?) null;
var PlatformToolsVersion    = (string?) null;
var PlatformVersion         = (string?) null;
var PreviewPlatformVersion  = (string?) null;
var WorkloadVersion         = (string?) null;

var options = new OptionSet {
	"Generate `release.json` from Feed XML file.",
	{ "i|feed=",
	  "The {PATH} to the Feed XML file to process.",
	  v => feed = v },
	{ "o|output=",
	  "The {FILE_PATH} for the JSON output.\nDefault is stdout.",
	  v => output = v },
	{ "build-tools-version=",
	  "The Android SDK Build-Tools {VERSION} dotnet/android is built against.",
	  v => BuildToolsVersion = v },
	{ "cmdline-tools-version=",
	  "The Android SDK cmdline-tools {VERSION} dotnet/android is built against.",
	  v => CmdlineToolsVersion = v },
	{ "jdk-version=",
	  "The JDK {VERSION} dotnet/android is built against.",
	  v => JdkVersion = v },
	{ "jdk-max-version=",
	  "The maximum JDK {VERSION} dotnet/android supports.",
	  v => JdkMaxVersion = v },
	{ "ndk-version=",
	  "The Android NDK {VERSION} dotnet/android is built against.",
	  v => NdkVersion = v },
	{ "platform-tools-version=",
	  "The Android SDK platform-tools version dotnet/android is built against.",
	  v => PlatformToolsVersion = v },
	{ "platform-version=",
	  "The stable Android SDK Platform {VERSION} dotnet/android binds.",
	  v => PlatformVersion = v },
	{ "preview-platform-version=",
	  "The preview Android SDK Platform {VERSION} dotnet/android binds.",
	  v => PreviewPlatformVersion = v },
	{ "workload-version=",
	  "The {VERSION} of the dotnet/android workload.",
	  v => WorkloadVersion = v },
	{ "v|verbose:",
	  "Set internal message Verbosity",
	  (int? v) => Verbosity = v.HasValue ? v.Value : Verbosity + 1 },
	{ "h|help",
	  "Show this help message and exit",
	  v => help = v != null },
};

XDocument doc;

try {
	options.Parse (args);

	if (help) {
		options.WriteOptionDescriptions (Console.Out);
		return;
	}

	if (string.IsNullOrEmpty (feed)) {
		Console.Error.WriteLine ($"{AppName}: --feed is required.");
		Console.Error.WriteLine ($"{AppName}: Use --help for more information.");
		return;
	}
	doc     = XDocument.Parse (await GetFeedContents (feed));
	if (doc.Root == null) {
		throw new InvalidOperationException ("Missing root element in XML feed.");
	}
}
catch (OptionException e) {
	Console.Error.WriteLine ($"{AppName}: {e.Message}");
	if (Verbosity > 0) {
		Console.Error.WriteLine (e.ToString ());
	}
	return;
}
catch (System.Xml.XmlException e) {
	Console.Error.WriteLine ($"{AppName}: invalid `--feed=PATH` value.  {e.Message}");
	if (Verbosity > 0) {
		Console.Error.WriteLine (e.ToString ());
	}
	return;
}

var PackageCreators = new Dictionary<string, Func<XDocument, IEnumerable<JObject>>> {
	["build-tool"]      = doc => CreatePackageEntries (doc, "build-tool",       BuildToolsVersion),
	["emulator"]        = doc => CreatePackageEntries (doc, "emulator",         null,                   optional: true),
	["cmdline-tools"]   = doc => CreatePackageEntries (doc, "cmdline-tools",    CmdlineToolsVersion),
	["ndk"]             = doc => CreatePackageEntries (doc, "ndk",              NdkVersion,             optional: true),
	["platform-tools"]  = doc => CreatePackageEntries (doc, "platform-tools",   PlatformToolsVersion),
	["platform"]        = CreatePlatformPackageEntries,
	["system-image"]    = CreateSystemImagePackageEntries,
	// ndk
};

var release = new JObject {
	new JProperty ("microsoft.net.sdk.android", new JObject {
		CreateWorkloadProperty (doc),
		CreateJdkProperty (doc),
		new JProperty ("androidsdk", new JObject {
			new JProperty ("packages", CreatePackagesArray (doc)),
		}),
	}),
};

using var writer = CreateWriter ();
release.WriteTo (writer);
writer.Flush ();

async Task<string> GetFeedContents (string feed)
{
	if (File.Exists (feed)) {
		return File.ReadAllText (feed);
	}
	if (Uri.TryCreate (feed, UriKind.Absolute, out var uri)) {
		return await GetFeedContentsFromUri (uri);
	}
	throw new NotSupportedException ($"Don't know what to do with --feed={feed}");
}

async Task<string> GetFeedContentsFromUri (Uri feed)
{
	using var client = new HttpClient ();
	var response = await client.GetAsync (feed);
	return await response.Content.ReadAsStringAsync ();
}

JsonWriter CreateWriter ()
{
	var w = string.IsNullOrEmpty (output)
		? new JsonTextWriter (Console.Out) { CloseOutput = false}
		: new JsonTextWriter (File.CreateText (output)) { CloseOutput = true };
	w.Formatting = Formatting.Indented;
	return w;
}

JProperty CreateWorkloadProperty (XDocument doc)
{
	var contents = new JObject (
		new JProperty ("alias", new JArray ("android")));
	if (!string.IsNullOrEmpty (WorkloadVersion))
		contents.Add (new JProperty ("version", WorkloadVersion));
	return new JProperty ("workload", contents);
}

JProperty CreateJdkProperty (XDocument doc)
{
	var v               = new Version (JdkVersion ?? "17.0");
	var start           = new Version (v.Major, v.Minor);
	var end             = GetMaxJdkVersion (v);
	var latestRevision  = JdkVersion ?? GetLatestRevision (doc, "jdk");
	var contents        = new JObject (
		new JProperty ("version", $"[{start},{end})"));
	if (!string.IsNullOrEmpty (latestRevision))
		contents.Add (new JProperty ("recommendedVersion", latestRevision));
	return new JProperty ("jdk", contents);
}

string GetMaxJdkVersion (Version v)
{
	if (!string.IsNullOrEmpty (JdkMaxVersion)) {
		return JdkMaxVersion;
	}
	return new Version (v.Major+1, 0).ToString ();
}

IEnumerable<XElement> GetSupportedElements (XDocument doc, string element)
{
	if (doc.Root == null) {
		return Array.Empty<XElement> ();
	}
	return doc.Root.Elements (element)
		.Where (e =>
			string.Equals ("False", e.ReqAttr ("obsolete"), StringComparison.OrdinalIgnoreCase) &&
			string.Equals ("False", e.ReqAttr ("preview"), StringComparison.OrdinalIgnoreCase))
		;
}

IEnumerable<(XElement Element, string Revision)> GetByRevisions (XDocument doc, string element)
{
	return GetSupportedElements (doc, element)
		.OrderByRevision ();
}

string? GetLatestRevision (XDocument doc, string element)
{
	return GetByRevisions (doc, element)
		.LastOrDefault ()
		.Revision;
}

IEnumerable<JObject> CreatePackageEntries (XDocument doc, string element, string? revision, bool optional = false)
{
	var item    = GetElementRevision (doc, element, revision);
	if (item == null) {
		yield break;
	}
	var path        = item.ReqAttr ("path");
	var reqRev      = item.ReqAttr ("revision");
	var sdkPackage  = new JObject {
			new JProperty ("id",        path),
	};

	// special-case platform-tools, which doesn't have a revision
	if (!path.Contains (reqRev)) {
		sdkPackage.Add (new JProperty ("recommendedVersion",    reqRev));
	}
	var entry       = new JObject {
		new JProperty ("desc",          item.ReqAttr ("description")),
		new JProperty ("sdkPackage",    sdkPackage),
		new JProperty ("optional",      optional.ToString ().ToLowerInvariant ()),
	};
	yield return entry;
}

XElement? GetElementRevision (XDocument doc, string element, string? revision)
{
	Version?  reqVersion	= revision != null ? new Version (revision) : null;;
	Version?  maxVersion    = null;
	XElement? entry         = null;
	foreach (var e in GetSupportedElements (doc, element)) {
		var r   = e.ReqAttr ("revision");
		var rv  = new Version (r);
		if (rv == reqVersion) {
			return e;
		}
		if (rv > maxVersion) {
			maxVersion  = rv;
			entry       = e;
		}
	}
	return entry;
}

IEnumerable<JObject> CreatePlatformPackageEntries (XDocument doc)
{
	string?     reqVersion  = PlatformVersion != null
		? $"platforms;{PlatformVersion}"
		: null;
	string?     maxVersion  = null;
	XElement?	entry       = null;
	foreach (var e in GetSupportedElements (doc, "platform")) {
		var path    = e.ReqAttr ("path");
		if (path == reqVersion) {
			entry = e;
			break;
		}
		if (string.Compare (path, maxVersion) > 0) {
			maxVersion  = path;
			entry       = e;
		}
	}
	if (entry == null) {
		yield break;
	}
	var platform    = new JObject {
		new JProperty ("desc",          entry.ReqAttr ("description")),
		new JProperty ("sdkPackage", new JObject {
			new JProperty ("id",    entry.ReqAttr ("path")),
		}),
		new JProperty ("optional",      "false"),
	};
	yield return platform;

	string?     previewPath = PreviewPlatformVersion != null
		? $"platforms;android-{PreviewPlatformVersion}"
		: null;
	XElement?   previewEntry    = doc.Elements ("platform")
		.FirstOrDefault (e => e.ReqAttr ("path") == previewPath);
	if (PreviewPlatformVersion != null) {
		yield return new JObject {
			new JProperty ("desc",          previewEntry?.ReqAttr ("description") ?? $"Android SDK Platform {PreviewPlatformVersion} (Preview)"),
			new JProperty ("sdkPackage", new JObject {
				new JProperty ("id",    previewPath),
			}),
			new JProperty ("optional",      "true"),
		};
	}
}

IEnumerable<JObject> CreateSystemImagePackageEntries (XDocument doc)
{
	// path="system-images;android-21;default;armeabi-v7a"
	var images      = from image in GetSupportedElements (doc, "system-image")
		let path       = image.ReqAttr ("path")
		let parts      = path.Split (';')
		where parts.Length > 3
		let targetApi   = parts [1]
		let apiImpl     = parts [2]     // google_apis or default
		let targetAbi   = parts [3]
		where apiImpl == "google_apis"  // prefer google_apis
		select new {
			Element     = image,
			Path        = path,
			TargetApi   = targetApi,
			ApiImpl     = apiImpl,
			TargetAbi   = targetAbi,
		};
	var maxTarget   = images.Select (image => image.TargetApi).OrderBy (v => v).Last ();
	var maxImages   = images.Where (image => image.TargetApi == maxTarget);

	var x64         = maxImages.Where (image => image.TargetAbi == "x86_64").FirstOrDefault ();
	var arm64       = maxImages.Where (image => image.TargetAbi == "arm64-v8a").FirstOrDefault ();
	if (x64 == null && arm64 == null) {
		yield break;
	}

	var id          = new JObject ();
	if (x64 != null) {
		id.Add (new JProperty ("win-x64",       x64.Path));
		id.Add (new JProperty ("mac-x64",       x64.Path));
		id.Add (new JProperty ("linux-x64",     x64.Path));
	}
	if (arm64 != null) {
		id.Add (new JProperty ("mac-arm64",     arm64.Path));
		id.Add (new JProperty ("linux-arm64",   arm64.Path));
	}

	var entry       = new JObject {
		new JProperty ("desc",          maxImages.First ().Element.ReqAttr ("description")),
		new JProperty ("sdkPackage",    new JObject {
			new JProperty ("id",    id),
		}),
		new JProperty ("optional",      "true"),
	};
	yield return entry;
}

JArray CreatePackagesArray (XDocument doc)
{
	var packages    = new JArray ();
	var names       = doc.Root!.Elements ()
		.Select (e => e.Name.LocalName)
		.Distinct ()
		.OrderBy (e => e);
	foreach (var name in names) {
		if (!PackageCreators.TryGetValue (name, out var creator)) {
			continue;
		}
		foreach (var e in creator (doc)) {
			packages.Add (e);
		}
	}
	return packages;
}

static class Extensions
{
	public static string ReqAttr (this XElement e, string attribute)
	{
		var v = (string?) e.Attribute (attribute);
		if (v == null) {
			throw new InvalidOperationException ($"Missing required attribute `{attribute}` in: `{e}");
		}
		return v;
	}

	public static IEnumerable<(XElement Element, string Revision)> OrderByRevision (this IEnumerable<XElement> elements)
	{
		return from e in elements
			let     revision    = e.ReqAttr ("revision")
			let     version     = new Version (revision.Contains (".") ? revision : revision + ".0")
			orderby version
			select (e, revision);
	}
}
