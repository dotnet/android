#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml.Linq;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

/// <summary>
/// Defines the property mapping infrastructure for converting <see cref="ComponentInfo"/>
/// properties to Android manifest XML attributes.
/// </summary>
static class PropertyMapper
{
	static readonly XNamespace AndroidNs = ManifestConstants.AndroidNs;

	internal enum MappingKind { String, Bool, Enum }

	internal readonly struct PropertyMapping
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

	internal static readonly PropertyMapping[] CommonMappings = [
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

	internal static readonly PropertyMapping[] ActivityMappings = [
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
		new ("LaunchMode", "launchMode", MappingKind.Enum, AndroidEnumConverter.LaunchModeToString),
		new ("ScreenOrientation", "screenOrientation", MappingKind.Enum, AndroidEnumConverter.ScreenOrientationToString),
		new ("ConfigurationChanges", "configChanges", MappingKind.Enum, AndroidEnumConverter.ConfigChangesToString),
		new ("WindowSoftInputMode", "windowSoftInputMode", MappingKind.Enum, AndroidEnumConverter.SoftInputToString),
		new ("DocumentLaunchMode", "documentLaunchMode", MappingKind.Enum, AndroidEnumConverter.DocumentLaunchModeToString),
		new ("UiOptions", "uiOptions", MappingKind.Enum, AndroidEnumConverter.UiOptionsToString),
		new ("PersistableMode", "persistableMode", MappingKind.Enum, AndroidEnumConverter.ActivityPersistableModeToString),
	];

	internal static readonly PropertyMapping[] ServiceMappings = [
		new ("IsolatedProcess", "isolatedProcess", MappingKind.Bool),
		new ("ForegroundServiceType", "foregroundServiceType", MappingKind.Enum, AndroidEnumConverter.ForegroundServiceTypeToString),
	];

	internal static readonly PropertyMapping[] ContentProviderMappings = [
		new ("Authorities", "authorities"),
		new ("GrantUriPermissions", "grantUriPermissions", MappingKind.Bool),
		new ("Syncable", "syncable", MappingKind.Bool),
		new ("MultiProcess", "multiprocess", MappingKind.Bool),
	];

	internal static readonly PropertyMapping[] ApplicationElementMappings = [
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

	internal static readonly PropertyMapping[] InstrumentationMappings = [
		new ("Label", "label"),
		new ("Icon", "icon"),
		new ("TargetPackage", "targetPackage"),
		new ("FunctionalTest", "functionalTest", MappingKind.Bool),
		new ("HandleProfiling", "handleProfiling", MappingKind.Bool),
	];

	internal static readonly PropertyMapping[] ApplicationPropertyMappings = [
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

	internal static void ApplyMappings (XElement element, IReadOnlyDictionary<string, object?> properties, PropertyMapping[] mappings, bool skipExisting = false)
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
				var strValue = m.EnumConverter (intValue);
				if (strValue is not null) {
					element.SetAttributeValue (AndroidNs + m.XmlAttributeName, strValue);
				}
				break;
			}
		}
	}

	internal static void MapComponentProperties (XElement element, ComponentInfo component)
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

	internal static void MapDictionaryProperties (XElement element, IReadOnlyDictionary<string, object?> props, string propertyName, string xmlAttrName)
	{
		if (props.TryGetValue (propertyName, out var value) && value is string s && !string.IsNullOrEmpty (s)) {
			element.SetAttributeValue (AndroidNs + xmlAttrName, s);
		}
	}

	internal static void MapDictionaryEnumProperty (XElement element, IReadOnlyDictionary<string, object?> props, string propertyName, string xmlAttrName, Func<int, string?> converter)
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
		var strValue = converter (intValue);
		if (strValue is not null) {
			element.SetAttributeValue (AndroidNs + xmlAttrName, strValue);
		}
	}
}
