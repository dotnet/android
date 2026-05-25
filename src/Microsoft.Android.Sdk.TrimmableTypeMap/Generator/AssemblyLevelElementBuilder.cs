using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

/// <summary>
/// Adds assembly-level manifest elements (permissions, uses-permissions, uses-features,
/// uses-library, uses-configuration, meta-data, property).
/// </summary>
static class AssemblyLevelElementBuilder
{
	internal static void AddAssemblyLevelElements (ManifestElement manifest, ManifestElement app, AssemblyManifestInfo info)
	{
		var existingPermissions = new HashSet<string> (
			manifest.Elements ("permission").Select (e => e.AndroidAttribute (ManifestConstants.AttributeName)).OfType<string> ());
		var existingPermissionGroups = new HashSet<string> (
			manifest.Elements ("permission-group").Select (e => e.AndroidAttribute (ManifestConstants.AttributeName)).OfType<string> ());
		var existingPermissionTrees = new HashSet<string> (
			manifest.Elements ("permission-tree").Select (e => e.AndroidAttribute (ManifestConstants.AttributeName)).OfType<string> ());
		var existingUsesPermissions = new HashSet<string> (
			manifest.Elements ("uses-permission").Select (e => e.AndroidAttribute (ManifestConstants.AttributeName)).OfType<string> ());

		// <permission> elements
		foreach (var perm in info.Permissions) {
			if (string.IsNullOrEmpty (perm.Name) || !existingPermissions.Add (perm.Name)) {
				continue;
			}
			var element = new ManifestElement ("permission");
			element.SetAndroidAttribute (ManifestConstants.AttributeName, perm.Name);
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
			var element = new ManifestElement ("permission-group");
			element.SetAndroidAttribute (ManifestConstants.AttributeName, pg.Name);
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
			var element = new ManifestElement ("permission-tree");
			element.SetAndroidAttribute (ManifestConstants.AttributeName, pt.Name);
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
			var element = new ManifestElement ("uses-permission");
			element.SetAndroidAttribute (ManifestConstants.AttributeName, up.Name);
			if (up.MaxSdkVersion.HasValue) {
				element.SetAndroidAttribute ("maxSdkVersion", up.MaxSdkVersion.Value.ToString (CultureInfo.InvariantCulture));
			}
			var usesPermissionFlags = up.UsesPermissionFlags;
			if (!usesPermissionFlags.IsNullOrEmpty ()) {
				element.SetAndroidAttribute ("usesPermissionFlags", usesPermissionFlags);
			}
			manifest.Add (element);
		}

		// <uses-feature> elements
		var existingFeatures = new HashSet<string> (
			manifest.Elements ("uses-feature").Select (e => e.AndroidAttribute (ManifestConstants.AttributeName)).OfType<string> ());
		foreach (var uf in info.UsesFeatures) {
			if (uf.Name is not null && existingFeatures.Add (uf.Name)) {
				var element = new ManifestElement ("uses-feature");
				element.SetAndroidAttribute (ManifestConstants.AttributeName, uf.Name);
				element.SetAndroidAttribute ("required", uf.Required ? "true" : "false");
				manifest.Add (element);
			} else if (uf.GLESVersion != 0) {
				var versionStr = $"0x{uf.GLESVersion:X8}";
				if (!manifest.Elements ("uses-feature").Any (e => e.AndroidAttribute ("glEsVersion") == versionStr)) {
					var element = new ManifestElement ("uses-feature");
					element.SetAndroidAttribute ("glEsVersion", versionStr);
					element.SetAndroidAttribute ("required", uf.Required ? "true" : "false");
					manifest.Add (element);
				}
			}
		}

		// <uses-library> elements inside <application>
		foreach (var ul in info.UsesLibraries) {
			if (string.IsNullOrEmpty (ul.Name)) {
				continue;
			}
			if (!app.Elements ("uses-library").Any (e => e.AndroidAttribute (ManifestConstants.AttributeName) == ul.Name)) {
				var element = new ManifestElement ("uses-library");
				element.SetAndroidAttribute (ManifestConstants.AttributeName, ul.Name);
				element.SetAndroidAttribute ("required", ul.Required ? "true" : "false");
				app.Add (element);
			}
		}

		// Assembly-level <meta-data> inside <application>
		foreach (var md in info.MetaData) {
			if (string.IsNullOrEmpty (md.Name)) {
				continue;
			}
			if (!app.Elements ("meta-data").Any (e => e.AndroidAttribute ("name") == md.Name)) {
				app.Add (ComponentElementBuilder.CreateMetaDataElement (md));
			}
		}

		// Assembly-level <property> inside <application>
		foreach (var prop in info.Properties) {
			if (string.IsNullOrEmpty (prop.Name)) {
				continue;
			}
			if (!app.Elements ("property").Any (e => e.AndroidAttribute ("name") == prop.Name)) {
				var element = new ManifestElement ("property");
				element.SetAndroidAttribute ("name", prop.Name);
				if (prop.Value is not null) {
					element.SetAndroidAttribute ("value", prop.Value);
				}
				if (prop.Resource is not null) {
					element.SetAndroidAttribute ("resource", prop.Resource);
				}
				app.Add (element);
			}
		}

		// <uses-configuration> elements
		foreach (var uc in info.UsesConfigurations) {
			var element = new ManifestElement ("uses-configuration");
			if (uc.ReqFiveWayNav) {
				element.SetAndroidAttribute ("reqFiveWayNav", "true");
			}
			if (uc.ReqHardKeyboard) {
				element.SetAndroidAttribute ("reqHardKeyboard", "true");
			}
			if (uc.ReqKeyboardType is not null) {
				element.SetAndroidAttribute ("reqKeyboardType", uc.ReqKeyboardType);
			}
			if (uc.ReqNavigation is not null) {
				element.SetAndroidAttribute ("reqNavigation", uc.ReqNavigation);
			}
			if (uc.ReqTouchScreen is not null) {
				element.SetAndroidAttribute ("reqTouchScreen", uc.ReqTouchScreen);
			}
			manifest.Add (element);
		}

		// <supports-gl-texture> elements
		var existingGLTextures = new HashSet<string> (
			manifest.Elements ("supports-gl-texture").Select (e => e.AndroidAttribute (ManifestConstants.AttributeName)).OfType<string> ());
		foreach (var gl in info.SupportsGLTextures) {
			if (existingGLTextures.Add (gl.Name)) {
				var element = new ManifestElement ("supports-gl-texture");
				element.SetAndroidAttribute (ManifestConstants.AttributeName, gl.Name);
				manifest.Add (element);
			}
		}
	}

	internal static void ApplyApplicationProperties (
		ManifestElement app,
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
		ManifestElement app,
		Dictionary<string, object?> properties,
		IReadOnlyList<JavaPeerInfo> allPeers,
		string propertyName,
		string xmlAttrName,
		Action<string>? warn)
	{
		if (app.HasAttribute (ManifestConstants.AndroidNamespace, xmlAttrName)) {
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
				app.SetAndroidAttribute (xmlAttrName, peer.JavaName.Replace ('/', '.'));
				return;
			}
		}

		warn?.Invoke ($"Could not resolve {propertyName} type '{managedName}' to a Java peer for android:{xmlAttrName}.");
	}

	internal static void AddInternetPermission (ManifestElement manifest)
	{
		if (!manifest.Elements ("uses-permission").Any (p =>
			p.AndroidAttribute ("name") == "android.permission.INTERNET")) {
			var element = new ManifestElement ("uses-permission");
			element.SetAndroidAttribute ("name", "android.permission.INTERNET");
			manifest.Add (element);
		}
	}
}
