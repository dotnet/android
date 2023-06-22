using System;
using System.Collections.Generic;

using Xamarin.Android.Tools;
using Xamarin.Android.Tasks.LLVMIR;

namespace Xamarin.Android.Tasks
{
	partial class MarshalMethodsNativeAssemblyGenerator
	{
		sealed class LlvmLifetimePointerSizeArgumentPlaceholder : LlvmIrInstructionPointerSizeArgumentPlaceholder
		{
			public override object? GetValue (LlvmIrModuleTarget target)
			{
				// the llvm.lifetime functions need a 64-bit integer, target.NativePointerSize is 32-bit
				return (ulong)(uint)base.GetValue (target);
			}
		}

		readonly MarshalMethodsTracingMode tracingMode;

		LlvmIrFunction? asprintf;
		LlvmIrFunction? free;

		LlvmIrFunction? llvm_lifetime_end;
		LlvmIrFunction? llvm_lifetime_start;

		LlvmIrFunction? mm_trace_func_enter;
		LlvmIrFunction? mm_trace_func_leave;
		LlvmIrFunction? mm_trace_get_boolean_string;
		LlvmIrFunction? mm_trace_get_c_string;
		LlvmIrFunction? mm_trace_get_class_name;
		LlvmIrFunction? mm_trace_get_object_class_name;
		LlvmIrFunction? mm_trace_init;

		void InitTracing (LlvmIrModule module)
		{
			var externalFunctionsAttributeSet = module.AddAttributeSet (MakeTraceFunctionsAttributeSet (module));

			InitLlvmFunctions (module);
			InitLibcFunctions (module, externalFunctionsAttributeSet);
			InitTracingFunctions (module, externalFunctionsAttributeSet);
		}

		void WriteTracingAtFunctionTop (LlvmIrModule module, LlvmIrFunctionBody body, LlvmIrFunction function)
		{
			LlvmIrLocalVariable render_buf = function.CreateLocalVariable (typeof(string), "render_buf");
			body.Alloca (render_buf);

			LlvmIrFunctionAttributeSet lifetimeAttrSet = MakeLlvmLifetimeAttributeSet (module);
			LlvmIrInstructions.Call call = body.Call (llvm_lifetime_start, arguments: new List<object?> { new LlvmLifetimePointerSizeArgumentPlaceholder (), render_buf });
			call.AttributeSet = lifetimeAttrSet;

			body.Store (render_buf, module.TbaaAnyPointer);
		}

		void InitLibcFunctions (LlvmIrModule module, LlvmIrFunctionAttributeSet? attrSet)
		{
			var asprintf_params = new List<LlvmIrFunctionParameter> {
				new (typeof(string), "strp") {
					NoUndef = true,
				},
				new (typeof(string), "fmt") {
					NoUndef = true,
				},
				new (typeof(void)) {
					IsVarArgs = true,
				},
			};

			var asprintf_sig = new LlvmIrFunctionSignature (
				name: "asprintf",
				returnType: typeof(int),
				parameters: asprintf_params
			);

			asprintf = module.DeclareExternalFunction (new LlvmIrFunction (asprintf_sig, attrSet));

			var free_params = new List<LlvmIrFunctionParameter> {
				new (typeof(IntPtr), "ptr") {
					AllocPtr = true,
					NoCapture = true,
					NoUndef = true,
				},
			};

			var free_sig = new LlvmIrFunctionSignature (
				name: "free",
				returnType: typeof(void),
				parameters: free_params
			);

			free = module.DeclareExternalFunction (new LlvmIrFunction (free_sig, MakeFreeFunctionAttributeSet (module)));
		}

		void InitLlvmFunctions (LlvmIrModule module)
		{
			var llvmFunctionsAttributeSet = module.AddAttributeSet (MakeLlvmIntrinsicFunctionsAttributeSet (module));
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

			llvm_lifetime_end = module.DeclareExternalFunction (
				new LlvmIrFunction ("llvm.lifetime.end", lifetime_sig, llvmFunctionsAttributeSet) {
					AddressSignificance = LlvmIrAddressSignificance.Default
				}
			);
		}

		void InitTracingFunctions (LlvmIrModule module, LlvmIrFunctionAttributeSet? attrSet)
		{
			 // Function names and declarations must match those in src/monodroid/jni/marshal-methods-tracing.hh
			var mm_trace_init_params = new List<LlvmIrFunctionParameter> {
				new (typeof(IntPtr), "env"),
			};

			var mm_trace_init_sig = new LlvmIrFunctionSignature (
				name: "_mm_trace_init",
				returnType: typeof(void),
				parameters: mm_trace_init_params
			);

			mm_trace_init = module.DeclareExternalFunction (new LlvmIrFunction (mm_trace_init_sig, attrSet));

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

			mm_trace_func_enter = module.DeclareExternalFunction (new LlvmIrFunction (mm_trace_func_enter_leave_sig, attrSet));
			mm_trace_func_leave = module.DeclareExternalFunction (new LlvmIrFunction ("_mm_trace_func_leave", mm_trace_func_enter_leave_sig, attrSet));

			var mm_trace_get_class_name_params = new List<LlvmIrFunctionParameter> {
				new (typeof(IntPtr), "env") {
					NoUndef = true,
				},
				new (typeof(_jclass), "v") {
					NoUndef = true,
				},
			};

			var mm_trace_get_class_name_sig = new LlvmIrFunctionSignature (
				name: "_mm_trace_get_class_name",
				returnType: typeof(string),
				parameters: mm_trace_get_class_name_params
			);

			mm_trace_get_class_name = module.DeclareExternalFunction (new LlvmIrFunction (mm_trace_get_class_name_sig, attrSet));

			var mm_trace_get_object_class_name_params = new List<LlvmIrFunctionParameter> {
				new (typeof(IntPtr), "env") {
					NoUndef = true,
				},
				new (typeof(_jobject), "v") {
					NoUndef = true,
				},
			};

			var mm_trace_get_object_class_name_sig = new LlvmIrFunctionSignature (
				name: "_mm_trace_get_object_class_name",
				returnType: typeof(string),
				parameters: mm_trace_get_object_class_name_params
			);

			mm_trace_get_object_class_name = module.DeclareExternalFunction (new LlvmIrFunction (mm_trace_get_object_class_name_sig, attrSet));

			var mm_trace_get_c_string_params = new List<LlvmIrFunctionParameter> {
				new (typeof(IntPtr), "env") {
					NoUndef = true,
				},
				new (typeof(_jstring), "v") {
					NoUndef = true,
				},
			};

			var mm_trace_get_c_string_sig = new LlvmIrFunctionSignature (
				name: "_mm_trace_get_c_string",
				returnType: typeof(string),
				parameters: mm_trace_get_c_string_params
			);

			mm_trace_get_c_string = module.DeclareExternalFunction (new LlvmIrFunction (mm_trace_get_c_string_sig, attrSet));

			var mm_trace_get_boolean_string_params = new List<LlvmIrFunctionParameter> {
				new (typeof(bool), "v") {
					NoUndef = true,
				},
			};

			var mm_trace_get_boolean_string_sig = new LlvmIrFunctionSignature (
				name: "_mm_trace_get_boolean_string",
				returnType: typeof(string),
				parameters: mm_trace_get_boolean_string_params
			);
			mm_trace_get_boolean_string_sig.ReturnAttributes.NonNull = true;

			mm_trace_get_boolean_string = module.DeclareExternalFunction (new LlvmIrFunction (mm_trace_get_boolean_string_sig, attrSet));
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

		LlvmIrFunctionAttributeSet MakeFreeFunctionAttributeSet (LlvmIrModule module)
		{
			var ret = new LlvmIrFunctionAttributeSet {
				new MustprogressFunctionAttribute (),
				new NounwindFunctionAttribute (),
				new WillreturnFunctionAttribute (),
				new AllockindFunctionAttribute ("free"),
				// TODO: LLVM 16+ feature, enable when we switch to this version
				// new MemoryFunctionAttribute {
				// 	Default = MemoryAttributeAccessKind.Write,
				// 	Argmem = MemoryAttributeAccessKind.None,
				// 	InaccessibleMem = MemoryAttributeAccessKind.None,
				// },
				new AllocFamilyFunctionAttribute ("malloc"),
				new NoTrappingMathFunctionAttribute (true),
				new StackProtectorBufferSizeFunctionAttribute (8),
			};

			return module.AddAttributeSet (ret);
		}

		LlvmIrFunctionAttributeSet MakeLlvmLifetimeAttributeSet (LlvmIrModule module)
		{
			var ret = new LlvmIrFunctionAttributeSet {
				new NounwindFunctionAttribute (),
			};

			ret.DoNotAddTargetSpecificAttributes = true;
			return module.AddAttributeSet (ret);
		}
	}
}
