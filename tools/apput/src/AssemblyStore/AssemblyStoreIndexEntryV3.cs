namespace ApplicationUtility;

/// <summary>
/// Version 3 assembly store index entry. Maps a name hash to a descriptor index
/// and indicates whether the assembly should be ignored on load.
/// </summary>
class AssemblyStoreIndexEntryV3 : AssemblyStoreIndexEntry
{
	public ulong NameHash { get; }
	public uint DescriptorIndex { get; }
	public bool Ignore { get; }

	public AssemblyStoreIndexEntryV3 (ulong nameHash, uint descriptorIndex, byte ignore)
	{
		NameHash = nameHash;
		DescriptorIndex = descriptorIndex;
		Ignore = ignore != 0;
	}
}
