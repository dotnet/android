namespace tmt;

class ELF64RelocationAddend
{
	public ulong Offset { get; }
	public ulong Info   { get; }
	public long Addend  { get; }

	public ELF64RelocationAddend (ulong offset, ulong info, long addend)
	{
		Offset = offset;
		Info = info;
		Addend = addend;
	}
}
