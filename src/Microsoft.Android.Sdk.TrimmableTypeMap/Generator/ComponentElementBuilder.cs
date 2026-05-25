using System;
using System.Globalization;
using System.Linq;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

/// <summary>
/// Builds XML elements for individual Android components (Activity, Service, BroadcastReceiver, ContentProvider).
/// </summary>
static class ComponentElementBuilder
{
	internal static ManifestElement? CreateComponentElement (JavaPeerInfo peer, string jniName)
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

		var element = new ManifestElement (elementName);
		element.SetAndroidAttribute (ManifestConstants.AttributeName, jniName);

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

	internal static void AddLauncherIntentFilter (ManifestElement activity)
	{
		// Check if there's already a launcher intent filter
		if (activity.Elements ("intent-filter").Any (f =>
			f.Elements ("action").Any (a => a.AndroidAttribute (ManifestConstants.AttributeName) == "android.intent.action.MAIN") &&
			f.Elements ("category").Any (c => c.AndroidAttribute (ManifestConstants.AttributeName) == "android.intent.category.LAUNCHER"))) {
			return;
		}

		// Add android:exported="true" if not already present
		if (!activity.HasAttribute (ManifestConstants.AndroidNamespace, "exported")) {
			activity.SetAndroidAttribute ("exported", "true");
		}

		var filter = new ManifestElement ("intent-filter");
		var action = new ManifestElement ("action");
		action.SetAndroidAttribute (ManifestConstants.AttributeName, "android.intent.action.MAIN");
		var category = new ManifestElement ("category");
		category.SetAndroidAttribute (ManifestConstants.AttributeName, "android.intent.category.LAUNCHER");
		filter.Add (action);
		filter.Add (category);
		activity.AddFirst (filter);
	}

	internal static ManifestElement CreateIntentFilterElement (IntentFilterInfo intentFilter)
	{
		var filter = new ManifestElement ("intent-filter");

		foreach (var action in intentFilter.Actions) {
			var element = new ManifestElement ("action");
			element.SetAndroidAttribute (ManifestConstants.AttributeName, action);
			filter.Add (element);
		}

		foreach (var category in intentFilter.Categories) {
			var element = new ManifestElement ("category");
			element.SetAndroidAttribute (ManifestConstants.AttributeName, category);
			filter.Add (element);
		}

		// Map IntentFilter properties to XML attributes
		if (intentFilter.Properties.TryGetValue ("Label", out var label) && label is string labelStr) {
			filter.SetAndroidAttribute ("label", labelStr);
		}
		if (intentFilter.Properties.TryGetValue ("Icon", out var icon) && icon is string iconStr) {
			filter.SetAndroidAttribute ("icon", iconStr);
		}
		if (intentFilter.Properties.TryGetValue ("Priority", out var priority) && priority is int priorityInt) {
			filter.SetAndroidAttribute ("priority", priorityInt.ToString (CultureInfo.InvariantCulture));
		}

		// Data elements
		AddIntentFilterDataElement (filter, intentFilter);

		return filter;
	}

	internal static void AddIntentFilterDataElement (ManifestElement filter, IntentFilterInfo intentFilter)
	{
		var dataElement = new ManifestElement ("data");
		bool hasData = false;

		if (intentFilter.Properties.TryGetValue ("DataScheme", out var scheme) && scheme is string schemeStr) {
			dataElement.SetAndroidAttribute ("scheme", schemeStr);
			hasData = true;
		}
		if (intentFilter.Properties.TryGetValue ("DataHost", out var host) && host is string hostStr) {
			dataElement.SetAndroidAttribute ("host", hostStr);
			hasData = true;
		}
		if (intentFilter.Properties.TryGetValue ("DataPath", out var path) && path is string pathStr) {
			dataElement.SetAndroidAttribute ("path", pathStr);
			hasData = true;
		}
		if (intentFilter.Properties.TryGetValue ("DataPathPattern", out var pattern) && pattern is string patternStr) {
			dataElement.SetAndroidAttribute ("pathPattern", patternStr);
			hasData = true;
		}
		if (intentFilter.Properties.TryGetValue ("DataPathPrefix", out var prefix) && prefix is string prefixStr) {
			dataElement.SetAndroidAttribute ("pathPrefix", prefixStr);
			hasData = true;
		}
		if (intentFilter.Properties.TryGetValue ("DataMimeType", out var mime) && mime is string mimeStr) {
			dataElement.SetAndroidAttribute ("mimeType", mimeStr);
			hasData = true;
		}
		if (intentFilter.Properties.TryGetValue ("DataPort", out var port) && port is string portStr) {
			dataElement.SetAndroidAttribute ("port", portStr);
			hasData = true;
		}

		if (hasData) {
			filter.Add (dataElement);
		}
	}

	internal static ManifestElement CreateMetaDataElement (MetaDataInfo meta)
	{
		var element = new ManifestElement ("meta-data");
		element.SetAndroidAttribute ("name", meta.Name);

		if (meta.Value is not null) {
			element.SetAndroidAttribute ("value", meta.Value);
		}
		if (meta.Resource is not null) {
			element.SetAndroidAttribute ("resource", meta.Resource);
		}
		return element;
	}

	internal static void UpdateApplicationElement (ManifestElement app, JavaPeerInfo peer)
	{
		string jniName = JniSignatureHelper.JniNameToJavaName (peer.JavaName);
		app.SetAndroidAttribute (ManifestConstants.AttributeName, jniName);

		var component = peer.ComponentAttribute;
		if (component is null) {
			return;
		}
		PropertyMapper.ApplyMappings (app, component.Properties, PropertyMapper.ApplicationElementMappings);
	}

	internal static void AddInstrumentation (ManifestElement manifest, JavaPeerInfo peer, string packageName)
	{
		string jniName = JniSignatureHelper.JniNameToJavaName (peer.JavaName);
		var element = new ManifestElement ("instrumentation");
		element.SetAndroidAttribute (ManifestConstants.AttributeName, jniName);

		var component = peer.ComponentAttribute;
		if (component is null) {
			return;
		}
		PropertyMapper.ApplyMappings (element, component.Properties, PropertyMapper.InstrumentationMappings);

		// Default targetPackage to the app package name, matching legacy ManifestDocument behavior
		if (!element.HasAttribute (ManifestConstants.AndroidNamespace, "targetPackage")) {
			element.SetAndroidAttribute ("targetPackage", packageName);
		}

		manifest.Add (element);
	}
}
