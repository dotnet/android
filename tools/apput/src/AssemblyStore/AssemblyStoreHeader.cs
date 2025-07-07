namespace ApplicationUtility;

/// <summary>
/// Represents a high-level description of the store hader. It means that this class
/// does **not** correspond to a physical format of the header in the store file. Instead,
/// it contains all the information gathered from the physical file, in a forward compatible
/// way. Forward compatibility means that all public the properties are virtual and nullable,
/// since it's possible that some of them will not be present in the future revisions of the
/// on-disk structure. No public property shall be removed, but any and all of them may be
/// `null` for any given version of the assembly store format. The only exception to this rule
/// is the `Version` property, since it is expected to be present in one way or another in all
/// the future format revisions.
/// </summary>
class AssemblyStoreHeader
{
	public AssemblyStoreVersion Version { get; protected set; }
	public uint? EntryCount { get; protected set; }
	public uint? IndexEntryCount { get; protected set; }
	public uint? IndexSize { get; protected set; }
}
