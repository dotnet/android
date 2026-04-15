using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

/// <summary>
/// Generates per-assembly acw-map.txt content from <see cref="JavaPeerInfo"/> records.
/// The acw-map.txt file maps managed type names to Java/ACW type names, consumed by
/// <c>_ConvertCustomView</c> to fix up custom view names in layout XMLs.
///
/// Format per type (3 lines):
///   Line 1: PartialAssemblyQualifiedName;JavaKey  (always written)
///   Line 2: ManagedKey;JavaKey
///   Line 3: CompatJniName;JavaKey
///
/// Java keys use dots (not slashes): e.g., "android.app.Activity"
/// </summary>
public static class AcwMapWriter
{
	/// <summary>
	/// Writes acw-map lines for the given <paramref name="peers"/> to the <paramref name="writer"/>.
	/// Per-assembly maps write all 3 line variants unconditionally. No conflict detection
	/// is performed — the merged acw-map.txt is a simple concatenation consumed by
	/// LoadMapFile which uses first-entry-wins semantics for duplicate keys.
	/// </summary>
	public static void Write (TextWriter writer, IEnumerable<JavaPeerInfo> peers)
	{
		foreach (var peer in peers.OrderBy (p => p.ManagedTypeName, StringComparer.Ordinal)) {
			string javaKey = JniSignatureHelper.JniNameToJavaName (peer.JavaName);
			string managedKey = peer.ManagedTypeName;
			string partialAsmQualifiedName = $"{managedKey}, {peer.AssemblyName}";
			string compatJniName = JniSignatureHelper.JniNameToJavaName (peer.CompatJniName);

			// Line 1: PartialAssemblyQualifiedName;JavaKey
			writer.Write (partialAsmQualifiedName);
			writer.Write (';');
			writer.WriteLine (javaKey);

			// Line 2: ManagedKey;JavaKey
			writer.Write (managedKey);
			writer.Write (';');
			writer.WriteLine (javaKey);

			// Line 3: CompatJniName;JavaKey
			writer.Write (compatJniName);
			writer.Write (';');
			writer.WriteLine (javaKey);
		}
	}
}
