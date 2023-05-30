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

		sealed class LlvmTypeInfo
		{
			public readonly bool IsPointer;
			public readonly bool IsAggregate;
			public readonly bool IsStructure;
			public readonly ulong Size;
			public readonly ulong MaxFieldAlignment;

			public LlvmTypeInfo (bool isPointer, bool isAggregate, bool isStructure, ulong size, ulong maxFieldAlignment)
			{
				IsPointer = isPointer;
				IsAggregate = isAggregate;
				IsStructure = isStructure;
				Size = size;
				MaxFieldAlignment = maxFieldAlignment;
			}
		}

		sealed class WriteContext
		{
			int currentIndentLevel = 0;

			public readonly TextWriter Output;
			public readonly LlvmIrModule Module;
			public string CurrentIndent { get; private set; } = String.Empty;

			public WriteContext (TextWriter writer, LlvmIrModule module)
			{
				Output = writer;
				Module = module;
			}

			public void IncreaseIndent ()
			{
				currentIndentLevel++;
				CurrentIndent = MakeIndentString ();
			}

			public void DecreaseIndent ()
			{
				if (currentIndentLevel > 0) {
					currentIndentLevel--;
				}
				CurrentIndent = MakeIndentString ();
			}

			string MakeIndentString () => currentIndentLevel > 0 ? new String (IndentChar, currentIndentLevel) : String.Empty;
		}

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
			var context = new WriteContext (writer, module);
			if (!String.IsNullOrEmpty (FilePath)) {
				WriteCommentLine (context, $" ModuleID = '{FileName}'");
				context.Output.WriteLine ($"source_filename = \"{FileName}\"");
			}

			context.Output.WriteLine (target.DataLayout.Render ());
			context.Output.WriteLine ($"target triple = \"{target.Triple}\"");
			WriteStructureDeclarations (context);
			WriteGlobalVariables (context);

			// Bottom of file
			WriteStrings (context);
			WriteExternalFunctionDeclarations (context);
			WriteAttributeSets (context);
		}

		void WriteStrings (WriteContext context)
		{
			if (context.Module.Strings == null || context.Module.Strings.Count == 0) {
				return;
			}

			context.Output.WriteLine ();
			WriteComment (context, " Strings");

			foreach (LlvmIrStringGroup group in context.Module.Strings) {
				context.Output.WriteLine ();

				if (!String.IsNullOrEmpty (group.Comment)) {
					WriteCommentLine (context, group.Comment);
				}

				foreach (LlvmIrStringVariable info in group.Strings) {
					string s = QuoteString ((string)info.Value, out ulong size);

					WriteGlobalVariableStart (context, info);
					context.Output.Write ('[');
					context.Output.Write (size.ToString (CultureInfo.InvariantCulture));
					context.Output.Write (" x i8] c");
					context.Output.Write (s);
					context.Output.Write (", align ");
					context.Output.WriteLine (target.GetAggregateAlignment (1, size).ToString (CultureInfo.InvariantCulture));
				}
			}
		}

		void WriteGlobalVariables (WriteContext context)
		{
			if (context.Module.GlobalVariables == null || context.Module.GlobalVariables.Count == 0) {
				return;
			}

			context.Output.WriteLine ();
			foreach (LlvmIrGlobalVariable gv in context.Module.GlobalVariables) {
				if (gv.BeforeWriteCallback != null) {
					gv.BeforeWriteCallback (gv, target);
				}
				WriteGlobalVariable (context, gv);
			}
		}

		void WriteGlobalVariableStart (WriteContext context, LlvmIrGlobalVariable variable)
		{
			if (!String.IsNullOrEmpty (variable.Comment)) {
				WriteCommentLine (context, variable.Comment);
			}
			context.Output.Write ('@');
			context.Output.Write (variable.Name);
			context.Output.Write (" = ");

			LlvmIrVariableOptions options = variable.Options ?? LlvmIrGlobalVariable.DefaultOptions;
			WriteLinkage (context, options.Linkage);
			WritePreemptionSpecifier (context, options.RuntimePreemption);
			WriteVisibility (context, options.Visibility);
			WriteAddressSignificance (context, options.AddressSignificance);
			WriteWritability (context, options.Writability);
		}

		void WriteGlobalVariable (WriteContext context, LlvmIrGlobalVariable variable)
		{
			context.Output.WriteLine ();
			WriteGlobalVariableStart (context, variable);
			WriteTypeAndValue (context, variable, out LlvmTypeInfo typeInfo);
			context.Output.Write (", align ");

			ulong alignment;
			if (typeInfo.IsAggregate) {
				uint count = GetAggregateValueElementCount (variable);
				alignment = (ulong)target.GetAggregateAlignment ((int)typeInfo.Size, count * typeInfo.Size);
			} else if (typeInfo.IsStructure) {
				alignment = (ulong)target.GetAggregateAlignment ((int)typeInfo.MaxFieldAlignment, typeInfo.Size);
			} else if (typeInfo.IsPointer) {
				alignment = target.NativePointerSize;
			} else {
				alignment = typeInfo.Size;
			}

			context.Output.WriteLine (alignment.ToString (CultureInfo.InvariantCulture));
		}

		void WriteTypeAndValue (WriteContext context, LlvmIrVariable variable, out LlvmTypeInfo typeInfo)
		{
			WriteType (context, variable, out typeInfo);
			context.Output.Write (' ');

			if (variable.Value == null) {
				if (typeInfo.IsPointer) {
					context.Output.Write ("null");
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

			WriteValue (context, valueType, variable);
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

		void WriteType (WriteContext context, LlvmIrVariable variable, out LlvmTypeInfo typeInfo)
		{
			WriteType (context, variable.Type, variable.Value, out typeInfo);
		}

		void WriteType (WriteContext context, Type type, object? value, out LlvmTypeInfo typeInfo)
		{
			if (type == typeof(StructureInstance)) {
				if (value == null) {
					throw new ArgumentException ("must not be null for structure instances", nameof (value));
				}

				var si = (StructureInstance)value;
				ulong alignment;

				if (si.Info.HasPointers && target.NativePointerSize > si.Info.MaxFieldAlignment) {
					alignment = target.NativePointerSize;
				} else {
					alignment = (ulong)si.Info.MaxFieldAlignment;
				}

				typeInfo = new LlvmTypeInfo (
					isPointer: false,
					isAggregate: false,
					isStructure: true,
					size: si.Info.Size,
					maxFieldAlignment: alignment
				);

				context.Output.Write ('%');
				context.Output.Write (si.Info.NativeTypeDesignator);
				context.Output.Write ('.');
				context.Output.Write (si.Info.Name);
				return;
			}

			string irType;
			ulong size;
			bool isPointer;

			if (IsArray (type)) {
				irType = GetIRType (type, out size, out isPointer);
				typeInfo = new LlvmTypeInfo (
					isPointer: isPointer,
					isAggregate: true,
					isStructure: false,
					size: size,
					maxFieldAlignment: size
				);

				context.Output.Write ('[');
				context.Output.Write (GetAggregateValueElementCount (type, value).ToString (CultureInfo.InvariantCulture));
				context.Output.Write (" x ");
				context.Output.Write (irType);
				context.Output.Write (']');
				return;
			}

			irType = GetIRType (type, out size, out isPointer);
			typeInfo = new LlvmTypeInfo (
				isPointer: isPointer,
				isAggregate: false,
				isStructure: false,
				size: size,
				maxFieldAlignment: size
			);
			context.Output.Write (irType);
		}

		bool IsArray (Type t) => t == typeof(LlvmIrArrayVariableInfo);

		void WriteValue (WriteContext context, Type valueType, LlvmIrVariable variable)
		{
			if (IsArray (variable.Type)) {
				uint count = GetAggregateValueElementCount (variable);
				if (count == 0) {
					context.Output.Write ("zeroinitializer");
					return;
				}

				WriteArray (context, (LlvmIrArrayVariableInfo)variable.Value);
				return;
			}

			WriteValue (context, valueType, variable.Value);
		}

		void WriteValue (WriteContext context, Type type, object? value)
		{
			if (value is LlvmIrVariable variableRef) {
				context.Output.Write (variableRef.Reference);
				return;
			}

			if (IsNumeric (type)) {
				context.Output.Write (MonoAndroidHelper.CultureInvariantToString (value));
				return;
			}

			if (type == typeof(bool)) {
				context.Output.Write ((bool)value ? '1' : '0');
				return;
			}

			if (type == typeof(StructureInstance)) {
				WriteStructureValue (context, (StructureInstance?)value);
				return;
			}

			throw new NotSupportedException ($"Internal error: value type '{type}' is unsupported");
		}

		void WriteStructureValue (WriteContext context, StructureInstance? instance)
		{
			if (instance == null) {
				context.Output.Write ("zeroinitializer");
				return;
			}

			context.Output.WriteLine ('{');
			context.IncreaseIndent ();

			StructureInfo info = instance.Info;
			int lastMember = info.Members.Count - 1;

			for (int i = 0; i < info.Members.Count; i++) {
				StructureMemberInfo smi = info.Members[i];

				context.Output.Write (context.CurrentIndent);
				WriteType (context, smi.MemberType, value: null, out _);
				context.Output.Write (' ');

				object? value = GetTypedMemberValue (context, info, smi, instance, smi.MemberType);
				WriteValue (context, smi.MemberType, value);

				if (i < lastMember) {
					context.Output.Write (", ");
				}

				WriteCommentLine (context, $" {MapManagedTypeToNative (smi.MemberType)} {smi.Info.Name}");
			}

			context.DecreaseIndent ();
			context.Output.Write ('}');
		}

		void WriteArray (WriteContext context, LlvmIrArrayVariableInfo arrayInfo)
		{
			context.Output.WriteLine (" [");
			context.IncreaseIndent ();

			string irType;
			if (arrayInfo.ElementType == typeof(LlvmIrStringVariable)) {
				irType = MapToIRType (typeof(string));
			} else {
				irType = MapToIRType (arrayInfo.ElementType);
			}

			bool first = true;
			foreach (object entry in arrayInfo.Entries) {
				if (!first) {
					context.Output.WriteLine (',');
				} else {
					first = false;
				}
				context.Output.Write (context.CurrentIndent);
				WriteType (context, arrayInfo.ElementType, entry, out _);
				context.Output.Write (' ');
				WriteValue (context, arrayInfo.ElementType, entry);
			}
			context.Output.WriteLine ();

			context.DecreaseIndent ();
			context.Output.Write (']');
		}

		void WriteLinkage (WriteContext context, LlvmIrLinkage linkage)
		{
			if (linkage == LlvmIrLinkage.Default) {
				return;
			}

			try {
				WriteAttribute (context, llvmLinkage[linkage]);
			} catch (Exception ex) {
				throw new InvalidOperationException ($"Internal error: unsupported writability '{linkage}'", ex);
			}
		}

		void WriteWritability (WriteContext context, LlvmIrWritability writability)
		{
			try {
				WriteAttribute (context, llvmWritability[writability]);
			} catch (Exception ex) {
				throw new InvalidOperationException ($"Internal error: unsupported writability '{writability}'", ex);
			}
		}

		void WriteAddressSignificance (WriteContext context, LlvmIrAddressSignificance addressSignificance)
		{
			if (addressSignificance == LlvmIrAddressSignificance.Default) {
				return;
			}

			try {
				WriteAttribute (context, llvmAddressSignificance[addressSignificance]);
			} catch (Exception ex) {
				throw new InvalidOperationException ($"Internal error: unsupported address significance '{addressSignificance}'", ex);
			}
		}

		void WriteVisibility (WriteContext context, LlvmIrVisibility visibility)
		{
			if (visibility == LlvmIrVisibility.Default) {
				return;
			}

			try {
				WriteAttribute (context, llvmVisibility[visibility]);
			} catch (Exception ex) {
				throw new InvalidOperationException ($"Internal error: unsupported visibility '{visibility}'", ex);
			}
		}

		void WritePreemptionSpecifier (WriteContext context, LlvmIrRuntimePreemption preemptionSpecifier)
		{
			if (preemptionSpecifier == LlvmIrRuntimePreemption.Default) {
				return;
			}

			try {
				WriteAttribute (context, llvmRuntimePreemption[preemptionSpecifier]);
			} catch (Exception ex) {
				throw new InvalidOperationException ($"Internal error: unsupported preemption specifier '{preemptionSpecifier}'", ex);
			}
		}

		/// <summary>
		/// Write attribute named in <paramref ref="attr"/> followed by a single space
		/// </summary>
		void WriteAttribute (WriteContext context, string attr)
		{
			context.Output.Write (attr);
			context.Output.Write (' ');
		}

		void WriteStructureDeclarations (WriteContext context)
		{
			if (context.Module.Structures == null || context.Module.Structures.Count == 0) {
				return;
			}

			foreach (StructureInfo si in context.Module.Structures) {
				context.Output.WriteLine ();
				WriteStructureDeclaration (context, si);
			}
		}

		void WriteStructureDeclaration (WriteContext context, StructureInfo si)
		{
			// $"%{typeDesignator}.{name} = type "
			context.Output.Write ('%');
			context.Output.Write (si.NativeTypeDesignator);
			context.Output.Write ('.');
			context.Output.Write (si.Name);
			context.Output.Write (" = type ");

			if (si.IsOpaque) {
				context.Output.WriteLine ("opaque");
			} else {
				context.Output.WriteLine ('{');
			}

			if (si.IsOpaque) {
				return;
			}

			context.IncreaseIndent ();
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
			context.DecreaseIndent ();

			context.Output.WriteLine ('}');

			void WriteStructureDeclarationField (string typeName, string comment, bool last)
			{
				context.Output.Write (context.CurrentIndent);
				context.Output.Write (typeName);
				if (!last) {
					context.Output.Write (", ");
				} else {
					context.Output.Write (' ');
				}

				if (!String.IsNullOrEmpty (comment)) {
					WriteCommentLine (context, comment);
				} else {
					context.Output.WriteLine ();
				}
			}
		}

		//
		// Functions syntax: https://llvm.org/docs/LangRef.html#functions
		//
		void WriteExternalFunctionDeclarations (WriteContext context)
		{
			if (context.Module.ExternalFunctions == null || context.Module.ExternalFunctions.Count == 0) {
				return;
			}

			context.Output.WriteLine ();
			foreach (LlvmIrFunction func in context.Module.ExternalFunctions) {
				// Must preserve state between calls, different targets may modify function state differently (e.g. set different parameter flags)
				ILlvmIrFunctionState funcState = func.SaveState ();

				foreach (LlvmIrFunctionParameter parameter in func.Signature.Parameters) {
					target.SetParameterFlags (parameter);
				}

				WriteFunctionAttributesComment (context, func);
				context.Output.Write ("declare ");
				WriteFunctionDeclarationLeadingDecorations (context, func);
				WriteFunctionSignature (context, func, writeParameterNames: false);
				WriteFunctionDeclarationTrailingDecorations (context, func);
				context.Output.WriteLine ();

				func.RestoreState (funcState);
			}
		}

		void WriteFunctionAttributesComment (WriteContext context, LlvmIrFunction func)
		{
			if (func.AttributeSet == null) {
				return;
			}

			context.Output.WriteLine ();
			WriteCommentLine (context, $"Function attributes: {func.AttributeSet.Render ()}");
		}

		void WriteFunctionDeclarationLeadingDecorations (WriteContext context, LlvmIrFunction func)
		{
			WriteFunctionLeadingDecorations (context, func, declaration: true);
		}

		void WriteFunctionDefinitionLeadingDecorations (WriteContext context, LlvmIrFunction func)
		{
			WriteFunctionLeadingDecorations (context, func, declaration: false);
		}

		void WriteFunctionLeadingDecorations (WriteContext context, LlvmIrFunction func, bool declaration)
		{
			if (func.Linkage != LlvmIrLinkage.Default) {
				context.Output.Write (llvmLinkage[func.Linkage]);
				context.Output.Write (' ');
			}

			if (!declaration && func.RuntimePreemption != LlvmIrRuntimePreemption.Default) {
				context.Output.Write (llvmRuntimePreemption[func.RuntimePreemption]);
				context.Output.Write (' ');
			}

			if (func.Visibility != LlvmIrVisibility.Default) {
				context.Output.Write (llvmVisibility[func.Visibility]);
				context.Output.Write (' ');
			}
		}

		void WriteFunctionDeclarationTrailingDecorations (WriteContext context, LlvmIrFunction func)
		{
			WriteFunctionTrailingDecorations (context, func, declaration: true);
		}

		void WriteFunctionDefinitionTrailingDecorations (WriteContext context, LlvmIrFunction func)
		{
			WriteFunctionTrailingDecorations (context, func, declaration: false);
		}

		void WriteFunctionTrailingDecorations (WriteContext context, LlvmIrFunction func, bool declaration)
		{
			if (func.AddressSignificance != LlvmIrAddressSignificance.Default) {
				context.Output.Write ($" {llvmAddressSignificance[func.AddressSignificance]}");
			}

			if (func.AttributeSet != null) {
				context.Output.Write ($" #{func.AttributeSet.Number}");
			}
		}

		void WriteFunctionSignature (WriteContext context, LlvmIrFunction func, bool writeParameterNames)
		{
			context.Output.Write (MapToIRType (func.Signature.ReturnType));
			context.Output.Write (" @");
			context.Output.Write (func.Signature.Name);
			context.Output.Write ('(');

			bool first = true;
			foreach (LlvmIrFunctionParameter parameter in func.Signature.Parameters) {
				if (!first) {
					context.Output.Write (", ");
				} else {
					first = false;
				}

				context.Output.Write (MapToIRType (parameter.Type));
				WriteParameterAttributes (context, parameter);
				if (writeParameterNames) {
					if (String.IsNullOrEmpty (parameter.Name)) {
						throw new InvalidOperationException ($"Internal error: parameter must have a name");
					}
					context.Output.Write (" %"); // Function arguments are always local variables
					context.Output.Write (parameter.Name);
				}
			}

			context.Output.Write (')');
		}

		void WriteParameterAttributes (WriteContext context, LlvmIrFunctionParameter parameter)
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

			context.Output.Write (' ');
			context.Output.Write (String.Join (" ", attributes));

			bool AttributeIsSet (bool? attr) => attr.HasValue && attr.Value;
		}

		void WriteAttributeSets (WriteContext context)
		{
			if (context.Module.AttributeSets == null || context.Module.AttributeSets.Count == 0) {
				return;
			}

			context.Output.WriteLine ();
			foreach (LlvmIrFunctionAttributeSet attrSet in context.Module.AttributeSets) {
				// Must not modify the original set, it is shared with other targets.
				var targetSet = new LlvmIrFunctionAttributeSet (attrSet);
				target.AddTargetSpecificAttributes (targetSet);

				IList<LlvmIrFunctionAttribute>? privateTargetSet = attrSet.GetPrivateTargetAttributes (target.TargetArch);
				if (privateTargetSet != null) {
					targetSet.Add (privateTargetSet);
				}

				context.Output.WriteLine ($"attributes #{targetSet.Number} {{ {targetSet.Render ()} }}");
			}
		}

		void WriteComment (WriteContext context, string comment)
		{
			context.Output.Write (';');
			context.Output.Write (comment);
		}

		void WriteCommentLine (WriteContext context, string comment)
		{
			WriteComment (context, comment);
			context.Output.WriteLine ();
		}

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

		object? GetTypedMemberValue (WriteContext context, StructureInfo info, StructureMemberInfo smi, StructureInstance instance, Type expectedType, object? defaultValue = null)
		{
			object? value = smi.GetValue (instance.Obj);
			if (value == null) {
				return defaultValue;
			}

			Type valueType = value.GetType ();
			if (valueType != expectedType) {
				throw new InvalidOperationException ($"Field '{smi.Info.Name}' of structure '{info.Name}' should have a value of '{expectedType}' type, instead it had a '{value.GetType ()}'");
			}

			if (valueType == typeof(string)) {
				return context.Module.LookupRequiredVariableForString ((string)value);
			}

			return value;
		}

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
