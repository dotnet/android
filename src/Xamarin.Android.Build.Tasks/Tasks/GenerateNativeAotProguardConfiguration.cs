#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Microsoft.Build.Framework;
using Microsoft.Android.Build.Tasks;
using Xamarin.Android.Tasks.JniRemapping;

namespace Xamarin.Android.Tasks;

public class GenerateNativeAotProguardConfiguration : AndroidTask
{
	const string TypeMetadataPrefix = "Type metadata: [";

	public override string TaskPrefix => "GNAPC";

	public ITaskItem [] NativeAotDgmlFiles { get; set; } = [];

	[Required]
	public string AcwMapFile { get; set; } = "";

	[Required]
	public string OutputFile { get; set; } = "";

	public string? R8MappingFile { get; set; }

	public string? R8RewriteManifestFile { get; set; }

	public string? R8ReachabilityManifestFile { get; set; }

	// When false, the ILC DGML is not consulted (it may not have been generated at all) and a
	// -keep rule is emitted for every Java type in the ACW map, so R8 keeps them all instead of
	// shrinking the unused ones. Large binding closures can add several MB of compressed DEX, but
	// this avoids generating and processing the very large ILC dependency graph.
	public bool TrimJavaCallableWrappers { get; set; } = true;

	public override bool RunTask ()
	{
		var dir = Path.GetDirectoryName (OutputFile);
		if (!dir.IsNullOrEmpty () && !Directory.Exists (dir)) {
			Directory.CreateDirectory (dir);
		}

		if (!File.Exists (AcwMapFile)) {
			Log.LogCodedError ("XA4320", Properties.Resources.XA4320, AcwMapFile);
			return !Log.HasLoggedErrors;
		}

		HashSet<string>? retainedTypeKeys = null;
		if (TrimJavaCallableWrappers) {
			if (NativeAotDgmlFiles.Length == 0) {
				Log.LogCodedError ("XA4319", Properties.Resources.XA4319);
				return !Log.HasLoggedErrors;
			}
			foreach (var dgmlFile in NativeAotDgmlFiles) {
				if (!File.Exists (dgmlFile.ItemSpec)) {
					Log.LogCodedError ("XA4321", Properties.Resources.XA4321, dgmlFile.ItemSpec);
					return !Log.HasLoggedErrors;
				}
			}
			retainedTypeKeys = LoadRetainedTypeKeysFromDgml ();
		}

		var allJavaTypes = LoadJavaTypesFromAcwMap (null);
		// A null retainedTypeKeys means "keep every Java type in the ACW map" (Java trimming disabled).
		var javaTypes = retainedTypeKeys == null ? allJavaTypes : LoadJavaTypesFromAcwMap (retainedTypeKeys);
		var reachableR8Entries = new HashSet<string> (StringComparer.Ordinal);
		R8Mapping? mapping = null;
		if (!R8MappingFile.IsNullOrEmpty ()) {
			if (!File.Exists (R8MappingFile)) {
				LogR8JniMappingError (string.Format (Properties.Resources.XA4327_SeedMappingNotFound, R8MappingFile));
				return false;
			}
			if (R8RewriteManifestFile.IsNullOrEmpty () || !File.Exists (R8RewriteManifestFile)) {
				LogR8JniMappingError (string.Format (Properties.Resources.XA4327_RewriteManifestNotFound, R8RewriteManifestFile));
				return false;
			}
			try {
				mapping = R8Mapping.Load (R8MappingFile);
				var rewriteEntries = File.ReadAllLines (R8RewriteManifestFile);
				// Comparing the seed mapping to itself validates every manifest entry and verifies
				// that it identifies an entry in the seed mapping.
				foreach (string conflict in mapping.GetReachabilityConflicts (mapping, rewriteEntries)) {
					throw new FormatException (conflict);
				}

				var allAcwTypes = new HashSet<string> (StringComparer.Ordinal);
				foreach (string javaTypeName in allJavaTypes) {
					allAcwTypes.Add (javaTypeName.Replace ('.', '/'));
				}
				var retainedAcwTypes = new HashSet<string> (StringComparer.Ordinal);
				foreach (string javaTypeName in javaTypes) {
					string jniTypeName = javaTypeName.Replace ('.', '/');
					retainedAcwTypes.Add (jniTypeName);
					if (mapping.TryGetRenamedClass (jniTypeName, out _)) {
						reachableR8Entries.Add (R8Mapping.BuildClassEntry (jniTypeName));
					}
				}
				foreach (string entry in rewriteEntries) {
					string [] parts = entry.Split ('\t');
					string owningType = parts [1];
					if (!allAcwTypes.Contains (owningType) || retainedAcwTypes.Contains (owningType)) {
						reachableR8Entries.Add (entry);
					}
				}
			} catch (FormatException ex) {
				LogR8JniMappingError (string.Format (Properties.Resources.XA4327_MappingDataFailure, ex.Message));
				return false;
			} catch (IOException ex) {
				LogR8JniMappingError (string.Format (Properties.Resources.XA4327_MappingDataFailure, ex.Message));
				return false;
			} catch (UnauthorizedAccessException ex) {
				LogR8JniMappingError (string.Format (Properties.Resources.XA4327_MappingDataFailure, ex.Message));
				return false;
			}
		}

		using var writer = new StringWriter ();
		writer.WriteLine ("# ACWs retained by NativeAOT ILC");
		if (mapping == null) {
			foreach (var javaTypeName in javaTypes) {
				writer.WriteLine ($"-keep class {javaTypeName} {{ *; }}");
			}
		} else {
			GenerateProguardConfiguration.WriteMappedRules (writer, reachableR8Entries);
		}
		File.WriteAllText (OutputFile, writer.ToString ());
		if (!R8ReachabilityManifestFile.IsNullOrEmpty ()) {
			WriteReachabilityManifest (R8ReachabilityManifestFile, reachableR8Entries);
		}

		if (TrimJavaCallableWrappers) {
			Log.LogMessage (MessageImportance.Low, "Generated {0} NativeAOT trimmable typemap ProGuard rules from {1} DGML file(s).", javaTypes.Count, NativeAotDgmlFiles.Length);
		} else {
			Log.LogMessage (MessageImportance.Low, "Generated {0} NativeAOT ProGuard rules keeping every Java type in the ACW map (Java trimming is disabled).", javaTypes.Count);
		}
		return !Log.HasLoggedErrors;
	}

	void LogR8JniMappingError (string detail)
		=> Log.LogCodedError ("XA4327", Properties.Resources.XA4327, detail);

	static void WriteReachabilityManifest (string path, IEnumerable<string> entries)
	{
		string? directory = Path.GetDirectoryName (path);
		if (!directory.IsNullOrEmpty ()) {
			Directory.CreateDirectory (directory);
		}
		File.WriteAllText (path, R8Mapping.CreateManifestContent (entries));
	}

	List<string> LoadJavaTypesFromAcwMap (HashSet<string>? retainedTypeKeys)
	{
		var javaTypes = new List<string> (retainedTypeKeys?.Count ?? 0);
		var seenJavaTypes = new HashSet<string> (StringComparer.Ordinal);
		foreach (var line in File.ReadLines (AcwMapFile)) {
			var separator = line.IndexOf (";", StringComparison.Ordinal);
			if (separator <= 0 || separator == line.Length - 1) {
				continue;
			}
			var managedTypeName = line.Substring (0, separator);
			var javaTypeName = line.Substring (separator + 1);
			if ((retainedTypeKeys == null || retainedTypeKeys.Contains (managedTypeName)) && seenJavaTypes.Add (javaTypeName)) {
				javaTypes.Add (javaTypeName);
			}
		}
		return javaTypes;
	}

	HashSet<string> LoadRetainedTypeKeysFromDgml ()
	{
		var typeKeys = new HashSet<string> (StringComparer.Ordinal);
		foreach (var dgmlFile in NativeAotDgmlFiles) {
			using var reader = XmlReader.Create (dgmlFile.ItemSpec, new XmlReaderSettings {
				DtdProcessing = DtdProcessing.Prohibit,
				XmlResolver = null,
			});

			bool readingNodes = false;
			while (reader.Read ()) {
				if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "Nodes") {
					readingNodes = true;
					continue;
				}
				if (reader.NodeType == XmlNodeType.EndElement && reader.LocalName == "Nodes") {
					break;
				}
				if (!readingNodes) {
					continue;
				}
				if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "Node") {
					continue;
				}

				var label = reader.GetAttribute ("Label");
				if (label.IsNullOrEmpty () || !label.StartsWith (TypeMetadataPrefix, StringComparison.Ordinal)) {
					continue;
				}

				var assemblyStart = TypeMetadataPrefix.Length;
				var assemblyEnd = label.IndexOf (']', assemblyStart);
				if (assemblyEnd < 0 || assemblyEnd == label.Length - 1) {
					continue;
				}

				var assemblyName = label.Substring (assemblyStart, assemblyEnd - assemblyStart);
				var managedTypeName = label.Substring (assemblyEnd + 1);
				typeKeys.Add ($"{managedTypeName}, {assemblyName}");
			}
		}

		return typeKeys;
	}
}
