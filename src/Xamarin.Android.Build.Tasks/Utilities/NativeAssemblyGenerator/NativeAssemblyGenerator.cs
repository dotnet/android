using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	abstract class NativeAssemblyGenerator
	{
		protected const string AutomaticStringSection = "autostr";

		internal class NativeType
		{
			public uint Size;
			public uint Alignment;
			public string? Name;
		}

		internal class StructureField
		{
			public NativeType NativeType;
			public string Text;
			public uint ArrayElements;

			public StructureField (NativeType nativeType, string text, uint arrayElements)
			{
				NativeType = nativeType;
				Text = text;
				ArrayElements = arrayElements;
			}
		}

		public class LabeledSymbol
		{
			public readonly string Label;
			public readonly bool Global;

			internal LabeledSymbol (string label, bool global)
			{
				Label = label;
				Global = global;
			}
		}

		public class LabeledStringSymbol : LabeledSymbol
		{
			public readonly string Contents;

			internal LabeledStringSymbol (string label, string contents, bool global)
				: base (label, global)
			{
				Contents = contents;
			}
		}

		public class StructureWriteContext
		{
			internal uint ByteAlignement = 0;
			internal uint Size = 0;
			internal Type? ReflectedType = null;
			internal List<StructureField> Fields = new List<StructureField> ();
			internal StringWriter TempWriter = new StringWriter ();
			internal List<StructureWriteContext>? ArrayOfStructures;

			internal StructureWriteContext (bool isArray = false)
			{
				if (isArray) {
					ArrayOfStructures = new List<StructureWriteContext> ();
				}
			}
		}

		sealed class StructureMemberInfo
		{
			public readonly MemberInfo Info;
			public readonly Type       Type;
			public readonly NativeAssemblerAttribute? Attr;

			public StructureMemberInfo (MemberInfo info, Type type, NativeAssemblerAttribute? attr)
			{
				Info = info;
				Type = type;
				Attr = attr;
			}
		}

		string fileName;
		bool typeMappingConfigured = false;

		Dictionary<string, ulong> localLabels = new Dictionary<string, ulong> (StringComparer.Ordinal);

		Dictionary<Type, NativeType?> typeMapping = new Dictionary<Type, NativeType?> () {
			{ typeof(bool),   null },
			{ typeof(byte),   null },
			{ typeof(double), null },
			{ typeof(float),  null },
			{ typeof(int),    null },
			{ typeof(long),   null },
			{ typeof(nint),   null },
			{ typeof(nuint),  null },
			{ typeof(sbyte),  null },
			{ typeof(short),  null },
			{ typeof(uint),   null },
			{ typeof(ulong),  null },
			{ typeof(ushort), null },
		};

		List<LabeledStringSymbol> stringTable = new List<LabeledStringSymbol> ();

		public TextWriter Output { get; }
		protected string Indent => "\t";

		protected virtual string TypeLead => "@";
		protected virtual string LineCommentStart => "//";
		protected virtual uint StructureFieldAlignment => 4;

		public abstract bool Is64Bit { get; }

		protected NativeAssemblyGenerator (TextWriter output, string fileName)
		{
			Output = output;
			this.fileName = fileName;
		}

		public static NativeAssemblyGenerator Create (AndroidTargetArch arch, StreamWriter output, string fileName)
		{
			return arch switch {
				AndroidTargetArch.Arm => new Arm32NativeAssemblyGenerator (output, fileName),
				AndroidTargetArch.Arm64 => new Arm64NativeAssemblyGenerator (output, fileName),
				AndroidTargetArch.X86 => new X86_32NativeAssemblyGenerator (output, fileName),
				AndroidTargetArch.X86_64 => new X86_64NativeAssemblyGenerator (output, fileName),
				_ => throw new InvalidOperationException ($"Unsupported Android target ABI {arch}")
			};
		}

		public static string GetAbiName (AndroidTargetArch arch)
		{
			return arch switch {
				AndroidTargetArch.Arm => "armeabi-v7a",
				AndroidTargetArch.Arm64 => "arm64-v8a",
				AndroidTargetArch.X86 => "x86",
				AndroidTargetArch.X86_64 => "x86_64",
				_ => throw new InvalidOperationException ($"Unsupported Android architecture: {arch}"),
			};
		}

		protected void ConfigureTypeMapping<T> (string name, uint size, uint alignment)
		{
			typeMapping[typeof(T)] = new NativeType {
				Size = size,
				Alignment = alignment,
				Name = name,
			};
		}

		protected abstract NativeType GetPointerType ();

		protected virtual void ConfigureTypeMappings (Dictionary<Type, NativeType?> mapping)
		{
			ConfigureTypeMapping<byte> (".byte", size: 1, alignment: 1);
			ConfigureTypeMapping<bool> (".byte", size: 1, alignment: 1);
			ConfigureTypeMapping<sbyte> (".byte", size: 1, alignment: 1);
		}

		void EnsureTypeMapping ()
		{
			if (typeMappingConfigured) {
				return;
			}

			ConfigureTypeMappings (typeMapping);

			foreach (var kvp in typeMapping) {
				Type managedType = kvp.Key;
				NativeType? nativeType = kvp.Value;

				if (nativeType == null) {
					throw new InvalidOperationException ($"Missing managed nativeType {managedType} mapping");
				}

				if (nativeType.Size == 0) {
					throw new InvalidOperationException ($"Missing size of native nativeType corresponding to managed nativeType {managedType}");
				}

				if (nativeType.Alignment == 0) {
					throw new InvalidOperationException ($"Missing byte alignment of native nativeType corresponding to managed nativeType {managedType}");
				}

				if (String.IsNullOrEmpty (nativeType.Name)) {
					throw new InvalidOperationException ($"Missing name of native nativeType corresponding to managed nativeType {managedType}");
				}
			}

			typeMappingConfigured = true;
		}

		public virtual void WriteFileTop ()
		{
			WriteDirective (".file", QuoteString (fileName));
		}

		public virtual void WriteFileEnd ()
		{
			Output.WriteLine ();

			if (stringTable.Count > 0) {
				WriteStringSection (Output, AutomaticStringSection);

				foreach (LabeledStringSymbol sym in stringTable) {
					WriteSymbol (Output, sym, local: true, ownSection: false);
				}

				Output.WriteLine ();
			}

			WriteDirective (".ident", QuoteString ("Xamarin.Android X.Y.Z"));
		}

		NativeType GetNativeType<T> ()
		{
			if (!typeMapping.TryGetValue (typeof(T), out NativeType? nativeType) || nativeType == null) {
				throw new InvalidOperationException ($"Managed nativeType {typeof(T)} has no native mapping");
			}

			return nativeType;
		}

		protected uint GetNativeTypeAlignment<T> ()
		{
			EnsureTypeMapping ();
			return GetNativeType<T> ().Alignment;
		}

		protected uint GetNativeTypeSize<T> ()
		{
			EnsureTypeMapping ();
			return GetNativeType<T> ().Size;
		}

		protected string GetNativeTypeName<T> ()
		{
			EnsureTypeMapping ();
			return GetNativeType<T> ().Name!;
		}

		protected string ToString (Type type, object value, bool hex)
		{
			if (type == typeof(bool)) {
				return ToString (typeof(byte), (bool)value ? 1 : 0, hex);
			}

			if (type == typeof(IntPtr)) {
				if (value.GetType () == typeof(string)) {
					// we have a pointer and a label
					return (string)value;
				}
			}

			if (hex) {
				return $"0x{value:x}";
			}

			return $"{value}";
		}

		protected string ToString<T> (T value, bool hex) where T: struct
		{
			return ToString (typeof(T), value, hex);
		}

		public void WriteEOL (string? comment = null)
		{
			WriteEOL (Output, comment);
		}

		public void WriteEOL (TextWriter writer, string? comment = null)
		{
			if (!String.IsNullOrEmpty (comment)) {
				writer.Write ($"{Indent}{LineCommentStart} {comment}");
			}
			writer.WriteLine ();
		}

		public void WriteInclude (string filePath, string? comment = null)
		{
			WriteInclude (Output, filePath, comment);
		}

		public void WriteInclude (TextWriter writer, string filePath, string? comment = null)
		{
			WriteDirectiveWithComment (writer, ".include", comment, QuoteString (filePath));
		}

		public void WriteCommentLine (string? comment = null, bool indent = true)
		{
			WriteCommentLine (Output, comment, indent);
		}

		public void WriteCommentLine (TextWriter writer, string? comment = null, bool indent = true)
		{
			if (indent) {
				writer.Write (Indent);
			}

			writer.Write (LineCommentStart);
			if (String.IsNullOrEmpty (comment)) {
				writer.WriteLine ();
				return;
			}

			writer.WriteLine ($" {comment}");
		}

		public StructureWriteContext StartStructure () => new StructureWriteContext ();
		public StructureWriteContext StartStructureArray () => new StructureWriteContext (isArray: true);

		List<StructureMemberInfo> GatherMembers<T> () where T: class
		{
			Type klass = typeof(T);
			var ret = new List<StructureMemberInfo> ();

			foreach (MemberInfo mi in klass.GetMembers ()) {
				Type? memberType = mi switch {
					FieldInfo fi => fi.FieldType,
					PropertyInfo pi => GetPropertyType (pi),
					_ => null
				};

				if (memberType != null) {
					NativeAssemblerAttribute? attr = mi.GetCustomAttribute <NativeAssemblerAttribute> ();
					if (attr == null || !attr.Ignore) {
						ret.Add (new StructureMemberInfo (mi, memberType, attr));
					}
				}
			}

			return ret;

			Type GetPropertyType (PropertyInfo pi)
			{
				if (!pi.CanRead) {
					throw new InvalidOperationException ($"Property '{pi.DeclaringType}.{pi.Name}' doesn't have a getter.");
				}

				return pi.PropertyType;
			}
		}

		public StructureWriteContext AddStructureArrayElement (StructureWriteContext status)
		{
			if (status.ArrayOfStructures == null) {
				throw new InvalidOperationException ("Structure context doesn't refer to array of structures, did you use StartStructureArray()?");
			}

			var ret = StartStructure ();
			status.ArrayOfStructures.Add (ret);

			return ret;
		}

		public void WriteStructure<T> (StructureWriteContext status, T[] collection) where T: class
		{
			WriteStructure<T> (status, (IEnumerable<T>)collection);
		}

		public void WriteStructure<T> (StructureWriteContext status, IEnumerable<T> collection) where T: class
		{
			List<StructureMemberInfo> members = GatherMembers<T> ();
			status.ArrayOfStructures = new List<StructureWriteContext> ();
			foreach (T obj in collection) {
				StructureWriteContext context = StartStructure ();
				status.ArrayOfStructures.Add (context);
				WriteStructure<T> (context, members, obj);
			}
		}

		public void WriteStructure<T> (StructureWriteContext status, T obj) where T: class
		{
			WriteStructure<T> (status, GatherMembers<T> (), obj);
		}

		void WriteStructure<T> (StructureWriteContext status, List<StructureMemberInfo> members, T obj) where T: class
		{
			status.ReflectedType = typeof(T);
			foreach (StructureMemberInfo smi in members) {
				object? value = smi.Info switch {
					FieldInfo fi => fi.GetValue (obj),
					PropertyInfo pi => pi.GetValue (obj),
					_ => throw new InvalidOperationException ($"Unsupported member nativeType {smi.Info}")
				};

				// TODO: handle all arrays
				// TODO: handle IntPtr
				// TODO: handle the context data provider attribute on members
				// TODO: handle NativeAssemblyAttribute on the class
				if (smi.Type == typeof(bool)) {
					WriteField<bool> (smi, value);
				} else if (smi.Type == typeof(byte)) {
					WriteField<byte> (smi, value);
				} else if (smi.Type == typeof(double)) {
					WriteField<double> (smi, value);
				} else if (smi.Type == typeof(float)) {
					WriteField<float> (smi, value);
				} else if (smi.Type == typeof(int)) {
					WriteField<int> (smi, value);
				} else if (smi.Type == typeof(long)) {
					WriteField<long> (smi, value);
				} else if (smi.Type == typeof(sbyte)) {
					WriteField<sbyte> (smi, value);
				} else if (smi.Type == typeof(short)) {
					WriteField<short> (smi, value);
				} else if (smi.Type == typeof(uint)) {
					WriteField<uint> (smi, value);
				} else if (smi.Type == typeof(ulong)) {
					WriteField<ulong> (smi, value);
				} else if (smi.Type == typeof(ushort)) {
					WriteField<ushort> (smi, value);
				} else if (smi.Type == typeof(nint)) {
					WriteField<nint> (smi, value);
				} else if (smi.Type == typeof(nuint)) {
					WriteField<nuint> (smi, value);
				} else if (smi.Type == typeof(char)) {
					// TODO: handle - convert to UTF8
				} else if (smi.Type == typeof(string)) {
					// TODO: handle inline strings (with optional maximum width and padding)
					if (smi.Attr != null && smi.Attr.PointerToSymbol) {
						WritePointer (status, (string)(object)value!, smi.Attr.Comment);
					} else {
						WriteStringPointer (status, (string)(object)value!, global: false, label: null, comment: GetComment<T> (smi.Info));
					}
				} else if (smi.Type == typeof(byte[])) {
					WriteArrayField<byte> (smi, value);
				} else if (smi.Type == typeof(int[])) {
					WriteArrayField<int> (smi, value);
				} else {
					throw new InvalidOperationException ($"Managed nativeType '{smi.Type}' is not supported");
				}
			}

			void WriteField<MT> (StructureMemberInfo smi, object? value) where MT: struct
			{
				WriteData<MT> (status, (MT)(object)value!, comment: GetComment<MT> (smi.Info));
			}

			void WriteArrayField<MT> (StructureMemberInfo smi, object? values) where MT: struct
			{
				WriteData<MT> (status, (MT[])(object)values!, comment: GetComment<MT> (smi.Info));
			}

			string GetComment<MT> (MemberInfo mi)
			{
				// TODO: get from custom attribute
				return mi.Name;
			}
		}

		void WriteSymbolType (TextWriter writer, string symbolName, SymbolType type, bool local, string? comment = null)
		{
			string symbolType;
			switch (type) {
				case SymbolType.Object:
					symbolType = "object";
					break;

				case SymbolType.Common:
					symbolType = "common";
					break;

				case SymbolType.NoType:
					symbolType = "notype";
					break;

				case SymbolType.Function:
					symbolType = "function";
					break;

				case SymbolType.TlsObject:
					symbolType = "tls_object";
					break;

				case SymbolType.GnuUniqueObject:
					symbolType = "gnu_unique_object";
					break;

				case SymbolType.GnuIndirectFunction:
					symbolType = "gnu_indirect_function";
					break;

				default:
					throw new InvalidOperationException ($"Unknown symbol nativeType '{type}'");
			}

			WriteDirectiveWithComment (writer, ".type", comment ?? symbolName, symbolName, $"{TypeLead}{symbolType}");
			if (!local) {
				WriteDirective (writer, ".global", symbolName);
			}
		}

		void WriteSymbolLabel (TextWriter writer, string symbolName)
		{
			writer.WriteLine ($"{symbolName}:");
		}

		void WriteSymbolSize (TextWriter writer, string symbolName, uint size)
		{
			WriteDirective (writer, ".size", symbolName, size);
		}

		public void WriteEmptySymbol (SymbolType symbolType, string symbolName, bool local = true)
		{
			WriteEmptySymbol (Output, symbolType, symbolName, local);
		}

		public void WriteEmptySymbol (TextWriter writer, SymbolType symbolType, string symbolName, bool local = true)
		{
			WriteSymbolType (writer, symbolName, symbolType, local);

			string label = local ? MakeLocalLabel (symbolName) : symbolName;
			WriteSymbolLabel (writer, label);
			WriteSymbolSize (writer, label, 0);
		}

		public LabeledSymbol WriteCommSymbol (string labelNamespace, uint size, uint alignment)
		{
			return WriteCommSymbol (Output, labelNamespace, size, alignment);
		}

		public LabeledSymbol WriteCommSymbol (TextWriter writer, string labelNamespace, uint size, uint alignment)
		{
			var sym = new LabeledSymbol (MakeLocalLabel (labelNamespace), global: false);

			WriteSymbolType (writer, sym.Label, SymbolType.Object, local: true);
			WriteDirective (writer, ".local", sym.Label);
			WriteDirective (writer, ".comm", sym.Label, size, alignment);
			writer.WriteLine ();

			return sym;
		}

		public void WriteSymbol (TextWriter writer, StructureWriteContext status, string symbolName, bool local = true)
		{
			string label = local ? MakeLocalLabel (symbolName) : symbolName;
			writer.WriteLine ();
			if (status.ReflectedType != null) {
				WriteCommentLine (writer);
				WriteCommentLine (writer, $"Generated from instance of: {status.ReflectedType.AssemblyQualifiedName}");
				WriteCommentLine (writer);
			}

			bool isArray;
			if (status.ArrayOfStructures != null) {
				if (status.ArrayOfStructures.Count == 0) {
					WriteDataSection (writer);
					WriteEmptySymbol (writer, SymbolType.Object, symbolName, local);
					return;
				}

				isArray = true;
			} else {
				isArray = false;
			}

			WriteSymbolType (writer, symbolName, SymbolType.Object, local);
			WriteDataSection (writer);

			StructureWriteContext c = isArray ? status.ArrayOfStructures![0] : status;
			WriteDirective (writer, ".p2align", Math.Log2 (c.ByteAlignement));
			WriteSymbolLabel (writer, label);

			uint size = 0;
			if (status.ArrayOfStructures != null) {
				foreach (StructureWriteContext context in status.ArrayOfStructures) {
					size += WriteSingleStructure (context);
					writer.WriteLine ();
				}
			} else {
				size = WriteSingleStructure (status);
			}

			WriteSymbolSize (writer, label, size);

			uint WriteSingleStructure (StructureWriteContext context)
			{
				uint sizeSoFar = 0;
				uint padding;

				foreach (StructureField field in context.Fields) {
					if (field.NativeType.Alignment > 1) {
						padding = sizeSoFar % field.NativeType.Alignment;
						if (padding > 0) {
							padding = field.NativeType.Alignment - padding;
							WriteDirective (writer, ".zero", padding);
							sizeSoFar += padding;
							context.Size += padding;
						}
					}
					writer.Write (field.Text);
					if (field.ArrayElements > 0) {
						sizeSoFar += field.NativeType.Size * field.ArrayElements;
					} else {
						sizeSoFar += field.NativeType.Size;
					}
				}

				padding = context.Size % context.ByteAlignement;
				if (padding > 0) {
					padding = context.ByteAlignement - padding;
					WriteDirective (writer, ".zero", padding);
					context.Size += padding;
				}

				return context.Size;
			}
		}

		public void WriteSymbol (StructureWriteContext status, string symbolName, bool local = true)
		{
			WriteSymbol (Output, status, symbolName, local);
		}

		public void WriteSymbol (LabeledStringSymbol str, bool local = true, bool ownSection = false)
		{
			WriteSymbol (Output, str, local, ownSection);
		}

		public void WriteSymbol (TextWriter writer, LabeledStringSymbol str, bool local = true, bool ownSection = false)
		{
			if (ownSection) {
				WriteStringSection (writer, str.Label);
			}

			WriteSymbolType (writer, str.Label, SymbolType.Object, local);
			WriteSymbolLabel (writer, str.Label);
			WriteDirective (writer, ".asciz", QuoteString (str.Contents));
			WriteSymbolSize (writer, str.Label, (uint)Encoding.UTF8.GetByteCount (str.Contents) + 1);
			writer.WriteLine ();
		}

		public void WriteSymbol<T> (string symbolName, T value, bool local = true, bool hex = true, string? comment = null) where T: struct
		{
			WriteSymbol<T> (Output, symbolName, value, local, hex, comment);
		}

		public void WriteSymbol<T> (TextWriter writer, string symbolName, T value, bool local = true, bool hex = true, string? comment = null) where T: struct
		{
			WriteSymbolType (writer, symbolName, SymbolType.Object, local, comment);
			NativeType nativeType = GetNativeType<T> ();
			WriteDirective (writer, ".p2align", Math.Log2 (nativeType.Alignment));
			WriteSymbolLabel (writer, symbolName);
			WriteData<T> (writer, value, hex);
		}

		public void WriteStringSymbol (string symbolName, string symbolValue, bool global = true)
		{
			WriteStringSymbol (Output, symbolName, symbolValue, global);
		}

		public void WriteStringSymbol (TextWriter writer, string symbolName, string symbolValue, bool global = true)
		{
			WriteSymbol (writer, new LabeledStringSymbol (symbolName, symbolValue, global), !global);
		}

		protected void WriteData (TextWriter writer, string typeName, string value, string? comment = null)
		{
			WriteDirectiveWithComment (writer, typeName, comment, value);
		}

		protected void WriteData (string typeName, string value, string? comment = null)
		{
			WriteData (Output, typeName, value, comment);
		}

		public uint WritePointer (string? label, string? comment = null)
		{
			return WritePointer (Output, label, comment);
		}

		public uint WritePointer (TextWriter writer, string? label, string? comment = null)
		{
			WriteData (writer, GetNativeTypeName<IntPtr> (), label == null ? "0" : label, comment);
			return GetNativeTypeSize<IntPtr> ();
		}

		public uint WritePointer (StructureWriteContext status, string? label, string? comment = null)
		{
			status.TempWriter.GetStringBuilder ().Clear ();

			uint ret = WritePointer (status.TempWriter, label, comment);
			AddStructureField (status, GetNativeType<IntPtr> (), status.TempWriter.ToString ());

			return ret;
		}

		public uint WriteStringPointer (string value, bool global = false, string? label = null, string? comment = null)
		{
			return WriteStringPointer (Output, value, global, label, comment);
		}

		public uint WriteStringPointer (TextWriter writer, string value, bool global = false, string? label = null, string? comment = null)
		{
			if (global && String.IsNullOrEmpty (label)) {
				throw new ArgumentException (nameof (label), "Must not be null or empty for global symbols");
			}

			if (String.IsNullOrEmpty (label)) {
				label = MakeLocalLabel (AutomaticStringSection);
			}

			var sym = new LabeledStringSymbol (label, value, global);
			stringTable.Add (sym);

			return WritePointer (writer, sym.Label, comment);
		}

		public uint WriteStringPointer (StructureWriteContext status, string value, bool global = false, string? label = null, string? comment = null)
		{
			status.TempWriter.GetStringBuilder ().Clear ();

			uint ret = WriteStringPointer (status.TempWriter, value, global, label, comment);
			AddStructureField (status, GetNativeType<IntPtr> (), status.TempWriter.ToString ());

			return ret;
		}

		public uint WriteInlineString (string value, uint fixedWidth = 0, string? comment = null)
		{
			return WriteInlineString (Output, value, fixedWidth, comment);
		}

		// fixedWidth includes the terminating 0
		public uint WriteInlineString (TextWriter writer, string value, uint fixedWidth = 0, string? comment = null)
		{
			int byteCount = Encoding.UTF8.GetByteCount (value);
			if (fixedWidth > 0 && byteCount > fixedWidth - 1) {
				throw new InvalidOperationException ($"String is too long, maximum allowed length is {fixedWidth - 1} bytes, the string is {byteCount} bytes long");
			}

			WriteDirectiveWithComment (writer, ".asciiz", comment, value);
			if (fixedWidth == 0) {
				return (uint)(byteCount + 1);
			}

			int padding = ((int)fixedWidth - 1) - byteCount;
			if (padding > 0) {
				WriteDirective (".zero", padding);
			} else
				padding = 0;

			return (uint)(byteCount + 1 + padding);
		}

		public virtual LabeledSymbol WriteEmptyBuffer (uint bufferSize, string symbolLabelOrNamespace, bool local = true)
		{
			return WriteEmptyBuffer (Output, bufferSize, symbolLabelOrNamespace, local);
		}

		public virtual LabeledSymbol WriteEmptyBuffer (TextWriter writer, uint bufferSize, string symbolLabelOrNamespace, bool local = true)
		{
			string label = local ? MakeLocalLabel (symbolLabelOrNamespace) : symbolLabelOrNamespace;
			var sym = new LabeledSymbol (label, !local);

			WriteSymbolType (writer, sym.Label, SymbolType.Object, local);
			WriteSymbolLabel (writer, sym.Label);
			WriteDirective (writer, ".zero", bufferSize);
			WriteSymbolSize (writer, sym.Label, bufferSize);

			return sym;
		}

		public virtual uint WriteData<T> (TextWriter writer, T value, bool hex = true, string? comment = null) where T: struct
		{
			WriteData (writer, GetNativeTypeName<T> (), ToString (value, hex), comment);
			return GetNativeTypeSize<T> ();
		}

		public virtual void WriteData<T> (StructureWriteContext status, T value, bool hex = true, string? comment = null) where T: struct
		{
			status.TempWriter.GetStringBuilder ().Clear ();
			WriteData (status, status.TempWriter, value, hex, comment);
		}

		protected void WriteData<T> (StructureWriteContext status, StringWriter sw, T value, bool hex = true, string? comment = null) where T: struct
		{
			WriteData (sw, value, hex, comment);
			AddStructureField (status, GetNativeType<T> (), sw.ToString ());
		}

		public virtual uint WriteData<T> (T[] value, bool hex = true, string? comment = null) where T: struct
		{
			return WriteData<T> (Output, value, hex, comment);
		}

		public virtual uint WriteData<T> (TextWriter writer, T[] value, bool hex = true, string? comment = null) where T: struct
		{
			WriteDirectiveName (writer, GetNativeTypeName<T> ());
			writer.Write (Indent);

			bool first = true;
			foreach (T v in value) {
				if (first) {
					first = false;
				} else {
					writer.Write (", ");
				}

				writer.Write (ToString (typeof(T), v, hex));
			}
			WriteEOL (writer, comment);

			return (uint)(GetNativeTypeSize<T> () * value.Length);
		}

		public virtual void WriteData<T> (StructureWriteContext status, T[] value, bool hex = true, string? comment = null) where T: struct
		{
			Type type = typeof(T);
			if (type == typeof(byte) || type == typeof(sbyte)) {
				status.TempWriter.GetStringBuilder ().Clear ();

				WriteData (status.TempWriter, value, hex, comment);
				AddStructureField (status, GetNativeType<T> (), status.TempWriter.ToString (), (uint)value.Length);
				return;
			}

			bool first = true;
			foreach (T v in value) {
				WriteData (status, v, hex, first ? comment : null);
				if (first) {
					first = false;
				}
			}
		}

		void AddStructureField (StructureWriteContext status, NativeType nativeType, string contents, uint arrayElements = 0)
		{
			// Structure alignment is always the same as its most strictly aligned component, as per
			//
			// https://github.com/ARM-software/abi-aa/blob/320a56971fdcba282b7001cf4b84abb4fd993131/aapcs64/aapcs64.rst#59composite-types
			// https://github.com/ARM-software/abi-aa/blob/320a56971fdcba282b7001cf4b84abb4fd993131/aapcs32/aapcs32.rst#53composite-types
			if (nativeType.Alignment > status.ByteAlignement) {
				status.ByteAlignement = nativeType.Alignment;
			}

			if (arrayElements > 0) {
				status.Size += nativeType.Size * arrayElements;
			} else {
				status.Size += nativeType.Size;
			}
			status.Fields.Add (new StructureField (nativeType, contents, arrayElements));
		}

		public virtual uint WriteData<T> (T value, bool hex = true, string? comment = null) where T: struct
		{
			return WriteData<T> (Output, value, hex, comment);
		}

		public void WriteDataSection (TextWriter writer, string? name = null, bool writable = true)
		{
			string sectionName = writable ? ".data" : ".rodata";
			if (!String.IsNullOrEmpty (name)) {
				sectionName = $"{sectionName}.{name}";
			}

			SectionFlags flags = SectionFlags.Allocatable;
			if (writable) {
				flags |= SectionFlags.Writable;
			}

			WriteSection (sectionName, flags, SectionType.Data);
		}

		public void WriteDataSection (string? name = null, bool writable = true)
		{
			WriteDataSection (Output, name, writable);
		}

		public void WriteStringSection (TextWriter writer, string name)
		{
			WriteSection (
				writer,
				$".rodata.{name}",
				SectionFlags.Allocatable | SectionFlags.HasCStrings | SectionFlags.Mergeable,
				SectionType.Data,
				flagSpecificArgs: "1"
			);
		}

		public void WriteStringSection (string name)
		{
			WriteStringSection (Output, name);
		}

		public void WriteSection (TextWriter writer, string name, SectionFlags flags = SectionFlags.None, SectionType type = SectionType.None, string? flagSpecificArgs = null, string? numberOrCustomFlag = null, string? numberOrCustomType = null)
		{
			writer.WriteLine ();
			writer.Write ($"{Indent}.section{Indent}{name}");
			if (flags != SectionFlags.None) {
				writer.Write (", \"");

				var list = new StringBuilder ();
				if ((flags & SectionFlags.Allocatable) == SectionFlags.Allocatable) {
					list.Append ("a");
				}

				if ((flags & SectionFlags.GnuMbind) == SectionFlags.GnuMbind) {
					list.Append ("d");
				}

				if ((flags & SectionFlags.Excluded) == SectionFlags.Excluded) {
					list.Append ("e");
				}

				if ((flags & SectionFlags.ReferencesOtherSection) == SectionFlags.ReferencesOtherSection) {
					list.Append ("o");
				}

				if ((flags & SectionFlags.Writable) == SectionFlags.Writable) {
					list.Append ("w");
				}

				if ((flags & SectionFlags.Executable) == SectionFlags.Executable) {
					list.Append ("x");
				}

				if ((flags & SectionFlags.Mergeable) == SectionFlags.Mergeable) {
					list.Append ("M");
				}

				if ((flags & SectionFlags.HasCStrings) == SectionFlags.HasCStrings) {
					list.Append ("S");
				}

				if ((flags & SectionFlags.GroupMember) == SectionFlags.GroupMember) {
					list.Append ("G");
				}

				if ((flags & SectionFlags.ThreadLocalStorage) == SectionFlags.ThreadLocalStorage) {
					list.Append ("T");
				}

				if ((flags & SectionFlags.Retained) == SectionFlags.Retained) {
					list.Append ("R");
				}

				if ((flags & SectionFlags.Number) == SectionFlags.Number || (flags & SectionFlags.Custom) == SectionFlags.Custom) {
					if (String.IsNullOrEmpty (numberOrCustomFlag)) {
						throw new InvalidOperationException ("Section number or target-specific flag value must be specified");
					}

					list.Append (numberOrCustomFlag);
				}

				writer.Write ($"{list}\"");
			}

			if (type != SectionType.None) {
				writer.Write ($", {TypeLead}");
				switch (type) {
					case SectionType.Data:
						writer.Write ("progbits");
						break;

					case SectionType.NoData:
						writer.Write ("nobits");
						break;

					case SectionType.InitArray:
						writer.Write ("init_array");
						break;

					case SectionType.FiniArray:
						writer.Write ("fini_array");
						break;

					case SectionType.PreInitArray:
						writer.Write ("preinit_array");
						break;

					case SectionType.Number:
					case SectionType.Custom:
						if (String.IsNullOrEmpty (numberOrCustomType)) {
							throw new InvalidOperationException ("NativeType number or target-specific nativeType name must be specified");
						}
						writer.Write (numberOrCustomType);
						break;
				}
			}

			if (!String.IsNullOrEmpty (flagSpecificArgs)) {
				writer.Write ($", {flagSpecificArgs}");
			}

			WriteEOL (writer);
		}

		public void WriteSection (string name, SectionFlags flags = SectionFlags.None, SectionType type = SectionType.None, string? flagSpecificArgs = null, string? numberOrCustomFlag = null, string? numberOrCustomType = null)
		{
			WriteSection (Output, name, flags, type, flagSpecificArgs, numberOrCustomFlag, numberOrCustomType);
		}

		public void WriteDirective (TextWriter writer, string name, params object[]? args)
		{
			WriteDirectiveWithComment (writer, name, comment: null, args: args);
		}

		public void WriteDirective (string name, params object[]? args)
		{
			WriteDirective (Output, name, args);
		}

		public void WriteDirectiveName (string name)
		{
			WriteDirectiveName (Output, name);
		}

		public void WriteDirectiveName (TextWriter writer, string name)
		{
			writer.Write ($"{Indent}{name}");
		}

		public void WriteDirectiveWithComment (TextWriter writer, string name, string? comment, params object[]? args)
		{
			WriteDirectiveName (writer, name);

			if (args != null && args.Length > 0) {
				writer.Write (Indent);
				WriteCommaSeparatedList (writer, args);
			}

			WriteEOL (writer, comment);
		}

		public void WriteDirectiveWithComment (string name, string? comment, params object[]? args)
		{
			WriteDirectiveWithComment (Output, name, comment, args);
		}

		protected void WriteCommaSeparatedList (TextWriter writer, object[]? args)
		{
			if (args == null || args.Length == 0) {
				return;
			}

			writer.Write (String.Join (", ", args));
		}

		protected void WriteCommaSeparatedList (object[]? args)
		{
			WriteCommaSeparatedList (Output, args);
		}

		protected string QuoteString (string s)
		{
			return $"\"{s}\"";
		}

		public string MakeLocalLabel (string labelNamespace)
		{
			if (!localLabels.TryGetValue (labelNamespace, out ulong counter)) {
				counter = 0;
				localLabels[labelNamespace] = counter;
			} else {
				localLabels[labelNamespace] = ++counter;
			}

			return $".L.{labelNamespace}.{counter}";
		}
	}
}
