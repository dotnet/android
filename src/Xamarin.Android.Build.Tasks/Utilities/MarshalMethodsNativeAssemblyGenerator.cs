using System;
using System.Collections.Generic;
using Microsoft.Build.Utilities;
using Xamarin.Android.Tasks.LLVMIR;

namespace Xamarin.Android.Tasks;

// The native host still expects xamarin_app_init even though marshal methods are disabled.
class MarshalMethodsNativeAssemblyGenerator : LlvmIrComposer
{
	[NativeClass]
	sealed class _JNIEnv
	{}

	public MarshalMethodsNativeAssemblyGenerator (TaskLoggingHelper log) : base (log)
	{}

	protected override void Construct (LlvmIrModule module)
	{
		module.DefaultStringGroup = "mm";

		var getFunctionPtrVariable = module.AddGlobalVariable (
			typeof (IntPtr),
			"get_function_pointer",
			null,
			LlvmIrVariableOptions.LocalWritableInsignificantAddr,
			" get_function_pointer (uint32_t mono_image_index, uint32_t class_index, uint32_t method_token, void*& target_ptr)"
		);

		var initParams = new List<LlvmIrFunctionParameter> {
			new (typeof (_JNIEnv), "env") {
				NoCapture = true,
				NoUndef = true,
				ReadNone = true,
			},
			new (typeof (IntPtr), "fn") {
				NoUndef = true,
			},
		};

		var initSignature = new LlvmIrFunctionSignature (
			name: "xamarin_app_init",
			returnType: typeof (void),
			parameters: initParams
		);

		var attributes = module.AddAttributeSet (new LlvmIrFunctionAttributeSet {
			new MustprogressFunctionAttribute (),
			new NofreeFunctionAttribute (),
			new NorecurseFunctionAttribute (),
			new NosyncFunctionAttribute (),
			new NounwindFunctionAttribute (),
			new WillreturnFunctionAttribute (),
			new MemoryFunctionAttribute {
				Default = MemoryAttributeAccessKind.Write,
				Argmem = MemoryAttributeAccessKind.None,
				InaccessibleMem = MemoryAttributeAccessKind.None,
			},
			new UwtableFunctionAttribute (),
			new MinLegalVectorWidthFunctionAttribute (0),
			new NoTrappingMathFunctionAttribute (true),
			new StackProtectorBufferSizeFunctionAttribute (8),
		});
		var init = new LlvmIrFunction (initSignature, attributes);

		// Name these variables so LLVM's unnamed labels remain sequential.
		var fnNullResult = init.CreateLocalVariable (typeof (bool), "fnIsNull");
		LlvmIrVariable putsResult = init.CreateLocalVariable (typeof (int), "putsResult");
		var ifThenInstructions = new List<LlvmIrInstruction> {
			module.CreatePuts ("get_function_pointer MUST be specified\n", putsResult),
			module.CreateAbort (),
			new LlvmIrInstructions.Unreachable (),
		};

		module.AddIfThenElse (init, fnNullResult, LlvmIrIcmpCond.Equal, initParams [1], null, ifThenInstructions);
		init.Body.Store (initParams [1], getFunctionPtrVariable, module.TbaaAnyPointer);
		init.Body.Ret (typeof (void));
		module.Add (init);
	}
}
