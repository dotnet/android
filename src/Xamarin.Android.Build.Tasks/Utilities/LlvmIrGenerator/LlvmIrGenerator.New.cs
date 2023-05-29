using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

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
			WriteStrings (writer, module);
			WriteExternalFunctionDeclarations (writer, module);
			WriteAttributeSets (writer, module);
		}

		void WriteStrings (TextWriter writer, LlvmIrModule module)
		{
			if (module.Strings == null || module.Strings.Count == 0) {
				return;
			}

			writer.WriteLine ();
			WriteComment (writer, " Strings");

			foreach (LlvmIrStringGroup group in module.Strings) {
				writer.WriteLine ();

				if (!String.IsNullOrEmpty (group.Comment)) {
					WriteCommentLine (writer, group.Comment);
				}

				foreach (LlvmIrStringVariable info in group.Strings) {
					string s = QuoteString ((string)info.Value, out ulong size);

					WriteGlobalVariableStart (writer, info);
					writer.Write ('[');
					writer.Write (size.ToString (CultureInfo.InvariantCulture));
					writer.Write (" x i8] c");
					writer.Write (s);
					writer.Write (", align ");
					writer.WriteLine (target.GetAggregateAlignment (1, size).ToString (CultureInfo.InvariantCulture));
				}
			}
		}

		void WriteGlobalVariables (TextWriter writer, LlvmIrModule module)
		{
			if (module.GlobalVariables == null || module.GlobalVariables.Count == 0) {
				return;
			}

			writer.WriteLine ();
			foreach (LlvmIrGlobalVariable gv in module.GlobalVariables) {
				WriteGlobalVariable (writer, gv);
			}
		}

		public void WriteGlobalVariableStart (TextWriter writer, LlvmIrGlobalVariable variable)
		{
			writer.Write ('@');
			writer.Write (variable.Name);
			writer.Write (" = ");

			LlvmIrVariableOptions options = variable.Options ?? LlvmIrGlobalVariable.DefaultOptions;
			WriteLinkage (writer, options.Linkage);
			WritePreemptionSpecifier (writer, options.RuntimePreemption);
			WriteVisibility (writer, options.Visibility);
			WriteAddressSignificance (writer, options.AddressSignificance);
			WriteWritability (writer, options.Writability);
		}

		public void WriteGlobalVariable (TextWriter writer, LlvmIrGlobalVariable variable)
		{
			WriteGlobalVariableStart (writer, variable);
			WriteTypeAndValue (writer, variable, out ulong typeSize, out bool isPointer, out bool isAggregate);
			writer.Write (", align ");

			ulong alignment;
			if (isAggregate) {
				uint count = GetAggregateValueElementCount (variable);
				alignment = (ulong)target.GetAggregateAlignment ((int)typeSize, count * typeSize);
			} else if (isPointer) {
				alignment = target.NativePointerSize;
			} else {
				alignment = typeSize;
			}

			writer.WriteLine (alignment.ToString (CultureInfo.InvariantCulture));
		}

		void WriteTypeAndValue (TextWriter writer, LlvmIrVariable variable, out ulong size, out bool isPointer, out bool isAggregate)
		{
			WriteType (writer, variable, out size, out isPointer, out isAggregate);
			writer.Write (' ');

			if (variable.Value == null) {
				if (isPointer) {
					writer.Write ("null");
				}

				throw new InvalidOperationException ($"Internal error: variable of type {variable.Type} must not have a null value");
			}

			Type valueType;
			if (variable.Value is LlvmIrVariable referencedVariable) {
				valueType = referencedVariable.Type;
			} else {
				valueType = variable.Value.GetType ();
			}

			if (valueType != variable.Type && !LlvmIrModule.NameValueArrayType.IsAssignableFrom (variable.Type)) {
				throw new InvalidOperationException ($"Internal error: variable type '{variable.Type}' is different to its value type, '{valueType}'");
			}

			WriteValue (writer, valueType, variable);
		}

		uint GetAggregateValueElementCount (LlvmIrVariable variable) => GetAggregateValueElementCount (variable.Type, variable.Value);

		uint GetAggregateValueElementCount (Type type, object? value)
		{
			if (!IsArray (type)) {
				throw new InvalidOperationException ($"Internal error: unknown type {type} when trying to determine aggregate type element count");
			}

			if (value == null) {
				return 0;
			}

			var info = (LlvmIrArrayVariableInfo)value;
			return (uint)info.Entries.Count;
		}

		void WriteType (TextWriter writer, LlvmIrVariable variable, out ulong size, out bool isPointer, out bool isAggregate)
		{
			WriteType (writer, variable.Type, variable.Value, out size, out isPointer, out isAggregate);
		}

		void WriteType (TextWriter writer, Type type, object? value, out ulong size, out bool isPointer, out bool isAggregate)
		{
			string irType;

			if (IsArray (type)) {
				isAggregate = true;
				irType = GetIRType (type, out size, out isPointer);

				writer.Write ('[');
				writer.Write (GetAggregateValueElementCount (type, value).ToString (CultureInfo.InvariantCulture));
				writer.Write (" x ");
				writer.Write (irType);
				writer.Write (']');
				return;
			}

			isAggregate = false;
			irType = GetIRType (type, out size, out isPointer);
			writer.Write (irType);
		}

		bool IsArray (Type t) => t == typeof(LlvmIrArrayVariableInfo);

		void WriteValue (TextWriter writer, Type valueType, LlvmIrVariable variable)
		{
			if (variable.Value is LlvmIrVariable variableRef) {
				writer.Write (variableRef.Reference);
				return;
			}

			if (IsArray (variable.Type)) {
				uint count = GetAggregateValueElementCount (variable);
				if (count == 0) {
					writer.Write ("zeroinitializer");
					return;
				}

				WriteArray (writer, (LlvmIrArrayVariableInfo)variable.Value);
				return;
			}

			if (IsNumeric (valueType)) {
				writer.Write (MonoAndroidHelper.CultureInvariantToString (variable.Value));
				return;
			}

			throw new NotSupportedException ($"Internal error: value type '{valueType}' is unsupported");
		}

		void WriteValue (TextWriter writer, Type type, object? value)
		{
			if (value is LlvmIrVariable variableRef) {
				writer.Write (variableRef.Reference);
				return;
			}

			if (IsNumeric (type)) {
				writer.Write (MonoAndroidHelper.CultureInvariantToString (value));
				return;
			}

			throw new NotSupportedException ($"Internal error: value type '{type}' is unsupported");
		}

		void WriteArray (TextWriter writer, LlvmIrArrayVariableInfo arrayInfo)
		{
			writer.WriteLine (" [");
			IncreaseIndent ();

			string irType;
			if (arrayInfo.ElementType == typeof(LlvmIrStringVariable)) {
				irType = MapToIRType (typeof(string));
			} else {
				irType = MapToIRType (arrayInfo.ElementType);
			}

			bool first = true;
			foreach (object entry in arrayInfo.Entries) {
				if (!first) {
					writer.WriteLine (',');
				} else {
					first = false;
				}
				writer.Write (currentIndent);
				WriteType (writer, arrayInfo.ElementType, entry, out _, out _, out _);
				writer.Write (' ');
				WriteValue (writer, arrayInfo.ElementType, entry);
			}
			writer.WriteLine ();

			DecreaseIndent ();
			writer.Write (']');
		}

		void WriteLinkage (TextWriter writer, LlvmIrLinkage linkage)
		{
			if (linkage == LlvmIrLinkage.Default) {
				return;
			}

			try {
				WriteAttribute (writer, llvmLinkage[linkage]);
			} catch (Exception ex) {
				throw new InvalidOperationException ($"Internal error: unsupported writability '{linkage}'", ex);
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

		public void WriteComment (TextWriter writer, string comment)
		{
			writer.Write (';');
			writer.Write (comment);
		}

		public void WriteCommentLine (TextWriter writer, string comment)
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

		/// <summary>
		/// Maps managed type to equivalent IR type.  Puts type size in <paramref name="size"/> and whether or not the type
		/// is a pointer in <paramref name="isPointer"/>.  When a type is determined to be a pointer, <paramref name="size"/>
		/// will be set to <c>0</c>, because this method doesn't have access to the generator target.  In order to adjust pointer
		/// size, the instance method <see cref="GetIRType"/> must be called (private to the generator as other classes should not
		/// have any need to know the pointer size).
		/// </summary>
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

		string GetIRType (Type type, out ulong size, out bool isPointer)
		{
			string ret = MapToIRType (type, out size, out isPointer);
			if (isPointer && size == 0) {
				size = target.NativePointerSize;
			}

			return ret;
		}

		public static string QuoteStringNoEscape (string s)
		{
			return $"\"{s}\"";
		}

		public static string QuoteString (string value, bool nullTerminated = true)
		{
			return QuoteString (value, out _, nullTerminated);
		}

		public static string QuoteString (byte[] bytes)
		{
			return QuoteString (bytes, bytes.Length, out _, nullTerminated: false);
		}

		public static string QuoteString (string value, out ulong stringSize, bool nullTerminated = true)
		{
			var encoding = Encoding.UTF8;
			int byteCount = encoding.GetByteCount (value);
			var bytes = ArrayPool<byte>.Shared.Rent (byteCount);
			try {
				encoding.GetBytes (value, 0, value.Length, bytes, 0);
				return QuoteString (bytes, byteCount, out stringSize, nullTerminated);
			} finally {
				ArrayPool<byte>.Shared.Return (bytes);
			}
		}

		public static string QuoteString (byte[] bytes, int byteCount, out ulong stringSize, bool nullTerminated = true)
		{
			var sb = new StringBuilder (byteCount * 2); // rough estimate of capacity

			byte b;
			for (int i = 0; i < byteCount; i++) {
				b = bytes [i];
				if (b != '"' && b != '\\' && b >= 32 && b < 127) {
					sb.Append ((char)b);
					continue;
				}

				sb.Append ('\\');
				sb.Append ($"{b:X2}");
			}

			stringSize = (ulong) byteCount;
			if (nullTerminated) {
				stringSize++;
				sb.Append ("\\00");
			}

			return QuoteStringNoEscape (sb.ToString ());
		}
	}
}
