namespace Xamarin.Android.Tasks.LLVMIR
{
	/// <summary>
	/// Represents options for LLVM IR variables, controlling linkage, visibility, writability, and other attributes.
	/// Provides predefined option sets for common variable types.
	/// </summary>
	class LlvmIrVariableOptions
	{
		/// <summary>
		/// Options for a global, writable, symbol with locally (module wide) unimportant address
		/// </summary>
		public static readonly LlvmIrVariableOptions Default = new LlvmIrVariableOptions {
			Writability = LlvmIrWritability.Writable,
			AddressSignificance = LlvmIrAddressSignificance.LocalUnnamed,
		};

		/// <summary>
		/// Options for a global, read-only, symbol with locally (module wide) unimportant address
		/// </summary>
		public static readonly LlvmIrVariableOptions GlobalConstant = new LlvmIrVariableOptions {
			Writability = LlvmIrWritability.Constant,
			AddressSignificance = LlvmIrAddressSignificance.LocalUnnamed,
		};

		/// <summary>
		/// Options for a global, writable, symbol
		/// </summary>
		public static readonly LlvmIrVariableOptions GlobalWritable = new LlvmIrVariableOptions {
			Writability = LlvmIrWritability.Writable,
			AddressSignificance = LlvmIrAddressSignificance.LocalUnnamed,
		};

		/// <summary>
		/// Options for a local, read-only, symbol
		/// </summary>
		public static readonly LlvmIrVariableOptions LocalConstant = new LlvmIrVariableOptions {
			Linkage = LlvmIrLinkage.Internal,
			Writability = LlvmIrWritability.Constant,
		};

		/// <summary>
		/// Options for a local, writable, symbol
		/// </summary>
		public static readonly LlvmIrVariableOptions LocalWritable = new LlvmIrVariableOptions {
			Linkage = LlvmIrLinkage.Internal,
			Writability = LlvmIrWritability.Writable,
		};

		/// <summary>
		/// Options for a local, writable, insignificant address symbol
		/// </summary>
		public static readonly LlvmIrVariableOptions LocalWritableInsignificantAddr = new LlvmIrVariableOptions {
			Linkage = LlvmIrLinkage.Internal,
			Writability = LlvmIrWritability.Writable,
			AddressSignificance = LlvmIrAddressSignificance.Unnamed,
		};

		/// <summary>
		/// Options for a local, read-only, string which will end up in a strings ELF section
		/// </summary>
		public static readonly LlvmIrVariableOptions LocalString = new LlvmIrVariableOptions {
			Linkage = LlvmIrLinkage.Private,
			Writability = LlvmIrWritability.Constant,
			AddressSignificance = LlvmIrAddressSignificance.Unnamed,
			RuntimePreemption = LlvmIrRuntimePreemption.Default,
		};

		/// <summary>
		/// Options for a local, read-only, C++ constexpr style string which will remain in the rodata ELF section
		/// </summary>
		public static readonly LlvmIrVariableOptions LocalConstexprString = new LlvmIrVariableOptions {
			Linkage = LlvmIrLinkage.Internal,
			Writability = LlvmIrWritability.Constant,
		};

		/// <summary>
		/// Options for a global, read-only, C++ constexpr style string which will remain in the rodata ELF section.
		/// </summary>
		public static readonly LlvmIrVariableOptions GlobalConstexprString = new LlvmIrVariableOptions {
			Writability = LlvmIrWritability.Constant,
		};

		/// <summary>
		/// Options for a global, read-only, constant pointer to string
		/// </summary>
		public static readonly LlvmIrVariableOptions GlobalConstantStringPointer = new LlvmIrVariableOptions {
			Writability = LlvmIrWritability.Constant,
			AddressSignificance = LlvmIrAddressSignificance.LocalUnnamed,
		};

		/// <summary>
		/// Gets or sets the linkage type for the variable.
		/// </summary>
		public LlvmIrLinkage Linkage { get; set; } = LlvmIrLinkage.Default;
		/// <summary>
		/// Gets or sets the runtime preemption setting for the variable.
		/// </summary>
		public LlvmIrRuntimePreemption RuntimePreemption { get; set; } = LlvmIrRuntimePreemption.DSOLocal;
		/// <summary>
		/// Gets or sets the visibility of the variable.
		/// </summary>
		public LlvmIrVisibility Visibility { get; set; } = LlvmIrVisibility.Default;
		/// <summary>
		/// Gets or sets the address significance of the variable.
		/// </summary>
		public LlvmIrAddressSignificance AddressSignificance { get; set; } = LlvmIrAddressSignificance.Default;
		/// <summary>
		/// Gets or sets whether the variable is writable or constant.
		/// </summary>
		public LlvmIrWritability Writability { get; set; } = LlvmIrWritability.Writable;

		/// <summary>
		/// Gets a value indicating whether this variable has global linkage.
		/// </summary>
		public bool IsGlobal => Linkage == LlvmIrLinkage.Default || (Linkage != LlvmIrLinkage.Private && Linkage != LlvmIrLinkage.Internal);
	}
}
