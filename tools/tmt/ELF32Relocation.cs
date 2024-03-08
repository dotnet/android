namespace tmt;

class ELF32Relocation
{
	public uint Offset { get; }
	public uint Info   { get; }

	public ELF32Relocation (uint offset, uint info)
	{
		Offset = offset;
		Info = info;
	}
}
