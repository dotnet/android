using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

/// <summary>
/// Generates AndroidManifest.xml from component attributes captured by the JavaPeerScanner.
/// This is the trimmable-path equivalent of ManifestDocument — it works from ComponentInfo
/// records instead of Cecil TypeDefinitions.
/// </summary>
class ManifestGenerator
{
	static readonly char [] PlaceholderSeparators = [';'];
	static readonly HashSet<string> ComponentElementNames = new (StringComparer.Ordinal) {
		"application",
		"activity",
		"instrumentation",
		"service",
		"receiver",
		"provider",
	};

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
	public Action<string>? Warn { get; set; }

	/// <summary>
	/// Generates the merged manifest from an optional pre-loaded template and writes it to <paramref name="outputPath"/>.
	/// Returns the list of additional content provider names (for ApplicationRegistration.java).
	/// </summary>
	public (ManifestDocument Document, IList<string> ProviderNames) Generate (
		ManifestDocument? manifestTemplate,
		IReadOnlyList<JavaPeerInfo> allPeers,
		AssemblyManifestInfo assemblyInfo)
	{
		var doc = manifestTemplate ?? ManifestDocument.CreateDefault (PackageName);
		var manifest = doc.Root;
		if (manifest is null) {
			throw new InvalidOperationException ("Manifest document has no root element.");
		}

		EnsureManifestAttributes (manifest);
		var app = EnsureApplicationElement (manifest);

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
				.Select (a => a.AndroidAttribute (ManifestConstants.AttributeName))
				.OfType<string> (),
			StringComparer.Ordinal);

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
				ComponentElementBuilder.AddInstrumentation (manifest, peer, PackageName);
				continue;
			}

			string jniName = JniSignatureHelper.JniNameToJavaName (peer.JavaName);
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
		var applicationJavaClass = ApplicationJavaClass;
		if (applicationJavaClass is not null && applicationJavaClass.Length > 0 && !app.HasAttribute (ManifestConstants.AndroidNamespace, ManifestConstants.AttributeName)) {
			app.SetAndroidAttribute (ManifestConstants.AttributeName, applicationJavaClass);
		}

		// Handle debuggable
		bool needDebuggable = Debug && !app.HasAttribute (ManifestConstants.AndroidNamespace, "debuggable");
		if (ForceDebuggable || needDebuggable) {
			app.SetAndroidAttribute ("debuggable", "true");
		}

		// Handle extractNativeLibs
		if (ForceExtractNativeLibs) {
			app.SetAndroidAttribute ("extractNativeLibs", "true");
		}

		// Add internet permission for debug
		if (Debug || NeedsInternet) {
			AssemblyLevelElementBuilder.AddInternetPermission (manifest);
		}

		// Apply manifest placeholders
		ApplyPlaceholders (doc, ManifestPlaceholders);

		return (doc, providerNames);
	}

	/// <summary>
	/// Manifest templates may use compat JNI names (e.g., "android.apptests.App")
	/// but the trimmable path generates JCWs with hashed package names (e.g., "crc64.../App").
	/// This method rewrites any compat name references to the actual JCW name so the
	/// Android runtime can find the class.
	/// </summary>
	void RewriteCompatNames (ManifestElement manifest, IReadOnlyList<JavaPeerInfo> allPeers)
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
		var packageName = manifest.Attribute ("package") ?? "";

		foreach (var element in manifest.DescendantsAndSelf ()) {
			if (!IsComponentElement (element)) {
				continue;
			}

			var name = element.AndroidAttribute (ManifestConstants.AttributeName);
			if (name is null) {
				continue;
			}
			var resolved = ManifestNameResolver.Resolve (name, packageName);
			if (compatToCrc.TryGetValue (resolved, out var crcName)) {
				element.SetAndroidAttribute (ManifestConstants.AttributeName, crcName);
			}
		}
	}

	static bool IsComponentElement (ManifestElement element)
	{
		return element.NamespaceName.Length == 0 && ComponentElementNames.Contains (element.LocalName);
	}

	void EnsureManifestAttributes (ManifestElement manifest)
	{
		manifest.SetNamespaceDeclaration ("android", ManifestConstants.AndroidNamespace);

		if (string.IsNullOrEmpty (manifest.Attribute ("package"))) {
			manifest.SetAttribute ("package", PackageName);
		}

		if (!manifest.HasAttribute (ManifestConstants.AndroidNamespace, "versionCode")) {
			manifest.SetAndroidAttribute ("versionCode",
				string.IsNullOrEmpty (VersionCode) ? "1" : VersionCode);
		}

		if (!manifest.HasAttribute (ManifestConstants.AndroidNamespace, "versionName")) {
			manifest.SetAndroidAttribute ("versionName",
				string.IsNullOrEmpty (VersionName) ? "1.0" : VersionName);
		}

		// Add <uses-sdk>
		if (!manifest.Elements ("uses-sdk").Any ()) {
			if (MinSdkVersion.IsNullOrEmpty ()) {
				throw new InvalidOperationException ("MinSdkVersion must be provided by MSBuild.");
			}
			if (TargetSdkVersion.IsNullOrEmpty ()) {
				throw new InvalidOperationException ("TargetSdkVersion must be provided by MSBuild.");
			}
			var usesSdk = new ManifestElement ("uses-sdk");
			usesSdk.SetAndroidAttribute ("minSdkVersion", MinSdkVersion);
			usesSdk.SetAndroidAttribute ("targetSdkVersion", TargetSdkVersion);
			manifest.AddFirst (usesSdk);
		}
	}

	ManifestElement EnsureApplicationElement (ManifestElement manifest)
	{
		var app = manifest.Element ("application");
		if (app is null) {
			app = new ManifestElement ("application");
			manifest.Add (app);
		}

		if (!app.HasAttribute (ManifestConstants.AndroidNamespace, "label") && !string.IsNullOrEmpty (ApplicationLabel)) {
			app.SetAndroidAttribute ("label", ApplicationLabel);
		}

		return app;
	}

	IList<string> AddRuntimeProviders (ManifestElement app)
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
		if (!app.Elements ("provider").Any (p => {
			var name = p.AndroidAttribute (ManifestConstants.AttributeName);
			return name == runtimeProviderName ||
				p.AndroidAttribute ("authorities")?.EndsWith (".__mono_init__", StringComparison.Ordinal) == true;
		})) {
			app.Add (CreateRuntimeProvider (runtimeProviderName, null, --appInitOrder));
		}

		var providerNames = new List<string> ();
		var procs = new List<string> ();

		foreach (var el in app.Elements ().ToList ()) {
			var proc = el.AndroidAttribute ("process");
			if (proc is null || procs.Contains (proc)) {
				continue;
			}
			if (el.NamespaceName != "") {
				continue;
			}
			switch (el.LocalName) {
			case "provider":
				var autho = el.AndroidAttribute ("authorities");
				if (autho is not null && autho.EndsWith (".__mono_init__", StringComparison.Ordinal)) {
					continue;
				}
				goto case "activity";
			case "activity":
			case "receiver":
			case "service":
				procs.Add (proc);
				string providerName = $"{className}_{procs.Count}";
				providerNames.Add (providerName);
				app.Add (CreateRuntimeProvider ($"{packageName}.{providerName}", proc, --appInitOrder));
				break;
			}
		}

		return providerNames;
	}

	ManifestElement CreateRuntimeProvider (string name, string? processName, int initOrder)
	{
		var provider = new ManifestElement ("provider");
		provider.SetAndroidAttribute ("name", name);
		provider.SetAndroidAttribute ("exported", "false");
		provider.SetAndroidAttribute ("initOrder", initOrder.ToString (System.Globalization.CultureInfo.InvariantCulture));
		if (processName is not null) {
			provider.SetAndroidAttribute ("process", processName);
		}
		provider.SetAndroidAttribute ("authorities", PackageName + "." + name + ".__mono_init__");
		return provider;
	}

	/// <summary>
	/// Replaces ${key} placeholders in all attribute values throughout the document.
	/// Placeholder format: "key1=value1;key2=value2"
	/// </summary>
	internal static void ApplyPlaceholders (ManifestDocument doc, string? placeholders)
	{
		if (placeholders.IsNullOrEmpty ()) {
			return;
		}

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

		if (doc.Root is null) {
			return;
		}

		foreach (var element in doc.Root.DescendantsAndSelf ()) {
			foreach (var attr in element.Attributes) {
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
