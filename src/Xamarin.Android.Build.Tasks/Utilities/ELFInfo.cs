using System;

using ELFSharp;
using ELFSharp.ELF;
using ELFSharp.ELF.Sections;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

class ELFInfo
{
	public bool HasDebugInfo { get; }
	public AndroidTargetArch Arch { get; }
	public bool Is64Bit { get; }
	public bool Is32Bit => !Is64Bit;

	public ELFInfo (IELF elf)
	{
		if (elf.Class == Class.NotELF) {
			throw new InvalidOperationException ("Internal error: not an ELF image. ELFInfo should have never been instantiated with it.");
		}

		Is64Bit = elf.Class == Class.Bit64;
		HasDebugInfo = HasDebugSymbols (elf);
		Arch = elf.Machine switch {
			Machine.ARM      => AndroidTargetArch.Arm,
                        Machine.Intel386 => AndroidTargetArch.X86,

                        Machine.AArch64  => AndroidTargetArch.Arm64,
                        Machine.AMD64    => AndroidTargetArch.X86_64,

                        _                => throw new NotSupportedException ($"Unsupported ELF architecture '{elf.Machine}'")
		};
	}

	static bool HasDebugSymbols (IELF elf)
	{
		// We look for a section named `.debug_info` or one of SHT_SYMTAB type, whichever comes first.
		foreach (ISection section in elf.Sections) {
			if (section.Type == SectionType.SymbolTable || MonoAndroidHelper.StringEquals (".debug_info", section.Name)) {
				return true;
			}
		}

		return false;
	}
}
