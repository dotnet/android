#nullable enable

using System;
using System.Globalization;
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
	static readonly XNamespace AndroidNs = "http://schemas.android.com/apk/res/android";
	static readonly XName AttName = AndroidNs + "name";
	static readonly char [] PlaceholderSeparators = [';'];

	enum MappingKind { String, Bool, Enum }

	readonly struct PropertyMapping
	{
		public string PropertyName { get; }
		public string XmlAttributeName { get; }
		public MappingKind Kind { get; }
		public Func<int, string?>? EnumConverter { get; }

		public PropertyMapping (string propertyName, string xmlAttributeName, MappingKind kind = MappingKind.String, Func<int, string?>? enumConverter = null)
		{
			PropertyName = propertyName;
			XmlAttributeName = xmlAttributeName;
			Kind = kind;
			EnumConverter = enumConverter;
		}
	}

	static readonly PropertyMapping[] CommonMappings = [
		new ("Label", "label"),
		new ("Description", "description"),
		new ("Icon", "icon"),
		new ("RoundIcon", "roundIcon"),
		new ("Permission", "permission"),
		new ("Process", "process"),
		new ("Enabled", "enabled", MappingKind.Bool),
		new ("DirectBootAware", "directBootAware", MappingKind.Bool),
		new ("Exported", "exported", MappingKind.Bool),
	];

	static readonly PropertyMapping[] ActivityMappings = [
		new ("Theme", "theme"),
		new ("ParentActivity", "parentActivityName"),
		new ("TaskAffinity", "taskAffinity"),
		new ("AllowTaskReparenting", "allowTaskReparenting", MappingKind.Bool),
		new ("AlwaysRetainTaskState", "alwaysRetainTaskState", MappingKind.Bool),
		new ("ClearTaskOnLaunch", "clearTaskOnLaunch", MappingKind.Bool),
		new ("ExcludeFromRecents", "excludeFromRecents", MappingKind.Bool),
		new ("FinishOnCloseSystemDialogs", "finishOnCloseSystemDialogs", MappingKind.Bool),
		new ("FinishOnTaskLaunch", "finishOnTaskLaunch", MappingKind.Bool),
		new ("HardwareAccelerated", "hardwareAccelerated", MappingKind.Bool),
		new ("NoHistory", "noHistory", MappingKind.Bool),
		new ("MultiProcess", "multiprocess", MappingKind.Bool),
		new ("StateNotNeeded", "stateNotNeeded", MappingKind.Bool),
		new ("Immersive", "immersive", MappingKind.Bool),
		new ("ResizeableActivity", "resizeableActivity", MappingKind.Bool),
		new ("SupportsPictureInPicture", "supportsPictureInPicture", MappingKind.Bool),
		new ("ShowForAllUsers", "showForAllUsers", MappingKind.Bool),
		new ("TurnScreenOn", "turnScreenOn", MappingKind.Bool),
		new ("LaunchMode", "launchMode", MappingKind.Enum, LaunchModeToString),
		new ("ScreenOrientation", "screenOrientation", MappingKind.Enum, ScreenOrientationToString),
		new ("ConfigurationChanges", "configChanges", MappingKind.Enum, ConfigChangesToString),
		new ("WindowSoftInputMode", "windowSoftInputMode", MappingKind.Enum, SoftInputToString),
		new ("DocumentLaunchMode", "documentLaunchMode", MappingKind.Enum, DocumentLaunchModeToString),
		new ("UiOptions", "uiOptions", MappingKind.Enum, UiOptionsToString),
		new ("PersistableMode", "persistableMode", MappingKind.Enum, ActivityPersistableModeToString),
	];

	static readonly PropertyMapping[] ServiceMappings = [
		new ("IsolatedProcess", "isolatedProcess", MappingKind.Bool),
		new ("ForegroundServiceType", "foregroundServiceType", MappingKind.Enum, ForegroundServiceTypeToString),
	];

	static readonly PropertyMapping[] ContentProviderMappings = [
		new ("Authorities", "authorities"),
		new ("GrantUriPermissions", "grantUriPermissions", MappingKind.Bool),
		new ("Syncable", "syncable", MappingKind.Bool),
		new ("MultiProcess", "multiprocess", MappingKind.Bool),
	];

	static readonly PropertyMapping[] ApplicationElementMappings = [
		new ("Label", "label"),
		new ("Icon", "icon"),
		new ("RoundIcon", "roundIcon"),
		new ("Theme", "theme"),
		new ("AllowBackup", "allowBackup", MappingKind.Bool),
		new ("SupportsRtl", "supportsRtl", MappingKind.Bool),
		new ("HardwareAccelerated", "hardwareAccelerated", MappingKind.Bool),
		new ("LargeHeap", "largeHeap", MappingKind.Bool),
		new ("Debuggable", "debuggable", MappingKind.Bool),
		new ("UsesCleartextTraffic", "usesCleartextTraffic", MappingKind.Bool),
	];

	static readonly PropertyMapping[] InstrumentationMappings = [
		new ("Label", "label"),
		new ("Icon", "icon"),
		new ("TargetPackage", "targetPackage"),
		new ("FunctionalTest", "functionalTest", MappingKind.Bool),
		new ("HandleProfiling", "handleProfiling", MappingKind.Bool),
	];

	static readonly PropertyMapping[] ApplicationPropertyMappings = [
		new ("Label", "label"),
		new ("Icon", "icon"),
		new ("RoundIcon", "roundIcon"),
		new ("Theme", "theme"),
		new ("NetworkSecurityConfig", "networkSecurityConfig"),
		new ("Description", "description"),
		new ("Logo", "logo"),
		new ("Permission", "permission"),
		new ("Process", "process"),
		new ("TaskAffinity", "taskAffinity"),
		new ("AllowBackup", "allowBackup", MappingKind.Bool),
		new ("SupportsRtl", "supportsRtl", MappingKind.Bool),
		new ("HardwareAccelerated", "hardwareAccelerated", MappingKind.Bool),
		new ("LargeHeap", "largeHeap", MappingKind.Bool),
		new ("Debuggable", "debuggable", MappingKind.Bool),
		new ("UsesCleartextTraffic", "usesCleartextTraffic", MappingKind.Bool),
		new ("RestoreAnyVersion", "restoreAnyVersion", MappingKind.Bool),
	];

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
	/// Generates the merged manifest and writes it to <paramref name="outputPath"/>.
	/// Returns the list of additional content provider names (for ApplicationRegistration.java).
	/// </summary>
	public IList<string> Generate (
		string? manifestTemplatePath,
		IReadOnlyList<JavaPeerInfo> allPeers,
		AssemblyManifestInfo assemblyInfo,
		string outputPath)
	{
		var doc = LoadOrCreateManifest (manifestTemplatePath);
		var manifest = doc.Root;
		if (manifest is null) {
			throw new InvalidOperationException ("Manifest document has no root element.");
		}

		EnsureManifestAttributes (manifest);
		var app = EnsureApplicationElement (manifest);

		// Apply assembly-level [Application] properties
		if (assemblyInfo.ApplicationProperties is not null) {
			ApplyApplicationProperties (app, assemblyInfo.ApplicationProperties);
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
				UpdateApplicationElement (app, peer);
				continue;
			}

			if (peer.ComponentAttribute.Kind == ComponentKind.Instrumentation) {
				AddInstrumentation (manifest, peer);
				continue;
			}

			string jniName = peer.JavaName.Replace ('/', '.');
			if (existingTypes.Contains (jniName)) {
				continue;
			}

			var element = CreateComponentElement (peer, jniName);
			if (element is not null) {
				app.Add (element);
			}
		}

		// Add assembly-level manifest elements
		AddAssemblyLevelElements (manifest, app, assemblyInfo);

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
			AddInternetPermission (manifest);
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

	XDocument LoadOrCreateManifest (string? templatePath)
	{
		if (!string.IsNullOrEmpty (templatePath) && File.Exists (templatePath)) {
			return XDocument.Load (templatePath);
		}

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

	XElement? CreateComponentElement (JavaPeerInfo peer, string jniName)
	{
		var component = peer.ComponentAttribute;
		if (component is null) {
			return null;
		}

		string elementName = component.Kind switch {
			ComponentKind.Activity => "activity",
			ComponentKind.Service => "service",
			ComponentKind.BroadcastReceiver => "receiver",
			ComponentKind.ContentProvider => "provider",
			_ => throw new NotSupportedException ($"Unsupported component kind: {component.Kind}"),
		};

		var element = new XElement (elementName, new XAttribute (AttName, jniName));

		// Map known properties to android: attributes
		MapComponentProperties (element, component);

		// Add intent filters
		foreach (var intentFilter in component.IntentFilters) {
			element.Add (CreateIntentFilterElement (intentFilter));
		}

		// Handle MainLauncher for activities
		if (component.Kind == ComponentKind.Activity && component.Properties.TryGetValue ("MainLauncher", out var ml) && ml is bool b && b) {
			AddLauncherIntentFilter (element);
		}

		// Add metadata
		foreach (var meta in component.MetaData) {
			element.Add (CreateMetaDataElement (meta));
		}

		return element;
	}

	void MapComponentProperties (XElement element, ComponentInfo component)
	{
		ApplyMappings (element, component.Properties, CommonMappings);

		var extra = component.Kind switch {
			ComponentKind.Activity => ActivityMappings,
			ComponentKind.Service => ServiceMappings,
			ComponentKind.ContentProvider => ContentProviderMappings,
			_ => null,
		};
		if (extra is not null) {
			ApplyMappings (element, component.Properties, extra);
		}

		// Handle InitOrder for ContentProvider (int, not a standard mapping)
		if (component.Kind == ComponentKind.ContentProvider && component.Properties.TryGetValue ("InitOrder", out var initOrder) && initOrder is int order) {
			element.SetAttributeValue (AndroidNs + "initOrder", order.ToString (CultureInfo.InvariantCulture));
		}
	}

	static void ApplyMappings (XElement element, IReadOnlyDictionary<string, object?> properties, PropertyMapping[] mappings, bool skipExisting = false)
	{
		foreach (var m in mappings) {
			if (!properties.TryGetValue (m.PropertyName, out var value) || value is null) {
				continue;
			}
			if (skipExisting && element.Attribute (AndroidNs + m.XmlAttributeName) is not null) {
				continue;
			}
			switch (m.Kind) {
			case MappingKind.String when value is string s && !string.IsNullOrEmpty (s):
				element.SetAttributeValue (AndroidNs + m.XmlAttributeName, s);
				break;
			case MappingKind.Bool when value is bool b:
				element.SetAttributeValue (AndroidNs + m.XmlAttributeName, b ? "true" : "false");
				break;
			case MappingKind.Enum when m.EnumConverter is not null:
				int intValue = value switch { int i => i, long l => (int)l, short s => s, byte b => b, _ => 0 };
				if (intValue != 0) {
					var strValue = m.EnumConverter (intValue);
					if (strValue is not null) {
						element.SetAttributeValue (AndroidNs + m.XmlAttributeName, strValue);
					}
				}
				break;
			}
		}
	}

	void AddLauncherIntentFilter (XElement activity)
	{
		// Check if there's already a launcher intent filter
		if (activity.Elements ("intent-filter").Any (f =>
			f.Elements ("action").Any (a => (string?)a.Attribute (AttName) == "android.intent.action.MAIN") &&
			f.Elements ("category").Any (c => (string?)c.Attribute (AttName) == "android.intent.category.LAUNCHER"))) {
			return;
		}

		// Add android:exported="true" if not already present
		if (activity.Attribute (AndroidNs + "exported") is null) {
			activity.Add (new XAttribute (AndroidNs + "exported", "true"));
		}

		var filter = new XElement ("intent-filter",
			new XElement ("action", new XAttribute (AttName, "android.intent.action.MAIN")),
			new XElement ("category", new XAttribute (AttName, "android.intent.category.LAUNCHER")));
		activity.AddFirst (filter);
	}

	static XElement CreateIntentFilterElement (IntentFilterInfo intentFilter)
	{
		var filter = new XElement ("intent-filter");

		foreach (var action in intentFilter.Actions) {
			filter.Add (new XElement ("action", new XAttribute (AttName, action)));
		}

		foreach (var category in intentFilter.Categories) {
			filter.Add (new XElement ("category", new XAttribute (AttName, category)));
		}

		// Map IntentFilter properties to XML attributes
		if (intentFilter.Properties.TryGetValue ("Label", out var label) && label is string labelStr) {
			filter.SetAttributeValue (AndroidNs + "label", labelStr);
		}
		if (intentFilter.Properties.TryGetValue ("Icon", out var icon) && icon is string iconStr) {
			filter.SetAttributeValue (AndroidNs + "icon", iconStr);
		}
		if (intentFilter.Properties.TryGetValue ("Priority", out var priority) && priority is int priorityInt) {
			filter.SetAttributeValue (AndroidNs + "priority", priorityInt.ToString (CultureInfo.InvariantCulture));
		}

		// Data elements
		AddIntentFilterDataElement (filter, intentFilter);

		return filter;
	}

	static void AddIntentFilterDataElement (XElement filter, IntentFilterInfo intentFilter)
	{
		var dataElement = new XElement ("data");
		bool hasData = false;

		if (intentFilter.Properties.TryGetValue ("DataScheme", out var scheme) && scheme is string schemeStr) {
			dataElement.SetAttributeValue (AndroidNs + "scheme", schemeStr);
			hasData = true;
		}
		if (intentFilter.Properties.TryGetValue ("DataHost", out var host) && host is string hostStr) {
			dataElement.SetAttributeValue (AndroidNs + "host", hostStr);
			hasData = true;
		}
		if (intentFilter.Properties.TryGetValue ("DataPath", out var path) && path is string pathStr) {
			dataElement.SetAttributeValue (AndroidNs + "path", pathStr);
			hasData = true;
		}
		if (intentFilter.Properties.TryGetValue ("DataPathPattern", out var pattern) && pattern is string patternStr) {
			dataElement.SetAttributeValue (AndroidNs + "pathPattern", patternStr);
			hasData = true;
		}
		if (intentFilter.Properties.TryGetValue ("DataPathPrefix", out var prefix) && prefix is string prefixStr) {
			dataElement.SetAttributeValue (AndroidNs + "pathPrefix", prefixStr);
			hasData = true;
		}
		if (intentFilter.Properties.TryGetValue ("DataMimeType", out var mime) && mime is string mimeStr) {
			dataElement.SetAttributeValue (AndroidNs + "mimeType", mimeStr);
			hasData = true;
		}
		if (intentFilter.Properties.TryGetValue ("DataPort", out var port) && port is string portStr) {
			dataElement.SetAttributeValue (AndroidNs + "port", portStr);
			hasData = true;
		}

		if (hasData) {
			filter.Add (dataElement);
		}
	}

	static XElement CreateMetaDataElement (MetaDataInfo meta)
	{
		var element = new XElement ("meta-data",
			new XAttribute (AndroidNs + "name", meta.Name));

		if (meta.Value is not null) {
			element.SetAttributeValue (AndroidNs + "value", meta.Value);
		}
		if (meta.Resource is not null) {
			element.SetAttributeValue (AndroidNs + "resource", meta.Resource);
		}
		return element;
	}

	void UpdateApplicationElement (XElement app, JavaPeerInfo peer)
	{
		string jniName = peer.JavaName.Replace ('/', '.');
		app.SetAttributeValue (AttName, jniName);

		var component = peer.ComponentAttribute;
		if (component is null) {
			return;
		}
		ApplyMappings (app, component.Properties, ApplicationElementMappings);
	}

	void AddInstrumentation (XElement manifest, JavaPeerInfo peer)
	{
		string jniName = peer.JavaName.Replace ('/', '.');
		var element = new XElement ("instrumentation",
			new XAttribute (AttName, jniName));

		var component = peer.ComponentAttribute;
		if (component is null) {
			return;
		}
		ApplyMappings (element, component.Properties, InstrumentationMappings);

		manifest.Add (element);
	}

	IList<string> AddRuntimeProviders (XElement app)
	{
		string packageName = "mono";
		string className = "MonoRuntimeProvider";

		if (string.Equals (AndroidRuntime, "nativeaot", StringComparison.OrdinalIgnoreCase)) {
			packageName = "net.dot.jni.nativeaot";
			className = "NativeAotRuntimeProvider";
		}

		app.Add (CreateRuntimeProvider ($"{packageName}.{className}", null, --appInitOrder));

		var providerNames = new List<string> ();
		var processAttrName = AndroidNs.GetName ("process");
		var procs = new List<string> ();

		foreach (var el in app.Elements ()) {
			var proc = el.Attribute (processAttrName);
			if (proc is null || procs.Contains (proc.Value)) {
				continue;
			}
			procs.Add (proc.Value);
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

	void AddAssemblyLevelElements (XElement manifest, XElement app, AssemblyManifestInfo info)
	{
		var existingPermissions = new HashSet<string> (
			manifest.Elements ("permission").Select (e => (string?)e.Attribute (AttName)).OfType<string> ());
		var existingUsesPermissions = new HashSet<string> (
			manifest.Elements ("uses-permission").Select (e => (string?)e.Attribute (AttName)).OfType<string> ());

		// <permission> elements
		foreach (var perm in info.Permissions) {
			if (string.IsNullOrEmpty (perm.Name) || existingPermissions.Contains (perm.Name)) {
				continue;
			}
			var element = new XElement ("permission", new XAttribute (AttName, perm.Name));
			MapDictionaryProperties (element, perm.Properties, "Label", "label");
			MapDictionaryProperties (element, perm.Properties, "Description", "description");
			MapDictionaryProperties (element, perm.Properties, "Icon", "icon");
			MapDictionaryProperties (element, perm.Properties, "PermissionGroup", "permissionGroup");
			MapDictionaryEnumProperty (element, perm.Properties, "ProtectionLevel", "protectionLevel", ProtectionToString);
			manifest.Add (element);
		}

		// <permission-group> elements
		foreach (var pg in info.PermissionGroups) {
			if (string.IsNullOrEmpty (pg.Name)) {
				continue;
			}
			var element = new XElement ("permission-group", new XAttribute (AttName, pg.Name));
			MapDictionaryProperties (element, pg.Properties, "Label", "label");
			MapDictionaryProperties (element, pg.Properties, "Description", "description");
			MapDictionaryProperties (element, pg.Properties, "Icon", "icon");
			manifest.Add (element);
		}

		// <permission-tree> elements
		foreach (var pt in info.PermissionTrees) {
			if (string.IsNullOrEmpty (pt.Name)) {
				continue;
			}
			var element = new XElement ("permission-tree", new XAttribute (AttName, pt.Name));
			MapDictionaryProperties (element, pt.Properties, "Label", "label");
			MapDictionaryProperties (element, pt.Properties, "Icon", "icon");
			manifest.Add (element);
		}

		// <uses-permission> elements
		foreach (var up in info.UsesPermissions) {
			if (string.IsNullOrEmpty (up.Name) || existingUsesPermissions.Contains (up.Name)) {
				continue;
			}
			var element = new XElement ("uses-permission", new XAttribute (AttName, up.Name));
			if (up.MaxSdkVersion.HasValue) {
				element.SetAttributeValue (AndroidNs + "maxSdkVersion", up.MaxSdkVersion.Value.ToString (CultureInfo.InvariantCulture));
			}
			manifest.Add (element);
		}

		// <uses-feature> elements
		var existingFeatures = new HashSet<string> (
			manifest.Elements ("uses-feature").Select (e => (string?)e.Attribute (AttName)).OfType<string> ());
		foreach (var uf in info.UsesFeatures) {
			if (uf.Name is not null && !existingFeatures.Contains (uf.Name)) {
				var element = new XElement ("uses-feature",
					new XAttribute (AttName, uf.Name),
					new XAttribute (AndroidNs + "required", uf.Required ? "true" : "false"));
				manifest.Add (element);
			} else if (uf.GLESVersion != 0) {
				var versionStr = $"0x{uf.GLESVersion:X8}";
				if (!manifest.Elements ("uses-feature").Any (e => (string?)e.Attribute (AndroidNs + "glEsVersion") == versionStr)) {
					var element = new XElement ("uses-feature",
						new XAttribute (AndroidNs + "glEsVersion", versionStr),
						new XAttribute (AndroidNs + "required", uf.Required ? "true" : "false"));
					manifest.Add (element);
				}
			}
		}

		// <uses-library> elements inside <application>
		foreach (var ul in info.UsesLibraries) {
			if (string.IsNullOrEmpty (ul.Name)) {
				continue;
			}
			if (!app.Elements ("uses-library").Any (e => (string?)e.Attribute (AttName) == ul.Name)) {
				app.Add (new XElement ("uses-library",
					new XAttribute (AttName, ul.Name),
					new XAttribute (AndroidNs + "required", ul.Required ? "true" : "false")));
			}
		}

		// Assembly-level <meta-data> inside <application>
		foreach (var md in info.MetaData) {
			if (string.IsNullOrEmpty (md.Name)) {
				continue;
			}
			if (!app.Elements ("meta-data").Any (e => (string?)e.Attribute (AndroidNs + "name") == md.Name)) {
				app.Add (CreateMetaDataElement (md));
			}
		}

		// Assembly-level <property> inside <application>
		foreach (var prop in info.Properties) {
			if (string.IsNullOrEmpty (prop.Name)) {
				continue;
			}
			if (!app.Elements ("property").Any (e => (string?)e.Attribute (AndroidNs + "name") == prop.Name)) {
				var element = new XElement ("property",
					new XAttribute (AndroidNs + "name", prop.Name));
				if (prop.Value is not null) {
					element.SetAttributeValue (AndroidNs + "value", prop.Value);
				}
				if (prop.Resource is not null) {
					element.SetAttributeValue (AndroidNs + "resource", prop.Resource);
				}
				app.Add (element);
			}
		}

		// <uses-configuration> elements
		foreach (var uc in info.UsesConfigurations) {
			var element = new XElement ("uses-configuration");
			if (uc.ReqFiveWayNav) {
				element.SetAttributeValue (AndroidNs + "reqFiveWayNav", "true");
			}
			if (uc.ReqHardKeyboard) {
				element.SetAttributeValue (AndroidNs + "reqHardKeyboard", "true");
			}
			if (uc.ReqKeyboardType is not null) {
				element.SetAttributeValue (AndroidNs + "reqKeyboardType", uc.ReqKeyboardType);
			}
			if (uc.ReqNavigation is not null) {
				element.SetAttributeValue (AndroidNs + "reqNavigation", uc.ReqNavigation);
			}
			if (uc.ReqTouchScreen is not null) {
				element.SetAttributeValue (AndroidNs + "reqTouchScreen", uc.ReqTouchScreen);
			}
			manifest.Add (element);
		}
	}

	static void ApplyApplicationProperties (XElement app, Dictionary<string, object?> properties)
	{
		ApplyMappings (app, properties, ApplicationPropertyMappings, skipExisting: true);
	}

	static void MapDictionaryProperties (XElement element, IReadOnlyDictionary<string, object?> props, string propertyName, string xmlAttrName)
	{
		if (props.TryGetValue (propertyName, out var value) && value is string s && !string.IsNullOrEmpty (s)) {
			element.SetAttributeValue (AndroidNs + xmlAttrName, s);
		}
	}

	static void MapDictionaryEnumProperty (XElement element, IReadOnlyDictionary<string, object?> props, string propertyName, string xmlAttrName, Func<int, string?> converter)
	{
		if (!props.TryGetValue (propertyName, out var value)) {
			return;
		}
		int intValue = value switch {
			int i => i,
			long l => (int)l,
			short s => s,
			byte b => b,
			_ => 0,
		};
		if (intValue != 0) {
			var strValue = converter (intValue);
			if (strValue is not null) {
				element.SetAttributeValue (AndroidNs + xmlAttrName, strValue);
			}
		}
	}

	static void AddInternetPermission (XElement manifest)
	{
		var androidNs = AndroidNs;
		if (!manifest.Elements ("uses-permission").Any (p =>
			(string?)p.Attribute (androidNs + "name") == "android.permission.INTERNET")) {
			manifest.Add (new XElement ("uses-permission",
				new XAttribute (androidNs + "name", "android.permission.INTERNET")));
		}
	}

	// Enum to string converters — ported from ManifestDocumentElement.cs
	// These match the android: XML attribute string values

	static string? LaunchModeToString (int value) => value switch {
		1 => "singleTop",
		2 => "singleTask",
		3 => "singleInstance",
		4 => "singleInstancePerTask",
		_ => null,
	};

	static string? ScreenOrientationToString (int value) => value switch {
		0 => "landscape",
		1 => "portrait",
		3 => "sensor",
		4 => "nosensor",
		5 => "user",
		6 => "behind",
		7 => "reverseLandscape",
		8 => "reversePortrait",
		9 => "sensorLandscape",
		10 => "sensorPortrait",
		11 => "fullSensor",
		12 => "userLandscape",
		13 => "userPortrait",
		14 => "fullUser",
		15 => "locked",
		-1 => "unspecified",
		_ => null,
	};

	static string? ConfigChangesToString (int value)
	{
		var parts = new List<string> ();
		if ((value & 0x0001) != 0) parts.Add ("mcc");
		if ((value & 0x0002) != 0) parts.Add ("mnc");
		if ((value & 0x0004) != 0) parts.Add ("locale");
		if ((value & 0x0008) != 0) parts.Add ("touchscreen");
		if ((value & 0x0010) != 0) parts.Add ("keyboard");
		if ((value & 0x0020) != 0) parts.Add ("keyboardHidden");
		if ((value & 0x0040) != 0) parts.Add ("navigation");
		if ((value & 0x0080) != 0) parts.Add ("orientation");
		if ((value & 0x0100) != 0) parts.Add ("screenLayout");
		if ((value & 0x0200) != 0) parts.Add ("uiMode");
		if ((value & 0x0400) != 0) parts.Add ("screenSize");
		if ((value & 0x0800) != 0) parts.Add ("smallestScreenSize");
		if ((value & 0x1000) != 0) parts.Add ("density");
		if ((value & 0x2000) != 0) parts.Add ("layoutDirection");
		if ((value & 0x4000) != 0) parts.Add ("colorMode");
		if ((value & 0x8000) != 0) parts.Add ("grammaticalGender");
		if ((value & 0x10000000) != 0) parts.Add ("fontWeightAdjustment");
		if ((value & 0x40000000) != 0) parts.Add ("fontScale");
		return parts.Count > 0 ? string.Join ("|", parts) : null;
	}

	static string? SoftInputToString (int value)
	{
		var parts = new List<string> ();
		int state = value & 0x0f;
		int adjust = value & 0xf0;
		if (state == 1) parts.Add ("stateUnchanged");
		else if (state == 2) parts.Add ("stateHidden");
		else if (state == 3) parts.Add ("stateAlwaysHidden");
		else if (state == 4) parts.Add ("stateVisible");
		else if (state == 5) parts.Add ("stateAlwaysVisible");
		if (adjust == 0x10) parts.Add ("adjustResize");
		else if (adjust == 0x20) parts.Add ("adjustPan");
		else if (adjust == 0x30) parts.Add ("adjustNothing");
		return parts.Count > 0 ? string.Join ("|", parts) : null;
	}

	static string? DocumentLaunchModeToString (int value) => value switch {
		1 => "intoExisting",
		2 => "always",
		3 => "never",
		_ => null,
	};

	static string? UiOptionsToString (int value) => value switch {
		1 => "splitActionBarWhenNarrow",
		_ => null,
	};

	static string? ForegroundServiceTypeToString (int value)
	{
		var parts = new List<string> ();
		if ((value & 0x00000001) != 0) parts.Add ("dataSync");
		if ((value & 0x00000002) != 0) parts.Add ("mediaPlayback");
		if ((value & 0x00000004) != 0) parts.Add ("phoneCall");
		if ((value & 0x00000008) != 0) parts.Add ("location");
		if ((value & 0x00000010) != 0) parts.Add ("connectedDevice");
		if ((value & 0x00000020) != 0) parts.Add ("mediaProjection");
		if ((value & 0x00000040) != 0) parts.Add ("camera");
		if ((value & 0x00000080) != 0) parts.Add ("microphone");
		if ((value & 0x00000100) != 0) parts.Add ("health");
		if ((value & 0x00000200) != 0) parts.Add ("remoteMessaging");
		if ((value & 0x00000400) != 0) parts.Add ("systemExempted");
		if ((value & 0x00000800) != 0) parts.Add ("shortService");
		if ((value & 0x40000000) != 0) parts.Add ("specialUse");
		return parts.Count > 0 ? string.Join ("|", parts) : null;
	}

	static string? ProtectionToString (int value)
	{
		int baseValue = value & 0x0f;
		return baseValue switch {
			0 => "normal",
			1 => "dangerous",
			2 => "signature",
			3 => "signatureOrSystem",
			_ => null,
		};
	}

	static string? ActivityPersistableModeToString (int value) => value switch {
		0 => "persistRootOnly",
		1 => "persistAcrossReboots",
		2 => "persistNever",
		_ => null,
	};

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
