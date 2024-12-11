using System.Net.Http;
using System.Xml.Linq;

using Mono.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

const string AppName = "release-json";

var RequiredPackages = new HashSet<string> {
	"platform-tools",
	"cmdline-tools",
	"build-tool",
	"platform",
};

var help                = false;
var feed                = (string?) null;
var output              = (string?) null;
int verbosity           = 0;
var workloadVersion     = (string?) null;

var options = new OptionSet {
	"Generate `release.json` from Feed XML file.",
	{ "i|feed=",
	  "The {PATH} to the Feed XML file.",
	  v => feed = v },
	{ "o|output=",
	  "The {PATH} to the output release.json file.",
	  v => output = v },
	{ "workload-version=",
	  "The {VERSION} of the workload to generate.",
	  v => workloadVersion = v },
	{ "v|verbose:",
	  "Set internal message verbosity",
	  (int? v) => verbosity = v.HasValue ? v.Value : verbosity + 1 },
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
	if (verbosity > 0) {
		Console.Error.WriteLine (e.ToString ());
	}
	return;
}
catch (System.Xml.XmlException e) {
	Console.Error.WriteLine ($"{AppName}: invalid `--feed=PATH` value.  {e.Message}");
	if (verbosity > 0) {
		Console.Error.WriteLine (e.ToString ());
	}
	return;
}

var PackageCreators = new Dictionary<string, Func<XDocument, IEnumerable<JObject>>> {
	["addon"]           = CreateAddonPackageEntries,
	["extra"]           = CreateExtraPackageEntries,
	["jdk"]             = doc => Array.Empty<JObject> (),
	["licenses"]        = doc => Array.Empty<JObject> (),
	["system-image"]    = CreateSystemImagePackageEntries,
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
	if (!string.IsNullOrEmpty (workloadVersion))
		contents.Add (new JProperty ("version", workloadVersion));
	return new JProperty ("workload", contents);
}

JProperty CreateJdkProperty (XDocument doc)
{
	var latestRevision  = GetLatestRevision (doc, "jdk");
	var contents        = new JObject (
		new JProperty ("version", "[17.0,18.0)"));
	if (!string.IsNullOrEmpty (latestRevision))
		contents.Add (new JProperty ("recommendedVersion", latestRevision));
	return new JProperty ("jdk", contents);
}

IEnumerable<XElement> GetSupportedElements (XDocument doc, string element)
{
	if (doc.Root == null) {
		return Array.Empty<XElement> ();
	}
	return doc.Root.Elements (element)
		.Where (e =>
			string.Equals ("False", e.ReqAttr ("obsolete"), StringComparison.OrdinalIgnoreCase) &&
			string.Equals ("False", e.ReqAttr ("preview"), StringComparison.OrdinalIgnoreCase));
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

IEnumerable<JObject> CreateAddonPackageEntries (XDocument doc)
{
	var allAddons   = GetSupportedElements (doc, "addon").ToList ()
		.OrderBy (e => e.ReqAttr ("path"));
	var paths       = allAddons
		.Select (e => GetEntryId (e))
		.Distinct ();
	foreach (var path in paths) {
		var addons  = allAddons
			.Where (e => GetEntryId (e) == path);
		var version = string.Join (",", addons.Select (e => e.ReqAttr ("revision")));
		var latest  = addons.Last ();
		var entry   = new JObject {
			new JProperty ("desc",                      latest.ReqAttr ("description")),
			new JProperty ("sdkPackage", new JObject {
				new JProperty ("id",                    path),
				new JProperty ("version",               "[" + version + "]"),
				new JProperty ("recommendedId",         latest.ReqAttr ("path")),
				new JProperty ("recommendedVersion",    latest.ReqAttr ("revision")),
			}),
			new JProperty ("optional",                "true"),
		};
		yield return entry;
	}
}

IEnumerable<JObject> CreateExtraPackageEntries (XDocument doc)
{
	var allExtras   = GetByRevisions (doc, "extra").ToList ();
	var paths       = allExtras
		.Select (e => e.Element.ReqAttr ("path"))
		.Distinct ();
	foreach (var path in paths) {
		var extras  = allExtras
			.Where (e => e.Element.ReqAttr ("path") == path);
		var version = string.Join (",", extras.Select (e => e.Revision));
		var latest  = extras.Last ();
		var entry   = new JObject {
			new JProperty ("desc",                      latest.Element.ReqAttr ("description")),
			new JProperty ("sdkPackage", new JObject {
				new JProperty ("id",                    path),
				new JProperty ("version",               "[" + version + "]"),
				new JProperty ("recommendedId",         latest.Element.ReqAttr ("path")),
				new JProperty ("recommendedVersion",    latest.Revision),
			}),
			new JProperty ("optional",                "true"),
		};
		yield return entry;
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
		let apiImpl     = parts [2] // google_apis or default
		let targetAbi   = parts [3]
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
		id.Add (new JProperty ("win-x64", x64.Path));
		id.Add (new JProperty ("mac-x64", x64.Path));
		id.Add (new JProperty ("linux-x64", x64.Path));
	}
	if (arm64 != null) {
		id.Add (new JProperty ("mac-arm64", arm64.Path));
		id.Add (new JProperty ("linux-arm64", arm64.Path));
	}

	var entry       = new JObject {
		new JProperty ("desc",      maxImages.First ().Element.ReqAttr ("description")),
		new JProperty ("sdkPackage", new JObject {
			new JProperty ("id",    id),
		}),
		new JProperty ("optional",  "true"),
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
		if (PackageCreators.TryGetValue (name, out var creator)) {
			foreach (var e in creator (doc)) {
				packages.Add (e);
			}
			continue;
		}
		var items       = GetSupportedElements (doc, name)
			.OrderBy (e => e.ReqAttr ("path"));
		if (!items.Any ()) {
			continue;
		}
		var version     = string.Join (",", items.Select (e => e.ReqAttr ("revision")));
		var latest      = items.Last ();

		var entry   = new JObject {
			new JProperty ("desc",                      latest.ReqAttr ("description")),
			new JProperty ("sdkPackage", new JObject {
				new JProperty ("id",                    GetEntryId (latest)),
				new JProperty ("version",               "[" + version + "]"),
				new JProperty ("recommendedId",         latest.ReqAttr ("path")),
				new JProperty ("recommendedVersion",    latest.ReqAttr ("revision")),
			}),
			new JProperty ("optional", (!RequiredPackages.Contains (name)).ToString ().ToLowerInvariant ()),
		};

		packages.Add (entry);
	}
	return packages;
}

string GetEntryId (XElement entry)
{
	var path    = entry.ReqAttr ("path");
	var semic   = path.LastIndexOf (';');
	if (semic < 0) {
		return path;
	}
	var hyphen  = path.LastIndexOf ('-');
	if (hyphen < 0) {
		return path.Substring (0, semic+1) + "*";
	}
	return path.Substring (0, Math.Max (hyphen, semic)+1) + "*";
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
