using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Utilities;
using NuGet.Packaging;

using ModuleReleaseData = Xamarin.Android.Tasks.TypeMapGenerator.ModuleReleaseData;
using TypeMapDebugEntry = Xamarin.Android.Tasks.TypeMapGenerator.TypeMapDebugEntry;
using TypeMapReleaseEntry = Xamarin.Android.Tasks.TypeMapGenerator.TypeMapReleaseEntry;

namespace Xamarin.Android.Tasks;

class TypeMapObjectsXmlFile
{
	static readonly XmlWriterSettings settings = new XmlWriterSettings {
		Indent = true,
		NewLineOnAttributes = false,
		OmitXmlDeclaration = true,
	};

	static readonly TypeMapObjectsXmlFile unscanned = new TypeMapObjectsXmlFile { WasScanned = false };

	public string? AssemblyName { get; set; }
	public Guid AssemblyMvid { get; set; } = Guid.Empty;
	public bool FoundJniNativeRegistration { get; set; }
	public List<TypeMapDebugEntry> JavaToManagedDebugEntries { get; } = [];
	public List<TypeMapDebugEntry> ManagedToJavaDebugEntries { get; } = [];
	public ModuleReleaseData? ModuleReleaseData { get; set; }
	public bool HasDebugEntries => JavaToManagedDebugEntries.Count > 0 || ManagedToJavaDebugEntries.Count > 0;
	public bool HasReleaseEntries => ModuleReleaseData is not null;

	public bool WasScanned { get; private set; }

	public void Export (string filename, TaskLoggingHelper log)
	{
		if (!HasDebugEntries && ModuleReleaseData == null) {
			WriteEmptyFile (filename, log);
			return;
		}

		using var sw = MemoryStreamPool.Shared.CreateStreamWriter ();

		using (var xml = XmlWriter.Create (sw, settings))
			Export (xml);

		sw.Flush ();

		Files.CopyIfStreamChanged (sw.BaseStream, filename);

		log.LogDebugMessage ($"Wrote '{filename}', {JavaToManagedDebugEntries.Count} JavaToManagedDebugEntries, {ManagedToJavaDebugEntries.Count} ManagedToJavaDebugEntries, FoundJniNativeRegistration: {FoundJniNativeRegistration}");
	}

	void Export (XmlWriter xml)
	{
		xml.WriteStartElement ("api");
		xml.WriteAttributeString ("type", HasDebugEntries ? "debug" : "release");
		xml.WriteAttributeStringIfNotDefault ("assembly-name", AssemblyName);

		if (AssemblyMvid != Guid.Empty) {
			xml.WriteAttributeString ("mvid", AssemblyMvid.ToString ("N"));
		}
		xml.WriteAttributeStringIfNotDefault ("found-jni-native-registration", FoundJniNativeRegistration);

		if (HasDebugEntries)
			ExportDebugData (xml);
		else if (HasReleaseEntries)
			ExportReleaseData (xml);

		xml.WriteEndElement ();
	}

	void ExportDebugData (XmlWriter xml)
	{
		if (JavaToManagedDebugEntries.Count > 0) {
			xml.WriteStartElement ("java-to-managed");

			foreach (var entry in JavaToManagedDebugEntries)
				WriteTypeMapDebugEntry (xml, entry);

			xml.WriteEndElement ();
		}

		if (ManagedToJavaDebugEntries.Count > 0) {
			xml.WriteStartElement ("managed-to-java");

			foreach (var entry in ManagedToJavaDebugEntries)
				WriteTypeMapDebugEntry (xml, entry);

			xml.WriteEndElement ();
		}
	}

	void WriteTypeMapDebugEntry (XmlWriter xml, TypeMapDebugEntry entry)
	{
		xml.WriteStartElement ("entry");
		xml.WriteAttributeStringIfNotDefault ("java-name", entry.JavaName);
		xml.WriteAttributeStringIfNotDefault ("managed-name", entry.ManagedName);
		xml.WriteAttributeStringIfNotDefault ("skip-in-java-to-managed", entry.SkipInJavaToManaged);
		xml.WriteAttributeStringIfNotDefault ("is-invoker", entry.IsInvoker);
		xml.WriteEndElement ();
	}

	void ExportReleaseData (XmlWriter xml)
	{
		if (ModuleReleaseData is null)
			return;

		xml.WriteStartElement ("module");

		xml.WriteAttributeStringIfNotDefault ("assembly-name", ModuleReleaseData.AssemblyName);
		xml.WriteAttributeStringIfNotDefault ("mvid", ModuleReleaseData.Mvid.ToString ("N"));
		xml.WriteAttributeStringIfNotDefault ("mvid-bytes", Convert.ToBase64String (ModuleReleaseData.MvidBytes));

		if (ModuleReleaseData.Types?.Length > 0) {
			xml.WriteStartElement ("types");

			foreach (var entry in ModuleReleaseData.DuplicateTypes)
				ExportTypeMapReleaseEntry (xml, entry, null);

			xml.WriteEndElement ();
		}

		if (ModuleReleaseData.DuplicateTypes?.Count > 0) {
			xml.WriteStartElement ("duplicates");

			foreach (var entry in ModuleReleaseData.DuplicateTypes)
				ExportTypeMapReleaseEntry (xml, entry, null);

			xml.WriteEndElement ();
		}

		if (ModuleReleaseData.TypesScratch?.Count > 0) {
			xml.WriteStartElement ("types-scratch");

			foreach (var kvp in ModuleReleaseData.TypesScratch)
				ExportTypeMapReleaseEntry (xml, kvp.Value, kvp.Key);

			xml.WriteEndElement ();
		}

		xml.WriteEndElement ();
	}

	void ExportTypeMapReleaseEntry (XmlWriter xml, TypeMapReleaseEntry entry, string? key)
	{
		xml.WriteStartElement ("entry");

		xml.WriteAttributeStringIfNotDefault ("key", key);
		xml.WriteAttributeStringIfNotDefault ("java-name", entry.JavaName);
		xml.WriteAttributeStringIfNotDefault ("managed-type-name", entry.ManagedTypeName);
		xml.WriteAttributeStringIfNotDefault ("token", entry.Token.ToString ());
		xml.WriteAttributeStringIfNotDefault ("skip-in-java-to-managed", entry.SkipInJavaToManaged);

		xml.WriteEndElement ();
	}

	/// <summary>
	/// Given an assembly path, return the path to the ".typemap.xml" file that should be next to it.
	/// </summary>
	public static string GetTypeMapObjectsXmlFilePath (string assemblyPath)
		=> Path.ChangeExtension (assemblyPath, ".typemap.xml");

	public static TypeMapObjectsXmlFile Import (string filename)
	{
		// If the file has zero length, then the assembly wasn't scanned because it couldn't contain JLOs.
		// This check is much faster than loading and parsing an empty XML file.
		var fi = new FileInfo (filename);

		if (fi.Length == 0)
			return unscanned;

		var xml = XDocument.Load (filename);
		var root = xml.Root ?? throw new InvalidOperationException ($"Invalid XML file '{filename}'");

		var type = root.GetRequiredAttribute ("type");
		var assemblyName = root.GetAttributeOrDefault ("assembly-name", (string?)null);
		var mvid = Guid.Parse (root.GetAttributeOrDefault ("mvid", Guid.Empty.ToString ()));
		var foundJniNativeRegistration = root.GetAttributeOrDefault ("found-jni-native-registration", false);

		var file = new TypeMapObjectsXmlFile {
			WasScanned = true,
			AssemblyName = assemblyName,
			AssemblyMvid = mvid,
			FoundJniNativeRegistration = foundJniNativeRegistration,
		};

		if (type == "debug")
			ImportDebugData (root, file);
		else if (type == "release")
			ImportReleaseData (root, file);

		return file;
	}

	static void ImportDebugData (XElement root, TypeMapObjectsXmlFile file)
	{
		var isMonoAndroid = root.GetAttributeOrDefault ("assembly-name", string.Empty) == "Mono.Android";
		var javaToManaged = root.Element ("java-to-managed");

		if (javaToManaged is not null) {
			foreach (var entry in javaToManaged.Elements ("entry"))
				file.JavaToManagedDebugEntries.Add (FromDebugEntryXml (entry, isMonoAndroid));
		}

		var managedToJava = root.Element ("managed-to-java");

		if (managedToJava is not null) {
			foreach (var entry in managedToJava.Elements ("entry"))
				file.ManagedToJavaDebugEntries.Add (FromDebugEntryXml (entry, isMonoAndroid));
		}
	}

	static void ImportReleaseData (XElement root, TypeMapObjectsXmlFile file)
	{
		var module = root.Element ("module");

		if (module is null)
			return;

		file.ModuleReleaseData = new ModuleReleaseData {
			AssemblyName = module.GetAttributeOrDefault ("assembly-name", string.Empty),
			Mvid = Guid.Parse (module.GetAttributeOrDefault ("mvid", Guid.Empty.ToString ())),
			MvidBytes = Convert.FromBase64String (module.GetAttributeOrDefault ("mvid-bytes", string.Empty)),
			TypesScratch = new Dictionary<string, TypeMapReleaseEntry> (StringComparer.Ordinal),
			DuplicateTypes = new List<TypeMapReleaseEntry> (),
		};

		if (module.Element ("types") is XElement types)
			file.ModuleReleaseData.Types = types.Elements ("entry")
				.Select (FromReleaseEntryXml)
				.ToArray ();

		if (module.Element ("duplicates") is XElement duplicates)
			file.ModuleReleaseData.DuplicateTypes.AddRange (duplicates.Elements ("entry")
				.Select (FromReleaseEntryXml));

		if (module.Element ("types-scratch") is XElement typesScratch)
			file.ModuleReleaseData.TypesScratch.AddRange (typesScratch.Elements ("entry")
				.Select (elem => new KeyValuePair<string, TypeMapReleaseEntry> (elem.GetAttributeOrDefault ("key", string.Empty), FromReleaseEntryXml (elem))));
	}

	public static void WriteEmptyFile (string destination, TaskLoggingHelper log)
	{
		log.LogDebugMessage ($"Writing empty file '{destination}'");

		// We write a zero byte file to indicate the file couldn't have JLO types and wasn't scanned
		File.Create (destination).Dispose ();
	}

	static TypeMapDebugEntry FromDebugEntryXml (XElement entry, bool isMonoAndroid)
	{
		var javaName = entry.GetAttributeOrDefault ("java-name", string.Empty);
		var managedName = entry.GetAttributeOrDefault ("managed-name", string.Empty);
		var skipInJavaToManaged = entry.GetAttributeOrDefault ("skip-in-java-to-managed", false);
		var isInvoker = entry.GetAttributeOrDefault ("is-invoker", false);

		return new TypeMapDebugEntry {
			JavaName = javaName,
			ManagedName = managedName,
			SkipInJavaToManaged = skipInJavaToManaged,
			IsInvoker = isInvoker,
			IsMonoAndroid = isMonoAndroid,
		};
	}

	static TypeMapReleaseEntry FromReleaseEntryXml (XElement entry)
	{
		var javaName = entry.GetAttributeOrDefault ("java-name", string.Empty);
		var managedTypeName = entry.GetAttributeOrDefault ("managed-type-name", string.Empty);
		var token = entry.GetAttributeOrDefault ("token", 0u);
		var skipInJavaToManaged = entry.GetAttributeOrDefault ("skip-in-java-to-managed", false);

		return new TypeMapReleaseEntry {
			JavaName = javaName,
			ManagedTypeName = managedTypeName,
			Token = token,
			SkipInJavaToManaged = skipInJavaToManaged,
		};
	}
}
