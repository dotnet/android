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

	[Required]
	public ITaskItem [] NativeAotDgmlFiles { get; set; } = [];

	[Required]
	public string AcwMapFile { get; set; } = "";

	[Required]
	public string OutputFile { get; set; } = "";

	public override bool RunTask ()
	{
		var dir = Path.GetDirectoryName (OutputFile);
		if (!dir.IsNullOrEmpty () && !Directory.Exists (dir)) {
			Directory.CreateDirectory (dir);
		}

		if (NativeAotDgmlFiles.Length == 0) {
			Log.LogCodedError ("XA4319", Properties.Resources.XA4319);
			return !Log.HasLoggedErrors;
		}
		if (!File.Exists (AcwMapFile)) {
			Log.LogCodedError ("XA4320", Properties.Resources.XA4320, AcwMapFile);
			return !Log.HasLoggedErrors;
		}
		foreach (var dgmlFile in NativeAotDgmlFiles) {
			if (!File.Exists (dgmlFile.ItemSpec)) {
				Log.LogCodedError ("XA4321", Properties.Resources.XA4321, dgmlFile.ItemSpec);
				return !Log.HasLoggedErrors;
			}
		}

		var retainedTypeKeys = LoadRetainedTypeKeysFromDgml ();
		var javaTypes = LoadJavaTypesFromAcwMap (retainedTypeKeys);

		using var writer = new StringWriter ();
		writer.WriteLine ("# ACWs retained by NativeAOT ILC");
		foreach (var javaTypeName in javaTypes) {
			writer.WriteLine ($"-keep class {javaTypeName} {{ *; }}");
		}
		Files.CopyIfStringChanged (writer.ToString (), OutputFile);

		Log.LogMessage (MessageImportance.Low, "Generated {0} NativeAOT trimmable typemap ProGuard rules from {1} DGML file(s).", javaTypes.Count, NativeAotDgmlFiles.Length);
		return !Log.HasLoggedErrors;
	}

	List<string> LoadJavaTypesFromAcwMap (HashSet<string> retainedTypeKeys)
	{
		var javaTypes = new List<string> (retainedTypeKeys.Count);
		var seenJavaTypes = new HashSet<string> (StringComparer.Ordinal);
		foreach (var line in File.ReadLines (AcwMapFile)) {
			var separator = line.IndexOf (";", StringComparison.Ordinal);
			if (separator <= 0 || separator == line.Length - 1) {
				continue;
			}
			var managedTypeName = line.Substring (0, separator);
			var javaTypeName = line.Substring (separator + 1);
			if (retainedTypeKeys.Contains (managedTypeName) && seenJavaTypes.Add (javaTypeName)) {
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
				// ILC DGML labels contain the managed type name without assembly qualification,
				// while acw-map.txt can disambiguate duplicate type names with the assembly-qualified form.
				typeKeys.Add (managedTypeName);
				typeKeys.Add ($"{managedTypeName}, {assemblyName}");
			}
		}

		return typeKeys;
	}
}
