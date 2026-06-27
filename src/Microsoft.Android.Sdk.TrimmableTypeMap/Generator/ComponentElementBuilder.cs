using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

/// <summary>
/// Builds XML elements for individual Android components (Activity, Service, BroadcastReceiver, ContentProvider).
/// </summary>
static class ComponentElementBuilder
{
	static readonly XNamespace AndroidNs = ManifestConstants.AndroidNs;
	static readonly XName AttName = ManifestConstants.AttName;

	internal static XElement? CreateComponentElement (JavaPeerInfo peer, string jniName, int targetSdkVersion = 0, IReadOnlyDictionary<string, string>? managedToManifestNames = null)
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
		PropertyMapper.MapComponentProperties (element, component, targetSdkVersion);

		// android:parentActivityName comes from a [Activity (ParentActivity = typeof (...))] and is
		// captured as the managed type name. Resolve it to the parent's Java/manifest name, matching
		// the legacy ManifestDocument behavior (JavaNativeTypeManager.ToJniName).
		ResolveParentActivityName (element, managedToManifestNames);

		// Add intent filters
		foreach (var intentFilter in component.IntentFilters) {
			element.Add (CreateIntentFilterElement (intentFilter));
		}

		// Add <layout> element from a [Layout] attribute, if present
		if (component.LayoutProperties is not null) {
			var layout = CreateLayoutElement (component.LayoutProperties);
			if (layout is not null) {
				element.Add (layout);
			}
		}

		// Handle MainLauncher for activities
		if (component.Kind == ComponentKind.Activity && component.Properties.TryGetValue ("MainLauncher", out var ml) && ml is bool b && b) {
			AddLauncherIntentFilter (element);
		}

		// Add metadata
		foreach (var meta in component.MetaData) {
			element.Add (CreateMetaDataElement (meta));
		}

		// The legacy ManifestDocumentElement.ToElement sorts attributes alphabetically
		// (specified.OrderBy (e => e)). Match that ordering so the generated manifest is
		// byte-compatible with the legacy path when AndroidManifestMerger='legacy' (the
		// manifestmerger.jar path re-sorts attributes itself, so this is also safe there).
		SortAttributesAlphabetically (element);

		return element;
	}

	// Reorders an element's attributes alphabetically by local name (case-insensitive),
	// matching the legacy manifest generator's attribute ordering.
	static void SortAttributesAlphabetically (XElement element)
	{
		var sorted = element.Attributes ()
			.OrderBy (a => a.Name.LocalName, StringComparer.OrdinalIgnoreCase)
			.ToList ();
		if (sorted.Count < 2) {
			return;
		}
		foreach (var attr in element.Attributes ().ToList ()) {
			attr.Remove ();
		}
		foreach (var attr in sorted) {
			element.Add (attr);
		}
	}

	static void ResolveParentActivityName (XElement element, IReadOnlyDictionary<string, string>? managedToManifestNames)
	{
		if (managedToManifestNames is null) {
			return;
		}

		var attr = element.Attribute (AndroidNs + "parentActivityName");
		if (attr is null) {
			return;
		}

		// The value may be assembly-qualified ("Foo.Bar, Asm [Version=...]"); use the type name part.
		var value = attr.Value;
		int comma = value.IndexOf (',');
		var typeName = (comma < 0 ? value : value.Substring (0, comma)).Trim ();

		if (managedToManifestNames.TryGetValue (typeName, out var manifestName)) {
			attr.Value = manifestName;
		}
	}

	internal static void AddLauncherIntentFilter (XElement activity)
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

	internal static XElement CreateIntentFilterElement (IntentFilterInfo intentFilter)
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
		AddIntentFilterDataElements (filter, intentFilter);

		return filter;
	}

	// Ordered to match the legacy IntentFilterAttribute.GetData emission order (singular block first,
	// then plural block), so trimmable manifest generation stays byte-for-byte compatible with ManifestDocument.
	static readonly (string SingularProperty, string PluralProperty, string AttributeName) [] IntentFilterDataProperties = [
		("DataHost",                "DataHosts",                "host"),
		("DataMimeType",            "DataMimeTypes",            "mimeType"),
		("DataPath",                "DataPaths",                "path"),
		("DataPathPattern",         "DataPathPatterns",         "pathPattern"),
		("DataPathPrefix",          "DataPathPrefixes",         "pathPrefix"),
		("DataPort",                "DataPorts",                "port"),
		("DataScheme",              "DataSchemes",              "scheme"),
		("DataPathSuffix",          "DataPathSuffixes",         "pathSuffix"),
		("DataPathAdvancedPattern", "DataPathAdvancedPatterns", "pathAdvancedPattern"),
	];

	internal static void AddIntentFilterDataElement (XElement filter, IntentFilterInfo intentFilter)
	{
		foreach (var (propertyName, _, attributeName) in IntentFilterDataProperties) {
			if (intentFilter.Properties.TryGetValue (propertyName, out var value) && value is string item && !string.IsNullOrEmpty (item)) {
				filter.Add (new XElement ("data", new XAttribute (AndroidNs + attributeName, item)));
			}
		}
	}

	internal static void AddIntentFilterDataElements (XElement filter, IntentFilterInfo intentFilter)
	{
		foreach (var (_, propertyName, attributeName) in IntentFilterDataProperties) {
			if (!intentFilter.Properties.TryGetValue (propertyName, out var value) || value is not IReadOnlyList<string> values) {
				continue;
			}

			foreach (var item in values) {
				if (!string.IsNullOrEmpty (item)) {
					filter.Add (new XElement ("data", new XAttribute (AndroidNs + attributeName, item)));
				}
			}
		}
	}

	internal static XElement CreateMetaDataElement (MetaDataInfo meta)
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

	// Maps [Layout] attribute properties to the <layout> element's android: attributes.
	static readonly (string Property, string Attribute) [] LayoutMappings = [
		("DefaultHeight", "defaultHeight"),
		("DefaultWidth", "defaultWidth"),
		("Gravity", "gravity"),
		("MinHeight", "minHeight"),
		("MinWidth", "minWidth"),
	];

	internal static XElement? CreateLayoutElement (IReadOnlyDictionary<string, object?> layoutProperties)
	{
		var element = new XElement ("layout");
		bool hasAttribute = false;

		foreach (var (property, attribute) in LayoutMappings) {
			if (layoutProperties.TryGetValue (property, out var value) && value is string s && !string.IsNullOrEmpty (s)) {
				element.SetAttributeValue (AndroidNs + attribute, s);
				hasAttribute = true;
			}
		}

		return hasAttribute ? element : null;
	}

	internal static void UpdateApplicationElement (XElement app, JavaPeerInfo peer, int targetSdkVersion = 0)
	{
		string jniName = JniSignatureHelper.JniNameToJavaName (peer.JavaName);
		app.SetAttributeValue (AttName, jniName);

		var component = peer.ComponentAttribute;
		if (component is null) {
			return;
		}
		PropertyMapper.ApplyMappings (app, component.Properties, PropertyMapper.ApplicationElementMappings, targetSdkVersion: targetSdkVersion);
	}

	internal static void AddInstrumentation (XElement manifest, JavaPeerInfo peer, string packageName)
	{
		string jniName = JniSignatureHelper.JniNameToJavaName (peer.JavaName);
		var element = new XElement ("instrumentation",
			new XAttribute (AttName, jniName));

		var component = peer.ComponentAttribute;
		if (component is null) {
			return;
		}
		PropertyMapper.ApplyMappings (element, component.Properties, PropertyMapper.InstrumentationMappings);

		// Default targetPackage to the app package name, matching legacy ManifestDocument behavior
		if (element.Attribute (AndroidNs + "targetPackage") is null) {
			element.SetAttributeValue (AndroidNs + "targetPackage", packageName);
		}

		manifest.Add (element);
	}
}
