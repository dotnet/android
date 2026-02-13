using System.Collections.Generic;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

/// <summary>
/// Converts JavaPeerInfo (from the SRM-based scanner) into IManifestTypeInfo
/// for consumption by ManifestDocument.Merge(). No Cecil dependency.
/// </summary>
static class ScannerManifestTypeInfoAdapter
{
	static readonly IReadOnlyList<ComponentAttributeInfo> EmptyAttributes = new ComponentAttributeInfo [0];

	public static List<IManifestTypeInfo> Convert (IReadOnlyList<JavaPeerInfo> peers)
	{
		var result = new List<IManifestTypeInfo> (peers.Count);

		foreach (var peer in peers) {
			if (peer.DoNotGenerateAcw)
				continue;

			var cd = peer.ComponentData;
			var javaName = peer.JavaName.Replace ('/', '.');

			result.Add (new ManifestTypeInfo {
				FullName = peer.ManagedTypeName,
				Namespace = peer.ManagedTypeNamespace,
				JavaName = javaName,
				CompatJavaName = javaName,
				IsAbstract = peer.IsAbstract,
				HasPublicParameterlessConstructor = HasPublicDefaultCtor (peer),
				ComponentKind = cd?.ComponentKind ?? ManifestComponentKind.None,
				ComponentAttribute = cd?.ComponentAttribute,
				IntentFilters = cd?.IntentFilters ?? EmptyAttributes,
				MetaDataEntries = cd?.MetaDataEntries ?? EmptyAttributes,
				PropertyAttributes = cd?.PropertyAttributes ?? EmptyAttributes,
				LayoutAttribute = cd?.LayoutAttribute,
				GrantUriPermissions = cd?.GrantUriPermissions ?? EmptyAttributes,
			});
		}

		return result;
	}

	public static List<IManifestTypeInfo> ScanAndConvert (IReadOnlyList<string> assemblyPaths)
	{
		using var scanner = new JavaPeerScanner ();
		return Convert (scanner.Scan (assemblyPaths));
	}

	static bool HasPublicDefaultCtor (JavaPeerInfo peer)
	{
		foreach (var ctor in peer.JavaConstructors) {
			if (ctor.JniSignature == "()V")
				return true;
		}
		return false;
	}
}
