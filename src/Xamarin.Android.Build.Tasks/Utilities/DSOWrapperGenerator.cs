using System;

namespace Xamarin.Android.Tasks;

/// <summary>
/// <para>
/// Puts passed files inside a real ELF shared library so that they
/// pass scrutiny when examined.  The payload is placed inside its own
/// section of a file, so the entire file is a 100% valid ELF image.
/// </para>
///
/// <para>
/// The generated files have their payload section positioned at the offset of
/// 16k (0x4000) from the beginning of file.  It's done this way because it not
/// only gives us enough room for the stub part of the ELF image to precede that
/// offset, but it also complies with Google policy of aligning to 16k **and**
/// is still nicely aligned to a 4k boundary on 32-bit systems.  This helps mmapping
/// the section on both 64-bit and 32-bit systems.
/// </para>
/// </summary>
/// <remarks>
/// The generated file **MUST NOT** be stripped with `llvm-strip` etc,
/// as it will remove the payload together with other sections it deems
/// unnecessary.
/// </remarks>
class DSOWrapperGenerator
{
	public static string WrapIt (string payloadFilePath)
	{
		return String.Empty;
	}
}
