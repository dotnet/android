using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

/// <summary>
/// Generates AndroidManifest.xml from component attributes captured by the JavaPeerScanner.
/// This is the trimmable-path equivalent of ManifestDocument — it works from ComponentInfo
/// records instead of Cecil TypeDefinitions.
/// </summary>
class ManifestGenerator
{
	static readonly XNamespace AndroidNs = ManifestConstants.AndroidNs;
	static readonly XName AttName = ManifestConstants.AttName;
	static readonly char [] PlaceholderSeparators = [';'];

	int appInitOrder = 2000000000;

	public string PackageName { get; set; } = "";
	public string ApplicationLabel { get; set; } = "";
	public string VersionCode { get; set; } = "";
	public string VersionName { get; set; } = "";
	public string MinSdkVersion { get; set; } = "21";
	public string TargetSdkVersion { get; set; } = "36";
	public string AndroidRuntime { get; set; } = "coreclr";
	public bool Debug { get; set; }
	public bool NeedsInternet { get; set; }
	public bool EmbedAssemblies { get; set; }
	public bool ForceDebuggable { get; set; }
	public bool ForceExtractNativeLibs { get; set; }
	public string? ManifestPlaceholders { get; set; }
	public string? ApplicationJavaClass { get; set; }

	/// <summary>
	/// Generates the merged manifest from an optional pre-loaded template and writes it to <paramref name="outputPath"/>.
	/// Returns the list of additional content provider names (for ApplicationRegistration.java).
	/// </summary>
	public IList<string> Generate (
		XDocument? manifestTemplate,
		IReadOnlyList<JavaPeerInfo> allPeers,
		AssemblyManifestInfo assemblyInfo,
		string outputPath)
	{
		var doc = manifestTemplate ?? CreateDefaultManifest ();
		var manifest = doc.Root;
		if (manifest is null) {
			throw new InvalidOperationException ("Manifest document has no root element.");
		}

		EnsureManifestAttributes (manifest);
		var app = EnsureApplicationElement (manifest);

		// Apply assembly-level [Application] properties
		if (assemblyInfo.ApplicationProperties is not null) {
			AssemblyLevelElementBuilder.ApplyApplicationProperties (app, assemblyInfo.ApplicationProperties);
		}

		var existingTypes = new HashSet<string> (
			app.Descendants ().Select (a => (string?)a.Attribute (AttName)).OfType<string> ());

		// Add components from scanned types
		foreach (var peer in allPeers) {
			if (peer.IsAbstract || peer.ComponentAttribute is null) {
				continue;
			}

			// Skip Application types (handled separately via assembly-level attribute)
			if (peer.ComponentAttribute.Kind == ComponentKind.Application) {
				ComponentElementBuilder.UpdateApplicationElement (app, peer);
				continue;
			}

			if (peer.ComponentAttribute.Kind == ComponentKind.Instrumentation) {
				ComponentElementBuilder.AddInstrumentation (manifest, peer);
				continue;
			}

			string jniName = peer.JavaName.Replace ('/', '.');
			if (existingTypes.Contains (jniName)) {
				continue;
			}

			var element = ComponentElementBuilder.CreateComponentElement (peer, jniName);
			if (element is not null) {
				app.Add (element);
			}
		}

		// Add assembly-level manifest elements
		AssemblyLevelElementBuilder.AddAssemblyLevelElements (manifest, app, assemblyInfo);

		// Add runtime provider
		var providerNames = AddRuntimeProviders (app);

		// Set ApplicationJavaClass
		if (!string.IsNullOrEmpty (ApplicationJavaClass) && app.Attribute (AttName) is null) {
			app.SetAttributeValue (AttName, ApplicationJavaClass);
		}

		// Handle debuggable
		bool needDebuggable = Debug && app.Attribute (AndroidNs + "debuggable") is null;
		if (ForceDebuggable || needDebuggable) {
			app.SetAttributeValue (AndroidNs + "debuggable", "true");
		}

		// Handle extractNativeLibs
		if (ForceExtractNativeLibs) {
			app.SetAttributeValue (AndroidNs + "extractNativeLibs", "true");
		}

		// Add internet permission for debug
		if (Debug || NeedsInternet) {
			AssemblyLevelElementBuilder.AddInternetPermission (manifest);
		}

		// Apply manifest placeholders
		string? placeholders = ManifestPlaceholders;
		if (placeholders is not null && placeholders.Length > 0) {
			ApplyPlaceholders (doc, placeholders);
		}

		// Write output
		var outputDir = Path.GetDirectoryName (outputPath);
		if (outputDir is not null) {
			Directory.CreateDirectory (outputDir);
		}
		doc.Save (outputPath);

		return providerNames;
	}

	XDocument CreateDefaultManifest ()
	{
		return new XDocument (
			new XDeclaration ("1.0", "utf-8", null),
			new XElement ("manifest",
				new XAttribute (XNamespace.Xmlns + "android", AndroidNs.NamespaceName),
				new XAttribute ("package", PackageName)));
	}

	void EnsureManifestAttributes (XElement manifest)
	{
		manifest.SetAttributeValue (XNamespace.Xmlns + "android", AndroidNs.NamespaceName);

		if (string.IsNullOrEmpty ((string?)manifest.Attribute ("package"))) {
			manifest.SetAttributeValue ("package", PackageName);
		}

		if (manifest.Attribute (AndroidNs + "versionCode") is null) {
			manifest.SetAttributeValue (AndroidNs + "versionCode",
				string.IsNullOrEmpty (VersionCode) ? "1" : VersionCode);
		}

		if (manifest.Attribute (AndroidNs + "versionName") is null) {
			manifest.SetAttributeValue (AndroidNs + "versionName",
				string.IsNullOrEmpty (VersionName) ? "1.0" : VersionName);
		}

		// Add <uses-sdk>
		if (!manifest.Elements ("uses-sdk").Any ()) {
			manifest.AddFirst (new XElement ("uses-sdk",
				new XAttribute (AndroidNs + "minSdkVersion", MinSdkVersion),
				new XAttribute (AndroidNs + "targetSdkVersion", TargetSdkVersion)));
		}
	}

	XElement EnsureApplicationElement (XElement manifest)
	{
		var app = manifest.Element ("application");
		if (app is null) {
			app = new XElement ("application");
			manifest.Add (app);
		}

		if (app.Attribute (AndroidNs + "label") is null && !string.IsNullOrEmpty (ApplicationLabel)) {
			app.SetAttributeValue (AndroidNs + "label", ApplicationLabel);
		}

		return app;
	}

	IList<string> AddRuntimeProviders (XElement app)
	{
		string packageName = "mono";
		string className = "MonoRuntimeProvider";

		if (string.Equals (AndroidRuntime, "nativeaot", StringComparison.OrdinalIgnoreCase)) {
			packageName = "net.dot.jni.nativeaot";
			className = "NativeAotRuntimeProvider";
		}

		// Check if runtime provider already exists in template
		string runtimeProviderName = $"{packageName}.{className}";
		if (!app.Elements ("provider").Any (p => {
			var name = (string?)p.Attribute (ManifestConstants.AttName);
			return name == runtimeProviderName ||
				((string?)p.Attribute (AndroidNs.GetName ("authorities")))?.EndsWith (".__mono_init__", StringComparison.Ordinal) == true;
		})) {
			app.Add (CreateRuntimeProvider (runtimeProviderName, null, --appInitOrder));
		}

		var providerNames = new List<string> ();
		var processAttrName = AndroidNs.GetName ("process");
		var procs = new List<string> ();

		foreach (var el in app.Elements ()) {
			var proc = el.Attribute (processAttrName);
			if (proc is null || procs.Contains (proc.Value)) {
				continue;
			}
			if (el.Name.NamespaceName != "") {
				continue;
			}
			switch (el.Name.LocalName) {
			case "provider":
				var autho = el.Attribute (AndroidNs.GetName ("authorities"));
				if (autho is not null && autho.Value.EndsWith (".__mono_init__", StringComparison.Ordinal)) {
					continue;
				}
				goto case "activity";
			case "activity":
			case "receiver":
			case "service":
				procs.Add (proc.Value);
				string providerName = $"{className}_{procs.Count}";
				providerNames.Add (providerName);
				app.Add (CreateRuntimeProvider ($"{packageName}.{providerName}", proc.Value, --appInitOrder));
				break;
			}
		}

		return providerNames;
	}

	XElement CreateRuntimeProvider (string name, string? processName, int initOrder)
	{
		return new XElement ("provider",
			new XAttribute (AndroidNs + "name", name),
			new XAttribute (AndroidNs + "exported", "false"),
			new XAttribute (AndroidNs + "initOrder", initOrder),
			processName is not null ? new XAttribute (AndroidNs + "process", processName) : null,
			new XAttribute (AndroidNs + "authorities", PackageName + "." + name + ".__mono_init__"));
	}

	/// <summary>
	/// Replaces ${key} placeholders in all attribute values throughout the document.
	/// Placeholder format: "key1=value1;key2=value2"
	/// </summary>
	static void ApplyPlaceholders (XDocument doc, string placeholders)
	{
		var replacements = new Dictionary<string, string> (StringComparer.Ordinal);
		foreach (var entry in placeholders.Split (PlaceholderSeparators, StringSplitOptions.RemoveEmptyEntries)) {
			var eqIndex = entry.IndexOf ('=');
			if (eqIndex > 0) {
				var key = entry.Substring (0, eqIndex).Trim ();
				var value = entry.Substring (eqIndex + 1).Trim ();
				replacements ["${" + key + "}"] = value;
			}
		}

		if (replacements.Count == 0) {
			return;
		}

		foreach (var element in doc.Descendants ()) {
			foreach (var attr in element.Attributes ()) {
				var val = attr.Value;
				foreach (var kvp in replacements) {
					if (val.Contains (kvp.Key)) {
						val = val.Replace (kvp.Key, kvp.Value);
					}
				}
				if (val != attr.Value) {
					attr.Value = val;
				}
			}
		}
	}
}
