using System;
using System.Collections.Generic;

using Xamarin.Android.Tools;
using Xamarin.Android.Tasks.LLVM.IR;

namespace Xamarin.Android.Tasks.New
{
	// TODO: remove these aliases once everything is migrated to the LLVM.IR namespace
	using LlvmIrAddressSignificance = LLVMIR.LlvmIrAddressSignificance;

	partial class MarshalMethodsNativeAssemblyGenerator
	{
		readonly MarshalMethodsTracingMode tracingMode;

		LlvmIrFunction? mm_trace_func_enter;
		LlvmIrFunction? mm_trace_func_leave;
		LlvmIrFunction? llvm_lifetime_start;
		LlvmIrFunction? llvm_lifetime_end;

		void InitTracing (LlvmIrModule module)
		{
			var llvmFunctionsAttributeSet = module.AddAttributeSet (MakeLlvmIntrinsicFunctionsAttributeSet (module));
			var traceFunctionsAttributeSet = module.AddAttributeSet (MakeTraceFunctionsAttributeSet (module));

			var llvm_lifetime_params = new List<LlvmIrFunctionParameter> {
				new (typeof(ulong), "size"),
				new (typeof(IntPtr), "pointer"),
			};

			var lifetime_sig = new LlvmIrFunctionSignature (
				name: "llvm.lifetime.start",
				returnType: typeof(void),
				parameters: llvm_lifetime_params
			);

			llvm_lifetime_start = module.DeclareExternalFunction (
				new LlvmIrFunction (lifetime_sig, llvmFunctionsAttributeSet) {
					AddressSignificance = LlvmIrAddressSignificance.Default
				}
			);
			llvm_lifetime_start = module.DeclareExternalFunction (
				new LlvmIrFunction ("llvm.lifetime.end", lifetime_sig, llvmFunctionsAttributeSet) {
					AddressSignificance = LlvmIrAddressSignificance.Default
				}
			);

			 // Function names and declarations must match those in src/monodroid/jni/marshal-methods-tracing.hh
			var mm_trace_func_enter_or_leave_params = new List<LlvmIrFunctionParameter> {
				new (typeof(IntPtr), "env"), // JNIEnv *env
				new (typeof(int), "tracing_mode"),
				new (typeof(uint), "mono_image_index"),
				new (typeof(uint), "class_index"),
				new (typeof(uint), "method_token"),
				new (typeof(string), "native_method_name"),
				new (typeof(string), "method_extra_info"),
			};

			var mm_trace_func_enter_leave_sig = new LlvmIrFunctionSignature (
				name: "_mm_trace_func_enter",
				returnType: typeof(void),
				parameters: mm_trace_func_enter_or_leave_params
			);

			mm_trace_func_enter = module.DeclareExternalFunction (new LlvmIrFunction (mm_trace_func_enter_leave_sig, traceFunctionsAttributeSet));
			mm_trace_func_leave = module.DeclareExternalFunction (new LlvmIrFunction ("_mm_trace_func_leave", mm_trace_func_enter_leave_sig, traceFunctionsAttributeSet));
		}

		LlvmIrFunctionAttributeSet MakeLlvmIntrinsicFunctionsAttributeSet (LlvmIrModule module)
		{
			return new LlvmIrFunctionAttributeSet {
				new ArgmemonlyFunctionAttribute (),
				new MustprogressFunctionAttribute (),
				new NocallbackFunctionAttribute (),
				new NofreeFunctionAttribute (),
				new NosyncFunctionAttribute (),
				new NounwindFunctionAttribute (),
				new WillreturnFunctionAttribute (),
			};
		}

		LlvmIrFunctionAttributeSet MakeTraceFunctionsAttributeSet (LlvmIrModule module)
		{
			var ret = new LlvmIrFunctionAttributeSet {
				new NounwindFunctionAttribute (),
				new NoTrappingMathFunctionAttribute (true),
				new StackProtectorBufferSizeFunctionAttribute (8),
			};

			ret.Add (AndroidTargetArch.Arm64, new FramePointerFunctionAttribute ("non-leaf"));
			ret.Add (AndroidTargetArch.Arm, new FramePointerFunctionAttribute ("all"));
			ret.Add (AndroidTargetArch.X86, new FramePointerFunctionAttribute ("none"));
			ret.Add (AndroidTargetArch.X86_64, new FramePointerFunctionAttribute ("none"));

			return ret;
		}
	}
}
