namespace ApplicationUtility;

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
