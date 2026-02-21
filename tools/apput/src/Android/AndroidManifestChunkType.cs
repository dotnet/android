namespace ApplicationUtility;

enum AndroidManifestChunkType : ushort
{
	Null              = 0x0000,
	StringPool        = 0x0001,
	Table             = 0x0002,
	Xml               = 0x0003,

	XmlFirstChunk     = 0x0100,
	XmlStartNamespace = 0x0100,
	XmlEndNamespace   = 0x0101,
	XmlStartElement   = 0x0102,
	XmlEndElement     = 0x0103,
	XmlCData          = 0x0104,
	XmlLastChunk      = 0x017f,
	XmlResourceMap    = 0x0180,

	TablePackage      = 0x0200,
	TableType         = 0x0201,
	TableTypeSpec     = 0x0202,
	TableLibrary      = 0x0203,
}
