using System;

namespace Xamarin.Android.Tasks.LLVMIR
{
	/// <summary>
	/// Base class for all the variable (local and global) as well as function parameter classes.
	/// </summary>
	abstract class LlvmIrVariable
	{
		public LlvmNativeFunctionSignature? NativeFunction { get; }
		public string? Name                                { get; }
		public Type Type                                   { get; }

		// Used when we need a pointer to pointer (etc) or when the type itself is not a pointer but we need one
		// in a given context (e.g. function parameters)
		public bool IsNativePointer                        { get; }

		protected LlvmIrVariable (Type type, string name, LlvmNativeFunctionSignature? signature, bool isNativePointer)
		{
			Type = type ?? throw new ArgumentNullException (nameof (type));
			Name = name;
			NativeFunction = signature;
			IsNativePointer = isNativePointer;
		}

		protected LlvmIrVariable (LlvmIrVariable variable, string name, bool isNativePointer)
		{
			Type = variable?.Type ?? throw new ArgumentNullException (nameof (variable));
			Name = name;
			NativeFunction = variable.NativeFunction;
			IsNativePointer = isNativePointer;
		}
	}
}
