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
		List<IStructureInfo> structures = new List<IStructureInfo> ();

		protected abstract string DataLayout { get; }
		protected abstract int PointerSize { get; }
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

			var ret = new StructureInfo<T> ();
			structures.Add (ret);

			return ret;
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
					Output.Write ($"{Indent}i8* getelementptr inbounds ([{size} x i8], [{size} x i8]* @{varName}, i32 0, i32 0)");
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
			WriteEOL ();

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

			Output.WriteLine ($"@{strSymbolName} = {options} constant c{QuoteString (value, out stringSize)}, align {GetAggregateAlignment (1, stringSize)}");
			if (!global) {
				return symbolName;
			}

			string indexType = Is64Bit ? "i64" : "i32";
			Output.WriteLine ($"@{symbolName} = local_unnamed_addr constant i8* getelementptr inbounds ([{stringSize} x i8], [{stringSize} x i8]* @{strSymbolName}, {indexType} 0, {indexType} 0), align {GetAggregateAlignment (PointerSize, stringSize)}");

			return symbolName;
		}

		public void WriteStructure<T> (string symbolName, T instance)
		{
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
				Output.Write (' ');
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

			writer.Write (';');

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
