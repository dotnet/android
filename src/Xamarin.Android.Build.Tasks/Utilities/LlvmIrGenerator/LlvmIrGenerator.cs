using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks.LLVMIR
{
	sealed class GeneratorStructureInstance : StructureInstance
	{
		public GeneratorStructureInstance (StructureInfo info, object instance)
			: base (info, instance)
		{}
	}

	sealed class GeneratorWriteContext
	{
		const char IndentChar = '\t';

		int currentIndentLevel = 0;

		public readonly TextWriter Output;
		public readonly LlvmIrModule Module;
		public readonly LlvmIrModuleTarget Target;
		public readonly LlvmIrMetadataManager MetadataManager;
		public string CurrentIndent { get; private set; } = String.Empty;
		public bool InVariableGroup { get; set; }
		public LlvmIrVariableNumberFormat NumberFormat { get; set; } = LlvmIrVariableNumberFormat.Default;

		public GeneratorWriteContext (TextWriter writer, LlvmIrModule module, LlvmIrModuleTarget target, LlvmIrMetadataManager metadataManager)
		{
			Output = writer;
			Module = module;
			Target = target;
			MetadataManager = metadataManager;
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

	partial class LlvmIrGenerator
	{
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

		sealed class BasicType
		{
			public readonly string Name;
			public readonly ulong Size;
			public readonly bool IsNumeric;
			public readonly bool IsUnsigned;
			public readonly bool PreferHex;
			public readonly string HexFormat;

			public BasicType (string name, ulong size, bool isNumeric = true, bool isUnsigned = false, bool? preferHex = null)
			{
				Name = name;
				Size = size;
				IsNumeric = isNumeric;
				IsUnsigned = isUnsigned;

				// If hex preference isn't specified, we determine whether the type wants to be represented in
				// the hexadecimal notation based on signedness.  Unsigned types will be represented in hexadecimal,
				// but signed types will remain decimal, as it's easier for humans to see the actual value of the
				// variable, given this note from LLVM IR manual:
				//
				//    Note that hexadecimal integers are sign extended from the number of active bits, i.e. the bit width minus the number of leading zeros. So ‘s0x0001’ of type ‘i16’ will be -1, not 1.
				//
				// See: https://llvm.org/docs/LangRef.html#simple-constants
				//
				if (preferHex.HasValue) {
					PreferHex = preferHex.Value;
				} else {
					PreferHex = isUnsigned;
				}
				if (!PreferHex) {
					HexFormat = String.Empty;
					return;
				}

				HexFormat = $"x{size * 2}";
			}
		}

		public const string IRPointerType = "ptr";

		static readonly Dictionary<Type, BasicType> basicTypeMap = new Dictionary<Type, BasicType> {
			{ typeof (bool),   new ("i1",     1, isNumeric: false, isUnsigned: true, preferHex: false) },
			{ typeof (byte),   new ("i8",     1, isUnsigned: true) },
			{ typeof (char),   new ("i16",    2, isUnsigned: true, preferHex: false) },
			{ typeof (sbyte),  new ("i8",     1) },
			{ typeof (short),  new ("i16",    2) },
			{ typeof (ushort), new ("i16",    2, isUnsigned: true) },
			{ typeof (int),    new ("i32",    4) },
			{ typeof (uint),   new ("i32",    4, isUnsigned: true) },
			{ typeof (long),   new ("i64",    8) },
			{ typeof (ulong),  new ("i64",    8, isUnsigned: true) },
			{ typeof (float),  new ("float",  4) },
			{ typeof (double), new ("double", 8) },
			{ typeof (void),   new ("void",   0, isNumeric: false, preferHex: false) },
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
			LlvmIrMetadataManager metadataManager = module.GetMetadataManagerCopy ();
			target.AddTargetSpecificMetadata (metadataManager);

			var context = new GeneratorWriteContext (writer, module, target, metadataManager);
			if (!String.IsNullOrEmpty (FilePath)) {
				WriteCommentLine (context, $" ModuleID = '{FileName}'");
				context.Output.WriteLine ($"source_filename = \"{FileName}\"");
			}

			context.Output.WriteLine (target.DataLayout.Render ());
			context.Output.WriteLine ($"target triple = \"{target.Triple}\"");
			WriteStructureDeclarations (context);
			WriteGlobalVariables (context);
			WriteFunctions (context);

			// Bottom of the file
			WriteStrings (context);
			WriteExternalFunctionDeclarations (context);
			WriteAttributeSets (context);
			WriteMetadata (context);
		}

		void WriteStrings (GeneratorWriteContext context)
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

		void WriteGlobalVariables (GeneratorWriteContext context)
		{
			if (context.Module.GlobalVariables == null || context.Module.GlobalVariables.Count == 0) {
				return;
			}

			foreach (LlvmIrGlobalVariable gv in context.Module.GlobalVariables) {
				context.NumberFormat = gv.NumberFormat;

				if (gv is LlvmIrGroupDelimiterVariable groupDelimiter) {
					if (!context.InVariableGroup && !String.IsNullOrEmpty (groupDelimiter.Comment)) {
						context.Output.WriteLine ();
						context.Output.Write (context.CurrentIndent);
						WriteComment (context, groupDelimiter.Comment);
					}

					context.InVariableGroup = !context.InVariableGroup;
					if (context.InVariableGroup) {
						context.Output.WriteLine ();
					}
					continue;
				}

				if (gv.BeforeWriteCallback != null) {
					gv.BeforeWriteCallback (gv, target, gv.BeforeWriteCallbackCallerState);
				}
				WriteGlobalVariable (context, gv);
			}
		}

		void WriteGlobalVariableStart (GeneratorWriteContext context, LlvmIrGlobalVariable variable)
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

		void WriteGlobalVariable (GeneratorWriteContext context, LlvmIrGlobalVariable variable)
		{
			if (!context.InVariableGroup) {
				context.Output.WriteLine ();
			}

			WriteGlobalVariableStart (context, variable);
			WriteTypeAndValue (context, variable, out LlvmTypeInfo typeInfo);
			context.Output.Write (", align ");

			ulong alignment;
			if (variable.Alignment.HasValue) {
				alignment = variable.Alignment.Value;
			} else if (typeInfo.IsAggregate) {
				ulong count = GetAggregateValueElementCount (context, variable);
				alignment = (ulong)target.GetAggregateAlignment ((int)typeInfo.MaxFieldAlignment, count * typeInfo.Size);
			} else if (typeInfo.IsStructure) {
				alignment = (ulong)target.GetAggregateAlignment ((int)typeInfo.MaxFieldAlignment, typeInfo.Size);
			} else if (typeInfo.IsPointer) {
				alignment = target.NativePointerSize;
			} else {
				alignment = typeInfo.Size;
			}

			context.Output.WriteLine (alignment.ToString (CultureInfo.InvariantCulture));
		}

		void WriteTypeAndValue (GeneratorWriteContext context, LlvmIrVariable variable, out LlvmTypeInfo typeInfo)
		{
			WriteType (context, variable, out typeInfo);
			context.Output.Write (' ');

			Type valueType;
			if (variable.Value is LlvmIrVariable referencedVariable) {
				valueType = referencedVariable.Type;
			} else {
				valueType = variable.Value?.GetType () ?? variable.Type;
			}

			if (variable.Value == null) {
				// Order of checks is important here. Aggregates can contain pointer types, in which case typeInfo.IsPointer
				// will be `true` and the aggregate would be incorrectly initialized with `null` instead of the correct
				// `zeroinitializer`
				if (typeInfo.IsAggregate) {
					WriteValue (context, valueType, variable);
					return;
				}

				if (typeInfo.IsPointer) {
					context.Output.Write ("null");
					return;
				}

				throw new InvalidOperationException ($"Internal error: variable '{variable.Name}'' of type {variable.Type} must not have a null value");
			}

			if (valueType != variable.Type && !LlvmIrModule.NameValueArrayType.IsAssignableFrom (variable.Type)) {
				throw new InvalidOperationException ($"Internal error: variable type '{variable.Type}' is different to its value type, '{valueType}'");
			}

			WriteValue (context, valueType, variable);
		}

		ulong GetAggregateValueElementCount (GeneratorWriteContext context, LlvmIrVariable variable) => GetAggregateValueElementCount (context, variable.Type, variable.Value, variable as LlvmIrGlobalVariable);

		ulong GetAggregateValueElementCount (GeneratorWriteContext context, Type type, object? value, LlvmIrGlobalVariable? globalVariable = null)
		{
			if (!type.IsArray ()) {
				throw new InvalidOperationException ($"Internal error: unknown type {type} when trying to determine aggregate type element count");
			}

			if (value == null) {
				if (globalVariable != null) {
					if (globalVariable.ArrayDataProvider != null) {
						return globalVariable.ArrayDataProvider.GetTotalDataSize (context.Target);
					}
					return globalVariable.ArrayItemCount;
				}
				return 0;
			}

			// TODO: use caching here
			if (type.ImplementsInterface (typeof(IDictionary<string, string>))) {
				return (uint)((IDictionary<string, string>)value).Count * 2;
			}

			if (type.ImplementsInterface (typeof(ICollection))) {
				return (uint)((ICollection)value).Count;
			}

			throw new InvalidOperationException ($"Internal error: should never get here");
		}

		void WriteType (GeneratorWriteContext context, LlvmIrVariable variable, out LlvmTypeInfo typeInfo)
		{
			WriteType (context, variable.Type, variable.Value, out typeInfo, variable as LlvmIrGlobalVariable);
		}

		void WriteType (GeneratorWriteContext context, StructureInstance si, StructureMemberInfo memberInfo, out LlvmTypeInfo typeInfo)
		{
			if (memberInfo.IsNativePointer) {
				typeInfo = new LlvmTypeInfo (
					isPointer: true,
					isAggregate: false,
					isStructure: false,
					size: target.NativePointerSize,
					maxFieldAlignment: target.NativePointerSize
				);

				context.Output.Write (IRPointerType);
				return;
			}

			if (memberInfo.IsInlineArray) {
				WriteArrayType (context, memberInfo.MemberType.GetArrayElementType (), memberInfo.ArrayElements, out typeInfo);
				return;
			}

			if (memberInfo.IsIRStruct ()) {
				var sim = new GeneratorStructureInstance (context.Module.GetStructureInfo (memberInfo.MemberType), memberInfo.GetValue (si.Obj));
				WriteStructureType (context, sim, out typeInfo);
				return;
			}

			WriteType (context, memberInfo.MemberType, value: null, out typeInfo);
		}

		void WriteStructureType (GeneratorWriteContext context, StructureInstance si, out LlvmTypeInfo typeInfo)
		{
			ulong alignment = GetStructureMaxFieldAlignment (si.Info);

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
		}

		void WriteType (GeneratorWriteContext context, Type type, object? value, out LlvmTypeInfo typeInfo, LlvmIrGlobalVariable? globalVariable = null)
		{
			if (IsStructureInstance (type)) {
				if (value == null) {
					throw new ArgumentException ("must not be null for structure instances", nameof (value));
				}

				WriteStructureType (context, (StructureInstance)value, out typeInfo);
				return;
			}

			string irType;
			ulong size;
			bool isPointer;

			if (type.IsArray ()) {
				Type elementType = type.GetArrayElementType ();
				ulong elementCount = GetAggregateValueElementCount (context, type, value, globalVariable);

				WriteArrayType (context, elementType, elementCount, globalVariable, out typeInfo);
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

		void WriteArrayType (GeneratorWriteContext context, Type elementType, ulong elementCount, out LlvmTypeInfo typeInfo)
		{
			WriteArrayType (context, elementType, elementCount, variable: null, out typeInfo);
		}

		void WriteArrayType (GeneratorWriteContext context, Type elementType, ulong elementCount, LlvmIrGlobalVariable? variable, out LlvmTypeInfo typeInfo)
		{
			string irType;
			ulong size;
			ulong maxFieldAlignment;
			bool isPointer;

			if (elementType.IsStructureInstance (out Type? structureType)) {
				StructureInfo si = context.Module.GetStructureInfo (structureType);

				irType = $"%{si.NativeTypeDesignator}.{si.Name}";
				size = si.Size;
				maxFieldAlignment = GetStructureMaxFieldAlignment (si);
				isPointer = false;
			} else {
				irType = GetIRType (elementType, out size, out isPointer);
				maxFieldAlignment = size;

				if (elementType.IsArray) {
					if (variable == null) {
						throw new InvalidOperationException ($"Internal error: array of arrays ({elementType}) requires variable to be defined");
					}

					// For the sake of simpler code, we currently assume that all the element arrays are of the same size, because that's the only scenario
					// that we use at this time.
					var value = variable.Value as ICollection;
					if (value == null) {
						throw new InvalidOperationException ($"Internal error: variable '{variable.Name}' of type '{variable.Type}' is required to have a value of type which implements the ICollection interface");
					}

					if (value.Count == 0) {
						throw new InvalidOperationException ($"Internal error: variable '{variable.Name}' of type '{variable.Type}' is required to have a value which is a non-empty ICollection");
					}

					Array? firstItem = null;
					foreach (object v in value) {
						firstItem = (Array)v;
						break;
					}

					if (firstItem == null) {
						throw new InvalidOperationException ($"Internal error: variable '{variable.Name}' of type '{variable.Type}' is required to have a value which is a non-empty ICollection with non-null elements");
					}

					irType = $"[{MonoAndroidHelper.CultureInvariantToString (firstItem.Length)} x {irType}]";
				}
			}
			typeInfo = new LlvmTypeInfo (
				isPointer: isPointer,
				isAggregate: true,
				isStructure: false,
				size: size,
				maxFieldAlignment: maxFieldAlignment
			);

			context.Output.Write ('[');
			context.Output.Write (elementCount.ToString (CultureInfo.InvariantCulture));
			context.Output.Write (" x ");
			context.Output.Write (irType);
			context.Output.Write (']');
		}

		ulong GetStructureMaxFieldAlignment (StructureInfo si)
		{
			if (si.HasPointers && target.NativePointerSize > si.MaxFieldAlignment) {
				return target.NativePointerSize;
			}

			return si.MaxFieldAlignment;
		}

		bool IsStructureInstance (Type t) => typeof(StructureInstance).IsAssignableFrom (t);

		void WriteValue (GeneratorWriteContext context, Type valueType, LlvmIrVariable variable)
		{
			if (variable is LlvmIrGlobalVariable globalVariable && globalVariable.ArrayDataProvider != null) {
				WriteStreamedArrayValue (context, globalVariable, globalVariable.ArrayDataProvider);
				return;
			}

			if (variable.Type.IsArray ()) {
				bool zeroInitialize = false;
				if (variable is LlvmIrGlobalVariable gv) {
					zeroInitialize = gv.ZeroInitializeArray || variable.Value == null;
				} else {
					zeroInitialize = GetAggregateValueElementCount (context, variable) == 0;
				}

				if (zeroInitialize) {
					context.Output.Write ("zeroinitializer");
					return;
				}

				WriteArrayValue (context, variable);
				return;
			}

			WriteValue (context, valueType, variable.Value);
		}

		void AssertArraySize (StructureInstance si, StructureMemberInfo smi, ulong length, ulong expectedLength)
		{
			if (length == expectedLength) {
				return;
			}

			throw new InvalidOperationException ($"Invalid array size in field '{smi.Info.Name}' of structure '{si.Info.Name}', expected {expectedLength}, found {length}");
		}

		void WriteInlineArray (GeneratorWriteContext context, byte[] bytes, bool encodeAsASCII)
		{
			if (encodeAsASCII) {
				context.Output.Write ('c');
				context.Output.Write (QuoteString (bytes, bytes.Length, out _, nullTerminated: false));
				return;
			}

			string irType = MapToIRType (typeof(byte));
			bool first = true;
			context.Output.Write ("[ ");
			foreach (byte b in bytes) {
				if (!first) {
					context.Output.Write (", ");
				} else {
					first = false;
				}

				context.Output.Write ($"{irType} u0x{b:x02}");
			}
			context.Output.Write (" ]");
		}

		void WriteValue (GeneratorWriteContext context, StructureInstance structInstance, StructureMemberInfo smi, object? value)
		{
			if (smi.IsNativePointer) {
				if (WriteNativePointerValue (context, structInstance, smi, value)) {
					return;
				}
			}

			if (smi.IsInlineArray) {
				Array a = (Array)value;
				ulong length = smi.ArrayElements == 0 ? (ulong)a.Length : smi.ArrayElements;

				if (smi.MemberType == typeof(byte[])) {
					var bytes = (byte[])value;

					// Byte arrays are represented in the same way as strings, without the explicit NUL termination byte
					AssertArraySize (structInstance, smi, length, smi.ArrayElements);
					WriteInlineArray (context, bytes, encodeAsASCII: false);
					return;
				}

				throw new NotSupportedException ($"Internal error: inline arrays of type {smi.MemberType} aren't supported at this point. Field {smi.Info.Name} in structure {structInstance.Info.Name}");
			}

			if (smi.IsIRStruct ()) {
				StructureInfo si = context.Module.GetStructureInfo (smi.MemberType);
				WriteValue (context, typeof(GeneratorStructureInstance), new GeneratorStructureInstance (si, value));
				return;
			}

			if (smi.Info.IsNativePointerToPreallocatedBuffer (out _)) {
				string bufferVariableName = context.Module.LookupRequiredBufferVariableName (structInstance, smi);
				context.Output.Write ('@');
				context.Output.Write (bufferVariableName);
				return;
			}

			WriteValue (context, smi.MemberType, value);
		}

		bool WriteNativePointerValue (GeneratorWriteContext context, StructureInstance si, StructureMemberInfo smi, object? value)
		{
			// Structure members decorated with the [NativePointer] attribute cannot have a
			// value other than `null`, unless they are strings or references to symbols

			if (smi.Info.PointsToSymbol (out string? symbolName)) {
				if (String.IsNullOrEmpty (symbolName) && smi.Info.UsesDataProvider ()) {
					if (si.Info.DataProvider == null) {
						throw new InvalidOperationException ($"Field '{smi.Info.Name}' of structure '{si.Info.Name}' points to a symbol, but symbol name wasn't provided and there's no configured data context provider");
					}
					symbolName = si.Info.DataProvider.GetPointedToSymbolName (si.Obj, smi.Info.Name);
				}

				if (String.IsNullOrEmpty (symbolName)) {
					context.Output.Write ("null");
				} else {
					context.Output.Write ('@');
					context.Output.Write (symbolName);
				}
				return true;
			}

			if (smi.MemberType != typeof(string)) {
				context.Output.Write ("null");
				return true;
			}

			return false;
		}

		string ToHex (BasicType basicTypeDesc, Type type, object? value)
		{
			const char prefixSigned = 's';
			const char prefixUnsigned = 'u';

			string hex;
			if (type == typeof(byte)) {
				hex = ((byte)value).ToString (basicTypeDesc.HexFormat, CultureInfo.InvariantCulture);
			} else if (type == typeof(ushort)) {
				hex = ((ushort)value).ToString (basicTypeDesc.HexFormat, CultureInfo.InvariantCulture);
			} else if (type == typeof(uint)) {
				hex = ((uint)value).ToString (basicTypeDesc.HexFormat, CultureInfo.InvariantCulture);
			} else if (type == typeof(ulong)) {
				hex = ((ulong)value).ToString (basicTypeDesc.HexFormat, CultureInfo.InvariantCulture);
			} else {
				throw new NotImplementedException ($"Conversion to hexadecimal from type {type} is not implemented");
			};

			return $"{(basicTypeDesc.IsUnsigned ? prefixUnsigned : prefixSigned)}0x{hex}";
		}

		void WriteValue (GeneratorWriteContext context, Type type, object? value)
		{
			if (value is LlvmIrVariable variableRef) {
				context.Output.Write (variableRef.Reference);
				return;
			}

			bool isBasic = basicTypeMap.TryGetValue (type, out BasicType basicTypeDesc);
			if (isBasic) {
				if (basicTypeDesc.IsNumeric) {
					bool hex = context.NumberFormat switch {
						LlvmIrVariableNumberFormat.Default => basicTypeDesc.PreferHex,
						LlvmIrVariableNumberFormat.Decimal => false,
						LlvmIrVariableNumberFormat.Hexadecimal => true,
						_ => throw new InvalidOperationException ($"Internal error: number format {context.NumberFormat} is unsupported")
					};

					context.Output.Write (
						hex ? ToHex (basicTypeDesc, type, value) :  MonoAndroidHelper.CultureInvariantToString (value)
					);
					return;
				}

				if (type == typeof(bool)) {
					context.Output.Write ((bool)value ? "true" : "false");
					return;
				}
			}

			if (IsStructureInstance (type)) {
				WriteStructureValue (context, (StructureInstance?)value);
				return;
			}

			if (type == typeof(IntPtr)) {
				// Pointers can only be `null` or a reference to variable
				context.Output.Write ("null");
				return;
			}

			if (type == typeof(string)) {
				if (value == null) {
					context.Output.Write ("null");
					return;
				}

				LlvmIrStringVariable sv = context.Module.LookupRequiredVariableForString ((string)value);
				context.Output.Write (sv.Reference);
				return;
			}

			if (type.IsArray) {
				if (type == typeof(byte[])) {
					WriteInlineArray (context, (byte[])value, encodeAsASCII: true);
					return;
				}

				throw new NotSupportedException ($"Internal error: array of type {type} is unsupported");
			}

			throw new NotSupportedException ($"Internal error: value type '{type}' is unsupported");
		}

		void WriteStructureValue (GeneratorWriteContext context, StructureInstance? instance)
		{
			if (instance == null || instance.IsZeroInitialized) {
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
				WriteType (context, instance, smi, out _);
				context.Output.Write (' ');

				object? value = GetTypedMemberValue (context, info, smi, instance, smi.MemberType);
				LlvmIrVariableNumberFormat numberFormat = smi.Info.GetNumberFormat ();
				LlvmIrVariableNumberFormat? savedNumberFormat = null;

				if (numberFormat != LlvmIrVariableNumberFormat.Default && numberFormat != context.NumberFormat) {
					savedNumberFormat = context.NumberFormat;
					context.NumberFormat = numberFormat;
				}

				WriteValue (context, instance, smi, value);

				if (savedNumberFormat.HasValue) {
					context.NumberFormat = savedNumberFormat.Value;
				}

				if (i < lastMember) {
					context.Output.Write (", ");
				}

				string? comment = info.GetCommentFromProvider (smi, instance);
				if (String.IsNullOrEmpty (comment)) {
					var sb = new StringBuilder (" ");
					sb.Append (MapManagedTypeToNative (smi));
					sb.Append (' ');
					sb.Append (smi.Info.Name);
					comment = sb.ToString ();
				}
				WriteCommentLine (context, comment);
			}

			context.DecreaseIndent ();
			context.Output.Write (context.CurrentIndent);
			context.Output.Write ('}');
		}

		void WriteArrayValueStart (GeneratorWriteContext context)
		{
			context.Output.WriteLine ('[');
			context.IncreaseIndent ();
		}

		void WriteArrayValueEnd (GeneratorWriteContext context)
		{
			context.DecreaseIndent ();
			context.Output.Write (']');
		}

 		uint GetArrayStride (LlvmIrVariable variable)
		{
			if ((variable.WriteOptions & LlvmIrVariableWriteOptions.ArrayFormatInRows) == LlvmIrVariableWriteOptions.ArrayFormatInRows) {
				return variable.ArrayStride > 0 ? variable.ArrayStride : 1;
			}

			return 1;
		}

		void WriteArrayEntries (GeneratorWriteContext context, LlvmIrVariable variable, ICollection? entries, Type elementType, uint stride, bool writeIndices, bool terminateWithComma = false)
		{
			bool first = true;
			bool ignoreComments = stride > 1;
			string? prevItemComment = null;
			ulong counter = 0;

			if (entries != null) {
				foreach (object entry in entries) {
					if (!first) {
						context.Output.Write (',');
						if (stride == 1 || counter % stride == 0) {
							WritePrevItemCommentOrNewline ();
							context.Output.Write (context.CurrentIndent);
						} else {
							context.Output.Write (' ');
						}
					} else {
						context.Output.Write (context.CurrentIndent);
						first = false;
					}

					if (!ignoreComments) {
						prevItemComment = null;
						if (variable.GetArrayItemCommentCallback != null) {
							prevItemComment = variable.GetArrayItemCommentCallback (variable, target, counter, entry, variable.GetArrayItemCommentCallbackCallerState);
						}

						if (writeIndices && String.IsNullOrEmpty (prevItemComment)) {
							prevItemComment = $" {counter}";
						}
					}

					counter++;
					WriteType (context, elementType, entry, out _);

					context.Output.Write (' ');
					WriteValue (context, elementType, entry);
				}
			}

			if (terminateWithComma) {
				if (!ignoreComments) {
					context.Output.WriteLine (); // must put comma outside the comment
					context.Output.Write (context.CurrentIndent);
				}
				context.Output.Write (',');
			}
			WritePrevItemCommentOrNewline ();

			void WritePrevItemCommentOrNewline ()
			{
				if (!ignoreComments && !String.IsNullOrEmpty (prevItemComment)) {
					context.Output.Write (' ');
					WriteCommentLine (context, prevItemComment);
				} else {
					context.Output.WriteLine ();
				}
			}
		}

		bool ArrayWantsToWriteIndices (LlvmIrVariable variable) => (variable.WriteOptions & LlvmIrVariableWriteOptions.ArrayWriteIndexComments) == LlvmIrVariableWriteOptions.ArrayWriteIndexComments;

		void WriteStreamedArrayValue (GeneratorWriteContext context, LlvmIrGlobalVariable variable, LlvmIrStreamedArrayDataProvider dataProvider)
		{
			ulong dataSizeSoFar = 0;
			ulong totalDataSize = dataProvider.GetTotalDataSize (context.Target);
			bool first = true;

			WriteArrayValueStart (context);
			while (true) {
				(LlvmIrStreamedArrayDataProviderState state, ICollection? data) = dataProvider.GetData (context.Target);
				if (state == LlvmIrStreamedArrayDataProviderState.NextSectionNoData) {
					continue;
				}

				bool mustHaveData = state != LlvmIrStreamedArrayDataProviderState.LastSectionNoData;
				if (mustHaveData) {
					if (data.Count == 0) {
						throw new InvalidOperationException ("Data must be provided for streamed arrays");
					}

					dataSizeSoFar += (ulong)data.Count;
					if (dataSizeSoFar > totalDataSize) {
						throw new InvalidOperationException ($"Data provider {dataProvider} is trying to write more data than declared");
					}

					if (first) {
						first = false;
					} else {
						context.Output.WriteLine ();
					}
					string comment = dataProvider.GetSectionStartComment (context.Target);

					if (comment.Length > 0) {
						context.Output.Write (context.CurrentIndent);
						WriteCommentLine (context, comment);
					}
				}

				bool lastSection = state == LlvmIrStreamedArrayDataProviderState.LastSection || state == LlvmIrStreamedArrayDataProviderState.LastSectionNoData;
				WriteArrayEntries (
					context,
					variable,
					data,
					dataProvider.ArrayElementType,
					GetArrayStride (variable),
					writeIndices: false,
					terminateWithComma: !lastSection
				);

				if (lastSection) {
					break;
				}

			}
			WriteArrayValueEnd (context);
		}

		void WriteArrayValue (GeneratorWriteContext context, LlvmIrVariable variable)
		{
			ICollection entries;
			if (variable.Type.ImplementsInterface (typeof(IDictionary<string, string>))) {
				var list = new List<string> ();
				foreach (var kvp in (IDictionary<string, string>)variable.Value) {
					list.Add (kvp.Key);
					list.Add (kvp.Value);
				}
				entries = list;
			} else {
				entries = (ICollection)variable.Value;
			}

			if (entries.Count == 0) {
				context.Output.Write ("zeroinitializer");
				return;
			}

			WriteArrayValueStart (context);

			WriteArrayEntries (
				context,
				variable,
				entries,
				variable.Type.GetArrayElementType (),
				GetArrayStride (variable),
				writeIndices: ArrayWantsToWriteIndices (variable)
			);

			WriteArrayValueEnd (context);
		}

		void WriteLinkage (GeneratorWriteContext context, LlvmIrLinkage linkage)
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

		void WriteWritability (GeneratorWriteContext context, LlvmIrWritability writability)
		{
			try {
				WriteAttribute (context, llvmWritability[writability]);
			} catch (Exception ex) {
				throw new InvalidOperationException ($"Internal error: unsupported writability '{writability}'", ex);
			}
		}

		void WriteAddressSignificance (GeneratorWriteContext context, LlvmIrAddressSignificance addressSignificance)
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

		void WriteVisibility (GeneratorWriteContext context, LlvmIrVisibility visibility)
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

		void WritePreemptionSpecifier (GeneratorWriteContext context, LlvmIrRuntimePreemption preemptionSpecifier)
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
		void WriteAttribute (GeneratorWriteContext context, string attr)
		{
			context.Output.Write (attr);
			context.Output.Write (' ');
		}

		void WriteStructureDeclarations (GeneratorWriteContext context)
		{
			if (context.Module.Structures == null || context.Module.Structures.Count == 0) {
				return;
			}

			foreach (StructureInfo si in context.Module.Structures) {
				context.Output.WriteLine ();
				WriteStructureDeclaration (context, si);
			}
		}

		void WriteStructureDeclaration (GeneratorWriteContext context, StructureInfo si)
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
		void WriteFunctions (GeneratorWriteContext context)
		{
			if (context.Module.Functions == null || context.Module.Functions.Count == 0) {
				return;
			}

			context.Output.WriteLine ();
			WriteComment (context, " Functions");

			foreach (LlvmIrFunction function in context.Module.Functions) {
				context.Output.WriteLine ();
				WriteFunctionComment (context, function);

				// Must preserve state between calls, different targets may modify function state differently (e.g. set different parameter flags
				ILlvmIrSavedFunctionState funcState = WriteFunctionPreamble (context, function, "define");
				WriteFunctionDefinitionLeadingDecorations (context, function);
				WriteFunctionSignature (context, function, writeParameterNames: true);
				WriteFunctionDefinitionTrailingDecorations (context, function);
				WriteFunctionBody (context, function);
				function.RestoreState (funcState);
			}
		}

		void WriteFunctionComment (GeneratorWriteContext context, LlvmIrFunction function)
		{
			if (String.IsNullOrEmpty (function.Comment)) {
				return;
			}

			foreach (string commentLine in function.Comment.Split ('\n')) {
				context.Output.Write (context.CurrentIndent);
				WriteCommentLine (context, commentLine);
			}
		}

		void WriteFunctionBody (GeneratorWriteContext context, LlvmIrFunction function)
		{
			context.Output.WriteLine ();
			context.Output.WriteLine ('{');
			context.IncreaseIndent ();

			foreach (LlvmIrFunctionBodyItem item in function.Body.Items) {
				item.Write (context, this);
			}

			context.DecreaseIndent ();
			context.Output.WriteLine ('}');
		}

		ILlvmIrSavedFunctionState WriteFunctionPreamble (GeneratorWriteContext context, LlvmIrFunction function, string keyword)
		{
			ILlvmIrSavedFunctionState funcState = function.SaveState ();

			foreach (LlvmIrFunctionParameter parameter in function.Signature.Parameters) {
				target.SetParameterFlags (parameter);
			}

			WriteFunctionAttributesComment (context, function);
			context.Output.Write (keyword);
			context.Output.Write (' ');

			return funcState;
		}

		void WriteExternalFunctionDeclarations (GeneratorWriteContext context)
		{
			if (context.Module.ExternalFunctions == null || context.Module.ExternalFunctions.Count == 0) {
				return;
			}

			context.Output.WriteLine ();
			WriteComment (context, " External functions");
			foreach (LlvmIrFunction function in context.Module.ExternalFunctions) {
				context.Output.WriteLine ();

				// Must preserve state between calls, different targets may modify function state differently (e.g. set different parameter flags)
				ILlvmIrSavedFunctionState funcState = WriteFunctionPreamble (context, function, "declare");
				WriteFunctionDeclarationLeadingDecorations (context, function);
				WriteFunctionSignature (context, function, writeParameterNames: false);
				WriteFunctionDeclarationTrailingDecorations (context, function);

				function.RestoreState (funcState);
			}
		}

		void WriteFunctionAttributesComment (GeneratorWriteContext context, LlvmIrFunction func)
		{
			if (func.AttributeSet == null) {
				return;
			}

			if (String.IsNullOrEmpty (func.Comment)) {
				context.Output.WriteLine ();
			}
			WriteCommentLine (context, $" Function attributes: {func.AttributeSet.Render ()}");
		}

		void WriteFunctionDeclarationLeadingDecorations (GeneratorWriteContext context, LlvmIrFunction func)
		{
			WriteFunctionLeadingDecorations (context, func, declaration: true);
		}

		void WriteFunctionDefinitionLeadingDecorations (GeneratorWriteContext context, LlvmIrFunction func)
		{
			WriteFunctionLeadingDecorations (context, func, declaration: false);
		}

		void WriteFunctionLeadingDecorations (GeneratorWriteContext context, LlvmIrFunction func, bool declaration)
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

		void WriteFunctionDeclarationTrailingDecorations (GeneratorWriteContext context, LlvmIrFunction func)
		{
			WriteFunctionTrailingDecorations (context, func, declaration: true);
		}

		void WriteFunctionDefinitionTrailingDecorations (GeneratorWriteContext context, LlvmIrFunction func)
		{
			WriteFunctionTrailingDecorations (context, func, declaration: false);
		}

		void WriteFunctionTrailingDecorations (GeneratorWriteContext context, LlvmIrFunction func, bool declaration)
		{
			if (func.AddressSignificance != LlvmIrAddressSignificance.Default) {
				context.Output.Write ($" {llvmAddressSignificance[func.AddressSignificance]}");
			}

			if (func.AttributeSet != null) {
				context.Output.Write ($" #{func.AttributeSet.Number}");
			}
		}

		public static void WriteReturnAttributes (GeneratorWriteContext context, LlvmIrFunctionSignature.ReturnTypeAttributes returnAttrs)
		{
			if (AttributeIsSet (returnAttrs.NoUndef)) {
				context.Output.Write ("noundef ");
			}

			if (AttributeIsSet (returnAttrs.NonNull)) {
				context.Output.Write ("nonnull ");
			}

			if (AttributeIsSet (returnAttrs.SignExt)) {
				context.Output.Write ("signext ");
			}

			if (AttributeIsSet (returnAttrs.ZeroExt)) {
				context.Output.Write ("zeroext ");
			}
		}

		void WriteFunctionSignature (GeneratorWriteContext context, LlvmIrFunction func, bool writeParameterNames)
		{
			if (func.ReturnsValue) {
				WriteReturnAttributes (context, func.Signature.ReturnAttributes);
			}

			context.Output.Write (MapToIRType (func.Signature.ReturnType));
			context.Output.Write (" @");
			context.Output.Write (func.Signature.Name);
			context.Output.Write ('(');

			bool first = true;
			bool varargsFound = false;

			foreach (LlvmIrFunctionParameter parameter in func.Signature.Parameters) {
				if (varargsFound) {
					throw new InvalidOperationException ($"Internal error: function '{func.Signature.Name}' has extra parameters following the C varargs parameter. This is not allowed.");
				}

				if (!first) {
					context.Output.Write (", ");
				} else {
					first = false;
				}

				if (parameter.IsVarArgs) {
					context.Output.Write ("...");
					varargsFound = true;
					continue;
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

		public static void WriteParameterAttributes (GeneratorWriteContext context, LlvmIrFunctionParameter parameter)
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
				attributes.Add ($"align({ValueOrPointerSize (parameter.Align.Value)})");
			}

			if (parameter.Dereferenceable.HasValue) {
				attributes.Add ($"dereferenceable({ValueOrPointerSize (parameter.Dereferenceable.Value)})");
			}

			if (attributes.Count == 0) {
				return;
			}

			context.Output.Write (' ');
			context.Output.Write (String.Join (" ", attributes));

			uint ValueOrPointerSize (uint? value)
			{
				if (value.Value == 0) {
					return context.Target.NativePointerSize;
				}

				return value.Value;
			}
		}

		static bool AttributeIsSet (bool? attr) => attr.HasValue && attr.Value;

		void WriteAttributeSets (GeneratorWriteContext context)
		{
			if (context.Module.AttributeSets == null || context.Module.AttributeSets.Count == 0) {
				return;
			}

			context.Output.WriteLine ();
			foreach (LlvmIrFunctionAttributeSet attrSet in context.Module.AttributeSets) {
				// Must not modify the original set, it is shared with other targets.
				var targetSet = new LlvmIrFunctionAttributeSet (attrSet);
				if (!attrSet.DoNotAddTargetSpecificAttributes) {
					target.AddTargetSpecificAttributes (targetSet);
				}

				IList<LlvmIrFunctionAttribute>? privateTargetSet = attrSet.GetPrivateTargetAttributes (target.TargetArch);
				if (privateTargetSet != null) {
					targetSet.Add (privateTargetSet);
				}

				context.Output.WriteLine ($"attributes #{targetSet.Number} = {{ {targetSet.Render ()} }}");
			}
		}

		void WriteMetadata (GeneratorWriteContext context)
		{
			if (context.MetadataManager.Items.Count == 0) {
				return;
			}

			context.Output.WriteLine ();
			WriteCommentLine (context, " Metadata");
			foreach (LlvmIrMetadataItem metadata in context.MetadataManager.Items) {
				context.Output.WriteLine (metadata.Render ());
			}
		}

		public void WriteComment (GeneratorWriteContext context, string comment)
		{
			context.Output.Write (';');
			context.Output.Write (comment);
		}

		public void WriteCommentLine (GeneratorWriteContext context, string comment)
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
		public static string MapManagedTypeToNative (Type type)
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

		static string MapManagedTypeToNative (StructureMemberInfo smi)
		{
			string nativeType = MapManagedTypeToNative (smi.MemberType);
			// Silly, but effective
			if (nativeType[nativeType.Length - 1] == '*') {
				return nativeType;
			}

			if (!smi.IsNativePointer) {
				return nativeType;
			}

			return $"{nativeType}*";
		}

		object? GetTypedMemberValue (GeneratorWriteContext context, StructureInfo info, StructureMemberInfo smi, StructureInstance instance, Type expectedType, object? defaultValue = null)
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

		public static string MapToIRType (Type type, out bool isPointer)
		{
			return MapToIRType (type, out _, out isPointer);
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

		public static bool IsFirstClassNonPointerType (Type type)
		{
			if (type == typeof(void)) {
				return false;
			}

			return basicTypeMap.ContainsKey (type);
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
