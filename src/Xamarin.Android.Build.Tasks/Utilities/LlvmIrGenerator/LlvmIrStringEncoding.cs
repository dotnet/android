#nullable enable
namespace Xamarin.Android.Tasks.LLVMIR;

/// <summary>
/// Specifies the encoding used for string variables in LLVM IR.
/// </summary>
enum LlvmIrStringEncoding
{
	/// <summary>
	/// UTF-8 encoding using i8 LLVM IR type.
	/// </summary>
	UTF8,
	/// <summary>
	/// Unicode (UTF-16) encoding using i16 LLVM IR type.
	/// </summary>
	Unicode,
}
