using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;

using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks.LLVM.IR
{
	// TODO: remove these aliases once everything is migrated to the LLVM.IR namespace
	using LlvmIrAddressSignificance = LLVMIR.LlvmIrAddressSignificance;
	using LlvmIrLinkage = LLVMIR.LlvmIrLinkage;
	using LlvmIrRuntimePreemption = LLVMIR.LlvmIrRuntimePreemption;
	using LlvmIrVisibility = LLVMIR.LlvmIrVisibility;
	using LlvmIrWritability = LLVMIR.LlvmIrWritability;
	using LlvmIrVariableOptions = LLVMIR.LlvmIrVariableOptions;

	partial class LlvmIrGenerator
	{
		const char IndentChar = '\t';

		sealed class BasicType
		{
			public readonly string Name;
			public readonly ulong Size;
			public readonly bool IsNumeric;

			public BasicType (string name, ulong size, bool isNumeric = true)
			{
				Name = name;
				Size = size;
				IsNumeric = isNumeric;
			}
		}

		public const string IRPointerType = "ptr";

		static readonly Dictionary<Type, BasicType> basicTypeMap = new Dictionary<Type, BasicType> {
			{ typeof (bool),   new ("i8",     1, isNumeric: false) },
			{ typeof (byte),   new ("i8",     1) },
			{ typeof (char),   new ("i16",    2) },
			{ typeof (sbyte),  new ("i8",     1) },
			{ typeof (short),  new ("i16",    2) },
			{ typeof (ushort), new ("i16",    2) },
			{ typeof (int),    new ("i32",    4) },
			{ typeof (uint),   new ("i32",    4) },
			{ typeof (long),   new ("i64",    8) },
			{ typeof (ulong),  new ("i64",    8) },
			{ typeof (float),  new ("float",  4) },
			{ typeof (double), new ("double", 8) },
			{ typeof (void),   new ("void",   0, isNumeric: false) },
		};

		public string FilePath           { get; }
		public string FileName           { get; }

		LlvmIrModuleTarget target;
		int currentIndentLevel = 0;
		string currentIndent = String.Empty;

		protected LlvmIrGenerator (string filePath, LlvmIrModuleTarget target)
		{
			FilePath = Path.GetFullPath (filePath);
			FileName = Path.GetFileName (filePath);
			this.target = target;
		}

		public static LlvmIrGenerator Create (AndroidTargetArch arch, string fileName)
		{
			return arch switch {
				AndroidTargetArch.Arm    => new LlvmIrGenerator (fileName, new LlvmIrModuleArmV7a ()),
				AndroidTargetArch.Arm64  => new LlvmIrGenerator (fileName, new LlvmIrModuleAArch64 ()),
				AndroidTargetArch.X86    => new LlvmIrGenerator (fileName, new LlvmIrModuleX86 ()),
				AndroidTargetArch.X86_64 => new LlvmIrGenerator (fileName, new LlvmIrModuleX64 ()),
				_ => throw new InvalidOperationException ($"Unsupported Android target ABI {arch}")
			};
		}

		public void Generate (TextWriter writer, LlvmIrModule module)
		{
			if (!String.IsNullOrEmpty (FilePath)) {
				WriteCommentLine (writer, $" ModuleID = '{FileName}'");
				writer.WriteLine ($"source_filename = \"{FileName}\"");
			}

			writer.WriteLine (target.DataLayout.Render ());
			writer.WriteLine ($"target triple = \"{target.Triple}\"");
			WriteStructureDeclarations (writer, module);
			WriteGlobalVariables (writer, module);

			// Bottom of file
			WriteExternalFunctionDeclarations (writer, module);
			WriteAttributeSets (writer, module);
		}

		void WriteGlobalVariables (TextWriter writer, LlvmIrModule module)
		{
			if (module.GlobalVariables == null || module.GlobalVariables.Count == 0) {
				return;
			}

			writer.WriteLine ();
			foreach (LlvmIrGlobalVariable gv in module.GlobalVariables) {
				writer.Write ('@');
				writer.Write (gv.Name);
				writer.Write (" = ");

				LlvmIrVariableOptions options = gv.Options ?? LlvmIrGlobalVariable.DefaultOptions;
				WriteLinkage (writer, options.Linkage);
				WritePreemptionSpecifier (writer, options.RuntimePreemption);
				WriteVisibility (writer, options.Visibility);
				WriteAddressSignificance (writer, options.AddressSignificance);
				WriteWritability (writer, options.Writability);

				WriteTypeAndValue (writer, gv, out ulong size, out bool isPointer);
				writer.Write (", align ");
				writer.Write ((isPointer ? target.NativePointerSize : size).ToString (CultureInfo.InvariantCulture));
			}
		}

		void WriteTypeAndValue (TextWriter writer, LlvmIrVariable variable, out ulong size, out bool isPointer)
		{
			string irType = MapToIRType (variable.Type, out size, out isPointer);
			writer.Write (irType);
			writer.Write (' ');

			if (variable.Value == null) {
				if (isPointer) {
					writer.Write ("null");
				}

				throw new InvalidOperationException ($"Internal error: variable of type {variable.Type} must not have a null value");
			}

			Type valueType = variable.Value.GetType ();
			if (valueType != variable.Type) {
				throw new InvalidOperationException ($"Internal error: variable type '{variable.Type}' is different to its value type, '{valueType}'");
			}

			WriteValue (writer, valueType, variable.Value);
		}

		void WriteValue (TextWriter writer, Type valueType, object value)
		{
			if (IsNumeric (valueType)) {
				writer.Write (MonoAndroidHelper.CultureInvariantToString (value));
				return;
			}

			throw new NotSupportedException ($"Internal error: value type '{valueType}' is unsupported");
		}

		void WriteLinkage (TextWriter writer, LlvmIrLinkage linkage)
		{
			if (linkage == LlvmIrLinkage.Default) {
				return;
			}

			try {
				WriteAttribute (writer, llvmLinkage[linkage]);
			} catch (Exception ex) {
				throw new InvalidOperationException ($"Internal error: unsupported writability '{writability}'", ex);
			}
		}

		void WriteWritability (TextWriter writer, LlvmIrWritability writability)
		{
			try {
				WriteAttribute (writer, llvmWritability[writability]);
			} catch (Exception ex) {
				throw new InvalidOperationException ($"Internal error: unsupported writability '{writability}'", ex);
			}
		}

		void WriteAddressSignificance (TextWriter writer, LlvmIrAddressSignificance addressSignificance)
		{
			if (addressSignificance == LlvmIrAddressSignificance.Default) {
				return;
			}

			try {
				WriteAttribute (writer, llvmAddressSignificance[addressSignificance]);
			} catch (Exception ex) {
				throw new InvalidOperationException ($"Internal error: unsupported address significance '{addressSignificance}'", ex);
			}
		}

		void WriteVisibility (TextWriter writer, LlvmIrVisibility visibility)
		{
			if (visibility == LlvmIrVisibility.Default) {
				return;
			}

			try {
				WriteAttribute (writer, llvmVisibility[visibility]);
			} catch (Exception ex) {
				throw new InvalidOperationException ($"Internal error: unsupported visibility '{visibility}'", ex);
			}
		}

		void WritePreemptionSpecifier (TextWriter writer, LlvmIrRuntimePreemption preemptionSpecifier)
		{
			if (preemptionSpecifier == LlvmIrRuntimePreemption.Default) {
				return;
			}

			try {
				WriteAttribute (writer, llvmRuntimePreemption[preemptionSpecifier]);
			} catch (Exception ex) {
				throw new InvalidOperationException ($"Internal error: unsupported preemption specifier '{preemptionSpecifier}'", ex);
			}
		}

		/// <summary>
		/// Write attribute named in <paramref ref="attr"/> followed by a single space
		/// </summary>
		void WriteAttribute (TextWriter writer, string attr)
		{
			writer.Write (attr);
			writer.Write (' ');
		}

		void WriteStructureDeclarations (TextWriter writer, LlvmIrModule module)
		{
			if (module.Structures == null || module.Structures.Count == 0) {
				Console.WriteLine (" #1");
				return;
			}

			foreach (IStructureInfo si in module.Structures) {
				Console.WriteLine (" #2");
				writer.WriteLine ();
				WriteStructureDeclaration (writer, si);
			}
		}

		void WriteStructureDeclaration (TextWriter writer, IStructureInfo si)
		{
			// $"%{typeDesignator}.{name} = type "
			writer.Write ('%');
			writer.Write (si.NativeTypeDesignator);
			writer.Write ('.');
			writer.Write (si.Name);
			writer.Write (" = type ");

			if (si.IsOpaque) {
				writer.WriteLine ("opaque");
			} else {
				writer.WriteLine ('{');
			}

			if (si.IsOpaque) {
				return;
			}

			IncreaseIndent ();
			for (int i = 0; i < si.Members.Count; i++) {
				StructureMemberInfo info = si.Members[i];
				string nativeType = MapManagedTypeToNative (info.MemberType);

				// TODO: nativeType can be an array, update to indicate that (and get the size)
				string arraySize;
				if (info.IsNativeArray) {
					arraySize = $"[{info.ArrayElements}]";
				} else {
					arraySize = String.Empty;
				}

				var comment = $" {nativeType} {info.Info.Name}{arraySize}";
				WriteStructureDeclarationField (info.IRType, comment, i == si.Members.Count - 1);
			}
			DecreaseIndent ();

			writer.WriteLine ('}');

			void WriteStructureDeclarationField (string typeName, string comment, bool last)
			{
				writer.Write (currentIndent);
				writer.Write (typeName);
				if (!last) {
					writer.Write (", ");
				} else {
					writer.Write (' ');
				}

				if (!String.IsNullOrEmpty (comment)) {
					WriteCommentLine (writer, comment);
				} else {
					writer.WriteLine ();
				}
			}
		}

		//
		// Functions syntax: https://llvm.org/docs/LangRef.html#functions
		//
		void WriteExternalFunctionDeclarations (TextWriter writer, LlvmIrModule module)
		{
			if (module.ExternalFunctions == null || module.ExternalFunctions.Count == 0) {
				return;
			}

			writer.WriteLine ();
			foreach (LlvmIrFunction func in module.ExternalFunctions) {
				// Must preserve state between calls, different targets may modify function state differently (e.g. set different parameter flags)
				ILlvmIrFunctionState funcState = func.SaveState ();

				foreach (LlvmIrFunctionParameter parameter in func.Signature.Parameters) {
					target.SetParameterFlags (parameter);
				}

				WriteFunctionAttributesComment (writer, func);
				writer.Write ("declare ");
				WriteFunctionDeclarationLeadingDecorations (writer, func);
				WriteFunctionSignature (writer, func, writeParameterNames: false);
				WriteFunctionDeclarationTrailingDecorations (writer, func);
				writer.WriteLine ();

				func.RestoreState (funcState);
			}
		}

		void WriteFunctionAttributesComment (TextWriter writer, LlvmIrFunction func)
		{
			if (func.AttributeSet == null) {
				return;
			}

			writer.WriteLine ();
			WriteCommentLine (writer, $"Function attributes: {func.AttributeSet.Render ()}");
		}

		void WriteFunctionDeclarationLeadingDecorations (TextWriter writer, LlvmIrFunction func)
		{
			WriteFunctionLeadingDecorations (writer, func, declaration: true);
		}

		void WriteFunctionDefinitionLeadingDecorations (TextWriter writer, LlvmIrFunction func)
		{
			WriteFunctionLeadingDecorations (writer, func, declaration: false);
		}

		void WriteFunctionLeadingDecorations (TextWriter writer, LlvmIrFunction func, bool declaration)
		{
			if (func.Linkage != LlvmIrLinkage.Default) {
				writer.Write (llvmLinkage[func.Linkage]);
				writer.Write (' ');
			}

			if (!declaration && func.RuntimePreemption != LlvmIrRuntimePreemption.Default) {
				writer.Write (llvmRuntimePreemption[func.RuntimePreemption]);
				writer.Write (' ');
			}

			if (func.Visibility != LlvmIrVisibility.Default) {
				writer.Write (llvmVisibility[func.Visibility]);
				writer.Write (' ');
			}
		}

		void WriteFunctionDeclarationTrailingDecorations (TextWriter writer, LlvmIrFunction func)
		{
			WriteFunctionTrailingDecorations (writer, func, declaration: true);
		}

		void WriteFunctionDefinitionTrailingDecorations (TextWriter writer, LlvmIrFunction func)
		{
			WriteFunctionTrailingDecorations (writer, func, declaration: false);
		}

		void WriteFunctionTrailingDecorations (TextWriter writer, LlvmIrFunction func, bool declaration)
		{
			if (func.AddressSignificance != LlvmIrAddressSignificance.Default) {
				writer.Write ($" {llvmAddressSignificance[func.AddressSignificance]}");
			}

			if (func.AttributeSet != null) {
				writer.Write ($" #{func.AttributeSet.Number}");
			}
		}

		void WriteFunctionSignature (TextWriter writer, LlvmIrFunction func, bool writeParameterNames)
		{
			writer.Write (MapToIRType (func.Signature.ReturnType));
			writer.Write (" @");
			writer.Write (func.Signature.Name);
			writer.Write ('(');

			bool first = true;
			foreach (LlvmIrFunctionParameter parameter in func.Signature.Parameters) {
				if (!first) {
					writer.Write (", ");
				} else {
					first = false;
				}

				writer.Write (MapToIRType (parameter.Type));
				WriteParameterAttributes (writer, parameter);
				if (writeParameterNames) {
					if (String.IsNullOrEmpty (parameter.Name)) {
						throw new InvalidOperationException ($"Internal error: parameter must have a name");
					}
					writer.Write (" %"); // Function arguments are always local variables
					writer.Write (parameter.Name);
				}
			}

			writer.Write (')');
		}

		void WriteParameterAttributes (TextWriter writer, LlvmIrFunctionParameter parameter)
		{
			var attributes = new List<string> ();
			if (AttributeIsSet (parameter.ImmArg)) {
				attributes.Add ("immarg");
			}

			if (AttributeIsSet (parameter.AllocPtr)) {
				attributes.Add ("allocptr");
			}

			if (AttributeIsSet (parameter.NoCapture)) {
				attributes.Add ("nocapture");
			}

			if (AttributeIsSet (parameter.NonNull)) {
				attributes.Add ("nonnull");
			}

			if (AttributeIsSet (parameter.NoUndef)) {
				attributes.Add ("noundef");
			}

			if (AttributeIsSet (parameter.ReadNone)) {
				attributes.Add ("readnone");
			}

			if (AttributeIsSet (parameter.SignExt)) {
				attributes.Add ("signext");
			}

			if (AttributeIsSet (parameter.ZeroExt)) {
				attributes.Add ("zeroext");
			}

			if (parameter.Align.HasValue) {
				attributes.Add ($"align({parameter.Align.Value})");
			}

			if (parameter.Dereferenceable.HasValue) {
				attributes.Add ($"dereferenceable({parameter.Dereferenceable.Value})");
			}

			if (attributes.Count == 0) {
				return;
			}

			writer.Write (' ');
			writer.Write (String.Join (" ", attributes));

			bool AttributeIsSet (bool? attr) => attr.HasValue && attr.Value;
		}

		void WriteAttributeSets (TextWriter writer, LlvmIrModule module)
		{
			if (module.AttributeSets == null || module.AttributeSets.Count == 0) {
				return;
			}

			writer.WriteLine ();
			foreach (LlvmIrFunctionAttributeSet attrSet in module.AttributeSets) {
				// Must not modify the original set, it is shared with other targets.
				var targetSet = new LlvmIrFunctionAttributeSet (attrSet);
				target.AddTargetSpecificAttributes (targetSet);

				IList<LlvmIrFunctionAttribute>? privateTargetSet = attrSet.GetPrivateTargetAttributes (target.TargetArch);
				if (privateTargetSet != null) {
					targetSet.Add (privateTargetSet);
				}

				writer.WriteLine ($"attributes #{targetSet.Number} {{ {targetSet.Render ()} }}");
			}
		}

		void WriteComment (TextWriter writer, string comment)
		{
			writer.Write (';');
			writer.Write (comment);
		}

		void WriteCommentLine (TextWriter writer, string comment)
		{
			WriteComment (writer, comment);
			writer.WriteLine ();
		}

		void IncreaseIndent ()
		{
			currentIndentLevel++;
			currentIndent = MakeIndentString ();
		}

		void DecreaseIndent ()
		{
			if (currentIndentLevel > 0) {
				currentIndentLevel--;
			}
			currentIndent = MakeIndentString ();
		}

		string MakeIndentString () => currentIndentLevel > 0 ? new String (IndentChar, currentIndentLevel) : String.Empty;

		static Type GetActualType (Type type)
		{
			// Arrays of types are handled elsewhere, so we obtain the array base type here
			if (type.IsArray) {
				return type.GetElementType ();
			}

			return type;
		}

		/// <summary>
		/// Map a managed <paramref name="type"/> to its <c>C++</c> counterpart. Only primitive types,
		/// <c>string</c> and <c>IntPtr</c> are supported.
		/// </summary>
		static string MapManagedTypeToNative (Type type)
		{
			Type baseType = GetActualType (type);

			if (baseType == typeof (bool)) return "bool";
			if (baseType == typeof (byte)) return "uint8_t";
			if (baseType == typeof (char)) return "char";
			if (baseType == typeof (sbyte)) return "int8_t";
			if (baseType == typeof (short)) return "int16_t";
			if (baseType == typeof (ushort)) return "uint16_t";
			if (baseType == typeof (int)) return "int32_t";
			if (baseType == typeof (uint)) return "uint32_t";
			if (baseType == typeof (long)) return "int64_t";
			if (baseType == typeof (ulong)) return "uint64_t";
			if (baseType == typeof (float)) return "float";
			if (baseType == typeof (double)) return "double";
			if (baseType == typeof (string)) return "char*";
			if (baseType == typeof (IntPtr)) return "void*";

			return type.GetShortName ();
		}

		static bool IsNumeric (Type type) => basicTypeMap.TryGetValue (type, out BasicType typeDesc) && typeDesc.IsNumeric;

		public static string MapToIRType (Type type)
		{
			return MapToIRType (type, out _, out _);
		}

		public static string MapToIRType (Type type, out ulong size)
		{
			return MapToIRType (type, out size, out _);
		}

		public static string MapToIRType (Type type, out ulong size, out bool isPointer)
		{
			type = GetActualType (type);
			if (!type.IsNativePointer () && basicTypeMap.TryGetValue (type, out BasicType typeDesc)) {
				size = typeDesc.Size;
				isPointer = false;
				return typeDesc.Name;
			}

			// if it's not a basic type, then it's an opaque pointer
			size = 0; // Will be determined by the specific target architecture class
			isPointer = true;
			return IRPointerType;
		}
	}
}
