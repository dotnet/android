namespace ApplicationUtility;

class AssemblyStoreVersion
{
	public uint MainVersion     { get; }
	public AssemblyStoreABI ABI { get; }
	public bool Is64Bit         { get; }

	internal AssemblyStoreVersion (uint mainVersion, AssemblyStoreABI abi, bool is64Bit)
	{
		MainVersion = mainVersion;
		ABI = abi;
		Is64Bit = is64Bit;
	}
}
