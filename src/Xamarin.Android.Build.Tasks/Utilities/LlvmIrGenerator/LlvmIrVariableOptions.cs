namespace Xamarin.Android.Tasks.LLVMIR
{
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

		public LlvmIrLinkage Linkage { get; set; } = LlvmIrLinkage.Default;
		public LlvmIrRuntimePreemption RuntimePreemption { get; set; } = LlvmIrRuntimePreemption.DSOLocal;
		public LlvmIrVisibility Visibility { get; set; } = LlvmIrVisibility.Default;
		public LlvmIrAddressSignificance AddressSignificance { get; set; } = LlvmIrAddressSignificance.Default;
		public LlvmIrWritability Writability { get; set; } = LlvmIrWritability.Writable;

		public bool IsGlobal => Linkage == LlvmIrLinkage.Default || (Linkage != LlvmIrLinkage.Private && Linkage != LlvmIrLinkage.Internal);
	}
}
