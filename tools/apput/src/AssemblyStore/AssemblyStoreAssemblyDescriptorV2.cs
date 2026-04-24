namespace ApplicationUtility;

/// <summary>
/// Assembly descriptor for version 2 of the assembly store format. Contains offsets and sizes
/// for assembly data, debug data, and config data.
/// </summary>
class AssemblyStoreAssemblyDescriptorV2 : AssemblyStoreAssemblyDescriptor
{
	public uint MappingIndex     { get; internal set; }
	public uint DataOffset       { get; internal set; }
	public uint DataSize         { get; internal set; }
	public uint DebugDataOffset  { get; internal set; }
	public uint DebugDataSize    { get; internal set; }
	public uint ConfigDataOffset { get; internal set; }
	public uint ConfigDataSize   { get; internal set; }
}
