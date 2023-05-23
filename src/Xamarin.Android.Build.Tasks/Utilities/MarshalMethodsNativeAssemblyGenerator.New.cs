using System;
using System.Collections.Generic;

using Xamarin.Android.Tasks.LLVM.IR;

namespace Xamarin.Android.Tasks
{
	partial class MarshalMethodsNativeAssemblyGenerator
	{
		LlvmIrFunction? mm_trace_func_enter;
		LlvmIrFunction? mm_trace_func_leave;

		protected override void Write (LlvmIrModule module)
		{
			InitTracing (module);
		}

		void InitTracing (LlvmIrModule module)
		{
			// TODO: fill
			var traceFunctionsAttributeSet = module.AddAttributeSet (MakeTraceFunctionsAttributeSet (module));

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
				name: mm_trace_func_enter_name,
				returnType: typeof(void),
				parameters: mm_trace_func_enter_or_leave_params,
				attributeSet: traceFunctionsAttributeSet
			);

			mm_trace_func_enter = module.DeclareExternalFunction (new LlvmIrFunction (mm_trace_func_enter_leave_sig));
			mm_trace_func_leave = module.DeclareExternalFunction (new LlvmIrFunction (mm_trace_func_leave_name, mm_trace_func_enter_leave_sig));
		}

		LlvmIrFunctionAttributeSet MakeTraceFunctionsAttributeSet (LlvmIrModule module)
		{
			// TODO: fill with defaults
			var ret = new LlvmIrFunctionAttributeSet ();
			module.Target.AddTargetSpecificAttributes (ret);
			return ret;
		}
	}
}
