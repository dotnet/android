using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks.LLVMIR
{
	abstract class LlvmIrGenerator
	{
		static readonly Dictionary<Type, string> typeMap = new Dictionary<Type, string> {
			{ typeof (bool), "i8" },
			{ typeof (byte), "i8" },
			{ typeof (sbyte), "i8" },
            { typeof (short), "i16" },
            { typeof (ushort), "i16" },
            { typeof (int), "i32" },
            { typeof (uint), "i32" },
            { typeof (long), "i64" },
            { typeof (ulong), "i64" },
            { typeof (float), "float" },
            { typeof (double), "double" },
            { typeof (string), "i8*" },
			{ typeof (IntPtr), "i8*" },
		};

		// https://llvm.org/docs/LangRef.html#single-value-types
		static readonly Dictionary<Type, ulong> typeSizes = new Dictionary<Type, ulong> {
			{ typeof (bool), 1 },
			{ typeof (byte), 1 },
			{ typeof (sbyte), 1 },
            { typeof (short), 2 },
            { typeof (ushort), 2 },
            { typeof (int), 4 },
            { typeof (uint), 4 },
            { typeof (long), 8 },
            { typeof (ulong), 8 },
            { typeof (float), 4 }, // floats are 32-bit
            { typeof (double), 8 }, // doubles are 64-bit
		};

		string fileName;
		ulong stringCounter = 0;
		ulong structStringCounter = 0;

		List<IStructureInfo> structures = new List<IStructureInfo> ();

		protected abstract string DataLayout { get; }
		public abstract int PointerSize { get; }
		protected abstract string Triple { get; }

		public bool Is64Bit { get; }
		public TextWriter Output { get; }
		public AndroidTargetArch TargetArch { get; }

		protected string Indent => "\t";
		protected LlvmIrMetadataManager MetadataManager { get; }

		protected LlvmIrGenerator (AndroidTargetArch arch, TextWriter output, string fileName)
		{
			Output = output;
			MetadataManager = new LlvmIrMetadataManager ();
			TargetArch = arch;
			Is64Bit = arch == AndroidTargetArch.X86_64 || arch == AndroidTargetArch.Arm64;
			this.fileName = fileName;
		}

		public static LlvmIrGenerator Create (AndroidTargetArch arch, StreamWriter output, string fileName)
		{
			LlvmIrGenerator ret = Instantiate ();
			ret.Init ();
			return ret;

			LlvmIrGenerator Instantiate ()
			{
				return arch switch {
					AndroidTargetArch.Arm => new Arm32LlvmIrGenerator (arch, output, fileName),
					AndroidTargetArch.Arm64 => new Arm64LlvmIrGenerator (arch, output, fileName),
					AndroidTargetArch.X86 => new X86LlvmIrGenerator (arch, output, fileName),
					AndroidTargetArch.X86_64 => new X64LlvmIrGenerator (arch, output, fileName),
					_ => throw new InvalidOperationException ($"Unsupported Android target ABI {arch}")
				};
			}
		}

		public static string MapManagedTypeToIR (Type type)
		{
			if (!typeMap.TryGetValue (type, out string irType)) {
				throw new InvalidOperationException ($"Unsupported managed type {type}");
			}

			return irType;
		}

		public string MapManagedTypeToIR (Type type, out ulong size)
		{
			string irType = MapManagedTypeToIR (type);
			if (!typeSizes.TryGetValue (type, out size)) {
				if (type == typeof (string) || type == typeof (IntPtr)) {
					size = (ulong)PointerSize;
				} else {
					throw new InvalidOperationException ($"Unsupported managed type {type}");
				}
			}

			return irType;
		}

		public string MapManagedTypeToIR<T> (out ulong size)
		{
			return MapManagedTypeToIR (typeof(T), out size);
		}

		public static string MapManagedTypeToNative (Type type)
		{
			if (type == typeof (bool)) return "bool";
			if (type == typeof (byte)) return "uint8_t";
			if (type == typeof (sbyte)) return "int8_t";
			if (type == typeof (short)) return "int16_t";
			if (type == typeof (ushort)) return "uint16_t";
			if (type == typeof (int)) return "int32_t";
			if (type == typeof (uint)) return "uint32_t";
			if (type == typeof (long)) return "int64_t";
			if (type == typeof (ulong)) return "uint64_t";
			if (type == typeof (float)) return "float";
			if (type == typeof (double)) return "double";
			if (type == typeof (string)) return "char*";
			if (type == typeof (IntPtr)) return "void*";

			return type.GetShortName ();
		}

		public virtual void Init ()
		{
			LlvmIrMetadataItem flags = MetadataManager.Add ("llvm.module.flags");
			LlvmIrMetadataItem ident = MetadataManager.Add ("llvm.ident");

			var flagsFields = new List<LlvmIrMetadataItem> ();
			AddModuleFlagsMetadata (flagsFields);

			foreach (LlvmIrMetadataItem item in flagsFields) {
				flags.AddReferenceField (item.Name);
			}

			LlvmIrMetadataItem identValue = MetadataManager.AddNumbered ($"Xamarin.Android {XABuildConfig.XamarinAndroidBranch} @ {XABuildConfig.XamarinAndroidCommitHash}");
			ident.AddReferenceField (identValue.Name);
		}

		public StructureInfo<T> MapStructure<T> ()
		{
			Type t = typeof(T);
			if (!t.IsClass && !t.IsValueType) {
				throw new InvalidOperationException ($"{t} must be a class or a struct");
			}

			var ret = new StructureInfo<T> (this);
			structures.Add (ret);

			return ret;
		}

		void WriteGlobalSymbolStart (string symbolName, bool constant, bool global)
		{
			var sb = new StringBuilder (global ? String.Empty : "private ");
			sb.Append ("local_unnamed_addr ");
			sb.Append (constant ? "constant " : "global ");

			Output.Write ($"@{symbolName} = {sb.ToString ()}");
		}

		object? GetTypedMemberValue<T> (StructureInfo<T> info, StructureMemberInfo<T> smi, StructureInstance<T> instance, Type expectedType, object? defaultValue = null)
		{
			object? value = smi.GetValue (instance.Obj);
			if (value == null) {
				return defaultValue;
			}

			if (value.GetType () != expectedType) {
				throw new InvalidOperationException ($"Field '{smi.Info.Name}' of structure '{info.Name}' should have a value of '{expectedType}' type, instead it had a '{value.GetType ()}'");
			}

			if (expectedType == typeof(bool)) {
				return (bool)value ? 1 : 0;
			}

			return value;
		}

		bool MaybeWriteStructureString<T> (StructureInfo<T> info, StructureMemberInfo<T> smi, StructureInstance<T> instance)
		{
			if (smi.MemberType != typeof(string)) {
				return false;
			}

			string? str = (string?)GetTypedMemberValue (info, smi, instance, typeof(string), null);
			if (str == null) {
				instance.AddStringData (smi, null, 0);
				return false;
			}
			string variableName = WriteString ($"__{info.Name}_{smi.Info.Name}_{structStringCounter++}", str, out ulong size);
			instance.AddStringData (smi, variableName, size);

			return true;
		}

		bool WriteStructureArrayStart<T> (StructureInfo<T> info, IList<StructureInstance<T>>? instances, string? symbolName = null, bool constant = true, bool global = false, string? initialComment = null)
		{
			if (global && String.IsNullOrEmpty (symbolName)) {
				throw new ArgumentException ("must not be null or empty for global symbols", nameof (symbolName));
			}

			bool named = !String.IsNullOrEmpty (symbolName);
			if (named || !String.IsNullOrEmpty (initialComment)) {
				WriteEOL ();
				WriteEOL (initialComment ?? symbolName);
			}

			if (instances != null && info.HasStrings) {
				// TODO: potentially de-duplicate strings? The linker should do it for us, but why start with a mess?
				bool wroteSomething = false;
				foreach (StructureInstance<T> instance in instances) {
					foreach (StructureMemberInfo<T> smi in info.Members) {
						if (MaybeWriteStructureString (info, smi, instance)) {
							wroteSomething = true;
						}
					}
				}

				if (wroteSomething) {
					WriteEOL ();
				}
			}

			if (named) {
				WriteGlobalSymbolStart (symbolName, constant, global);
			}

			return named;
		}

		void WriteStructureArrayEnd<T> (StructureInfo<T> info, string? symbolName, ulong count, bool named, bool skipFinalComment = false)
		{
			Output.Write ($", align {GetAggregateAlignment (info.MaxFieldAlignment, info.Size * count)}");
			if (named && !skipFinalComment) {
				WriteEOL ($"end of '{symbolName!}' array");
			} else {
				WriteEOL ();
			}
		}

		/// <summary>
		/// Writes an array of <paramref name="count"/> zero-initialized entries
		/// </summary>
		public void WriteStructureArray<T> (StructureInfo<T> info, ulong count, string? symbolName = null, bool constant = true, bool global = false, bool writeFieldComment = true, string? initialComment = null)
		{
			bool named = WriteStructureArrayStart<T> (info, null, symbolName, constant, global, initialComment);

			Output.Write ($"[{count} x %struct.{info.Name}] zeroinitializer");

			WriteStructureArrayEnd<T> (info, symbolName, (ulong)count, named, skipFinalComment: true);
		}

		public void WriteStructureArray<T> (StructureInfo<T> info, IList<StructureInstance<T>>? instances, string? symbolName = null, bool constant = true, bool global = false, bool writeFieldComment = true, string? initialComment = null)
		{
			bool named = WriteStructureArrayStart<T> (info, instances, symbolName, constant, global, initialComment);
			int count = instances != null ? instances.Count : 0;

			Output.Write ($"[{count} x %struct.{info.Name}] ");
			if (instances != null) {
				Output.WriteLine ("[");
				string fieldIndent = $"{Indent}{Indent}";
				for (int i = 0; i < count; i++) {
					StructureInstance<T> instance = instances[i];

					WriteStructureBody (info, instance, writeFieldComment: true, fieldIndent: fieldIndent, structIndent: Indent);
					if (i < count - 1) {
						Output.Write (", ");
					}
					WriteEOL ();
				}
				Output.Write ("]");
			} else {
				Output.Write ("zeroinitializer");
			}

			WriteStructureArrayEnd<T> (info, symbolName, (ulong)count, named, skipFinalComment: instances == null);
		}

		void WriteStructureBody<T> (StructureInfo<T> info, StructureInstance<T> instance, bool writeFieldComment, string fieldIndent, string structIndent)
		{
			Output.WriteLine ($"{structIndent}%struct.{info.Name} {{");

			for (int i = 0; i < info.Members.Count; i++) {
				StructureMemberInfo<T> smi = info.Members[i];

				Output.Write (fieldIndent);
				if (smi.IsNativePointer) {
					WritePointer (smi);
				} else {
					Output.Write ($"{smi.IRType} {GetTypedMemberValue (info, smi, instance, smi.MemberType)}");
				}

				if (i < info.Members.Count - 1) {
					Output.Write (", ");
				}

				if (writeFieldComment) {
					// TODO: append value in hex for integer types, LLVM IR doesn't support hex constants for integer fields (only for floats and doubles)
					WriteEOL (info.GetCommentFromProvider (smi, instance) ?? smi.Info.Name);
				} else {
					WriteEOL ();
				}
			}

			Output.Write ($"{structIndent}}}");

			void WritePointer (StructureMemberInfo<T> smi)
			{
				if (info.HasStrings) {
					StructureStringData? ssd = instance.GetStringData (smi);
					if (ssd != null) {
						WriteGetStringPointer (ssd.VariableName, ssd.Size, indent: false);
						return;
					}
				}

				object? value = smi.GetValue (instance.Obj);
				if (value == null || ((value is IntPtr) && (IntPtr)value == IntPtr.Zero)) {
					WriteNullPointer ();
					return;
				}

				if (value.GetType ().IsPrimitive) {
					ulong v = Convert.ToUInt64 (value);
					if (v == 0) {
						WriteNullPointer ();
						return;
					}
				}

				throw new InvalidOperationException ($"Non-null pointers to objects of managed type '{smi.Info.MemberType}' (IR type '{smi.IRType}') currently not supported (value: {value})");

				void WriteNullPointer ()
				{
					Output.Write ($"{smi.IRType} null");
				}
			}
		}

		public void WriteStructure<T> (StructureInfo<T> info, StructureInstance<T> instance, string? symbolName = null, bool constant = true, bool global = false, bool writeFieldComment = true)
		{
			if (global && String.IsNullOrEmpty (symbolName)) {
				throw new ArgumentException ("must not be null or empty for global symbols", nameof (symbolName));
			}

			bool named = !String.IsNullOrEmpty (symbolName);
			if (named) {
				WriteEOL ();
				WriteEOL (symbolName);
			}

			if (info.HasStrings) {
				bool wroteSomething = false;
				foreach (StructureMemberInfo<T> smi in info.Members) {
					if (MaybeWriteStructureString (info, smi, instance)) {
						wroteSomething = true;
					}
				}

				if (wroteSomething) {
					WriteEOL ();
				}
			}

			if (named) {
				WriteGlobalSymbolStart (symbolName, constant, global);
			}

			WriteStructureBody (info, instance, writeFieldComment, fieldIndent: Indent, structIndent: String.Empty);
			Output.WriteLine ($", align {info.MaxFieldAlignment}");
		}

		void WriteGetStringPointer (string? variableName, ulong size, bool indent = true)
		{
			if (indent) {
				Output.Write (Indent);
			}

			if (String.IsNullOrEmpty (variableName)) {
				Output.Write ($"i8* null");
			} else {
				Output.Write ($"i8* getelementptr inbounds ([{size} x i8], [{size} x i8]* @{variableName}, i32 0, i32 0)");
			}
		}

		public void WriteNameValueArray (string symbolName, IDictionary<string, string> arrayContents, bool constexprStrings = true)
		{
			WriteEOL ();
			WriteEOL (symbolName);

			var strings = new List<(ulong stringSize, string varName)> ();
			long i = 0;
			foreach (var kvp in arrayContents) {
				string name = kvp.Key;
				string value = kvp.Value;

				WriteArrayString (name, $"n_{i}");
				WriteArrayString (value, $"v_{i}");
				i++;
			}
			if (strings.Count > 0) {
				Output.WriteLine ();
			}

			Output.Write ($"@{symbolName} = local_unnamed_addr constant [{strings.Count} x i8*]");

			if (strings.Count > 0) {
				Output.WriteLine (" [");

				for (int j = 0; j < strings.Count; j++) {
					ulong size = strings[j].stringSize;
					string varName = strings[j].varName;

					//
					// Syntax: https://llvm.org/docs/LangRef.html#getelementptr-instruction
					// the two indices following {varName} have the following meanings:
					//
					//  - The first index is into the **pointer** itself
					//  - The second index is into the **pointed to** value
					//
					// Better explained here: https://llvm.org/docs/GetElementPtr.html#id4
					//
					WriteGetStringPointer (varName, size);
					if (j < strings.Count - 1) {
						Output.WriteLine (",");
					}
				}
				WriteEOL ();
			} else {
				Output.Write (" zeroinitializer");
			}

			var arraySize = (ulong)(strings.Count * PointerSize);
			if (strings.Count > 0) {
				Output.Write ("]");
			}
			Output.WriteLine ($", align {GetAggregateAlignment (PointerSize, arraySize)}");

			void WriteArrayString (string str, string symbolSuffix)
			{
				string name = WriteString ($"__{symbolName}_{symbolSuffix}", str, out ulong size, global: false);
				strings.Add (new (size, name));
			}
		}

		public void WriteVariable<T> (string symbolName, T value, bool global = true, bool isConstant = true)
		{
			if (typeof(T) == typeof(string)) {
				WriteString (symbolName, (string)(object)value, global, isConstant);
				return;
			}

			string irType = MapManagedTypeToIR<T> (out ulong size);
			string options = global ? String.Empty : "internal ";
			string type = isConstant ? "constant" : "global";

			Output.WriteLine ($"@{symbolName} = {options}local_unnamed_addr {type} {irType} {value}, align {size}");
		}

		/// <summary>
		/// Writes a private string. Strings without symbol names aren't exported, but they may be referenced by other
		/// symbols
		/// </summary>
		public string WriteString (string value, bool constexpr = true)
		{
			string name = $"@.str";
			if (stringCounter > 0) {
				name += $".{stringCounter}";
			}
			stringCounter++;
			return WriteString (name, value, global: false, constexpr: constexpr);
		}

		public string WriteString (string symbolName, string value, bool global = false, bool constexpr = true)
		{
			return WriteString (symbolName, value, out _, global, constexpr);
		}

		public string WriteString (string symbolName, string value, out ulong stringSize, bool global = false, bool constexpr = true)
		{
			string options;
			if (constexpr) {
				options = "internal";
			} else {
				options = "private unnamed_addr";
			}

			string strSymbolName;
			if (global) {
				strSymbolName = $"__{symbolName}";
			} else {
				strSymbolName = symbolName;
			}

			string quotedString = QuoteString (value, out stringSize);
			Output.WriteLine ($"@{strSymbolName} = {options} constant [{stringSize} x i8] c{quotedString}, align {GetAggregateAlignment (1, stringSize)}");
			if (!global) {
				return symbolName;
			}

			string indexType = Is64Bit ? "i64" : "i32";
			Output.WriteLine ($"@{symbolName} = local_unnamed_addr constant i8* getelementptr inbounds ([{stringSize} x i8], [{stringSize} x i8]* @{strSymbolName}, {indexType} 0, {indexType} 0), align {GetAggregateAlignment (PointerSize, stringSize)}");

			return symbolName;
		}

		public virtual void WriteFileTop ()
		{
			WriteCommentLine ($"ModuleID = '{fileName}'");
			WriteDirective ("source_filename", QuoteStringNoEscape (fileName));
			WriteDirective ("target datalayout", QuoteStringNoEscape (DataLayout));
			WriteDirective ("target triple", QuoteStringNoEscape (Triple));
		}

		public virtual void WriteFileEnd ()
		{
			Output.WriteLine ();

			foreach (LlvmIrMetadataItem metadata in MetadataManager.Items) {
				Output.WriteLine (metadata.Render ());
			}
		}

		public void WriteStructureDeclarations ()
		{
			if (structures.Count == 0) {
				return;
			}

			Output.WriteLine ();
			foreach (IStructureInfo si in structures) {
				si.RenderDeclaration (this);
			}
		}

		public void WriteStructureDeclarationStart (string name)
		{
			WriteEOL ();
			Output.WriteLine ($"%struct.{name} = type {{");
		}

		public void WriteStructureDeclarationEnd ()
		{
			Output.WriteLine ("}");
		}

		public void WriteStructureDeclarationField (string typeName, string comment, bool last)
		{
			Output.Write ($"{Indent}{typeName}");
			if (!last) {
				Output.Write (",");
			}

			if (!String.IsNullOrEmpty (comment)) {
				WriteCommentLine (comment);
			} else {
				WriteEOL ();
			}
		}

		protected virtual void AddModuleFlagsMetadata (List<LlvmIrMetadataItem> flagsFields)
		{
			flagsFields.Add (MetadataManager.AddNumbered (LlvmIrModuleMergeBehavior.Error, "wchar_size", 4));
			flagsFields.Add (MetadataManager.AddNumbered (LlvmIrModuleMergeBehavior.Max, "PIC Level", 2));
		}

		// Alignment for arrays, structures and unions
		protected virtual int GetAggregateAlignment (int maxFieldAlignment, ulong dataSize)
		{
			return maxFieldAlignment;
		}

		public void WriteCommentLine (string? comment = null, bool indent = false)
		{
			WriteCommentLine (Output, comment, indent);
		}

		public void WriteCommentLine (TextWriter writer, string? comment = null, bool indent = false)
		{
			if (indent) {
				writer.Write (Indent);
			}

			writer.Write (" ;");

			if (!String.IsNullOrEmpty (comment)) {
				writer.Write (' ');
				writer.Write (comment);
			}

			writer.WriteLine ();
		}

		public void WriteEOL (string? comment = null)
		{
			WriteEOL (Output, comment);
		}

		public void WriteEOL (TextWriter writer, string? comment = null)
		{
			if (!String.IsNullOrEmpty (comment)) {
				WriteCommentLine (writer, comment);
				return;
			}
			writer.WriteLine ();
		}

		public void WriteDirectiveWithComment (TextWriter writer, string name, string? comment, string? value)
		{
			writer.Write (name);

			if (!String.IsNullOrEmpty (value)) {
				writer.Write ($" = {value}");
			}

			WriteEOL (writer, comment);
		}

		public void WriteDirectiveWithComment (string name, string? comment, string? value)
		{
			WriteDirectiveWithComment (name, comment, value);
		}

		public void WriteDirective (TextWriter writer, string name, string? value)
		{
			WriteDirectiveWithComment (writer, name, comment: null, value: value);
		}

		public void WriteDirective (string name, string value)
		{
			WriteDirective (Output, name, value);
		}

		public static string QuoteStringNoEscape (string s)
		{
			return $"\"{s}\"";
		}

		public static string QuoteString (string value, bool nullTerminated = true)
		{
			return QuoteString (value, out _, nullTerminated);
		}

		public static string QuoteString (string value, out ulong stringSize, bool nullTerminated = true)
		{
			byte[] bytes = Encoding.UTF8.GetBytes (value);
			var sb = new StringBuilder ();

			foreach (byte b in value) {
				if (b >= 32 && b < 127) {
					sb.Append ((char)b);
					continue;
				}

				sb.Append ($"\\{b:X2}");
			}

			stringSize = (ulong)bytes.Length;
			if (nullTerminated) {
				stringSize++;
				sb.Append ("\\00");
			}

			return QuoteStringNoEscape (sb.ToString ());
		}
	}
}
