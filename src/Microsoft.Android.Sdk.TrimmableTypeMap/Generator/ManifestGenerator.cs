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
	static readonly HashSet<string> ComponentElementNames = new (StringComparer.Ordinal) {
		"application",
		"activity",
		"instrumentation",
		"service",
		"receiver",
		"provider",
	};

	/// <summary>Warning code for library-manifest merge failures (maps to XA4302).</summary>
	internal const int LibraryManifestMergeWarningCode = 4302;

	int appInitOrder = 2000000000;

	public string PackageName { get; set; } = "";
	public string ApplicationLabel { get; set; } = "";
	public string VersionCode { get; set; } = "";
	public string VersionName { get; set; } = "";
	public string MinSdkVersion { get; set; } = "";
	public string TargetSdkVersion { get; set; } = "";
	public string RuntimeProviderJavaName { get; set; } = "";
	public bool Debug { get; set; }
	public bool NeedsInternet { get; set; }
	public bool EmbedAssemblies { get; set; }
	public bool ForceDebuggable { get; set; }
	public bool ForceExtractNativeLibs { get; set; }
	public string? ManifestPlaceholders { get; set; }
	public string? ApplicationJavaClass { get; set; }
	public Action<int, string>? Warn { get; set; }
	public Action<string>? WarnInvalidPlaceholder { get; set; }

	/// <summary>
	/// Absolute paths to extracted library (.aar) manifests that must be merged into the
	/// application manifest. Only used by the legacy manifest merger; manifestmerger.jar handles
	/// this downstream in the _ManifestMerger target. Mirrors the legacy ManifestDocument merge.
	/// </summary>
	public IReadOnlyList<string> LibraryManifests { get; set; } = [];

	static readonly XNamespace ToolsNs = "http://schemas.android.com/tools";

	// Attributes whose values are component names that must be qualified with the library's
	// own package when they are relative (start with '.'). Mirrors ManifestDocument.ManifestAttributeFixups.
	static readonly Dictionary<string, string []> ManifestAttributeFixups = new (StringComparer.Ordinal) {
		{ "activity", ["name"] },
		{ "application", ["backupAgent"] },
		{ "instrumentation", ["name"] },
		{ "provider", ["name"] },
		{ "receiver", ["name"] },
		{ "service", ["name"] },
	};

	/// <summary>
	/// Generates the merged manifest from an optional pre-loaded template and writes it to <paramref name="outputPath"/>.
	/// Returns the list of additional content provider names (for ApplicationRegistration.java).
	/// </summary>
	public (XDocument Document, IList<string> ProviderNames) Generate (
		XDocument? manifestTemplate,
		IReadOnlyList<JavaPeerInfo> allPeers,
		AssemblyManifestInfo assemblyInfo)
	{
		var doc = manifestTemplate ?? CreateDefaultManifest ();
		var manifest = doc.Root;
		if (manifest is null) {
			throw new InvalidOperationException ("Manifest document has no root element.");
		}

		EnsureManifestAttributes (manifest);
		var app = EnsureApplicationElement (manifest);
		var targetSdkVersionValue = GetTargetSdkVersionValue (manifest);

		// Rewrite compat JNI names in the template to CRC names BEFORE collecting
		// existing types, so the duplicate check works correctly.
		RewriteCompatNames (manifest, allPeers);

		// Apply assembly-level [Application] properties
		if (assemblyInfo.ApplicationProperties is not null) {
			AssemblyLevelElementBuilder.ApplyApplicationProperties (app, assemblyInfo.ApplicationProperties, allPeers, Warn);
		}

		var existingTypes = new HashSet<string> (
			app.Descendants ()
				.Where (IsComponentElement)
				.Select (a => (string?) a.Attribute (AttName))
				.OfType<string> (),
			StringComparer.Ordinal);

		// Map managed type names to their Java/manifest names so component properties that reference
		// other types (e.g. [Activity (ParentActivity = typeof (...))]) can be resolved.
		var managedToManifestNames = new Dictionary<string, string> (allPeers.Count, StringComparer.Ordinal);
		foreach (var peer in allPeers) {
			if (!string.IsNullOrEmpty (peer.ManagedTypeName)) {
				managedToManifestNames [peer.ManagedTypeName] = JniSignatureHelper.JniNameToJavaName (peer.JavaName);
			}
		}

		// Add components from scanned types
		foreach (var peer in allPeers) {
			if (peer.IsAbstract || peer.ComponentAttribute is null) {
				continue;
			}

			// Skip Application types (handled separately via assembly-level attribute)
			if (peer.ComponentAttribute.Kind == ComponentKind.Application) {
				ComponentElementBuilder.UpdateApplicationElement (app, peer, targetSdkVersionValue);
				continue;
			}

			if (peer.ComponentAttribute.Kind == ComponentKind.Instrumentation) {
				ComponentElementBuilder.AddInstrumentation (manifest, peer, PackageName);
				continue;
			}

			string jniName = JniSignatureHelper.JniNameToJavaName (peer.JavaName);
			if (existingTypes.Contains (jniName)) {
				continue;
			}

			var element = ComponentElementBuilder.CreateComponentElement (peer, jniName, targetSdkVersionValue, managedToManifestNames);
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
		if (targetSdkVersionValue >= 23 &&
		    (ForceExtractNativeLibs || app.Attribute (AndroidNs + "extractNativeLibs") is null)) {
			app.SetAttributeValue (AndroidNs + "extractNativeLibs", "true");
		}

		// Add internet permission for debug
		if (Debug || NeedsInternet) {
			AssemblyLevelElementBuilder.AddInternetPermission (manifest);
		}

		// Merge extracted library (.aar) manifests (legacy merger only — manifestmerger.jar does
		// this downstream). Mirrors ManifestDocument: merge, then dedup, then strip node="remove",
		// all before placeholder substitution so ${applicationId} resolves in merged content.
		MergeLibraryManifests (manifest);
		RemoveDuplicateElements (doc);
		RemoveNodes (doc);

		// Apply manifest placeholders
		ApplyPlaceholders (doc, ManifestPlaceholders, PackageName, WarnInvalidPlaceholder);

		return (doc, providerNames);
	}

	/// <summary>
	/// Merges each library manifest's top-level elements into the application manifest, mirroring
	/// ManifestDocument.MergeLibraryManifest: elements with a matching android:name append their
	/// children to the existing element, otherwise the element is added; relative component names
	/// are qualified with the library's own package.
	/// </summary>
	void MergeLibraryManifests (XElement manifest)
	{
		foreach (var path in LibraryManifests) {
			if (path.IsNullOrEmpty () || !File.Exists (path)) {
				continue;
			}

			XDocument libDoc;
			try {
				libDoc = XDocument.Load (path);
			} catch (Exception ex) {
				Warn?.Invoke (LibraryManifestMergeWarningCode, $"Unable to merge library manifest '{path}': {ex.Message}");
				continue;
			}

			if (libDoc.Root is not { } libRoot) {
				continue;
			}

			var package = (string?) libRoot.Attribute ("package") ?? "";
			foreach (var top in libRoot.Elements ().ToList ()) {
				var name = (string?) top.Attribute (AndroidNs + "name");
				XElement? existing = name is not null
					? manifest.Elements (top.Name).FirstOrDefault (e => (string?) e.Attribute (AndroidNs + "name") == name)
					: manifest.Elements (top.Name).FirstOrDefault ();

				if (existing is not null) {
					// Append the library element's children to the matching element.
					existing.Add (FixupNameElements (package, top.Nodes ()));
				} else {
					manifest.Add (FixupNameElements (package, [top]));
				}
			}
		}
	}

	/// <summary>
	/// Qualifies relative component names (those starting with '.') with the supplied package,
	/// mirroring ManifestDocument.FixupNameElements.
	/// </summary>
	static IEnumerable<XNode> FixupNameElements (string packageName, IEnumerable<XNode> nodes)
	{
		var nodeList = nodes.ToList ();
		foreach (var element in nodeList.OfType<XElement> ().Where (x => ManifestAttributeFixups.ContainsKey (x.Name.LocalName))) {
			var attributes = ManifestAttributeFixups [element.Name.LocalName];
			foreach (var attr in element.Attributes ().Where (x => attributes.Contains (x.Name.LocalName))) {
				if (attr.Value.StartsWith (".", StringComparison.Ordinal)) {
					attr.Value = packageName + attr.Value;
				}
			}
		}
		return nodeList;
	}

	/// <summary>
	/// Removes structurally-identical duplicate elements, mirroring ManifestDocument.RemoveDuplicateElements.
	/// </summary>
	static void RemoveDuplicateElements (XDocument doc)
	{
		foreach (var duplicate in ResolveDuplicates (doc.Elements ()).ToList ()) {
			duplicate.Remove ();
		}
	}

	static IEnumerable<XElement> ResolveDuplicates (IEnumerable<XElement> elements)
	{
		var elementList = elements.ToList ();
		foreach (var e in elementList) {
			foreach (var d in ResolveDuplicates (e.Elements ())) {
				yield return d;
			}
		}
		foreach (var d in elementList.GroupBy (x => x.ToString (SaveOptions.DisableFormatting)).SelectMany (x => x.Skip (1))) {
			yield return d;
		}
	}

	/// <summary>
	/// Removes elements marked with tools:node="remove", mirroring ManifestDocument.RemoveNodes.
	/// </summary>
	static void RemoveNodes (XDocument doc)
	{
		foreach (var node in doc.Descendants ().ToList ()) {
			if (node.Attribute (ToolsNs + "node")?.Value == "remove") {
				node.Remove ();
			}
		}
	}

	XDocument CreateDefaultManifest ()
	{
		return new XDocument (
			new XDeclaration ("1.0", "utf-8", null),
			new XElement ("manifest",
				new XAttribute (XNamespace.Xmlns + "android", AndroidNs.NamespaceName),
				new XAttribute ("package", PackageName)));
	}

	/// <summary>
	/// Manifest templates may use compat JNI names (e.g., "android.apptests.App")
	/// but the trimmable path generates JCWs with hashed package names (e.g., "crc64.../App").
	/// This method rewrites any compat name references to the actual JCW name so the
	/// Android runtime can find the class.
	/// </summary>
	void RewriteCompatNames (XElement manifest, IReadOnlyList<JavaPeerInfo> allPeers)
	{
		// Build mapping: fully-qualified compat Java name → CRC Java name
		var compatToCrc = new Dictionary<string, string> (allPeers.Count, StringComparer.Ordinal);
		foreach (var peer in allPeers) {
			string javaName = JniSignatureHelper.JniNameToJavaName (peer.JavaName);
			string compatName = JniSignatureHelper.JniNameToJavaName (peer.CompatJniName);
			if (javaName != compatName) {
				compatToCrc [compatName] = javaName;
			}
		}

		if (compatToCrc.Count == 0) {
			return;
		}

		// Rewrite android:name attributes throughout the manifest. Android allows
		// android:name to be specified as:
		//   - fully qualified ("com.example.app.MainActivity")
		//   - relative to the manifest package, starting with '.' (".MainActivity")
		//   - bare, with no '.' at all ("MainActivity"), also relative to the package
		// Resolve to the fully-qualified form before the lookup, then write the CRC
		// name back so duplicate detection later in the pipeline works correctly.
		var packageName = (string?) manifest.Attribute ("package") ?? "";

		foreach (var element in manifest.DescendantsAndSelf ()) {
			if (!IsComponentElement (element)) {
				continue;
			}

			var nameAttr = element.Attribute (AttName);
			if (nameAttr is null) {
				continue;
			}
			var resolved = ManifestNameResolver.Resolve (nameAttr.Value, packageName);
			if (compatToCrc.TryGetValue (resolved, out var crcName)) {
				nameAttr.Value = crcName;
			}
		}
	}

	static bool IsComponentElement (XElement element)
	{
		return element.Name.NamespaceName.Length == 0 && ComponentElementNames.Contains (element.Name.LocalName);
	}

	void EnsureManifestAttributes (XElement manifest)
	{
		manifest.SetAttributeValue (XNamespace.Xmlns + "android", AndroidNs.NamespaceName);

		// Resolve the package: when the template uses a placeholder token such as "${PACKAGENAME}"
		// (or has no package), write the resolved value provided by MSBuild ($(_AndroidPackage),
		// produced by GetAndroidPackageName, which substitutes placeholders and canonicalizes the
		// package). This matches the legacy GenerateMainAndroidManifest; a valid explicit package is
		// preserved so compat-name resolution keeps using it.
		var packageAttr = (string?) manifest.Attribute ("package") ?? "";
		if ((packageAttr.Length == 0 || packageAttr.Contains ("${")) && !PackageName.IsNullOrEmpty ()) {
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
		var usesSdk = manifest.Element ("uses-sdk");
		if (usesSdk is null) {
			if (MinSdkVersion.IsNullOrEmpty ()) {
				throw new InvalidOperationException ("MinSdkVersion must be provided by MSBuild.");
			}
			if (TargetSdkVersion.IsNullOrEmpty ()) {
				throw new InvalidOperationException ("TargetSdkVersion must be provided by MSBuild.");
			}
			manifest.AddFirst (new XElement ("uses-sdk",
				new XAttribute (AndroidNs + "minSdkVersion", MinSdkVersion),
				new XAttribute (AndroidNs + "targetSdkVersion", TargetSdkVersion)));
		} else if (usesSdk.Attribute (AndroidNs + "minSdkVersion") is null) {
			if (MinSdkVersion.IsNullOrEmpty ()) {
				throw new InvalidOperationException ("MinSdkVersion must be provided by MSBuild.");
			}
			usesSdk.SetAttributeValue (AndroidNs + "minSdkVersion", MinSdkVersion);
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

	int GetTargetSdkVersionValue (XElement manifest)
	{
		// Parity note: legacy ManifestDocument resolves the target SDK via VersionResolver.GetApiLevelFromId,
		// which also maps codename ids (e.g. preview API names) to integers. That resolver lives in
		// Xamarin.Android.Build.Tasks and isn't referenceable from this netstandard2.0 generator, so we only
		// handle integer values here. A codename in the user-authored manifest falls through to the MSBuild
		// TargetSdkVersion property, which is always already resolved to an integer (see TrimmableTypeMapGenerator).
		var targetSdk = (string?) manifest.Element ("uses-sdk")?.Attribute (AndroidNs + "targetSdkVersion");
		if (int.TryParse (targetSdk, out int value)) {
			return value;
		}
		if (int.TryParse (TargetSdkVersion, out value)) {
			return value;
		}
		// Fail loudly rather than silently using 0 (which would emit a wrong manifest), matching legacy
		// ManifestDocument, which throws InvalidOperationException on an unrecognized targetSdkVersion.
		throw new InvalidOperationException (
			$"The targetSdkVersion ('{targetSdk ?? TargetSdkVersion}') could not be resolved to an integer API level.");
	}

	IList<string> AddRuntimeProviders (XElement app)
	{
		if (RuntimeProviderJavaName.IsNullOrEmpty ()) {
			throw new InvalidOperationException ("RuntimeProviderJavaName must be provided by MSBuild.");
		}
		int lastDot = RuntimeProviderJavaName.LastIndexOf ('.');
		if (lastDot < 0 || lastDot == RuntimeProviderJavaName.Length - 1) {
			throw new InvalidOperationException ($"RuntimeProviderJavaName must be a fully-qualified Java type name: '{RuntimeProviderJavaName}'.");
		}

		string packageName = RuntimeProviderJavaName.Substring (0, lastDot);
		string className = RuntimeProviderJavaName.Substring (lastDot + 1);

		// Check if runtime provider already exists in template
		string runtimeProviderName = RuntimeProviderJavaName;
		bool directBootAware = DirectBootAware (app);
		if (!app.Elements ("provider").Any (p => {
			var name = (string?)p.Attribute (ManifestConstants.AttName);
			return name == runtimeProviderName ||
				((string?)p.Attribute (AndroidNs.GetName ("authorities")))?.EndsWith (".__mono_init__", StringComparison.Ordinal) == true;
		})) {
			app.Add (CreateRuntimeProvider (runtimeProviderName, null, --appInitOrder, directBootAware));
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
				app.Add (CreateRuntimeProvider ($"{packageName}.{providerName}", proc.Value, --appInitOrder, directBootAware));
				break;
			}
		}

		return providerNames;
	}

	static bool DirectBootAware (XElement app)
	{
		var directBootAwareAttrName = AndroidNs.GetName ("directBootAware");
		if (IsDirectBootAware (app.Attribute (directBootAwareAttrName))) {
			return true;
		}

		foreach (var element in app.Elements ()) {
			if (IsDirectBootAware (element.Attribute (directBootAwareAttrName))) {
				return true;
			}
		}

		return false;
	}

	static bool IsDirectBootAware (XAttribute? attribute)
	{
		return attribute is not null &&
			bool.TryParse (attribute.Value, out bool value) &&
			value;
	}

	XElement CreateRuntimeProvider (string name, string? processName, int initOrder, bool directBootAware)
	{
		return new XElement ("provider",
			new XAttribute (AndroidNs + "name", name),
			new XAttribute (AndroidNs + "exported", "false"),
			new XAttribute (AndroidNs + "initOrder", initOrder),
			directBootAware ? new XAttribute (AndroidNs + "directBootAware", "true") : null,
			processName is not null ? new XAttribute (AndroidNs + "process", processName) : null,
			new XAttribute (AndroidNs + "authorities", PackageName + "." + name + ".__mono_init__"));
	}

	/// <summary>
	/// Replaces ${key} placeholders in all attribute values throughout the document.
	/// Placeholder format: "key1=value1;key2=value2"
	/// </summary>
	internal static void ApplyPlaceholders (XDocument doc, string? placeholders, string? packageName = null, Action<string>? warnInvalidPlaceholder = null)
	{
		var replacements = new Dictionary<string, string> (StringComparer.Ordinal);
		if (!placeholders.IsNullOrEmpty ()) {
			foreach (var entry in placeholders.Split (PlaceholderSeparators, StringSplitOptions.RemoveEmptyEntries)) {
				var eqIndex = entry.IndexOf ('=');
				if (eqIndex >= 0) {
					var key = entry.Substring (0, eqIndex).Trim ();
					// Normalize '\' to Path.DirectorySeparatorChar to stay byte-for-byte identical to the
					// legacy pipeline on every platform: there the substituted manifest is re-encoded by
					// aapt2, which rewrites backslashes to the platform separator ('/' on Unix, '\' preserved
					// on Windows) across the whole manifest. The trimmable generator writes the merged
					// manifest directly (no aapt2 re-encode of these values), so it applies the same
					// per-platform normalization to every value. The ManifestPlaceholders build test pins
					// this for both the legacy (CoreCLR) and trimmable (NativeAOT) paths, so a hardcoded '/'
					// would fail on Windows.
					var value = entry.Substring (eqIndex + 1).Trim ().Replace ('\\', Path.DirectorySeparatorChar);
					replacements ["${" + key + "}"] = value;
				} else if (eqIndex < 0) {
					// An entry without '=' is not a valid key=value pair. Mirror the legacy
					// ManifestDocument.ReplacePlaceholders behavior and warn (XA1010).
					warnInvalidPlaceholder?.Invoke (placeholders);
				}
			}
		}

		// ${applicationId} is a built-in placeholder that always resolves to the application
		// package name (mirrors ManifestDocument.Save, which does
		// s.Replace ("${applicationId}", PackageName) before applying the user placeholders).
		// It is set last so it wins over any user-supplied "applicationId" entry, matching the
		// legacy ordering, and is what substitutes merged library-manifest values such as
		// "${applicationId}.permission.C2D_MESSAGE".
		if (!packageName.IsNullOrEmpty ()) {
			replacements ["${applicationId}"] = packageName;
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
