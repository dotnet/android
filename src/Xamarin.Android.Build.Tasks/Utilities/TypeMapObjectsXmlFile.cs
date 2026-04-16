using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Utilities;

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
		xml.WriteAttributeString ("managed-type-token-id", entry.ManagedTypeTokenId.ToString (CultureInfo.InvariantCulture));
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

		using var reader = XmlReader.Create (filename);

		if (!reader.ReadToFollowing ("api"))
			throw new InvalidOperationException ($"Invalid XML file '{filename}'");

		var type = reader.GetAttribute ("type") ?? throw new InvalidOperationException ("Missing required attribute 'type'");
		var assemblyName = reader.GetAttribute ("assembly-name");
		var mvid = Guid.TryParse (reader.GetAttribute ("mvid"), out var parsedMvid) ? parsedMvid : Guid.Empty;
		var foundJniNativeRegistration = bool.TryParse (reader.GetAttribute ("found-jni-native-registration"), out var parsedJni) && parsedJni;

		var file = new TypeMapObjectsXmlFile {
			WasScanned = true,
			AssemblyName = assemblyName,
			AssemblyMvid = mvid,
			FoundJniNativeRegistration = foundJniNativeRegistration,
		};

		if (type == "debug")
			ImportDebugData (reader, file);
		else if (type == "release")
			ImportReleaseData (reader, file);

		return file;
	}

	static void ImportDebugData (XmlReader reader, TypeMapObjectsXmlFile file)
	{
		var assemblyName = file.AssemblyName ?? string.Empty;
		var isMonoAndroid = assemblyName == "Mono.Android";

		while (reader.Read ()) {
			if (reader.NodeType != XmlNodeType.Element)
				continue;

			if (reader.Name == "java-to-managed")
				ReadDebugEntries (reader, file.JavaToManagedDebugEntries, assemblyName, isMonoAndroid);
			else if (reader.Name == "managed-to-java")
				ReadDebugEntries (reader, file.ManagedToJavaDebugEntries, assemblyName, isMonoAndroid);
		}
	}

	static void ReadDebugEntries (XmlReader reader, List<TypeMapDebugEntry> entries, string assemblyName, bool isMonoAndroid)
	{
		if (reader.IsEmptyElement)
			return;

		int depth = reader.Depth;

		while (reader.Read ()) {
			if (reader.NodeType == XmlNodeType.EndElement && reader.Depth == depth)
				return;

			if (reader.NodeType == XmlNodeType.Element && reader.Name == "entry") {
				entries.Add (new TypeMapDebugEntry {
					JavaName = reader.GetAttribute ("java-name") ?? string.Empty,
					ManagedName = reader.GetAttribute ("managed-name") ?? string.Empty,
					ManagedTypeTokenId = uint.TryParse (reader.GetAttribute ("managed-type-token-id"), out var tokenId) ? tokenId : 0u,
					SkipInJavaToManaged = bool.TryParse (reader.GetAttribute ("skip-in-java-to-managed"), out var skip) && skip,
					IsInvoker = bool.TryParse (reader.GetAttribute ("is-invoker"), out var invoker) && invoker,
					IsMonoAndroid = isMonoAndroid,
					AssemblyName = assemblyName,
				});
			}
		}
	}

	static void ImportReleaseData (XmlReader reader, TypeMapObjectsXmlFile file)
	{
		if (!reader.ReadToFollowing ("module"))
			return;

		file.ModuleReleaseData = new ModuleReleaseData {
			AssemblyName = reader.GetAttribute ("assembly-name") ?? string.Empty,
			Mvid = Guid.TryParse (reader.GetAttribute ("mvid"), out var mvid) ? mvid : Guid.Empty,
			MvidBytes = Convert.FromBase64String (reader.GetAttribute ("mvid-bytes") ?? string.Empty),
			TypesScratch = new Dictionary<string, TypeMapReleaseEntry> (StringComparer.Ordinal),
			DuplicateTypes = new List<TypeMapReleaseEntry> (),
		};

		if (reader.IsEmptyElement)
			return;

		int depth = reader.Depth;

		while (reader.Read ()) {
			if (reader.NodeType == XmlNodeType.EndElement && reader.Depth == depth)
				return;

			if (reader.NodeType != XmlNodeType.Element)
				continue;

			switch (reader.Name) {
				case "types":
					var types = new List<TypeMapReleaseEntry> ();
					ReadReleaseEntries (reader, types);
					file.ModuleReleaseData.Types = types.ToArray ();
					break;
				case "duplicates":
					ReadReleaseEntries (reader, file.ModuleReleaseData.DuplicateTypes);
					break;
				case "types-scratch":
					ReadReleaseScratchEntries (reader, file.ModuleReleaseData.TypesScratch);
					break;
			}
		}
	}

	static void ReadReleaseEntries (XmlReader reader, List<TypeMapReleaseEntry> entries)
	{
		if (reader.IsEmptyElement)
			return;

		int depth = reader.Depth;

		while (reader.Read ()) {
			if (reader.NodeType == XmlNodeType.EndElement && reader.Depth == depth)
				return;

			if (reader.NodeType == XmlNodeType.Element && reader.Name == "entry")
				entries.Add (ReadReleaseEntry (reader));
		}
	}

	static void ReadReleaseScratchEntries (XmlReader reader, Dictionary<string, TypeMapReleaseEntry> entries)
	{
		if (reader.IsEmptyElement)
			return;

		int depth = reader.Depth;

		while (reader.Read ()) {
			if (reader.NodeType == XmlNodeType.EndElement && reader.Depth == depth)
				return;

			if (reader.NodeType == XmlNodeType.Element && reader.Name == "entry") {
				var key = reader.GetAttribute ("key") ?? string.Empty;
				entries[key] = ReadReleaseEntry (reader);
			}
		}
	}

	static TypeMapReleaseEntry ReadReleaseEntry (XmlReader reader) => new TypeMapReleaseEntry {
		JavaName = reader.GetAttribute ("java-name") ?? string.Empty,
		ManagedTypeName = reader.GetAttribute ("managed-type-name") ?? string.Empty,
		Token = uint.TryParse (reader.GetAttribute ("token"), out var token) ? token : 0u,
		SkipInJavaToManaged = bool.TryParse (reader.GetAttribute ("skip-in-java-to-managed"), out var skip) && skip,
	};

	public static void WriteEmptyFile (string destination, TaskLoggingHelper log)
	{
		log.LogDebugMessage ($"Writing empty file '{destination}'");

		// We write a zero byte file to indicate the file couldn't have JLO types and wasn't scanned
		File.Create (destination).Dispose ();
	}

}
