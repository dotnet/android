using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Xamarin.Android.Tasks.LLVM.IR
{
	// TODO: remove these aliases once everything is migrated to the LLVM.IR namespace
	using LlvmIrAddressSignificance = Xamarin.Android.Tasks.LLVMIR.LlvmIrAddressSignificance;

	class LlvmIrFunctionParameter : LlvmIrLocalVariable
	{
		// To save on time, we declare only attributes that are actually used in our generated code.  More will be added, as needed.

		/// <summary>
		/// <c>align(n)</c> attribute, see <see href="https://github.com/llvm/llvm-project/blob/5729e63ac7b47c6ad40f904fedafad3c07cf71ea/llvm/docs/LangRef.rst#L1239"/>
		/// </summary>
		public uint? Align { get; set; }

		/// <summary>
		/// <c>allocptr</c> attribute, see <see href="https://github.com/llvm/llvm-project/blob/5729e63ac7b47c6ad40f904fedafad3c07cf71ea/llvm/docs/LangRef.rst#L1413"/>
		/// </summary>
		public bool AllocPtr { get; set; }

		/// <summary>
		/// <c>dereferenceable(n)</c> attribute, see <see href="https://github.com/llvm/llvm-project/blob/5729e63ac7b47c6ad40f904fedafad3c07cf71ea/llvm/docs/LangRef.rst#L1324"/>
		/// </summary>
		public uint? Dereferenceable { get; set; }

		/// <summary>
		/// <c>immarg</c> attribute, see <see href="https://github.com/llvm/llvm-project/blob/5729e63ac7b47c6ad40f904fedafad3c07cf71ea/llvm/docs/LangRef.rst#L1383"/>
		/// </summary>
		public bool ImmArg { get; set; }

		/// <summary>
		/// <c>nocapture</c> attribute, see <see href="https://github.com/llvm/llvm-project/blob/5729e63ac7b47c6ad40f904fedafad3c07cf71ea/llvm/docs/LangRef.rst#L1279"/>
		/// </summary>
		public bool NoCapture { get; set; }

		/// <summary>
		/// <c>nonnull</c> attribute, see <see href="https://github.com/llvm/llvm-project/blob/5729e63ac7b47c6ad40f904fedafad3c07cf71ea/llvm/docs/LangRef.rst#L1316"/>
		/// </summary>
		public bool NonNull { get; set; }

		/// <summary>
		/// <c>noundef</c> attribute, see <see href="https://github.com/llvm/llvm-project/blob/5729e63ac7b47c6ad40f904fedafad3c07cf71ea/llvm/docs/LangRef.rst#L1390"/>
		/// </summary>
		public bool NoUndef { get; set; }

		/// <summary>
		/// <c>noundef</c> attribute, see <see href="https://github.com/llvm/llvm-project/blob/5729e63ac7b47c6ad40f904fedafad3c07cf71ea/llvm/docs/LangRef.rst#L1420"/>
		/// </summary>
		public bool ReadNone { get; set; }

		/// <summary>
		/// <c>zeroext</c> attribute, see <see href="https://github.com/llvm/llvm-project/blob/5729e63ac7b47c6ad40f904fedafad3c07cf71ea/llvm/docs/LangRef.rst#L1090"/>
		/// </summary>
		public bool ZeroExt { get; set; }

		public LlvmIrFunctionParameter (Type type, string? name = null)
			: base (type, name)
		{
			NameMatters = false;
		}
	}

	class LlvmIrFunctionSignature : IEquatable<LlvmIrFunctionSignature>
	{
		public string Name                               { get; }
		public Type ReturnType                           { get; }
		public IList<LlvmIrFunctionParameter> Parameters { get; }
		public LlvmIrFunctionAttributeSet? AttributeSet  { get; set; }

		public LlvmIrFunctionSignature (string name, Type returnType, IList<LlvmIrFunctionParameter>? parameters = null, LlvmIrFunctionAttributeSet? attributeSet = null)
		{
			if (String.IsNullOrEmpty (name)) {
				throw new ArgumentException ("must not be null or empty", nameof (name));
			}

			Name = name;
			ReturnType = returnType;
			Parameters = parameters ?? new List<LlvmIrFunctionParameter> ();
			AttributeSet = attributeSet;
		}

		/// <summary>
		/// Create new signature using data from the <see cref="templateSignature"/> one, with the exception of name.
		/// Useful when there are several functions with different names but identical parameters and return types.
		/// </summary>
		public LlvmIrFunctionSignature (string name, LlvmIrFunctionSignature templateSignature)
			: this (name, templateSignature.ReturnType, templateSignature.Parameters, templateSignature.AttributeSet)
		{}

		public override int GetHashCode ()
		{
			int hc =
				Name.GetHashCode () ^
				Parameters.GetHashCode () ^
				ReturnType.GetHashCode () ^
				(AttributeSet?.GetHashCode () ?? 0);

			foreach (LlvmIrFunctionParameter p in Parameters) {
				hc ^= p.GetHashCode ();
			}

			return hc;
		}

	        public override bool Equals (object obj)
		{
			var sig = obj as LlvmIrFunctionSignature;
			if (sig == null) {
				return false;
			}

			return Equals (sig);
		}

	        public bool Equals (LlvmIrFunctionSignature other)
		{
			if (other == null) {
				return false;
			}

			if (Parameters.Count != other.Parameters.Count ||
			    ReturnType != other.ReturnType ||
			    String.Compare (Name, other.Name, StringComparison.Ordinal) != 0
			) {
				return false;
			}

			for (int i = 0; i < Parameters.Count; i++) {
				if (Parameters[i] != other.Parameters[i]) {
					return false;
				}
			}

			return true;
		}
	}

	/// <summary>
	/// Describes a native function to be emitted or declared and keeps code emitting state between calls to various generator.
	/// methods.
	/// </summary>
	class LlvmIrFunction : IEquatable<LlvmIrFunction>
	{
		public LlvmIrFunctionSignature Signature             { get; }
		public LlvmIrAddressSignificance AddressSignificance { get; set; } = LlvmIrAddressSignificance.LocalUnnamed;

		public LlvmIrFunction (LlvmIrFunctionSignature signature)
		{
			Signature = signature;
		}

		/// <summary>
		/// Create new function using data from the <see cref="templateSignature"/> signature, with the exception of name.
		/// Useful when there are several functions with different names but identical parameters and return types.
		/// </summary>
		public LlvmIrFunction (string name, LlvmIrFunctionSignature templateSignature)
			: this (new LlvmIrFunctionSignature (name, templateSignature))
		{}

		public LlvmIrFunction (string name, Type returnType, List<LlvmIrFunctionParameter>? parameters = null, LlvmIrFunctionAttributeSet? attributeSet = null)
			: this (new LlvmIrFunctionSignature (name, returnType, parameters, attributeSet))
		{}

		public override int GetHashCode ()
		{
			return Signature.GetHashCode ();
		}

		public override bool Equals (object obj)
		{
			var func = obj as LlvmIrFunction;
			if (func == null) {
				return false;
			}

			return Equals (func);
		}

	        public bool Equals (LlvmIrFunction other)
		{
			if (other == null) {
				return false;
			}

			return Signature == other.Signature;
		}
	}
}

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
			: base (variable, name, variable.IsNativePointer || isNativePointer)
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

		public LlvmIrFunctionArgument (LlvmIrFunctionLocalVariable variable, bool isNull = false)
		{
			Type = typeof(LlvmIrFunctionLocalVariable);
			Value = isNull ? null : variable;
			IsNativePointer = variable.IsNativePointer;
		}

		public LlvmIrFunctionArgument (LlvmIrVariableReference variable, bool isNull = false)
		{
			Type = typeof(LlvmIrVariableReference);
			Value = isNull ? null : variable;
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
		public string PreviousBlockStartLabel                         => previousBlockStartLabel;
		public string PreviousBlockEndLabel                           => previousBlockEndLabel;

		// Function writing state
		public string Indent                              { get; private set; } = LlvmIrGenerator.Indent;

		// Used for unnamed function parameters as well as unnamed local variables
		uint localSlot = 0;
		uint indentLevel = 1;

		// This is a hack to work around a requirement in LLVM IR compiler that we cannot meet with a forward-only, single-pass generator like ours.  Namely:
		// LLVM IR compiler uses a monotonically increasing counter for all the unnamed function parameters, local variables and labels and it expects them to be
		// used in strict sequence in the generated code.  However, branch instructions need to know their target labels, so in a generator like ours we'd have to allocate
		// their names before outputting the branch instruction and the blocks that we refer to.  However, if those blocks use unnamed (counted) variables themselves, then
		// they would be allocated numbers out of sequence, resulting in code similar to:
		//
		//         br i1 %7, label %8, label %9
		//    8:
		//         %11 = load i8*, i8** %func_params_render, align 8
		//         br label %10
		//
		//    9:
		//         store i8* null, i8** %func_params_render, align 8
		//         br label %10
		//
		//    10:
		//
		// In this instance, the LLVM IR compiler would complain about the line after `8:` as follows:
		//
		//   error: instruction expected to be numbered '%9'
		//
		// Since we have no time to rewrite the generator in some manner that would support this scenario (e.g. two-pass generator with an AST and label/parameter/variable
		// placeholders), we need to employ a different technique: named labels.  They won't be subject to the samme restrictions, but they pose another problem - if a
		// given block of code is output more than once and generates the same label names, we'd have another error on our hands.  Thus this counter variable, which will
		// generate a unique label name by appending a number to some prefix
		uint labelCounter = 0;

		// Names of the previous basic code block's start and end delimiters (labels), needed when the `phi` instruction is emitted.  The instruction needs to refer to the
		// code block preceding the current one, which will be the two labels preceding the current one (including the implicit label pointing to the beginning of the
		// function, before any code.
		//
		// See also https://llvm.org/docs/LangRef.html#phi-instruction
		//
		string previousBlockStartLabel;
		string previousBlockEndLabel;
		string currentBlockStartLabel;

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
			ImplicitFuncTopLabel = previousBlockStartLabel = previousBlockEndLabel = currentBlockStartLabel = localSlot.ToString (CultureInfo.InvariantCulture);
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

		/// <summary>
		/// Return name of a local label with unique name.
		/// </summary>
		public string MakeUniqueLabel (string? prefix = null)
		{
			string name = String.IsNullOrEmpty (prefix) ? "ll" : prefix;
			return ShiftBlockLabels ($"{name}{labelCounter++}");
		}

		public string MakeLabel (string labelName)
		{
			return ShiftBlockLabels (labelName);
		}

		string ShiftBlockLabels (string newCurrentLabel)
		{
			previousBlockStartLabel = previousBlockEndLabel;
			previousBlockEndLabel = currentBlockStartLabel;
			currentBlockStartLabel = newCurrentLabel;

			return currentBlockStartLabel;
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
