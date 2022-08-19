using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Reflection;

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
		public bool IsCplusPlusReference { get; }

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
	}

	class LlvmIrFunctionArgument
	{
		public object Value { get; }
		public Type Type    { get; }

		public LlvmIrFunctionArgument (Type type, object? value = null)
		{
			Type = type ?? throw new ArgumentNullException (nameof (type));

			if (value != null && value.GetType () != type) {
				throw new ArgumentException ($"value type '{value.GetType ()}' does not match the argument type '{type}'");
			}

			Value = value;
		}

		public LlvmIrFunctionArgument (LlvmIrFunctionLocalVariable variable)
		{
			Type = typeof(LlvmIrFunctionLocalVariable);
			Value = variable;
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

		public LlvmIrFunction (string name, Type returnType, int attributeSetID, List<LlvmIrFunctionParameter>? parameters = null)
		{
			if (String.IsNullOrEmpty (name)) {
				throw new ArgumentException ("must not be null or empty", nameof (name));
			}
			Name = name;
			ReturnType = returnType ?? throw new ArgumentNullException (nameof (returnType));
			AttributeSetID = attributeSetID;
			Parameters = parameters?.Select (p => EnsureParameterName (p))?.ToList ()?.AsReadOnly ();
			ParameterVariables = Parameters?.Select (p => new LlvmIrFunctionLocalVariable (p.Type, p.Name))?.ToList ()?.AsReadOnly ();

			// Unnamed local variables need to start from the value which equals [number_of_unnamed_parameters] + 1,
			// since there's an implicit label created for the top of the function whose name is `[number_of_unnamed_parameters]`
			ImplicitFuncTopLabel = localSlot.ToString (CultureInfo.InvariantCulture);
			localSlot++;

			LlvmIrFunctionParameter EnsureParameterName (LlvmIrFunctionParameter parameter)
			{
				if (parameter == null) {
					throw new InvalidOperationException ("null parameters aren't allowed");
				}

				if (!String.IsNullOrEmpty (parameter.Name)) {
					return parameter;
				}

				string name = GetNextSlotName ();
				if (parameter.NativeFunction != null) {
					return new LlvmIrFunctionParameter (parameter.NativeFunction, name, parameter.IsNativePointer, parameter.IsCplusPlusReference);
				}
				return new LlvmIrFunctionParameter (parameter.Type, name, parameter.IsNativePointer, parameter.IsCplusPlusReference);
			}
		}

		public LlvmIrFunctionLocalVariable MakeLocalVariable (Type type, string? name = null)
		{
			if (String.IsNullOrEmpty (name)) {
				name = GetNextSlotName ();
			}

			return new LlvmIrFunctionLocalVariable (type, name);
		}

		public LlvmIrFunctionLocalVariable MakeLocalVariable (LlvmIrVariable variable, string? name = null)
		{
			if (String.IsNullOrEmpty (name)) {
				name = GetNextSlotName ();
			}

			return new LlvmIrFunctionLocalVariable (variable, name);
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
