using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

/// <summary>
/// Adds assembly-level manifest elements (permissions, uses-permissions, uses-features,
/// uses-library, uses-configuration, meta-data, property).
/// </summary>
static class AssemblyLevelElementBuilder
{
	static readonly XNamespace AndroidNs = ManifestConstants.AndroidNs;
	static readonly XName AttName = ManifestConstants.AttName;

	internal static void AddAssemblyLevelElements (XElement manifest, XElement app, AssemblyManifestInfo info)
	{
		var existingPermissions = new HashSet<string> (
			manifest.Elements ("permission").Select (e => (string?)e.Attribute (AttName)).OfType<string> ());
		var existingPermissionGroups = new HashSet<string> (
			manifest.Elements ("permission-group").Select (e => (string?)e.Attribute (AttName)).OfType<string> ());
		var existingPermissionTrees = new HashSet<string> (
			manifest.Elements ("permission-tree").Select (e => (string?)e.Attribute (AttName)).OfType<string> ());
		var existingUsesPermissions = new HashSet<string> (
			manifest.Elements ("uses-permission").Select (e => (string?)e.Attribute (AttName)).OfType<string> ());

		// <permission> elements
		foreach (var perm in info.Permissions) {
			if (string.IsNullOrEmpty (perm.Name) || !existingPermissions.Add (perm.Name)) {
				continue;
			}
			var element = new XElement ("permission", new XAttribute (AttName, perm.Name));
			PropertyMapper.MapDictionaryProperties (element, perm.Properties, "Label", "label");
			PropertyMapper.MapDictionaryProperties (element, perm.Properties, "Description", "description");
			PropertyMapper.MapDictionaryProperties (element, perm.Properties, "Icon", "icon");
			PropertyMapper.MapDictionaryProperties (element, perm.Properties, "RoundIcon", "roundIcon");
			PropertyMapper.MapDictionaryProperties (element, perm.Properties, "PermissionGroup", "permissionGroup");
			PropertyMapper.MapDictionaryEnumProperty (element, perm.Properties, "ProtectionLevel", "protectionLevel", AndroidEnumConverter.ProtectionToString);
			manifest.Add (element);
		}

		// <permission-group> elements
		foreach (var pg in info.PermissionGroups) {
			if (string.IsNullOrEmpty (pg.Name) || !existingPermissionGroups.Add (pg.Name)) {
				continue;
			}
			var element = new XElement ("permission-group", new XAttribute (AttName, pg.Name));
			PropertyMapper.MapDictionaryProperties (element, pg.Properties, "Label", "label");
			PropertyMapper.MapDictionaryProperties (element, pg.Properties, "Description", "description");
			PropertyMapper.MapDictionaryProperties (element, pg.Properties, "Icon", "icon");
			PropertyMapper.MapDictionaryProperties (element, pg.Properties, "RoundIcon", "roundIcon");
			manifest.Add (element);
		}

		// <permission-tree> elements
		foreach (var pt in info.PermissionTrees) {
			if (string.IsNullOrEmpty (pt.Name) || !existingPermissionTrees.Add (pt.Name)) {
				continue;
			}
			var element = new XElement ("permission-tree", new XAttribute (AttName, pt.Name));
			PropertyMapper.MapDictionaryProperties (element, pt.Properties, "Label", "label");
			PropertyMapper.MapDictionaryProperties (element, pt.Properties, "Icon", "icon");
			PropertyMapper.MapDictionaryProperties (element, pt.Properties, "RoundIcon", "roundIcon");
			manifest.Add (element);
		}

		// <uses-permission> elements
		foreach (var up in info.UsesPermissions) {
			if (string.IsNullOrEmpty (up.Name) || !existingUsesPermissions.Add (up.Name)) {
				continue;
			}
			var element = new XElement ("uses-permission", new XAttribute (AttName, up.Name));
			if (up.MaxSdkVersion.HasValue) {
				element.SetAttributeValue (AndroidNs + "maxSdkVersion", up.MaxSdkVersion.Value.ToString (CultureInfo.InvariantCulture));
			}
			if (!string.IsNullOrEmpty (up.UsesPermissionFlags)) {
				element.SetAttributeValue (AndroidNs + "usesPermissionFlags", up.UsesPermissionFlags);
			}
			manifest.Add (element);
		}

		// <uses-feature> elements
		var existingFeatures = new HashSet<string> (
			manifest.Elements ("uses-feature").Select (e => (string?)e.Attribute (AttName)).OfType<string> ());
		foreach (var uf in info.UsesFeatures) {
			if (uf.Name is not null && existingFeatures.Add (uf.Name)) {
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
				app.Add (ComponentElementBuilder.CreateMetaDataElement (md));
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

		// <supports-gl-texture> elements
		var existingGLTextures = new HashSet<string> (
			manifest.Elements ("supports-gl-texture").Select (e => (string?)e.Attribute (AttName)).OfType<string> ());
		foreach (var gl in info.SupportsGLTextures) {
			if (existingGLTextures.Add (gl.Name)) {
				manifest.Add (new XElement ("supports-gl-texture", new XAttribute (AttName, gl.Name)));
			}
		}
	}

	internal static void ApplyApplicationProperties (
		XElement app,
		Dictionary<string, object?> properties,
		IReadOnlyList<JavaPeerInfo> allPeers,
		Action<string>? warn = null)
	{
		PropertyMapper.ApplyMappings (app, properties, PropertyMapper.ApplicationPropertyMappings, skipExisting: true);

		// BackupAgent and ManageSpaceActivity are Type properties — resolve managed type names to JNI names
		ApplyTypeProperty (app, properties, allPeers, "BackupAgent", "backupAgent", warn);
		ApplyTypeProperty (app, properties, allPeers, "ManageSpaceActivity", "manageSpaceActivity", warn);
	}

	static void ApplyTypeProperty (
		XElement app,
		Dictionary<string, object?> properties,
		IReadOnlyList<JavaPeerInfo> allPeers,
		string propertyName,
		string xmlAttrName,
		Action<string>? warn)
	{
		if (app.Attribute (AndroidNs + xmlAttrName) is not null) {
			return;
		}
		if (!properties.TryGetValue (propertyName, out var value) || value is not string managedName || managedName.Length == 0) {
			return;
		}

		// Strip assembly qualification if present (e.g., "MyApp.MyAgent, MyAssembly")
		var commaIndex = managedName.IndexOf (',');
		if (commaIndex > 0) {
			managedName = managedName.Substring (0, commaIndex).Trim ();
		}

		foreach (var peer in allPeers) {
			if (peer.ManagedTypeName == managedName) {
				app.SetAttributeValue (AndroidNs + xmlAttrName, peer.JavaName.Replace ('/', '.'));
				return;
			}
		}

		warn?.Invoke ($"Could not resolve {propertyName} type '{managedName}' to a Java peer for android:{xmlAttrName}.");
	}

	internal static void AddInternetPermission (XElement manifest)
	{
		if (!manifest.Elements ("uses-permission").Any (p =>
			(string?)p.Attribute (AndroidNs + "name") == "android.permission.INTERNET")) {
			manifest.Add (new XElement ("uses-permission",
				new XAttribute (AndroidNs + "name", "android.permission.INTERNET")));
		}
	}
}
