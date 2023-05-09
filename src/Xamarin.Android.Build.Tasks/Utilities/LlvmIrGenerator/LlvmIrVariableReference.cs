using System;

namespace Xamarin.Android.Tasks.LLVMIR
{
	/// <summary>
	/// References either a local or global variable.
	/// </summary>
	class LlvmIrVariableReference : LlvmIrVariable
	{
		public string Reference { get; }

		public LlvmIrVariableReference (Type type, string name, bool isGlobal, bool isNativePointer = false)
			: base (type, name, signature: null, isNativePointer: isNativePointer)
		{
			if (String.IsNullOrEmpty (name)) {
				throw new ArgumentException ("must not be null or empty", nameof (name));
			}
			Reference = MakeReference (isGlobal, name);
		}

		public LlvmIrVariableReference (LlvmNativeFunctionSignature signature, string name, bool isGlobal, bool isNativePointer = false)
			: base (typeof(LlvmNativeFunctionSignature), name, signature, isNativePointer)
		{
			if (signature == null) {
				throw new ArgumentNullException (nameof (signature));
			}

			if (String.IsNullOrEmpty (name)) {
				throw new ArgumentException ("must not be null or empty", nameof (name));
			}

			Reference = MakeReference (isGlobal, name);
		}

		public LlvmIrVariableReference (LlvmIrVariable variable, bool isGlobal, bool isNativePointer = false)
			: base (variable, variable?.Name, isNativePointer || variable.IsNativePointer)
		{
			if (String.IsNullOrEmpty (variable?.Name)) {
				throw new ArgumentException ("variable name must not be null or empty", nameof (variable));
			}

			Reference = MakeReference (isGlobal, variable?.Name);
		}

		string MakeReference (bool isGlobal, string name)
		{
			return $"{(isGlobal ? '@' : '%')}{Name}";
		}
	}
}
