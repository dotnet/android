using System.Collections.Generic;
using System.IO;

using ELFSharp.ELF.Sections;

namespace ApplicationUtility;

/// <summary>
/// Abstract base for reading ELF relocation-with-addend sections.
/// </summary>
/// <typeparam name="TUnsigned">The unsigned integer type matching the ELF class.</typeparam>
/// <typeparam name="TSigned">The signed integer type matching the ELF class.</typeparam>
/// <typeparam name="TRela">The concrete <see cref="ELF_Rela{TUnsigned,TSigned}"/> type.</typeparam>
abstract class RelocationSectionAddend<TUnsigned, TSigned, TRela>
	where TUnsigned: notnull
	where TRela: notnull, ELF_Rela<TUnsigned, TSigned>
{
	public abstract Dictionary<TUnsigned, TRela> Entries { get; }
}

/// <summary>
/// Reads and stores 64-bit ELF relocation-with-addend entries from a <c>.rela.dyn</c> section.
/// </summary>
class RelocationSectionAddend64 : RelocationSectionAddend<ulong, long, ELF64_Rela>
{
	public override Dictionary<ulong, ELF64_Rela> Entries { get; } = new Dictionary<ulong, ELF64_Rela> ();

	public RelocationSectionAddend64 (Section<ulong> relaDynSection)
	{
		byte[] data = relaDynSection.GetContents ();
		using var stream = new MemoryStream (data);
		using var reader = new BinaryReader (stream);

		while (stream.Position < stream.Length) {
			var entry = new ELF64_Rela (reader, (ulong)stream.Position);
			Entries.Add (entry.r_offset, entry);
		}
	}
}
