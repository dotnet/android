namespace Xamarin.Android.Tasks.LLVMIR
{
	class LlvmIrVariableOptions
	{
		public static readonly LlvmIrVariableOptions Default = new LlvmIrVariableOptions ();

		public static readonly LlvmIrVariableOptions GlobalConstant = new LlvmIrVariableOptions {
			Writability = LlvmIrWritability.Constant,
			AddressSignificance = LlvmIrAddressSignificance.LocalUnnamed,
		};

		public static readonly LlvmIrVariableOptions LocalConstant = new LlvmIrVariableOptions {
			Linkage = LlvmIrLinkage.Internal,
			Writability = LlvmIrWritability.Constant,
		};

		public static readonly LlvmIrVariableOptions LocalString = new LlvmIrVariableOptions {
			Linkage = LlvmIrLinkage.Private,
			Writability = LlvmIrWritability.Constant,
			AddressSignificance = LlvmIrAddressSignificance.Unnamed,
		};

		public static readonly LlvmIrVariableOptions LocalConstexprString = new LlvmIrVariableOptions {
			Linkage = LlvmIrLinkage.Internal,
			Writability = LlvmIrWritability.Constant,
		};

		public static readonly LlvmIrVariableOptions GlobalConstexprString = new LlvmIrVariableOptions {
			Writability = LlvmIrWritability.Constant,
		};

		public static readonly LlvmIrVariableOptions GlobalConstantStringPointer = new LlvmIrVariableOptions {
			Writability = LlvmIrWritability.Constant,
			AddressSignificance = LlvmIrAddressSignificance.LocalUnnamed,
		};

		public LlvmIrLinkage Linkage { get; set; } = LlvmIrLinkage.Default;
		public LlvmIrRuntimePreemption RuntimePreemption { get; set; } = LlvmIrRuntimePreemption.Default;
		public LlvmIrVisibility Visibility { get; set; } = LlvmIrVisibility.Default;
		public LlvmIrAddressSignificance AddressSignificance { get; set; } = LlvmIrAddressSignificance.Default;
		public LlvmIrWritability Writability { get; set; } = LlvmIrWritability.Writable;

		public bool IsGlobal => Linkage == LlvmIrLinkage.Default || (Linkage != LlvmIrLinkage.Private && Linkage != LlvmIrLinkage.Internal);
	}
}
