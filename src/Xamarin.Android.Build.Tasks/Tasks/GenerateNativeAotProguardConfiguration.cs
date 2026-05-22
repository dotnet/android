#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Microsoft.Build.Framework;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	public class GenerateNativeAotProguardConfiguration : AndroidTask
	{
		public override string TaskPrefix => "GNAPC";

		[Required]
		public string NativeAotDgmlFile { get; set; } = "";

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

			if (!File.Exists (NativeAotDgmlFile)) {
				Log.LogError ("NativeAOT DGML file '{0}' was not found.", NativeAotDgmlFile);
				return false;
			}
			if (!File.Exists (AcwMapFile)) {
				Log.LogError ("ACW map file '{0}' was not found.", AcwMapFile);
				return false;
			}

			var retainedTypeKeys = LoadRetainedTypeKeysFromDgml ();
			var javaTypes = LoadJavaTypesFromAcwMap (retainedTypeKeys);

			using var writer = File.CreateText (OutputFile);
			writer.WriteLine ("# ACWs retained by NativeAOT ILC");
			foreach (var javaTypeName in javaTypes) {
				writer.WriteLine ($"-keep class {javaTypeName} {{ *; }}");
			}

			Log.LogMessage (MessageImportance.Low, $"Generated {javaTypes.Count} NativeAOT trimmable typemap ProGuard rules from '{NativeAotDgmlFile}'.");
			return !Log.HasLoggedErrors;
		}

		List<string> LoadJavaTypesFromAcwMap (HashSet<string> retainedTypeKeys)
		{
			var javaTypes = new List<string> ();
			foreach (var line in File.ReadLines (AcwMapFile)) {
				var separator = line.IndexOf (';');
				if (separator <= 0 || separator == line.Length - 1) {
					continue;
				}
				var managedTypeName = line.Substring (0, separator);
				var javaTypeName = line.Substring (separator + 1);
				if (retainedTypeKeys.Contains (managedTypeName) && !javaTypes.Contains (javaTypeName)) {
					javaTypes.Add (javaTypeName);
				}
			}
			return javaTypes;
		}

		HashSet<string> LoadRetainedTypeKeysFromDgml ()
		{
			var typeKeys = new HashSet<string> (StringComparer.Ordinal);
			using var reader = XmlReader.Create (NativeAotDgmlFile, new XmlReaderSettings {
				DtdProcessing = DtdProcessing.Prohibit,
			});

			while (reader.Read ()) {
				if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "Node") {
					continue;
				}

				var label = reader.GetAttribute ("Label");
				if (label.IsNullOrEmpty () || !label.StartsWith ("Type metadata: [", StringComparison.Ordinal)) {
					continue;
				}

				var assemblyStart = "Type metadata: [".Length;
				var assemblyEnd = label.IndexOf (']', assemblyStart);
				if (assemblyEnd < 0 || assemblyEnd == label.Length - 1) {
					continue;
				}

				var assemblyName = label.Substring (assemblyStart, assemblyEnd - assemblyStart);
				var managedTypeName = label.Substring (assemblyEnd + 1);
				typeKeys.Add (managedTypeName);
				typeKeys.Add ($"{managedTypeName}, {assemblyName}");
			}

			return typeKeys;
		}
	}
}
