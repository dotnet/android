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

		return element;
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
		AddIntentFilterDataElements (filter, intentFilter);

		return filter;
	}

	// Each data attribute produces its own <data> element, matching the legacy
	// IntentFilterAttribute.GetData () behavior. Singular properties are emitted first,
	// in this order, followed by the plural (array) properties.
	static readonly (string Property, string Attribute) [] SingularDataMappings = [
		("DataHost", "host"),
		("DataMimeType", "mimeType"),
		("DataPath", "path"),
		("DataPathPattern", "pathPattern"),
		("DataPathPrefix", "pathPrefix"),
		("DataPort", "port"),
		("DataScheme", "scheme"),
		("DataPathSuffix", "pathSuffix"),
		("DataPathAdvancedPattern", "pathAdvancedPattern"),
	];

	static readonly (string Property, string Attribute) [] PluralDataMappings = [
		("DataHosts", "host"),
		("DataMimeTypes", "mimeType"),
		("DataPaths", "path"),
		("DataPathPatterns", "pathPattern"),
		("DataPathPrefixes", "pathPrefix"),
		("DataPorts", "port"),
		("DataSchemes", "scheme"),
		("DataPathSuffixes", "pathSuffix"),
		("DataPathAdvancedPatterns", "pathAdvancedPattern"),
	];

	internal static void AddIntentFilterDataElements (XElement filter, IntentFilterInfo intentFilter)
	{
		foreach (var (property, attribute) in SingularDataMappings) {
			if (intentFilter.Properties.TryGetValue (property, out var value) && value is string s && !string.IsNullOrEmpty (s)) {
				filter.Add (new XElement ("data", new XAttribute (AndroidNs + attribute, s)));
			}
		}

		foreach (var (property, attribute) in PluralDataMappings) {
			if (!intentFilter.Properties.TryGetValue (property, out var value) || value is not IReadOnlyList<string> values) {
				continue;
			}

			foreach (var item in values) {
				if (!string.IsNullOrEmpty (item)) {
					filter.Add (new XElement ("data", new XAttribute (AndroidNs + attribute, item)));
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
