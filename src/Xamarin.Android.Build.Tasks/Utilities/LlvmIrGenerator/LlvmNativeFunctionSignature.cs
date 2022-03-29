using System;
using System.Collections.Generic;
using System.Linq;

namespace Xamarin.Android.Tasks.LLVMIR
{
	/// <summary>
	/// Contains signature/description of a native function.  All the types used for parameters or return value must
	/// be mappable to LLVM IR types.  This class can be used to describe pointers to functions which have no corresponding
	/// managed method (e.g. `xamarin_app_init` used by marshal methods).  Additionally, an optional default value can be
	/// specified, to be used whenever a variable of this type is emitted (e.g. <see cref="LlvmIrGenerator.WriteVariable").
	/// </summary>
	class LlvmNativeFunctionSignature
	{
		public Type ReturnType { get; }
		public IList<LlvmIrFunctionParameter>? Parameters { get; }
		public object? FieldValue { get; set; }

		public LlvmNativeFunctionSignature (Type returnType, List<LlvmIrFunctionParameter>? parameters = null)
		{
			ReturnType = returnType ?? throw new ArgumentNullException (nameof (returnType));
			Parameters = parameters?.Select (p => EnsureValidParameter (p))?.ToList ()?.AsReadOnly ();

			LlvmIrFunctionParameter EnsureValidParameter (LlvmIrFunctionParameter parameter)
			{
				if (parameter == null) {
					throw new InvalidOperationException ("null parameters aren't allowed");
				}

				return parameter;
			}
		}
	}
}
