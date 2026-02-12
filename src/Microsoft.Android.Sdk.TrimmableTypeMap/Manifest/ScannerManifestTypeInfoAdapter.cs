using System;
using System.Collections.Generic;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

/// <summary>
/// Converts JavaPeerInfo + ComponentData (from the SRM-based scanner) into
/// IManifestTypeInfo for consumption by ManifestDocument.Merge().
/// This adapter runs in the trimmable path — no Cecil dependency.
/// </summary>
static class ScannerManifestTypeInfoAdapter
{
	/// <summary>
	/// Creates IManifestTypeInfo objects from scan results.
	/// ComponentData is now carried by JavaPeerInfo itself (populated during scanning).
	/// </summary>
	public static List<IManifestTypeInfo> Convert (IReadOnlyList<JavaPeerInfo> peers)
	{
		var result = new List<IManifestTypeInfo> (peers.Count);

		foreach (var peer in peers) {
			if (peer.DoNotGenerateAcw)
				continue;

			var componentData = peer.ComponentData;

			var javaName = peer.JavaName.Replace ('/', '.');
			var info = new ManifestTypeInfo {
				FullName = peer.ManagedTypeName,
				Namespace = peer.ManagedTypeNamespace,
				JavaName = javaName,
				CompatJavaName = javaName, // TODO: compute compat name with md5 hash when needed
				IsAbstract = peer.IsAbstract,
				HasPublicParameterlessConstructor = HasPublicDefaultCtor (peer),
				ComponentKind = componentData?.ComponentKind ?? ManifestComponentKind.None,
				ComponentAttribute = componentData?.ComponentAttribute,
				IntentFilters = componentData?.IntentFilters ?? (IReadOnlyList<ComponentAttributeInfo>)Array.Empty<ComponentAttributeInfo> (),
				MetaDataEntries = componentData?.MetaDataEntries ?? (IReadOnlyList<ComponentAttributeInfo>)Array.Empty<ComponentAttributeInfo> (),
				PropertyAttributes = componentData?.PropertyAttributes ?? (IReadOnlyList<ComponentAttributeInfo>)Array.Empty<ComponentAttributeInfo> (),
				LayoutAttribute = componentData?.LayoutAttribute,
				GrantUriPermissions = componentData?.GrantUriPermissions ?? (IReadOnlyList<ComponentAttributeInfo>)Array.Empty<ComponentAttributeInfo> (),
			};

			result.Add (info);
		}

		return result;
	}

	/// <summary>
	/// Scans a set of assemblies and produces IManifestTypeInfo objects.
	/// </summary>
	public static List<IManifestTypeInfo> ScanAndConvert (IReadOnlyList<string> assemblyPaths)
	{
		using var scanner = new JavaPeerScanner ();
		var peers = scanner.Scan (assemblyPaths);
		return Convert (peers);
	}

	static bool HasPublicDefaultCtor (JavaPeerInfo peer)
	{
		// If there are any registered constructors, check for a parameterless one
		foreach (var ctor in peer.JavaConstructors) {
			if (ctor.JniSignature == "()V")
				return true;
		}

		// Types with activation ctors have some constructor available
		// but the check for XA4213 is about a public parameterless ctor
		return false;
	}
}
