using System;
using System.Text;
using System.Collections.Generic;

using Xamarin.Android.Tasks.LLVMIR;
using System.Collections;

namespace Xamarin.Android.Tasks
{
	using CecilMethodDefinition = global::Mono.Cecil.MethodDefinition;
	using CecilParameterDefinition = global::Mono.Cecil.ParameterDefinition;

	partial class MarshalMethodsNativeAssemblyGenerator
	{
		const string mm_trace_init_name                  = "_mm_trace_init";
		const string mm_trace_func_enter_name            = "_mm_trace_func_enter";
		const string mm_trace_func_leave_name            = "_mm_trace_func_leave";
		const string mm_trace_get_class_name_name        = "_mm_trace_get_class_name";
		const string mm_trace_get_object_class_name_name = "_mm_trace_get_object_class_name";
		const string mm_trace_get_c_string_name          = "_mm_trace_get_c_string";
		const string mm_trace_get_boolean_string_name    = "_mm_trace_get_boolean_string";
		const string asprintf_name                       = "asprintf";
		const string free_name                           = "free";

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
			public readonly Type? Upcast;
			public readonly TracingRenderArgumentFunction RenderFunction = TracingRenderArgumentFunction.None;
			public readonly bool MustBeFreed;

			public AsprintfParameterOperation (Type? upcast, TracingRenderArgumentFunction renderFunction)
			{
				Upcast = upcast;
				RenderFunction = renderFunction;
			}

			public AsprintfParameterOperation (Type upcast)
				: this (upcast, TracingRenderArgumentFunction.None)
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
			public readonly List<LlvmIrVariableReference> VariablesToFree = new List<LlvmIrVariableReference> ();
			public readonly List<LlvmIrVariableReference> VariadicArgs = new List<LlvmIrVariableReference> ();

			public AsprintfCallState (string format, List<AsprintfParameterTransform?> parameterTransforms)
			{
				Format = format;
				ParameterTransforms = parameterTransforms;
			}
		}

		sealed class TracingState
		{
			public List<LlvmIrFunctionArgument>? trace_enter_leave_args            = null;
			public LlvmIrFunctionLocalVariable? tracingParamsStringLifetimeTracker = null;
			public List<LlvmIrFunctionArgument>? asprintfVariadicArgs              = null;
			public LlvmIrVariableReference? asprintfAllocatedStringAccessorRef     = null;
			public LlvmIrVariableReference? asprintfAllocatedStringVarRef          = null;
		}

		List<LlvmIrFunctionParameter>? mm_trace_func_enter_or_leave_params;
		int mm_trace_func_enter_leave_extra_info_param_index = -1;
		List<LlvmIrFunctionParameter>? get_function_pointer_params;
		LlvmIrVariableReference? mm_trace_init_ref;
		LlvmIrVariableReference? mm_trace_func_enter_ref;
		LlvmIrVariableReference? mm_trace_func_leave_ref;
		LlvmIrVariableReference? mm_trace_get_class_name_ref;
		LlvmIrVariableReference? mm_trace_get_object_class_name_ref;
		LlvmIrVariableReference? mm_trace_get_c_string_ref;
		LlvmIrVariableReference? mm_trace_get_boolean_string_ref;
		LlvmIrVariableReference? asprintf_ref;
		LlvmIrVariableReference? free_ref;

		void InitializeTracing (LlvmIrGenerator generator)
		{
			if (tracingMode == MarshalMethodsTracingMode.None) {
				return;
			}

			// Function names and declarations must match those in src/monodroid/jni/marshal-methods-tracing.hh
			mm_trace_func_enter_or_leave_params = new List<LlvmIrFunctionParameter> {
				new LlvmIrFunctionParameter (typeof(_JNIEnv), "env", isNativePointer: true), // JNIEnv *env
				new LlvmIrFunctionParameter (typeof(int), "tracing_mode"),
				new LlvmIrFunctionParameter (typeof(uint), "mono_image_index"),
				new LlvmIrFunctionParameter (typeof(uint), "class_index"),
				new LlvmIrFunctionParameter (typeof(uint), "method_token"),
				new LlvmIrFunctionParameter (typeof(string), "native_method_name"),
				new LlvmIrFunctionParameter (typeof(string), "method_extra_info"),
			};
			mm_trace_func_enter_leave_extra_info_param_index = mm_trace_func_enter_or_leave_params.Count - 1;

			var mm_trace_func_enter_sig = new LlvmNativeFunctionSignature (
				returnType: typeof(void),
				parameters: mm_trace_func_enter_or_leave_params

			);
			mm_trace_func_enter_ref = new LlvmIrVariableReference (mm_trace_func_enter_sig, mm_trace_func_enter_name, isGlobal: true);

			var mm_trace_func_leave_sig = new LlvmNativeFunctionSignature (
				returnType: typeof(void),
				parameters: mm_trace_func_enter_or_leave_params
			);
			mm_trace_func_leave_ref = new LlvmIrVariableReference (mm_trace_func_leave_sig, mm_trace_func_leave_name, isGlobal: true);

			var asprintf_sig = new LlvmNativeFunctionSignature (
				returnType: typeof(int),
				parameters: new List<LlvmIrFunctionParameter> {
					new LlvmIrFunctionParameter (typeof(string), isNativePointer: true) {
						NoUndef = true,
					},
					new LlvmIrFunctionParameter (typeof(string)) {
						NoUndef = true,
					},
					new LlvmIrFunctionParameter (typeof(void)) {
						IsVarargs = true,
					}
				}
			);
			asprintf_ref = new LlvmIrVariableReference (asprintf_sig, asprintf_name, isGlobal: true);

			var free_sig = new LlvmNativeFunctionSignature (
				returnType: typeof(void),
				parameters: new List<LlvmIrFunctionParameter> {
					new LlvmIrFunctionParameter (typeof(string)) {
						NoCapture = true,
						NoUndef = true,
					},
				}
			);
			free_ref = new LlvmIrVariableReference (free_sig, free_name, isGlobal: true);

			var mm_trace_init_sig = new LlvmNativeFunctionSignature (
				returnType: typeof(void),
				parameters: new List<LlvmIrFunctionParameter> {
					new LlvmIrFunctionParameter (typeof(_JNIEnv), "env", isNativePointer: true) {
						NoUndef = true,
					},
				}
			);
			mm_trace_init_ref = new LlvmIrVariableReference (mm_trace_init_sig, mm_trace_init_name, isGlobal: true);

			var mm_trace_get_class_name_sig = new LlvmNativeFunctionSignature (
				returnType: typeof(string),
				parameters: new List<LlvmIrFunctionParameter> {
					new LlvmIrFunctionParameter (typeof(_JNIEnv), "env", isNativePointer: true) {
						NoUndef = true,
					},
					new LlvmIrFunctionParameter (typeof(_jclass), "v") {
						NoUndef = true,
					},
				}
			);
			mm_trace_get_class_name_ref = new LlvmIrVariableReference (mm_trace_get_class_name_sig, mm_trace_get_class_name_name, isGlobal: true);

			var mm_trace_get_object_class_name_sig = new LlvmNativeFunctionSignature (
				returnType: typeof(string),
				parameters: new List<LlvmIrFunctionParameter> {
					new LlvmIrFunctionParameter (typeof(_JNIEnv), "env", isNativePointer: true) {
						NoUndef = true,
					},
					new LlvmIrFunctionParameter (typeof(_jobject), "v") {
						NoUndef = true,
					},
				}
			);
			mm_trace_get_object_class_name_ref = new LlvmIrVariableReference (mm_trace_get_object_class_name_sig, mm_trace_get_object_class_name_name, isGlobal: true);

			var mm_trace_get_c_string_sig = new LlvmNativeFunctionSignature (
				returnType: typeof(string),
				parameters: new List<LlvmIrFunctionParameter> {
					new LlvmIrFunctionParameter (typeof(_JNIEnv), "env", isNativePointer: true) {
						NoUndef = true,
					},
					new LlvmIrFunctionParameter (typeof(_jstring), "v") {
						NoUndef = true,
					},
				}
			);
			mm_trace_get_c_string_ref = new LlvmIrVariableReference (mm_trace_get_c_string_sig, mm_trace_get_c_string_name, isGlobal: true);

			var mm_trace_get_boolean_string_sig = new LlvmNativeFunctionSignature (
				returnType: typeof(string),
				parameters: new List<LlvmIrFunctionParameter> {
					new LlvmIrFunctionParameter (typeof(_JNIEnv), "env", isNativePointer: true) {
						NoUndef = true,
					},
					new LlvmIrFunctionParameter (typeof(byte), "v") {
						NoUndef = true,
					},
				}
			);
			mm_trace_get_boolean_string_ref = new LlvmIrVariableReference (mm_trace_get_boolean_string_sig, mm_trace_get_boolean_string_name, isGlobal: true);

			AddTraceFunctionDeclaration (asprintf_name, asprintf_sig, LlvmIrGenerator.FunctionAttributesJniMethods);
			AddTraceFunctionDeclaration (free_name, free_sig, LlvmIrGenerator.FunctionAttributesLibcFree);
			AddTraceFunctionDeclaration (mm_trace_init_name, mm_trace_init_sig, LlvmIrGenerator.FunctionAttributesJniMethods);
			AddTraceFunctionDeclaration (mm_trace_func_enter_name, mm_trace_func_enter_sig, LlvmIrGenerator.FunctionAttributesJniMethods);
			AddTraceFunctionDeclaration (mm_trace_func_leave_name, mm_trace_func_leave_sig, LlvmIrGenerator.FunctionAttributesJniMethods);
			AddTraceFunctionDeclaration (mm_trace_get_class_name_name, mm_trace_get_class_name_sig, LlvmIrGenerator.FunctionAttributesJniMethods);
			AddTraceFunctionDeclaration (mm_trace_get_object_class_name_name, mm_trace_get_object_class_name_sig, LlvmIrGenerator.FunctionAttributesJniMethods);
			AddTraceFunctionDeclaration (mm_trace_get_c_string_name, mm_trace_get_c_string_sig, LlvmIrGenerator.FunctionAttributesJniMethods);
			AddTraceFunctionDeclaration (mm_trace_get_boolean_string_name, mm_trace_get_boolean_string_sig, LlvmIrGenerator.FunctionAttributesJniMethods);

			void AddTraceFunctionDeclaration (string name, LlvmNativeFunctionSignature sig, int attributeSetID)
			{
				var func = new LlvmIrFunction (
					name: name,
					returnType: sig.ReturnType,
					attributeSetID: attributeSetID,
					parameters: sig.Parameters
				);
				generator.AddExternalFunction (func);
			}
		}

		void AddPrintfFormatAndTransforms (StringBuilder sb, Type type, List<AsprintfParameterTransform?> parameterTransforms)
		{
			string format;
			AsprintfParameterTransform? transform = null;

			if (type == typeof(string)) {
				format = "\"%s\"";
			} else if (type == typeof(_jclass)) {
				format = "%s @%p";
				transform = new AsprintfParameterTransform {
					new AsprintfParameterOperation (TracingRenderArgumentFunction.GetClassName, mustBeFreed: true),
					new AsprintfParameterOperation (),
				};
			} else if (type == typeof(_jobject)) {
				format = "%s @%p";
				transform = new AsprintfParameterTransform {
					new AsprintfParameterOperation (TracingRenderArgumentFunction.GetObjectClassname, mustBeFreed: true),
					new AsprintfParameterOperation (),
				};
			} else if (type == typeof(_jstring)) {
				format = "\"%s\"";
				transform = new AsprintfParameterTransform {
					new AsprintfParameterOperation (TracingRenderArgumentFunction.GetCString, mustBeFreed: true),
					new AsprintfParameterOperation (),
				};
			} else if (type == typeof(IntPtr) || typeof(_jobject).IsAssignableFrom (type) || type == typeof(_JNIEnv)) {
				format = "%p";
			} else if (type == typeof(bool)) {
				format = "%s";
				transform = new AsprintfParameterTransform {
					new AsprintfParameterOperation (TracingRenderArgumentFunction.GetBooleanString, mustBeFreed: false),
				};
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

		AsprintfCallState FinishPrintfState (StringBuilder sb, List<AsprintfParameterTransform?> parameterTransforms, string endChars = ")")
		{
			sb.Append (endChars);
			return new AsprintfCallState (sb.ToString (), parameterTransforms);
		}

		AsprintfCallState GetPrintfStateForFunctionParams (MarshalMethodInfo method, LlvmIrFunction func)
		{
			(StringBuilder ret, List<AsprintfParameterTransform?> parameterOps) = InitPrintfState ();
			bool first = true;

			List<LlvmIrFunctionParameter> nativeMethodParameters = method.Parameters;
			Mono.Collections.Generic.Collection<CecilParameterDefinition>? managedMethodParameters = method.Method.RegisteredMethod?.Parameters;

			int expectedRegisteredParamCount = nativeMethodParameters.Count - 2;
			if (managedMethodParameters != null && managedMethodParameters.Count != expectedRegisteredParamCount) {
				throw new InvalidOperationException ($"Internal error: unexpected number of registered method parameters. Should be {expectedRegisteredParamCount}, but is {managedMethodParameters.Count}");
			}

			if (nativeMethodParameters.Count != func.Parameters.Count) {
				throw new InvalidOperationException ($"Internal error: number of native method parameter ({nativeMethodParameters.Count}) doesn't match the number of marshal method parameters ({func.Parameters.Count})");
			}

			var variadicArgs = new List<LlvmIrVariableReference> ();
			for (int i = 0; i < nativeMethodParameters.Count; i++) {
				LlvmIrFunctionParameter parameter = nativeMethodParameters[i];
				if (!first) {
					ret.Append (", ");
				} else {
					first = false;
				}

				// Native method will have two more parameters than its managed counterpart - one for JNIEnv* and another for jclass.  They always are the first two
				// parameters, so we start looking at the managed parameters only once the first two are out of the way
				AddPrintfFormatAndTransforms (ret, VerifyAndGetActualParameterType (parameter, i >= 2 ? managedMethodParameters[i - 2] : null), parameterOps);
				variadicArgs.Add (new LlvmIrVariableReference (func.ParameterVariables[i], isGlobal: false));
			}

			return FinishPrintfState (ret, parameterOps);

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

		AsprintfCallState GetPrintfStateForReturnValue (LlvmIrFunctionLocalVariable localVariable)
		{
			(StringBuilder ret, List<AsprintfParameterTransform?> parameterTransforms) = InitPrintfState ("=>[");

			AddPrintfFormatAndTransforms (ret, localVariable.Type, parameterTransforms);

			return FinishPrintfState (ret, parameterTransforms, "]");
		}

		LlvmIrVariableReference DoWriteAsprintfCall (LlvmIrGenerator generator, LlvmIrFunction func, string format, List<LlvmIrFunctionArgument> variadicArgs, LlvmIrVariableReference allocatedStringVarRef)
		{
			LlvmIrGenerator.StringSymbolInfo asprintfFormatSym = generator.AddString (format, $"asprintf_fmt_{func.Name}");

			var asprintfArgs = new List<LlvmIrFunctionArgument> {
				new LlvmIrFunctionArgument (allocatedStringVarRef) {
					NonNull = true,
					NoUndef = true,
				},
				new LlvmIrFunctionArgument (asprintfFormatSym) {
					NoUndef = true,
				},
			};

			asprintfArgs.AddRange (variadicArgs);

			generator.WriteEOL ();
			generator.WriteCommentLine ($"Format: {format}", indent: true);
			LlvmIrFunctionLocalVariable? result = generator.EmitCall (func, asprintf_ref, asprintfArgs, marker: defaultCallMarker, AttributeSetID: -1);
			LlvmIrVariableReference? resultRef = new LlvmIrVariableReference (result, isGlobal: false);

			// Check whether asprintf returned a negative value (it returns -1 at failure, but we widen the check just in case)
			LlvmIrFunctionLocalVariable asprintfResultVariable = generator.EmitIcmpInstruction (func, LlvmIrIcmpCond.SignedLessThan, resultRef, "0");
			var asprintfResultVariableRef = new LlvmIrVariableReference (asprintfResultVariable, isGlobal: false);

			string asprintfIfThenLabel = func.MakeUniqueLabel ("if.then");
			string asprintfIfElseLabel = func.MakeUniqueLabel ("if.else");
			string ifElseDoneLabel = func.MakeUniqueLabel ("if.done");

			generator.EmitBrInstruction (func, asprintfResultVariableRef, asprintfIfThenLabel, asprintfIfElseLabel);

			// Condition is true if asprintf **failed**
			generator.EmitLabel (func, asprintfIfThenLabel);
			generator.EmitStoreInstruction<string> (func, allocatedStringVarRef, null);
			generator.EmitBrInstruction (func, ifElseDoneLabel);

			generator.EmitLabel (func, asprintfIfElseLabel);
			LlvmIrFunctionLocalVariable bufferPointerVar = generator.EmitLoadInstruction (func, allocatedStringVarRef);
			LlvmIrVariableReference bufferPointerVarRef = new LlvmIrVariableReference (bufferPointerVar, isGlobal: false);
			generator.EmitBrInstruction (func, ifElseDoneLabel);

			generator.EmitLabel (func, ifElseDoneLabel);
			LlvmIrFunctionLocalVariable allocatedStringValueVar = generator.EmitPhiInstruction (
				func,
				allocatedStringVarRef,
				new List<(LlvmIrVariableReference? variableRef, string label)> {
					(null, func.PreviousBlockStartLabel),
					(bufferPointerVarRef, func.PreviousBlockEndLabel),
				}
			);

			return new LlvmIrVariableReference (allocatedStringValueVar, isGlobal: false, isNativePointer: true);
		}


		void AddAsprintfArgument (LlvmIrGenerator generator, LlvmIrFunction func, List<LlvmIrFunctionArgument> asprintfArgs, List<AsprintfParameterOperation>? paramOps, LlvmIrFunctionLocalVariable paramVar)
		{
			if (paramOps == null || paramOps.Count == 0) {
				asprintfArgs.Add (new LlvmIrFunctionArgument (paramVar) { NoUndef = true });
				return;
			}

			foreach (AsprintfParameterOperation paramOp in paramOps) {
				LlvmIrVariableReference? paramRef = null;

				if (paramOp.RenderFunction != TracingRenderArgumentFunction.None) {
				}

				if (paramOp.Upcast != null) {
				}

				paramRef = new LlvmIrVariableReference (paramVar, isGlobal: false);
				LlvmIrFunctionLocalVariable upcastVar = generator.EmitUpcast (func, paramRef, paramOps.Upcast);
				asprintfArgs.Add (
					new LlvmIrFunctionArgument (upcastVar) {
						NoUndef = true,
					}
				);
			}
		}

		LlvmIrVariableReference WriteAsprintfCall (LlvmIrGenerator generator, LlvmIrFunction func, string format, List<LlvmIrFunctionArgument> variadicArgs, List<AsprintfParameterTransform?> parameterTransforms, LlvmIrVariableReference allocatedStringVarRef)
		{
			if (variadicArgs.Count != parameterTransforms.Count) {
				throw new ArgumentException (nameof (parameterTransforms), $"Number of transforms ({parameterTransforms.Count}) is not equal to the number of variadic arguments ({variadicArgs.Count})");
			}

			var asprintfArgs = new List<LlvmIrFunctionArgument> ();

			for (int i = 0; i < variadicArgs.Count; i++) {
				if (parameterTransforms[i] == null) {
					asprintfArgs.Add (variadicArgs[i]);
					continue;
				}

				if (variadicArgs[i].Value is LlvmIrFunctionLocalVariable paramVar) {
					AddAsprintfArgument (generator, func, asprintfArgs, parameterTransforms[i]?.Operations, paramVar);
					continue;
				}

				throw new InvalidOperationException ($"Unexpected argument type {variadicArgs[i].Type}");
			}

			return DoWriteAsprintfCall (generator, func, format, asprintfArgs, allocatedStringVarRef);
		}

		LlvmIrVariableReference WriteAsprintfCall (LlvmIrGenerator generator, LlvmIrFunction func, string format, LlvmIrFunctionLocalVariable retVal, AsprintfParameterOperation retValOp, LlvmIrVariableReference allocatedStringVarRef)
		{
			var asprintfArgs = new List<LlvmIrFunctionArgument> ();
			AddAsprintfArgument (generator, func, asprintfArgs, new List<AsprintfParameterOperation> { retValOp }, retVal);

			return DoWriteAsprintfCall (generator, func, format, asprintfArgs, allocatedStringVarRef);
		}

		TracingState? WriteMarshalMethodTracingTop (LlvmIrGenerator generator, MarshalMethodInfo method, LlvmIrFunction func)
		{
			if (tracingMode == MarshalMethodsTracingMode.None) {
				return null;
			}

			CecilMethodDefinition nativeCallback = method.Method.NativeCallback;
			var state = new TracingState ();

			const string paramsLocalVarName = "func_params_render";

			generator.WriteCommentLine ("Tracing code start", indent: true);
			(LlvmIrFunctionLocalVariable asprintfAllocatedStringVar, state.tracingParamsStringLifetimeTracker) = generator.EmitAllocStackVariable (func, typeof(string), paramsLocalVarName);
			state.asprintfAllocatedStringVarRef = new LlvmIrVariableReference (asprintfAllocatedStringVar, isGlobal: false);
			generator.EmitStoreInstruction<string> (func, state.asprintfAllocatedStringVarRef, null);

			state.asprintfVariadicArgs = new List<LlvmIrFunctionArgument> ();
			foreach (LlvmIrFunctionLocalVariable lfv in func.ParameterVariables) {
				state.asprintfVariadicArgs.Add (
					new LlvmIrFunctionArgument (lfv) {
						NoUndef = true,
					}
				);
			}

			AsprintfCallState asprintfState = GetPrintfStateForFunctionParams (method, func);
			state.asprintfAllocatedStringAccessorRef = WriteAsprintfCall (generator, func, asprintfState, state.asprintfVariadicArgs, state.asprintfAllocatedStringVarRef);

			state.trace_enter_leave_args = new List<LlvmIrFunctionArgument> {
				new LlvmIrFunctionArgument (func.ParameterVariables[0]), // JNIEnv* env
				new LlvmIrFunctionArgument (mm_trace_func_enter_or_leave_params[1], (int)tracingMode),
				new LlvmIrFunctionArgument (mm_trace_func_enter_or_leave_params[2], method.AssemblyCacheIndex),
				new LlvmIrFunctionArgument (mm_trace_func_enter_or_leave_params[3], method.ClassCacheIndex),
				new LlvmIrFunctionArgument (mm_trace_func_enter_or_leave_params[4], nativeCallback.MetadataToken.ToUInt32 ()),
				new LlvmIrFunctionArgument (mm_trace_func_enter_or_leave_params[5], method.NativeSymbolName),
				new LlvmIrFunctionArgument (state.asprintfAllocatedStringAccessorRef),
			};

			generator.EmitCall (func, mm_trace_func_enter_ref, state.trace_enter_leave_args, marker: defaultCallMarker);
			asprintfAllocatedStringVar = generator.EmitLoadInstruction (func, state.asprintfAllocatedStringVarRef);

			generator.EmitCall (
				func,
				free_ref,
				new List<LlvmIrFunctionArgument> {
					new LlvmIrFunctionArgument (asprintfAllocatedStringVar) {
						NoUndef = true,
							},
				},
				marker: defaultCallMarker
			);
			generator.WriteCommentLine ("Tracing code end", indent: true);
			generator.WriteEOL ();

			return state;
		}

		void WriteMarshalMethodTracingBottom (TracingState? state, LlvmIrGenerator generator, LlvmIrFunction func, LlvmIrFunctionLocalVariable? result)
		{
			if (tracingMode == MarshalMethodsTracingMode.None) {
				return;
			}

			if (state == null) {
				throw new InvalidOperationException ("Internal error: tracing state is required.");
			}

			generator.WriteCommentLine ("Tracing code start", indent: true);

			LlvmIrFunctionArgument extraInfoArg;
			if (result != null) {
				AsprintfCallState asprintfState = GetPrintfStateForReturnValue (result);
				state.asprintfAllocatedStringAccessorRef = WriteAsprintfCall (generator, func, asprintfState.Format, result, asprintfState.ParameterTransforms[0], state.asprintfAllocatedStringVarRef);
				extraInfoArg = new LlvmIrFunctionArgument (state.asprintfAllocatedStringAccessorRef) {
					NoUndef = true,
				};
			} else {
				extraInfoArg = new LlvmIrFunctionArgument (state.asprintfAllocatedStringVarRef, isNull: true) {
					NoUndef = true,
				};
			}

			if (mm_trace_func_enter_leave_extra_info_param_index < 0) {
				throw new InvalidOperationException ("Internal error: index of the extra info parameter is unknown");
			}
			state.trace_enter_leave_args[mm_trace_func_enter_leave_extra_info_param_index] = extraInfoArg;

			generator.EmitCall (func, mm_trace_func_leave_ref, state.trace_enter_leave_args, marker: defaultCallMarker);
			generator.EmitDeallocStackVariable (func, state.tracingParamsStringLifetimeTracker);

			generator.WriteCommentLine ("Tracing code end", indent: true);
		}

		void WriteTracingInit (LlvmIrGenerator generator, LlvmIrFunction func)
		{
			if (tracingMode == MarshalMethodsTracingMode.None) {
				return;
			}

			var trace_init_args = new List<LlvmIrFunctionArgument> {
				new LlvmIrFunctionArgument (func.ParameterVariables[0]), // JNIEnv* env
			};

			generator.EmitCall (func, mm_trace_init_ref, trace_init_args, marker: defaultCallMarker);
		}
	}
}
