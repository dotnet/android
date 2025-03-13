#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Java.Interop.Tools.JavaCallableWrappers.Adapters;
using Java.Interop.Tools.JavaCallableWrappers.CallableWrapperMembers;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks.Utilities;

class JavaObjectsXmlFile
{
	static XmlWriterSettings settings = new XmlWriterSettings {
		Indent = true,
		NewLineOnAttributes = false,
		OmitXmlDeclaration = true,
	};

	static readonly JavaObjectsXmlFile unscanned = new JavaObjectsXmlFile { WasScanned = false };

	public List<ACWMapEntry> ACWMapEntries { get; } = new List<ACWMapEntry> ();
	public List<CallableWrapperType> JavaCallableWrappers { get; } = new List<CallableWrapperType> ();
	public bool WasScanned { get; private set; }

	public void Export (string filename)
	{
		using var sw = MemoryStreamPool.Shared.CreateStreamWriter ();

		using (var xml = XmlWriter.Create (sw, settings))
			Export (xml);

		sw.Flush ();

		Files.CopyIfStreamChanged (sw.BaseStream, filename);
	}

	void Export (XmlWriter xml)
	{
		xml.WriteStartElement ("api");

		ExportJCWTypes (xml);
		ExportACWMappingTypes (xml);

		xml.WriteEndElement ();
	}

	void ExportJCWTypes (XmlWriter xml)
	{
		if (JavaCallableWrappers.Count == 0)
			return;

		xml.WriteStartElement ("jcw");

		XmlExporter.Export (xml, JavaCallableWrappers);

		xml.WriteEndElement ();
	}

	void ExportACWMappingTypes (XmlWriter xml)
	{
		if (ACWMapEntries.Count == 0)
			return;

		var t = ACWMapEntries.First ();

		xml.WriteStartElement ("acw");

		xml.WriteAttributeStringIfNotDefault ("partial-assembly-name", t.PartialAssemblyName);
		xml.WriteAttributeStringIfNotDefault ("module-name", t.ModuleName);

		foreach (var type in ACWMapEntries) {
			xml.WriteStartElement ("type");
			xml.WriteAttributeStringIfNotDefault ("assembly-qualified-name", type.AssemblyQualifiedName);
			xml.WriteAttributeStringIfNotDefault ("compat-jni-name", type.CompatJniName);
			xml.WriteAttributeStringIfNotDefault ("java-key", type.JavaKey);
			xml.WriteAttributeStringIfNotDefault ("managed-key", type.ManagedKey);
			xml.WriteAttributeStringIfNotDefault ("partial-assembly-qualified-name", type.PartialAssemblyQualifiedName);
			xml.WriteEndElement ();
		}

		xml.WriteEndElement ();
	}

	public static JavaObjectsXmlFile Import (string filename, JavaObjectsXmlFileReadType readType)
	{
		// If the file has zero length, then the assembly wasn't scanned because it couldn't contain JLOs
		var fi = new FileInfo (filename);

		if (fi.Length == 0)
			return unscanned;

		var file = new JavaObjectsXmlFile {
			WasScanned = true,
		};

		var xml = XDocument.Load (filename);

		if (readType.HasFlag (JavaObjectsXmlFileReadType.JCW) && xml.Root?.Element ("jcw") is XElement jcw)
			file.JavaCallableWrappers.AddRange (XmlImporter.Import (jcw));


		if (readType.HasFlag (JavaObjectsXmlFileReadType.ACW) && xml.Root?.Element ("acw") is XElement acw) {
			var partialAssemblyName = acw.GetAttributeOrDefault ("partial-assembly-name", string.Empty);
			var moduleName = acw.GetAttributeOrDefault ("module-name", string.Empty);

			foreach (var type in acw.Elements ("type")) {
				var entry = ACWMapEntry.Create (type, partialAssemblyName, moduleName);
				file.ACWMapEntries.Add (entry);
			}
		}

		return file;
	}

	public static void WriteEmptyFile (string destination, TaskLoggingHelper log)
	{
		log.LogDebugMessage ($"Writing empty file '{destination}'");

		// We write a zero byte file to indicate the file couldn't have JLO types and wasn't scanned
		File.Create (destination).Dispose ();
	}
}

[Flags]
enum JavaObjectsXmlFileReadType
{
	None = 0,
	ACW = 1,
	JCW = 2,
}
