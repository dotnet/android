#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Microsoft.Build.Framework;
using Microsoft.Android.Build.Tasks;

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

	// When false, the ILC DGML is not consulted (it may not have been generated at all) and a
	// -keep rule is emitted for every Java Callable Wrapper in the ACW map, so R8 keeps them all
	// instead of shrinking the unused ones. This trades a small amount of dex size for skipping the
	// very large DGML files and the DGML parsing/scan, which dominate NativeAOT build time.
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

		// A null retainedTypeKeys means "keep every ACW" (Java trimming disabled).
		var javaTypes = LoadJavaTypesFromAcwMap (retainedTypeKeys);

		using var writer = new StringWriter ();
		writer.WriteLine ("# ACWs retained by NativeAOT ILC");
		foreach (var javaTypeName in javaTypes) {
			writer.WriteLine ($"-keep class {javaTypeName} {{ *; }}");
		}
		Files.CopyIfStringChanged (writer.ToString (), OutputFile);

		if (TrimJavaCallableWrappers) {
			Log.LogMessage (MessageImportance.Low, "Generated {0} NativeAOT trimmable typemap ProGuard rules from {1} DGML file(s).", javaTypes.Count, NativeAotDgmlFiles.Length);
		} else {
			Log.LogMessage (MessageImportance.Low, "Generated {0} NativeAOT ProGuard rules keeping all ACWs (Java Callable Wrapper trimming is disabled).", javaTypes.Count);
		}
		return !Log.HasLoggedErrors;
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

			while (reader.Read ()) {
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
