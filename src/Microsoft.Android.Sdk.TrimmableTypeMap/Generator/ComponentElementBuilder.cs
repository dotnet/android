using System;
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

	internal static XElement? CreateComponentElement (JavaPeerInfo peer, string jniName)
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
		PropertyMapper.MapComponentProperties (element, component);

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

		return filter;
	}

	internal static void AddIntentFilterDataElement (XElement filter, IntentFilterInfo intentFilter)
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

	internal static void UpdateApplicationElement (XElement app, JavaPeerInfo peer)
	{
		string jniName = JniSignatureHelper.JniNameToJavaName (peer.JavaName);
		app.SetAttributeValue (AttName, jniName);

		var component = peer.ComponentAttribute;
		if (component is null) {
			return;
		}
		PropertyMapper.ApplyMappings (app, component.Properties, PropertyMapper.ApplicationElementMappings);
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
		if (element.Attribute (AndroidNs + "targetPackage") is null) {
			var manifestPackage = (string?) manifest.Attribute ("package");
			if (!manifestPackage.IsNullOrEmpty ()) {
				element.SetAttributeValue (AndroidNs + "targetPackage", manifestPackage);
			}
		}

		// Default targetPackage to the app package name, matching legacy ManifestDocument behavior
		if (element.Attribute (AndroidNs + "targetPackage") is null) {
			element.SetAttributeValue (AndroidNs + "targetPackage", packageName);
		}

		manifest.Add (element);
	}
}
