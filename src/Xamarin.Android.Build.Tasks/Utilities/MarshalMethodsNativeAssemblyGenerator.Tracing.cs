using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

using Xamarin.Android.Tools;
using Xamarin.Android.Tasks.LLVMIR;

namespace Xamarin.Android.Tasks;

using CecilMethodDefinition = global::Mono.Cecil.MethodDefinition;
using CecilParameterDefinition = global::Mono.Cecil.ParameterDefinition;

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

	enum TracingRenderArgumentFunction
	{
		None,
		GetClassName,
		GetObjectClassname,
		GetCString,
		GetBooleanString,
	}

	sealed class AsprintfParameterOperation
	{
		public readonly Type? UpcastType;
		public readonly TracingRenderArgumentFunction RenderFunction = TracingRenderArgumentFunction.None;
		public readonly bool MustBeFreed;

		public AsprintfParameterOperation (Type? upcastType, TracingRenderArgumentFunction renderFunction)
		{
			UpcastType = upcastType;
			RenderFunction = renderFunction;
		}

		public AsprintfParameterOperation (Type upcastType)
			: this (upcastType, TracingRenderArgumentFunction.None)
		{}

		public AsprintfParameterOperation (TracingRenderArgumentFunction renderFunction, bool mustBeFreed)
			: this (null, renderFunction)
		{
			MustBeFreed = mustBeFreed;
		}

		public AsprintfParameterOperation ()
			: this (null, TracingRenderArgumentFunction.None)
		{}
	}

	sealed class AsprintfParameterTransform : IEnumerable<AsprintfParameterOperation>
	{
		public List<AsprintfParameterOperation> Operations { get; } = new List<AsprintfParameterOperation> ();

		public void Add (AsprintfParameterOperation parameterOp)
		{
			Operations.Add (parameterOp);
		}

		public IEnumerator<AsprintfParameterOperation> GetEnumerator ()
		{
			return ((IEnumerable<AsprintfParameterOperation>)Operations).GetEnumerator ();
		}

		IEnumerator IEnumerable.GetEnumerator ()
		{
			return ((IEnumerable)Operations).GetEnumerator ();
		}
	}

	sealed class AsprintfCallState
	{
		public readonly string Format;
		public readonly List<AsprintfParameterTransform?> ParameterTransforms;
		public readonly List<LlvmIrVariable> VariablesToFree       = new List<LlvmIrVariable> ();
		public readonly List<LlvmIrVariable> VariadicArgsVariables = new List<LlvmIrVariable> ();

		public AsprintfCallState (string format, List<AsprintfParameterTransform?> parameterTransforms)
		{
			Format = format;
			ParameterTransforms = parameterTransforms;
		}
	}

	sealed class TracingState
	{
		public List<object?>? trace_enter_leave_args                   = null;
		public LlvmIrLocalVariable? tracingParamsStringLifetimeTracker = null;
		public List<object?>? asprintfVariadicArgs                     = null;
		public LlvmIrLocalVariable? asprintfAllocatedStringVar         = null;
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

	void WriteTracingAtFunctionTop (LlvmIrModule module, MarshalMethodInfo method, LlvmIrFunctionBody body, LlvmIrFunction function, MarshalMethodsWriteState writeState)
	{
		if (tracingMode == MarshalMethodsTracingMode.None) {
			return;
		}

		module.RegisterString (method.NativeSymbolName);

		body.AddComment (" Tracing code start");

		var tracingState = new TracingState {
			asprintfAllocatedStringVar = function.CreateLocalVariable (typeof(string), "render_buf"),
		};
		body.Alloca (tracingState.asprintfAllocatedStringVar);

		LlvmIrFunctionAttributeSet lifetimeAttrSet = MakeLlvmLifetimeAttributeSet (module);
		LlvmIrInstructions.Call call = body.Call (llvm_lifetime_start, arguments: new List<object?> { new LlvmLifetimePointerSizeArgumentPlaceholder (), tracingState.asprintfAllocatedStringVar });
		call.AttributeSet = lifetimeAttrSet;

		body.Store (tracingState.asprintfAllocatedStringVar, module.TbaaAnyPointer);

		tracingState.asprintfVariadicArgs = new List<object?> (function.Signature.Parameters);

		AsprintfCallState asprintfState = GetPrintfStateForFunctionParams (module, method, function);
		AddAsprintfCall (function, asprintfState, tracingState);

		CecilMethodDefinition nativeCallback = method.Method.NativeCallback;
		var assemblyCacheIndexPlaceholder = new MarshalMethodAssemblyIndexValuePlaceholder (method, writeState.AssemblyCacheState);
		tracingState.trace_enter_leave_args = new List<object?> {
			function.Signature.Parameters[0], // JNIEnv *env
			(int)tracingMode,
			assemblyCacheIndexPlaceholder,
			method.ClassCacheIndex,
			nativeCallback.MetadataToken.ToUInt32 (),
			method.NativeSymbolName,
			tracingState.asprintfAllocatedStringVar,
		};
		body.Call (mm_trace_func_enter, arguments: tracingState.trace_enter_leave_args);
		body.Call (free, arguments: new List<object?> { tracingState.asprintfAllocatedStringVar });

		body.AddComment (" Tracing code end");
	}

	void AddPrintfFormatAndTransforms (StringBuilder sb, Type type, List<AsprintfParameterTransform?> parameterTransforms, out bool isNativePointer)
	{
		string format;
		AsprintfParameterTransform? transform = null;
		isNativePointer = false;

		if (type == typeof(string)) {
			format = "\"%s\"";
			isNativePointer = true;
		} else if (type == typeof(_jclass)) {
			format = "%s @%p";
			transform = new AsprintfParameterTransform {
				new AsprintfParameterOperation (TracingRenderArgumentFunction.GetClassName, mustBeFreed: true),
				new AsprintfParameterOperation (),
			};
			isNativePointer = true;
		} else if (type == typeof(_jobject)) {
			format = "%s @%p";
			transform = new AsprintfParameterTransform {
				new AsprintfParameterOperation (TracingRenderArgumentFunction.GetObjectClassname, mustBeFreed: true),
				new AsprintfParameterOperation (),
			};
			isNativePointer = true;
		} else if (type == typeof(_jstring)) {
			format = "\"%s\"";
			transform = new AsprintfParameterTransform {
				new AsprintfParameterOperation (TracingRenderArgumentFunction.GetCString, mustBeFreed: true),
				new AsprintfParameterOperation (),
			};
			isNativePointer = true;
		} else if (type == typeof(IntPtr) || typeof(_jobject).IsAssignableFrom (type) || type == typeof(_JNIEnv)) {
			format = "%p";
			isNativePointer = true;
		} else if (type == typeof(bool)) {
			format = "%s";
			transform = new AsprintfParameterTransform {
				new AsprintfParameterOperation (TracingRenderArgumentFunction.GetBooleanString, mustBeFreed: false),
			};
			isNativePointer = true;
		} else if (type == typeof(byte) || type == typeof(ushort)) {
			format = "%u";
			transform = new AsprintfParameterTransform {
				new AsprintfParameterOperation (typeof(uint)),
			};
		} else if (type == typeof(sbyte) || type == typeof(short)) {
			format = "%d";
			transform = new AsprintfParameterTransform {
				new AsprintfParameterOperation (typeof(int)),
			};
		} else if (type == typeof(char)) {
			format = "'\\%x'";
			transform = new AsprintfParameterTransform {
				new AsprintfParameterOperation (typeof(uint)),
			};
		} else if (type == typeof(int)) {
			format = "%d";
		} else if (type == typeof(uint)) {
			format = "%u";
		} else if (type == typeof(long)) {
			format = "%ld";
		} else if (type == typeof(ulong)) {
			format = "%lu";
		} else if (type == typeof(float)) {
			format = "%g";
			transform = new AsprintfParameterTransform {
				new AsprintfParameterOperation (typeof(double)),
			};
		} else if (type == typeof(double)) {
			format = "%g";
		} else {
			throw new InvalidOperationException ($"Unsupported type '{type}'");
		};

		parameterTransforms.Add (transform);
		sb.Append (format);
	}

	(StringBuilder sb, List<AsprintfParameterTransform?> parameterOps) InitPrintfState (string startChars = "(")
	{
		return (new StringBuilder (startChars), new List<AsprintfParameterTransform?> ());
	}

	AsprintfCallState FinishPrintfState (LlvmIrModule module, StringBuilder sb, List<AsprintfParameterTransform?> parameterTransforms, string endChars = ")")
	{
		sb.Append (endChars);
		string format = sb.ToString ();
		module.RegisterString (format);
		return new AsprintfCallState (format, parameterTransforms);
	}

	AsprintfCallState GetPrintfStateForFunctionParams (LlvmIrModule module, MarshalMethodInfo method, LlvmIrFunction func)
	{
		(StringBuilder ret, List<AsprintfParameterTransform?> parameterOps) = InitPrintfState ();
		bool first = true;

		List<LlvmIrFunctionParameter> nativeMethodParameters = method.Parameters;
		Mono.Collections.Generic.Collection<CecilParameterDefinition>? managedMethodParameters = method.Method.RegisteredMethod?.Parameters ?? method.Method.ImplementedMethod?.Parameters;
		int expectedRegisteredParamCount = nativeMethodParameters.Count - 2;
		if (managedMethodParameters != null && managedMethodParameters.Count != expectedRegisteredParamCount) {
			throw new InvalidOperationException ($"Internal error: unexpected number of registered method parameters. Should be {expectedRegisteredParamCount}, but is {managedMethodParameters.Count}");
		}

		if (nativeMethodParameters.Count != func.Signature.Parameters.Count) {
			throw new InvalidOperationException ($"Internal error: number of native method parameter ({nativeMethodParameters.Count}) doesn't match the number of marshal method parameters ({func.Signature.Parameters.Count})");
		}

		var variadicArgs = new List<LlvmIrVariable> ();
		bool haveManagedParams = managedMethodParameters != null;
		for (int i = 0; i < nativeMethodParameters.Count; i++) {
			LlvmIrFunctionParameter parameter = nativeMethodParameters[i];

			if (!first) {
				ret.Append (", ");
			} else {
				first = false;
			}

			// Native method will have two more parameters than its managed counterpart - one for JNIEnv* and another for jclass.  They always are the first two
			// parameters, so we start looking at the managed parameters only once the first two are out of the way
			CecilParameterDefinition? managedParameter = haveManagedParams && i >= 2 ? managedMethodParameters[i - 2] : null;
			AddPrintfFormatAndTransforms (ret, VerifyAndGetActualParameterType (parameter, managedParameter), parameterOps, out bool isNativePointer);
			variadicArgs.Add (func.Signature.Parameters[i]);
		}

		AsprintfCallState state = FinishPrintfState (module, ret, parameterOps);
		state.VariadicArgsVariables.AddRange (variadicArgs);
		return state;

		Type VerifyAndGetActualParameterType (LlvmIrFunctionParameter nativeParameter, CecilParameterDefinition? managedParameter)
		{
			if (managedParameter == null) {
				return nativeParameter.Type;
			}

			if (nativeParameter.Type == typeof(byte) && String.Compare ("System.Boolean", managedParameter.Name, StringComparison.Ordinal) == 0) {
				// `bool`, as a non-blittable type, is mapped to `byte` by the marshal method rewriter
				return typeof(bool);
			}

			return nativeParameter.Type;
		}
	}

	AsprintfCallState GetPrintfStateForReturnValue (LlvmIrModule module, LlvmIrLocalVariable localVariable)
	{
		(StringBuilder ret, List<AsprintfParameterTransform?> parameterTransforms) = InitPrintfState ("=>[");

		AddPrintfFormatAndTransforms (ret, localVariable.Type, parameterTransforms, out bool isNativePointer);

		return FinishPrintfState (module, ret, parameterTransforms, "]");
	}

	void DoWriteAsprintfCall (LlvmIrFunction func, List<object?> variadicArgs, AsprintfCallState asprintfState, TracingState tracingState)
	{
		var asprintfArgs = new List<object?> {
			tracingState.asprintfAllocatedStringVar,
			asprintfState.Format,
		};
		asprintfArgs.AddRange (variadicArgs);

		LlvmIrLocalVariable asprintf_ret = func.CreateLocalVariable (typeof(int), "asprintf_ret");
		LlvmIrInstructions.Call call = func.Body.Call (asprintf, asprintf_ret, asprintfArgs);
		call.Comment = $"Format: {asprintfState.Format}";

		// Check whether asprintf returned a negative value (it returns -1 at failure, but we widen the check just in case)
		LlvmIrLocalVariable asprintf_failed = func.CreateLocalVariable (typeof(bool), "asprintf_failed");
		func.Body.Icmp (LlvmIrIcmpCond.SignedLessThan, asprintf_failed, (int)0, asprintf_failed);

		var asprintfIfThenLabel = new LlvmIrFunctionLabelItem ();
		var asprintfIfElseLabel = new LlvmIrFunctionLabelItem ();
		var ifElseDoneLabel = new LlvmIrFunctionLabelItem ();

		func.Body.Br (asprintf_failed, asprintfIfThenLabel, asprintfIfElseLabel);

		// Condition is true if asprintf **failed**
		func.Body.Add (asprintfIfThenLabel);
		LlvmIrLocalVariable bufferPointerNull = func.CreateLocalVariable (typeof(IntPtr), "bufferPointerNull");
		func.Body.Store (bufferPointerNull);
		func.Body.Br (ifElseDoneLabel);

		func.Body.Add (asprintfIfElseLabel);
		LlvmIrLocalVariable bufferPointerAllocated = func.CreateLocalVariable (typeof(IntPtr), "bufferPointerAllocated");
		func.Body.Load (tracingState.asprintfAllocatedStringVar, bufferPointerAllocated);
		func.Body.Br (ifElseDoneLabel);

		func.Body.Add (ifElseDoneLabel);
		func.Body.Phi (tracingState.asprintfAllocatedStringVar, bufferPointerNull, asprintfIfThenLabel, bufferPointerAllocated, asprintfIfElseLabel);
	}

	LlvmIrVariable? WriteTransformFunctionCall (LlvmIrFunction func, AsprintfCallState asprintfState, AsprintfParameterOperation paramOp, LlvmIrVariable paramVar)
	{
		if (paramOp.RenderFunction == TracingRenderArgumentFunction.None) {
			return null;
		}

		var transformerArgs = new List<object?> ();
		LlvmIrFunction transformerFunc;

		switch (paramOp.RenderFunction) {
			case TracingRenderArgumentFunction.GetClassName:
				transformerFunc = mm_trace_get_class_name;
				AddJNIEnvArgument ();
				break;

			case TracingRenderArgumentFunction.GetObjectClassname:
				transformerFunc = mm_trace_get_object_class_name;
				AddJNIEnvArgument ();
				break;

			case TracingRenderArgumentFunction.GetCString:
				transformerFunc = mm_trace_get_c_string;
				AddJNIEnvArgument ();
				break;

			case TracingRenderArgumentFunction.GetBooleanString:
				transformerFunc = mm_trace_get_boolean_string;
				break;

			default:
				throw new InvalidOperationException ($"Internal error: unsupported transformer function {paramOp.RenderFunction}");
		};
		transformerArgs.Add (paramVar);

		if (!transformerFunc.ReturnsValue) {
			return null;
		}
		LlvmIrLocalVariable? result = func.CreateLocalVariable (transformerFunc.Signature.ReturnType);
		func.Body.Call (transformerFunc, result, transformerArgs);

		if (paramOp.MustBeFreed) {
			asprintfState.VariablesToFree.Add (result);
		}

		return result;

		void AddJNIEnvArgument ()
		{
			transformerArgs.Add (func.Signature.Parameters[0]);
		}
	}

	void AddAsprintfArgument (LlvmIrFunction func, AsprintfCallState asprintfState, List<object?> asprintfArgs, List<AsprintfParameterOperation>? paramOps, LlvmIrVariable paramVar)
	{
		if (paramOps == null || paramOps.Count == 0) {
			asprintfArgs.Add (paramVar);
			return;
		}

		foreach (AsprintfParameterOperation paramOp in paramOps) {
			LlvmIrVariable? paramRef = WriteTransformFunctionCall (func, asprintfState, paramOp, paramVar);

			if (paramOp.UpcastType != null) {
				LlvmIrLocalVariable upcastVar = func.CreateLocalVariable ((paramRef ?? paramVar).Type);
				func.Body.Ext (paramRef ?? paramVar, paramOp.UpcastType, upcastVar);
				paramRef = upcastVar;
			}

			if (paramRef == null) {
				paramRef = paramVar;
			}

			asprintfArgs.Add (paramRef);
		}
	}

	void AddAsprintfCall (LlvmIrFunction function, AsprintfCallState asprintfState, TracingState tracingState)
	{
		if (asprintfState.VariadicArgsVariables.Count != asprintfState.ParameterTransforms.Count) {
			throw new ArgumentException (nameof (asprintfState), $"Number of transforms ({asprintfState.ParameterTransforms.Count}) is not equal to the number of variadic arguments ({asprintfState.VariadicArgsVariables.Count})");
		}

		var asprintfArgs = new List<object?> ();
		for (int i = 0; i < asprintfState.VariadicArgsVariables.Count; i++) {
			AddAsprintfArgument (function, asprintfState, asprintfArgs, asprintfState.ParameterTransforms[i]?.Operations, asprintfState.VariadicArgsVariables[i]);
		}

		DoWriteAsprintfCall (function, asprintfArgs, asprintfState, tracingState);
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
