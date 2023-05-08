using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Xamarin.Android.Tasks.LLVMIR
{
	class LlvmIrFunctionLocalVariable : LlvmIrVariable
	{
		public LlvmIrFunctionLocalVariable (Type type, string? name = null, bool isNativePointer = false)
			: base (type, name, signature: null, isNativePointer: isNativePointer)
		{}

		public LlvmIrFunctionLocalVariable (LlvmNativeFunctionSignature nativeFunction, string? name = null, bool isNativePointer = false)
			: base (typeof(LlvmNativeFunctionSignature), name, nativeFunction, isNativePointer: isNativePointer)
		{
			if (nativeFunction == null) {
				throw new ArgumentNullException(nameof (nativeFunction));
			}
		}

		public LlvmIrFunctionLocalVariable (LlvmIrVariable variable, string? name = null, bool isNativePointer = false)
			: base (variable, name, isNativePointer)
		{}
	}

	class LlvmIrFunctionParameter : LlvmIrFunctionLocalVariable
	{
		public bool Immarg               { get; set; }
		public bool IsCplusPlusReference { get; }
		public bool IsVarargs            { get; set; }
		public bool NoCapture            { get; set; }
		public bool NoUndef              { get; set; }

		public LlvmIrFunctionParameter (LlvmIrFunctionParameter other, string? name = null)
			: this (other.Type, name, other.IsNativePointer, other.IsCplusPlusReference)
		{
			CopyProperties (other);
		}

		// This is most decidedly weird... poor API design ;)
		public LlvmIrFunctionParameter (LlvmNativeFunctionSignature nativeFunction, LlvmIrFunctionParameter otherParam, string? name = null)
			: this (nativeFunction, name, otherParam.IsNativePointer, otherParam.IsCplusPlusReference)
		{
			CopyProperties (otherParam);
		}

		public LlvmIrFunctionParameter (Type type, string? name = null, bool isNativePointer = false, bool isCplusPlusReference = false)
			: base (type, name, isNativePointer)
		{
			IsCplusPlusReference = isCplusPlusReference;
		}

		public LlvmIrFunctionParameter (LlvmNativeFunctionSignature nativeFunction, string? name = null, bool isNativePointer = false, bool isCplusPlusReference = false)
			: base (nativeFunction, name, isNativePointer)
		{
			IsCplusPlusReference = isCplusPlusReference;
		}

		void CopyProperties (LlvmIrFunctionParameter other)
		{
			Immarg = other.Immarg;
			IsVarargs = other.IsVarargs;
			NoCapture = other.NoCapture;
			NoUndef = other.NoUndef;
		}
	}

	class LlvmIrFunctionArgument
	{
		public object Value { get; }
		public Type Type    { get; }
		public bool IsNativePointer { get; }
		public bool NonNull { get; set; }
		public bool NoUndef { get; set; }

		public LlvmIrFunctionArgument (LlvmIrFunctionParameter parameter, object? value = null)
		{
			Type = parameter?.Type ?? throw new ArgumentNullException (nameof (parameter));
			IsNativePointer = parameter.IsNativePointer;

			if (value != null && value.GetType () != Type) {
				throw new ArgumentException ($"value type '{value.GetType ()}' does not match the argument type '{Type}'");
			}

			Value = value;
		}

		public LlvmIrFunctionArgument (LlvmIrGenerator.StringSymbolInfo symbol)
		{
			Type = typeof(LlvmIrGenerator.StringSymbolInfo);
			Value = symbol;
		}

		public LlvmIrFunctionArgument (LlvmIrFunctionLocalVariable variable)
		{
			Type = typeof(LlvmIrFunctionLocalVariable);
			Value = variable;
			IsNativePointer = variable.IsNativePointer;
		}

		public LlvmIrFunctionArgument (LlvmIrVariableReference variable)
		{
			Type = typeof(LlvmIrVariableReference);
			Value = variable;
			IsNativePointer = variable.IsNativePointer;
		}
	}

	/// <summary>
	/// Describes a native function to be emitted and keeps code emitting state between calls to various generator
	/// methods.
	/// </summary>
	class LlvmIrFunction
	{
		const string Indent1 = LlvmIrGenerator.Indent;
		const string Indent2 = LlvmIrGenerator.Indent + LlvmIrGenerator.Indent;

		// Function signature
		public string Name                                            { get; }
		public Type ReturnType                                        { get; }
		public  int AttributeSetID                                    { get; }
		public IList<LlvmIrFunctionParameter>? Parameters             { get; }
		public string ImplicitFuncTopLabel                            { get; }
		public IList<LlvmIrFunctionLocalVariable>? ParameterVariables { get; }

		// Function writing state
		public string Indent                              { get; private set; } = LlvmIrGenerator.Indent;

		// Used for unnamed function parameters as well as unnamed local variables
		uint localSlot = 0;
		uint indentLevel = 1;

		public LlvmIrFunction (string name, Type returnType, int attributeSetID, IList<LlvmIrFunctionParameter>? parameters = null, bool skipParameterNames = false)
		{
			if (String.IsNullOrEmpty (name)) {
				throw new ArgumentException ("must not be null or empty", nameof (name));
			}
			Name = name;
			ReturnType = returnType ?? throw new ArgumentNullException (nameof (returnType));
			AttributeSetID = attributeSetID;
			Parameters = parameters?.Select (p => EnsureParameterName (p))?.ToList ()?.AsReadOnly ();
			ParameterVariables = Parameters?.Select (p => new LlvmIrFunctionLocalVariable (p.Type, p.Name, isNativePointer: p.IsNativePointer))?.ToList ()?.AsReadOnly ();

			// Unnamed local variables need to start from the value which equals [number_of_unnamed_parameters] + 1,
			// since there's an implicit label created for the top of the function whose name is `[number_of_unnamed_parameters]`
			ImplicitFuncTopLabel = localSlot.ToString (CultureInfo.InvariantCulture);
			localSlot++;

			LlvmIrFunctionParameter EnsureParameterName (LlvmIrFunctionParameter parameter)
			{
				if (parameter == null) {
					throw new InvalidOperationException ("null parameters aren't allowed");
				}

				if (skipParameterNames) {
					return parameter;
				}

				if (!String.IsNullOrEmpty (parameter.Name)) {
					return parameter;
				}

				string name = GetNextSlotName ();
				if (parameter.NativeFunction != null) {
					return new LlvmIrFunctionParameter (parameter.NativeFunction, parameter, name);
				}
				return new LlvmIrFunctionParameter (parameter, name);
			}
		}

		public LlvmIrFunctionLocalVariable MakeLocalVariable (Type type, string? name = null, bool isNativePointer = false)
		{
			if (String.IsNullOrEmpty (name)) {
				name = GetNextSlotName ();
			}

			return new LlvmIrFunctionLocalVariable (type, name, isNativePointer: isNativePointer);
		}

		public LlvmIrFunctionLocalVariable MakeLocalVariable (LlvmIrVariable variable, string? name = null, bool isNativePointer = false)
		{
			if (String.IsNullOrEmpty (name)) {
				name = GetNextSlotName ();
			}

			return new LlvmIrFunctionLocalVariable (variable, name, isNativePointer: isNativePointer);
		}

		public void IncreaseIndent ()
		{
			indentLevel++;
			Indent = MakeIndent (indentLevel);
		}

		public void DecreaseIndent ()
		{
			if (indentLevel == 0) {
				return;
			}

			indentLevel--;
			Indent = MakeIndent (indentLevel);
		}

		string MakeIndent (uint level)
		{
			switch (level) {
				case 0:
					return String.Empty;

				case 1:
					return Indent1;

				case 2:
					return Indent2;

				default:
					var sb = new StringBuilder ();
					for (uint i = 0; i < level; i++) {
						sb.Append (LlvmIrGenerator.Indent);
					}
					return sb.ToString ();
			}
		}

		string GetNextSlotName ()
		{
			string name = $"{localSlot.ToString (CultureInfo.InvariantCulture)}";
			localSlot++;
			return name;
		}
	}
}
