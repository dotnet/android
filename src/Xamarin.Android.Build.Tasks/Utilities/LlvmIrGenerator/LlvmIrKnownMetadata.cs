#nullable enable
namespace Xamarin.Android.Tasks.LLVMIR;

/// <summary>
/// Contains constants for well-known LLVM IR metadata names.
/// </summary>
sealed class LlvmIrKnownMetadata
{
	/// <summary>
	/// The "llvm.module.flags" metadata name used for module-level flags.
	/// </summary>
	public const string LlvmModuleFlags = "llvm.module.flags";
	/// <summary>
	/// The "llvm.ident" metadata name used for module identification.
	/// </summary>
	public const string LlvmIdent = "llvm.ident";
}
