using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;

namespace ApplicationUtility;

//
// Based on https://github.com/androguard/androguard/tree/832104db3eb5dc3cc66b30883fa8ce8712dfa200/androguard/core/axml
//
class AXMLParser
{
	// Position of fields inside an attribute
	const int ATTRIBUTE_IX_NAMESPACE_URI = 0;
	const int ATTRIBUTE_IX_NAME          = 1;
	const int ATTRIBUTE_IX_VALUE_STRING  = 2;
	const int ATTRIBUTE_IX_VALUE_TYPE    = 3;
	const int ATTRIBUTE_IX_VALUE_DATA    = 4;
	const int ATTRIBUTE_LENGHT           = 5;

	const long MinimumDataSize           = 8;
	const long MaximumDataSize           = (long)UInt32.MaxValue;

	const uint ComplexUnitMask           = 0x0f;

	static readonly float[] RadixMultipliers = {
		0.00390625f,
		3.051758E-005f,
		1.192093E-007f,
		4.656613E-010f,
	};

	static readonly string[] DimensionUnits = {
		"px",
		"dip",
		"sp",
		"pt",
		"in",
		"mm",
	};

	static readonly string[] FractionUnits = {
		"%",
		"%p",
	};

	Stream data;
	long dataSize;
	ARSCHeader axmlHeader;
	uint fileSize;
	AndroidManifestStringBlock stringPool;
	bool valid = true;
	long initialPosition;

	public bool IsValid => valid;

	public AXMLParser (Stream data)
	{
		this.data = data;
		dataSize = data.Length;

		// Minimum is a single ARSCHeader, which would be a strange edge case...
		if (dataSize < MinimumDataSize) {
			throw new InvalidDataException ($"Input data size too small for it to be valid AXML content ({dataSize} < {MinimumDataSize})");
		}

		// This would be even stranger, if an AXML file is larger than 4GB...
		// But this is not possible as the maximum chunk size is a unsigned 4 byte int.
		if (dataSize > MaximumDataSize) {
			throw new InvalidDataException ($"Input data size too large for it to be a valid AXML content ({dataSize} > {MaximumDataSize})");
		}

		try {
			axmlHeader = new ARSCHeader (data);
		} catch (Exception) {
			Log.Error ("Error parsing the first data header");
			throw;
		}

		if (axmlHeader.HeaderSize != 8) {
			throw new InvalidDataException ($"This does not look like AXML data. header size does not equal 8. header size = {axmlHeader.Size}");
		}

		fileSize = axmlHeader.Size;
		if (fileSize > dataSize) {
			throw new InvalidDataException ($"This does not look like AXML data. Declared data size does not match real size: {fileSize} vs {dataSize}");
		}

		if (fileSize < dataSize) {
			Log.Warning ($"Declared data size ({fileSize}) is smaller than total data size ({dataSize}). Was something appended to the file? Trying to parse it anyways.");
		}

		if (axmlHeader.Type != AndroidManifestChunkType.Xml) {
			Log.Warning ($"AXML file has an unusual resource type, trying to parse it anyways. Resource Type: 0x{(ushort)axmlHeader.Type:04x}");
		}

		ARSCHeader stringPoolHeader = new ARSCHeader (data, AndroidManifestChunkType.StringPool);
		if (stringPoolHeader.HeaderSize != 28) {
			throw new InvalidDataException ($"This does not look like an AXML file. String chunk header size does not equal 28. Header size = {stringPoolHeader.Size}");
		}

		stringPool = new AndroidManifestStringBlock (data, stringPoolHeader);
		initialPosition = data.Position;
	}

	public XmlDocument? Parse ()
	{
		valid = true;

		XmlDocument ret = new XmlDocument ();
		XmlDeclaration declaration = ret.CreateXmlDeclaration ("1.0", stringPool.IsUTF8 ? "UTF-8" : "UTF-16", null);
		ret.InsertBefore (declaration, ret.DocumentElement);

		using var reader = Utilities.GetReaderAndRewindStream (data);
		ARSCHeader? header;
		string? nsPrefix = null;
		string? nsUri = null;
		uint prefixIndex = 0;
		uint uriIndex = 0;
		var nsUriToPrefix = new Dictionary<string, string> (StringComparer.Ordinal);
		XmlNode? currentNode = ret.DocumentElement;

		while (data.Position < dataSize) {
			header = new ARSCHeader (data);

			// Special chunk: Resource Map. This chunk might follow the string pool.
			if (header.Type == AndroidManifestChunkType.XmlResourceMap) {
				if (!SkipOverResourceMap (header, reader)) {
					valid = false;
					break;
				}
				continue;
			}

			// XML chunks

			// Skip over unknown types
			if (!Enum.IsDefined (typeof(AndroidManifestChunkType), header.TypeRaw)) {
				Log.Warning ($"Unknown chunk type 0x{header.TypeRaw:x} at offset {data.Position}. Skipping over {header.Size} bytes");
				data.Seek (header.Size, SeekOrigin.Current);
				continue;
			}

			// Check that we read a correct header
			if (header.HeaderSize != 16) {
				Log.Warning ($"XML chunk header size is not 16. Chunk type {header.Type} (0x{header.TypeRaw:x}), chunk size {header.Size}");
				data.Seek (header.Size, SeekOrigin.Current);
				continue;
			}

			// Line Number of the source file, only used as meta information
			uint lineNumber = reader.ReadUInt32 ();

			// Comment_Index (usually 0xffffffff)
			uint commentIndex = reader.ReadUInt32 ();

			if (commentIndex != 0xffffffff && (header.Type == AndroidManifestChunkType.XmlStartNamespace || header.Type == AndroidManifestChunkType.XmlEndNamespace)) {
				Log.Warning ($"Unhandled Comment at namespace chunk: {commentIndex}");
			}

			if (header.Type == AndroidManifestChunkType.XmlStartNamespace) {
				prefixIndex = reader.ReadUInt32 ();
				uriIndex = reader.ReadUInt32 ();

				nsPrefix = stringPool.GetString (prefixIndex);
				nsUri = stringPool.GetString (uriIndex);

				if (!String.IsNullOrEmpty (nsUri)) {
					nsUriToPrefix[nsUri] = nsPrefix ?? String.Empty;
				}

				Log.Debug ($"Start of Namespace mapping: prefix {prefixIndex}: '{nsPrefix}' --> uri {uriIndex}: '{nsUri}'");

				if (String.IsNullOrEmpty (nsUri)) {
					Log.Warning ($"Namespace prefix '{nsPrefix}' resolves to empty URI.");
				}

				continue;
			}

			if (header.Type == AndroidManifestChunkType.XmlEndNamespace) {
				// Namespace handling is **really** simplified, since we expect to deal only with AndroidManifest.xml which should have just one namespace.
				// There should be no problems with that. Famous last words.
				uint endPrefixIndex = reader.ReadUInt32 ();
				uint endUriIndex = reader.ReadUInt32 ();

				Log.Debug ($"End of Namespace mapping: prefix {endPrefixIndex}, uri {endUriIndex}");
				if (endPrefixIndex != prefixIndex) {
					Log.Warning ($"Prefix index of Namespace end doesn't match the last Namespace prefix index: {prefixIndex} != {endPrefixIndex}");
				}

				if (endUriIndex != uriIndex) {
					Log.Warning ($"URI index of Namespace end doesn't match the last Namespace URI index: {uriIndex} != {endUriIndex}");
				}

				string? endUri = stringPool.GetString (endUriIndex);
				if (!String.IsNullOrEmpty (endUri) && nsUriToPrefix.ContainsKey (endUri)) {
					nsUriToPrefix.Remove (endUri);
				}

				nsPrefix = null;
				nsUri = null;
				prefixIndex = 0;
				uriIndex = 0;

				continue;
			}

			uint tagNsUriIndex;
			uint tagNameIndex;
			string? tagName;
//			string? tagNs; // TODO: implement

			if (header.Type == AndroidManifestChunkType.XmlStartElement) {
				// The TAG consists of some fields:
				// * (chunk_size, line_number, comment_index - we read before)
				// * namespace_uri
				// * name
				// * flags
				// * attribute_count
				// * class_attribute
				// After that, there are two lists of attributes, 20 bytes each
				tagNsUriIndex = reader.ReadUInt32 ();
				tagNameIndex = reader.ReadUInt32 ();
				uint tagFlags = reader.ReadUInt32 ();
				uint attributeCount = reader.ReadUInt32 () & 0xffff;
				uint classAttribute = reader.ReadUInt32 ();

				// Tag name is, of course, required but instead of throwing an exception should we find none, we use a fake name in hope that we can still salvage
				// the document.
				tagName = stringPool.GetString (tagNameIndex) ?? "unnamedTag";
				Log.Debug ($"Start of tag '{tagName}', NS URI index {tagNsUriIndex}");
				Log.Debug ($"Reading tag attributes ({attributeCount}):");

				string? tagNsUri = tagNsUriIndex != 0xffffffff ? stringPool.GetString (tagNsUriIndex) : null;
				string? tagNsPrefix;

				if (String.IsNullOrEmpty (tagNsUri) || !nsUriToPrefix.TryGetValue (tagNsUri, out tagNsPrefix)) {
					tagNsPrefix = null;
				}

				XmlElement element = ret.CreateElement (tagNsPrefix, tagName, tagNsUri);
				if (currentNode == null) {
					ret.AppendChild (element);
					if (!String.IsNullOrEmpty (nsPrefix) && !String.IsNullOrEmpty (nsUri)) {
						ret.DocumentElement!.SetAttribute ($"xmlns:{nsPrefix}", nsUri);
					}
				} else {
					currentNode.AppendChild (element);
				}
				currentNode = element;

				for (uint i = 0; i < attributeCount; i++) {
					uint attrNsIdx = reader.ReadUInt32 (); // string index
					uint attrNameIdx = reader.ReadUInt32 (); // string index
					uint attrValue = reader.ReadUInt32 ();
					uint attrType = reader.ReadUInt32 () >> 24;
					uint attrData = reader.ReadUInt32 ();

					string? attrNs = attrNsIdx != 0xffffffff ? stringPool.GetString (attrNsIdx) : String.Empty;
					string? attrName = stringPool.GetString (attrNameIdx);

					if (String.IsNullOrEmpty (attrName)) {
						Log.Warning ($"Attribute without name, ignoring. Offset: {data.Position}");
						continue;
					}

					Log.Debug ($"  '{attrName}': ns == '{attrNs}'; value == 0x{attrValue:x}; type == 0x{attrType:x}; data == 0x{attrData:x}");
					XmlAttribute attr;

					if (!String.IsNullOrEmpty (attrNs)) {
						attr = ret.CreateAttribute (nsUriToPrefix[attrNs], attrName, attrNs);
					} else {
						attr = ret.CreateAttribute (attrName!);
					}
					attr.Value = GetAttributeValue (attrValue, attrType, attrData);
					element.SetAttributeNode (attr);
				}
				continue;
			}

			if (header.Type == AndroidManifestChunkType.XmlEndElement) {
				tagNsUriIndex = reader.ReadUInt32 ();
				tagNameIndex = reader.ReadUInt32 ();

				tagName = stringPool.GetString (tagNameIndex);
				Log.Debug ($"End of tag '{tagName}', NS URI index {tagNsUriIndex}");
				currentNode = currentNode?.ParentNode!;
				continue;
			}

			// TODO: add support for CDATA
		}

		return ret;
	}

	string GetAttributeValue (uint attrValue, uint attrType, uint attrData)
	{
		if (!Enum.IsDefined (typeof(AndroidManifestAttributeType), attrType)) {
			Log.Warning ($"Unknown attribute type value 0x{attrType:x}, returning empty attribute value (data == 0x{attrData:x}). Offset: {data.Position}");
			return String.Empty;
		}

		switch ((AndroidManifestAttributeType)attrType) {
			case AndroidManifestAttributeType.Null:
				return attrData == 0 ? "?NULL?" : String.Empty;

			case AndroidManifestAttributeType.Reference:
				return $"@{MaybePrefix()}{attrData:x08}";

			case AndroidManifestAttributeType.Attribute:
				return $"?{MaybePrefix()}{attrData:x08}";

			case AndroidManifestAttributeType.String:
				return stringPool.GetString (attrData) ?? String.Empty;

			case AndroidManifestAttributeType.Float:
				return $"{(float)attrData}";

			case AndroidManifestAttributeType.Dimension:
				return $"{ComplexToFloat(attrData)}{DimensionUnits[attrData & ComplexUnitMask]}";

			case AndroidManifestAttributeType.Fraction:
				return $"{ComplexToFloat(attrData) * 100.0f}{FractionUnits[attrData & ComplexUnitMask]}";

			case AndroidManifestAttributeType.IntDec:
				return attrData.ToString ();

			case AndroidManifestAttributeType.IntHex:
				return $"0x{attrData:X08}";

			case AndroidManifestAttributeType.IntBoolean:
				return attrData == 0 ? "false" : "true";

			case AndroidManifestAttributeType.IntColorARGB8:
			case AndroidManifestAttributeType.IntColorRGB8:
			case AndroidManifestAttributeType.IntColorARGB4:
			case AndroidManifestAttributeType.IntColorRGB4:
				return $"#{attrData:X08}";
		}

		return String.Empty;

		string MaybePrefix ()
		{
			if (attrData >> 24 == 1) {
				return "android:";
			}
			return String.Empty;
		}

		float ComplexToFloat (uint value)
		{
			return (float)(value & 0xffffff00) * RadixMultipliers[(value >> 4) & 3];
		}
	}

	bool SkipOverResourceMap (ARSCHeader header, BinaryReader reader)
	{
		Log.Debug ("AXML contains a resource map");

		// Check size: < 8 bytes mean that the chunk is not complete
		// Should be aligned to 4 bytes.
		if (header.Size < 8 || (header.Size % 4) != 0) {
			Log.Error ("Invalid chunk size in chunk XML_RESOURCE_MAP");
			return false;
		}

		// Since our main interest is in reading AndroidManifest.xml, we're going to skip over the table
		for (int i = 0; i < (header.Size - header.HeaderSize) / 4; i++) {
			reader.ReadUInt32 ();
		}

		return true;
	}
}
