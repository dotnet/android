using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

/// <summary>
/// Generates acw-map.txt from JavaPeerInfo scanner results.
/// The acw-map format is consumed by _ConvertCustomView to fix up
/// custom view names in layout XML files.
///
/// Each type produces up to 3 lines:
///   PartialAssemblyQualifiedName;JavaKey
///   ManagedKey;JavaKey
///   CompatJniName;JavaKey
///
/// Types with DoNotGenerateAcw = true are excluded (MCW binding types).
/// </summary>
static class AcwMapWriter
{
	/// <summary>
	/// Writes per-assembly acw-map lines for a single assembly's scan results.
	/// Returns the lines (not yet written to disk) so they can be merged later.
	/// </summary>
	public static List<AcwMapEntry> CreateEntries (IReadOnlyList<JavaPeerInfo> peers, string assemblyName)
	{
		var entries = new List<AcwMapEntry> ();

		foreach (var peer in peers) {
			if (peer.DoNotGenerateAcw)
				continue;

			if (peer.AssemblyName != assemblyName)
				continue;

			var javaKey = peer.JavaName.Replace ('/', '.');
			var managedKey = peer.ManagedTypeName;
			var partialAssemblyQualifiedName = $"{peer.ManagedTypeName}, {peer.AssemblyName}";
			// Compat JNI name uses the same format for now
			var compatJniName = javaKey;

			entries.Add (new AcwMapEntry {
				JavaKey = javaKey,
				ManagedKey = managedKey,
				PartialAssemblyQualifiedName = partialAssemblyQualifiedName,
				CompatJniName = compatJniName,
				AssemblyName = peer.AssemblyName,
			});
		}

		return entries;
	}

	/// <summary>
	/// Writes acw-map.txt from merged entries.
	/// Detects XA4214 (duplicate managed key) and XA4215 (duplicate Java key) conflicts.
	/// Returns true if no errors were found, false if XA4215 errors prevent output.
	/// </summary>
	public static AcwMapResult WriteMap (IReadOnlyList<AcwMapEntry> entries, TextWriter writer)
	{
		var managed = new Dictionary<string, AcwMapEntry> (entries.Count, StringComparer.Ordinal);
		var java = new Dictionary<string, AcwMapEntry> (entries.Count, StringComparer.Ordinal);
		var managedConflicts = new Dictionary<string, List<string>> (0, StringComparer.Ordinal);
		var javaConflicts = new Dictionary<string, List<string>> (0, StringComparer.Ordinal);

		foreach (var entry in entries.OrderBy (e => e.ManagedKey, StringComparer.Ordinal)) {
			writer.Write (entry.PartialAssemblyQualifiedName);
			writer.Write (';');
			writer.WriteLine (entry.JavaKey);

			bool hasConflict = false;

			if (managed.TryGetValue (entry.ManagedKey, out var managedConflict)) {
				if (!managedConflict.AssemblyName.Equals (entry.AssemblyName, StringComparison.Ordinal)) {
					if (!managedConflicts.TryGetValue (entry.ManagedKey, out var list))
						managedConflicts.Add (entry.ManagedKey, list = new List<string> { managedConflict.AssemblyName });
					list.Add (entry.AssemblyName);
				}
				hasConflict = true;
			}

			if (java.TryGetValue (entry.JavaKey, out var javaConflict)) {
				if (!javaConflict.AssemblyName.Equals (entry.AssemblyName, StringComparison.Ordinal)) {
					if (!javaConflicts.TryGetValue (entry.JavaKey, out var list))
						javaConflicts.Add (entry.JavaKey, list = new List<string> { javaConflict.PartialAssemblyQualifiedName });
					list.Add (entry.PartialAssemblyQualifiedName);
				}
				hasConflict = true;
			}

			if (!hasConflict) {
				managed.Add (entry.ManagedKey, entry);
				java.Add (entry.JavaKey, entry);

				writer.Write (entry.ManagedKey);
				writer.Write (';');
				writer.WriteLine (entry.JavaKey);

				writer.Write (entry.CompatJniName);
				writer.Write (';');
				writer.WriteLine (entry.JavaKey);
			}
		}

		return new AcwMapResult {
			ManagedConflicts = managedConflicts,
			JavaConflicts = javaConflicts,
		};
	}

	/// <summary>
	/// Convenience: writes acw-map.txt to a file, only if content changed.
	/// Returns the result with conflict information.
	/// </summary>
	public static AcwMapResult WriteMapToFile (IReadOnlyList<AcwMapEntry> entries, string outputPath)
	{
		using var sw = new StringWriter ();
		var result = WriteMap (entries, sw);

		if (result.JavaConflicts.Count > 0)
			return result;

		var content = sw.ToString ();
		WriteIfChanged (outputPath, content);
		return result;
	}

	static void WriteIfChanged (string path, string content)
	{
		if (File.Exists (path)) {
			var existing = File.ReadAllText (path);
			if (string.Equals (existing, content, StringComparison.Ordinal))
				return;
		}

		var dir = Path.GetDirectoryName (path);
		if (!string.IsNullOrEmpty (dir) && !Directory.Exists (dir))
			Directory.CreateDirectory (dir);

		File.WriteAllText (path, content);
	}
}

sealed class AcwMapEntry
{
	public string JavaKey { get; set; } = "";
	public string ManagedKey { get; set; } = "";
	public string PartialAssemblyQualifiedName { get; set; } = "";
	public string CompatJniName { get; set; } = "";
	public string AssemblyName { get; set; } = "";
}

sealed class AcwMapResult
{
	public Dictionary<string, List<string>> ManagedConflicts { get; set; } = new ();
	public Dictionary<string, List<string>> JavaConflicts { get; set; } = new ();
	public bool HasErrors => JavaConflicts.Count > 0;
	public bool HasWarnings => ManagedConflicts.Count > 0;
}
