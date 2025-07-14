using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks.LLVMIR;

/// <summary>
/// Abstract base class for LLVM IR module targets that define architecture-specific code generation settings.
/// Each target architecture implements this class to provide specific data layout, triple, and compilation settings.
/// </summary>
abstract class LlvmIrModuleTarget
{
	/// <summary>
	/// Gets the data layout specification for this target architecture.
	/// </summary>
	public abstract LlvmIrDataLayout DataLayout  { get; }
	/// <summary>
	/// Gets the LLVM target triple for this architecture.
	/// </summary>
	public abstract string Triple                { get; }
	/// <summary>
	/// Gets the Android target architecture this module target represents.
	/// </summary>
	public abstract AndroidTargetArch TargetArch { get; }
	/// <summary>
	/// Gets the size of native pointers in bytes for this target architecture.
	/// </summary>
	public abstract uint NativePointerSize       { get; }
	/// <summary>
	/// Gets a value indicating whether this target architecture is 64-bit.
	/// </summary>
	public abstract bool Is64Bit                 { get; }

	/// <summary>
	/// Adds target-specific attributes which are common to many attribute sets. Usually this specifies CPU type, tuning and
	/// features.
	/// </summary>
	/// <param name="attrSet">The function attribute set to add target-specific attributes to.</param>
	public virtual void AddTargetSpecificAttributes (LlvmIrFunctionAttributeSet attrSet)
	{}

	/// <summary>
	/// Adds target-specific metadata to the metadata manager.
	/// </summary>
	/// <param name="manager">The metadata manager to add target-specific metadata to.</param>
	public virtual void AddTargetSpecificMetadata (LlvmIrMetadataManager manager)
	{}

	/// <summary>
	/// Sets target-specific parameter flags for function parameters.
	/// </summary>
	/// <param name="parameter">The function parameter to set flags on.</param>
	public virtual void SetParameterFlags (LlvmIrFunctionParameter parameter)
	{
		if (!parameter.NoUndef.HasValue) {
			parameter.NoUndef = true;
		}
	}

	/// <summary>
	/// Sets the <c>zeroext</c> or <c>signext</c> attributes on the parameter, if not set previously and if
	/// the parameter is a small integral type.  Out of our supported architectures, all except AArch64 set
	/// the flags, thus the reason to put this method in the base class.
	/// </summary>
	/// <param name="parameter">The function parameter to potentially set upcasting flags on.</param>
	protected void SetIntegerParameterUpcastFlags (LlvmIrFunctionParameter parameter)
	{
		if (parameter.Type == typeof(bool) ||
		    parameter.Type == typeof(byte) ||
		    parameter.Type == typeof(char) ||
		    parameter.Type == typeof(ushort))
		{
			if (!parameter.ZeroExt.HasValue) {
				parameter.ZeroExt = true;
				parameter.SignExt = false;
			}
			return;
		}

		if (parameter.Type == typeof(sbyte) ||
		    parameter.Type == typeof(short))
		{
			if (!parameter.SignExt.HasValue) {
				parameter.SignExt = true;
				parameter.ZeroExt = false;
			}
		}
	}

	/// <summary>
	/// Gets the alignment for aggregate objects (structures, arrays) based on the maximum field alignment and data size.
	/// </summary>
	/// <param name="maxFieldAlignment">The maximum alignment requirement of any field in the aggregate.</param>
	/// <param name="dataSize">The total size of the aggregate data.</param>
	/// <returns>The alignment to use for the aggregate object.</returns>
	public virtual int GetAggregateAlignment (int maxFieldAlignment, ulong dataSize)
	{
		return maxFieldAlignment;
	}

	/// <summary>
	/// Gets or creates the module flags metadata item for this target.
	/// </summary>
	/// <param name="manager">The metadata manager to get the flags from.</param>
	/// <returns>The module flags metadata item.</returns>
	protected LlvmIrMetadataItem GetFlagsMetadata (LlvmIrMetadataManager manager)
	{
		LlvmIrMetadataItem? flags = manager.GetItem (LlvmIrKnownMetadata.LlvmModuleFlags);
		if (flags == null) {
			flags = manager.Add (LlvmIrKnownMetadata.LlvmModuleFlags);
		}

		return flags;
	}
}
