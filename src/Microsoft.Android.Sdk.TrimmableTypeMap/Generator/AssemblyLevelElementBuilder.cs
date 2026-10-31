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

		// Assembly-level <meta-data> inside <application>
		foreach (var md in info.MetaData) {
			if (string.IsNullOrEmpty (md.Name)) {
				continue;
			}
			app.Add (ComponentElementBuilder.CreateMetaDataElement (md));
		}

		// Assembly-level <property> inside <application>
		foreach (var prop in info.Properties) {
			if (string.IsNullOrEmpty (prop.Name)) {
				continue;
			}
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

	internal static void AddInstrumentations (XElement manifest, IEnumerable<InstrumentationInfo> instrumentations)
	{
		var existingInstrumentations = new HashSet<string> (
			manifest.Descendants ("instrumentation").Select (e => (string?)e.Attribute (AttName)).OfType<string> ());
		foreach (var instrumentation in instrumentations) {
			if (string.IsNullOrEmpty (instrumentation.Name)) {
				continue;
			}
			if (!existingInstrumentations.Add (instrumentation.Name)) {
				continue;
			}
			var element = new XElement ("instrumentation", new XAttribute (AttName, instrumentation.Name));
			PropertyMapper.ApplyMappings (element, instrumentation.Properties, PropertyMapper.InstrumentationMappings);
			if (element.Attribute (AndroidNs + "targetPackage") is null) {
				element.SetAttributeValue (AndroidNs + "targetPackage", (string?) manifest.Attribute ("package"));
			}
			manifest.Add (element);
		}
	}

	internal static void AddUsesLibraries (XElement app, IEnumerable<UsesLibraryInfo> libraries)
	{
		var existingLibraries = new HashSet<string> (
			app.Elements ("uses-library").Select (e => (string?)e.Attribute (AttName)).OfType<string> ());
		foreach (var library in libraries) {
			if (!string.IsNullOrEmpty (library.Name) && existingLibraries.Add (library.Name)) {
				app.Add (new XElement ("uses-library",
					new XAttribute (AttName, library.Name),
					new XAttribute (AndroidNs + "required", library.Required ? "true" : "false")));
			}
		}
	}

	internal static void ApplyApplicationProperties (
		XElement app,
		IReadOnlyDictionary<string, object?> properties,
		IReadOnlyList<JavaPeerInfo> allPeers,
		Action<int, string>? warn = null,
		ISet<XAttribute>? writtenAttributes = null)
	{
		PropertyMapper.ApplyMappings (
			app,
			properties,
			PropertyMapper.ApplicationPropertyMappings,
			writtenAttributes: writtenAttributes);

		// BackupAgent and ManageSpaceActivity are Type properties — resolve managed type names to JNI names
		ApplyTypeProperty (app, properties, allPeers, "BackupAgent", "backupAgent", warn, writtenAttributes);
		ApplyTypeProperty (app, properties, allPeers, "ManageSpaceActivity", "manageSpaceActivity", warn, writtenAttributes);
	}

	static void ApplyTypeProperty (
		XElement app,
		IReadOnlyDictionary<string, object?> properties,
		IReadOnlyList<JavaPeerInfo> allPeers,
		string propertyName,
		string xmlAttrName,
		Action<int, string>? warn,
		ISet<XAttribute>? writtenAttributes)
	{
		if (!properties.TryGetValue (propertyName, out var value) || value is not string managedName || managedName.Length == 0) {
			return;
		}

		var peer = FindPeerByAssemblyQualifiedName (allPeers, managedName);
		if (peer is not null) {
			if (propertyName == "BackupAgent" && !IsSubclassOf (peer, "android/app/backup/BackupAgent", allPeers)) {
				throw new InvalidOperationException (
					$"The Type '{peer.ManagedTypeName}', referenced by the Android.App.ApplicationAttribute.BackupAgent property, must be a subclass of the type Android.App.Backup.BackupAgent.");
			}
			app.SetAttributeValue (AndroidNs + xmlAttrName, JniSignatureHelper.JniNameToJavaBinaryName (peer.JavaName));
			var attribute = app.Attribute (AndroidNs + xmlAttrName);
			if (attribute is not null) {
				writtenAttributes?.Add (attribute);
			}
			return;
		}

		// Code 0 = no XA code assigned; the caller may silently ignore it.
		warn?.Invoke (0, $"Could not resolve {propertyName} type '{managedName}' to a Java peer for android:{xmlAttrName}.");
	}

	internal static JavaPeerInfo? FindPeerByAssemblyQualifiedName (IReadOnlyList<JavaPeerInfo> allPeers, string value)
	{
		var managedTypeName = value.Trim ();
		string? assemblyName = null;
		var commaIndex = managedTypeName.IndexOf (',');
		if (commaIndex > 0) {
			var assemblyStart = commaIndex + 1;
			var assemblyEnd = managedTypeName.IndexOf (',', assemblyStart);
			assemblyName = managedTypeName.Substring (
				assemblyStart,
				assemblyEnd < 0 ? managedTypeName.Length - assemblyStart : assemblyEnd - assemblyStart).Trim ();
			managedTypeName = managedTypeName.Substring (0, commaIndex).Trim ();
		}

		return allPeers.FirstOrDefault (peer =>
			peer.ManagedTypeName == managedTypeName &&
			(assemblyName is null || peer.AssemblyName == assemblyName));
	}

	internal static bool IsSubclassOf (JavaPeerInfo peer, string baseJavaName, IReadOnlyList<JavaPeerInfo> allPeers)
	{
		var currentBase = peer.BaseJavaName;
		var visited = new HashSet<string> (StringComparer.Ordinal);
		while (currentBase is not null && visited.Add (currentBase)) {
			if (currentBase == baseJavaName) {
				return true;
			}
			currentBase = allPeers.FirstOrDefault (candidate => candidate.JavaName == currentBase)?.BaseJavaName;
		}
		return false;
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
