using System;
using System.IO;

namespace ApplicationUtility;

/// <summary>
/// Abstract representation of an ELF relocation-with-addend (<c>Rela</c>) entry.
/// </summary>
/// <typeparam name="TUnsigned">The unsigned integer type (uint or ulong) matching the ELF class.</typeparam>
/// <typeparam name="TSigned">The signed integer type (int or long) matching the ELF class.</typeparam>
abstract class ELF_Rela<TUnsigned, TSigned>
{
	protected abstract long StructureSize { get; }

	public readonly TUnsigned r_offset;
	public readonly TUnsigned r_info;
	public readonly TSigned   r_addend;

	protected ELF_Rela (BinaryReader reader, ulong offsetIntoData)
	{
		if (reader.BaseStream.Length < (long)offsetIntoData + StructureSize) {
			throw new ArgumentOutOfRangeException ("Data array too short");
		}
		ReadData (reader, out r_offset, out r_info, out r_addend);
	}

	protected abstract void ReadData (BinaryReader reader, out TUnsigned offset, out TUnsigned info, out TSigned addend);

	public override string ToString()
	{
		return $"{GetType ()}: r_offset == 0x{r_offset:x} ({r_offset}); r_info == 0x{r_info:x} ({r_info}); r_addend == 0x{r_addend:x} ({r_addend})";
	}
}

/// <summary>
/// 64-bit ELF relocation-with-addend entry (<c>Elf64_Rela</c>).
/// </summary>
sealed class ELF64_Rela : ELF_Rela<ulong, long>
{
	protected override long StructureSize => 3 * sizeof (ulong);

	public ELF64_Rela (BinaryReader data, ulong offsetIntoData)
		: base (data, offsetIntoData)
	{}

	protected override void ReadData (BinaryReader reader, out ulong offset, out ulong info, out long addend)
	{
		offset = reader.ReadUInt64 ();
		info = reader.ReadUInt64 ();
		addend = reader.ReadInt64 ();
	}
}
