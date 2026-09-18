#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using Microsoft.Build.Framework;
using Microsoft.Android.Build.Tasks;
using Xamarin.Android.Tasks;
using Properties = Xamarin.Android.Tasks.Properties;

namespace Microsoft.Android.Tasks;

public class ExtractTypeMapKeysFromDgml : AndroidTask
{
	const string DgmlNamespace = "http://schemas.microsoft.com/vs/2009/dgml";
	const string TypeMetadataPrefix = "Type metadata: [";

	public override string TaskPrefix => "ETMKD";

	public ITaskItem [] NativeAotDgmlFiles { get; set; } = [];

	[Required]
	public string AcwMapFile { get; set; } = "";

	public bool TrimJavaCallableWrappers { get; set; } = true;

	[Required]
	public string OutputFile { get; set; } = "";

	public override bool RunTask ()
	{
		if (!File.Exists (AcwMapFile)) {
			Log.LogCodedError ("XA4320", Properties.Resources.XA4320, AcwMapFile);
			return false;
		}

		HashSet<string>? retainedTypeKeys = null;
		if (TrimJavaCallableWrappers) {
			if (NativeAotDgmlFiles.Length == 0) {
				Log.LogCodedError ("XA4319", Properties.Resources.XA4319);
				return false;
			}
			retainedTypeKeys = new HashSet<string> (StringComparer.Ordinal);
			foreach (var dgmlFile in NativeAotDgmlFiles) {
				if (!File.Exists (dgmlFile.ItemSpec)) {
					Log.LogCodedError ("XA4321", Properties.Resources.XA4321, dgmlFile.ItemSpec);
					return false;
				}
				try {
					LoadRetainedTypeKeysFromDgml (dgmlFile.ItemSpec, retainedTypeKeys);
				} catch (Exception e) when (e is XmlException || e is IOException || e is UnauthorizedAccessException) {
					Log.LogCodedError ("XA4327", Properties.Resources.XA4327, dgmlFile.ItemSpec, e.Message);
					return false;
				}
			}
		}

		SortedSet<string> javaTypes;
		try {
			javaTypes = LoadJavaTypesFromAcwMap (retainedTypeKeys);
		} catch (Exception e) when (e is FormatException || e is IOException || e is UnauthorizedAccessException || e is DecoderFallbackException) {
			Log.LogCodedError ("XA4327", Properties.Resources.XA4327, AcwMapFile, e.Message);
			return false;
		}

		var directory = Path.GetDirectoryName (OutputFile);
		if (!directory.IsNullOrEmpty ()) {
			Directory.CreateDirectory (directory);
		}
		using (var writer = new StreamWriter (OutputFile, false, new UTF8Encoding (false, true)) { NewLine = "\n" }) {
			foreach (var javaType in javaTypes) {
				writer.WriteLine (javaType);
			}
		}

		Log.LogMessage (MessageImportance.Low, "Extracted {0} retained Java type map keys from the NativeAOT ACW map.", javaTypes.Count);
		return !Log.HasLoggedErrors;
	}

	SortedSet<string> LoadJavaTypesFromAcwMap (HashSet<string>? retainedTypeKeys)
	{
		var javaTypes = new SortedSet<string> (StringComparer.Ordinal);
		int lineNumber = 0;
		foreach (var line in File.ReadLines (AcwMapFile, new UTF8Encoding (true, true))) {
			lineNumber++;
			if (line.Length == 0) {
				continue;
			}
			var separator = line.IndexOf (';');
			if (separator <= 0 || separator == line.Length - 1) {
				throw new FormatException ($"ACW map line {lineNumber} must contain a managed key and a Java class name separated by ';'.");
			}

			var managedTypeName = line.Substring (0, separator);
			var javaTypeName = line.Substring (separator + 1).Replace ('.', '/');
			if (managedTypeName.IsNullOrWhiteSpace () || !IsJavaClassName (javaTypeName)) {
				throw new FormatException ($"ACW map line {lineNumber} contains an invalid managed key or Java class name.");
			}

			// Null means Java trimming is disabled, so even the unqualified ACW entries are retained.
			if (retainedTypeKeys == null || retainedTypeKeys.Contains (managedTypeName)) {
				javaTypes.Add (javaTypeName);
			}
		}
		return javaTypes;
	}

	static bool IsJavaClassName (string name)
	{
		bool segmentStart = true;
		for (int i = 0; i < name.Length; i++) {
			if (name [i] == '/') {
				if (segmentStart) {
					return false;
				}
				segmentStart = true;
				continue;
			}

			var category = CharUnicodeInfo.GetUnicodeCategory (name, i);
			switch (category) {
			case UnicodeCategory.UppercaseLetter:
			case UnicodeCategory.LowercaseLetter:
			case UnicodeCategory.TitlecaseLetter:
			case UnicodeCategory.ModifierLetter:
			case UnicodeCategory.OtherLetter:
			case UnicodeCategory.LetterNumber:
			case UnicodeCategory.CurrencySymbol:
			case UnicodeCategory.ConnectorPunctuation:
				break;
			case UnicodeCategory.DecimalDigitNumber:
			case UnicodeCategory.NonSpacingMark:
			case UnicodeCategory.SpacingCombiningMark:
				if (segmentStart) {
					return false;
				}
				break;
			default:
				return false;
			}

			if (char.IsHighSurrogate (name [i])) {
				i++;
			}
			segmentStart = false;
		}
		return !segmentStart;
	}

	static void LoadRetainedTypeKeysFromDgml (string path, HashSet<string> typeKeys)
	{
		using var reader = XmlReader.Create (path, new XmlReaderSettings {
			DtdProcessing = DtdProcessing.Prohibit,
			XmlResolver = null,
		});

		if (reader.MoveToContent () != XmlNodeType.Element || reader.LocalName != "DirectedGraph" ||
				(reader.NamespaceURI.Length != 0 && reader.NamespaceURI != DgmlNamespace)) {
			throw new XmlException ("Expected a DGML DirectedGraph document.");
		}

		string graphNamespace = reader.NamespaceURI;
		bool readingNodes = false;
		// Read through EOF, not just </Nodes>, so a truncated graph never looks like a valid retained set.
		while (reader.Read ()) {
			if (reader.NamespaceURI != graphNamespace) {
				continue;
			}
			if (reader.Depth == 1 && reader.LocalName == "Nodes") {
				readingNodes = reader.NodeType == XmlNodeType.Element && !reader.IsEmptyElement;
				continue;
			}
			if (!readingNodes || reader.Depth != 2 || reader.NodeType != XmlNodeType.Element || reader.LocalName != "Node") {
				continue;
			}

			var label = reader.GetAttribute ("Label");
			if (label.IsNullOrEmpty () || !label.StartsWith ("Type metadata:", StringComparison.Ordinal)) {
				continue;
			}

			if (!label.StartsWith (TypeMetadataPrefix, StringComparison.Ordinal)) {
				throw new XmlException ($"Invalid NativeAOT type metadata label '{label}'.");
			}
			var assemblyStart = TypeMetadataPrefix.Length;
			var assemblyEnd = label.IndexOf (']', assemblyStart);
			if (assemblyEnd <= assemblyStart || assemblyEnd == label.Length - 1) {
				throw new XmlException ($"Invalid NativeAOT type metadata label '{label}'.");
			}

			var assemblyName = label.Substring (assemblyStart, assemblyEnd - assemblyStart);
			var managedTypeName = label.Substring (assemblyEnd + 1);
			if (assemblyName.IsNullOrWhiteSpace () || managedTypeName.IsNullOrWhiteSpace ()) {
				throw new XmlException ($"Invalid NativeAOT type metadata label '{label}'.");
			}
			typeKeys.Add ($"{managedTypeName}, {assemblyName}");
		}
	}
}
