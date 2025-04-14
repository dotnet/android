using System;
using System.Collections.Generic;

namespace Xamarin.Android.Tasks.LLVMIR
{
	partial class LlvmIrGenerator
	{
		// https://llvm.org/docs/LangRef.html#linkage-types
		static readonly Dictionary<LlvmIrLinkage, string> llvmLinkage = new Dictionary<LlvmIrLinkage, string> {
			{ LlvmIrLinkage.Default, String.Empty },
			{ LlvmIrLinkage.Private, "private" },
			{ LlvmIrLinkage.Internal, "internal" },
			{ LlvmIrLinkage.AvailableExternally, "available_externally" },
			{ LlvmIrLinkage.LinkOnce, "linkonce" },
			{ LlvmIrLinkage.Weak, "weak" },
			{ LlvmIrLinkage.Common, "common" },
			{ LlvmIrLinkage.Appending, "appending" },
			{ LlvmIrLinkage.ExternWeak, "extern_weak" },
			{ LlvmIrLinkage.LinkOnceODR, "linkonce_odr" },
			{ LlvmIrLinkage.External, "external" },
		};

		// https://llvm.org/docs/LangRef.html#runtime-preemption-specifiers
		static readonly Dictionary<LlvmIrRuntimePreemption, string> llvmRuntimePreemption = new Dictionary<LlvmIrRuntimePreemption, string> {
			{ LlvmIrRuntimePreemption.Default, String.Empty },
			{ LlvmIrRuntimePreemption.DSOPreemptable, "dso_preemptable" },
			{ LlvmIrRuntimePreemption.DSOLocal, "dso_local" },
		};

		// https://llvm.org/docs/LangRef.html#visibility-styles
		static readonly Dictionary<LlvmIrVisibility, string> llvmVisibility = new Dictionary<LlvmIrVisibility, string> {
			{ LlvmIrVisibility.Default, "default" },
			{ LlvmIrVisibility.Hidden, "hidden" },
			{ LlvmIrVisibility.Protected, "protected" },
		};

		// https://llvm.org/docs/LangRef.html#global-variables
		static readonly Dictionary<LlvmIrAddressSignificance, string> llvmAddressSignificance = new Dictionary<LlvmIrAddressSignificance, string> {
			{ LlvmIrAddressSignificance.Default, String.Empty },
			{ LlvmIrAddressSignificance.Unnamed, "unnamed_addr" },
			{ LlvmIrAddressSignificance.LocalUnnamed, "local_unnamed_addr" },
		};

		// https://llvm.org/docs/LangRef.html#global-variables
		static readonly Dictionary<LlvmIrWritability, string> llvmWritability = new Dictionary<LlvmIrWritability, string> {
			{ LlvmIrWritability.Constant, "constant" },
			{ LlvmIrWritability.Writable, "global" },
		};

		// https://llvm.org/docs/LangRef.html#callingconv
		static readonly Dictionary<LlvmIrCallingConvention, string> llvmCallingConvention = new () {
			{ LlvmIrCallingConvention.Ccc, "ccc" },
			{ LlvmIrCallingConvention.Fastcc, "fastcc" },
			{ LlvmIrCallingConvention.Tailcc, "tailcc" },
		};
	}
}
