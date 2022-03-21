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

		public class NativeType
		{
			public uint Size;
			public uint Alignment;
			public string? Name;
		}

		public class StructureField
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
			internal uint ByteAlignment = 0;
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

		sealed class StructureReflectionCacheEntry
		{
			public List<StructureMemberInfo> MemberInfos;
			public NativeAssemblerStructContextDataProvider? ContextDataProvider;
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

		Dictionary<Type, StructureReflectionCacheEntry> structureReflectionCache = new Dictionary<Type, StructureReflectionCacheEntry> ();

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
			WriteDirective (".file", QuoteString (fileName.Replace ("\\", "\\\\")));
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

			WriteDirective (".ident", QuoteString ($"Xamarin.Android {XABuildConfig.XamarinAndroidBranch} @ {XABuildConfig.XamarinAndroidCommitHash}"));
		}

		NativeType GetNativeType<T> ()
		{
			EnsureTypeMapping ();
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

		public void WriteEOL (string? comment = null, bool useBlockComment = false)
		{
			WriteEOL (Output, comment, useBlockComment);
		}

		public void WriteEOL (TextWriter writer, string? comment = null, bool useBlockComment = false)
		{
			if (!String.IsNullOrEmpty (comment)) {
				WriteCommentLine (writer, comment, useBlockComment: useBlockComment);
				return;
			}
			writer.WriteLine ();
		}

		public void WriteInclude (string filePath, string? comment = null, bool useBlockComment = false)
		{
			WriteInclude (Output, filePath, comment, useBlockComment);
		}

		public void WriteInclude (TextWriter writer, string filePath, string? comment = null, bool useBlockComment = false)
		{
			WriteDirectiveWithComment (writer, ".include", comment, useBlockComment, QuoteString (filePath));
		}

		public void WriteCommentLine (string? comment = null, bool indent = true, bool useBlockComment = false)
		{
			WriteCommentLine (Output, comment, indent);
		}

		public void WriteCommentLine (TextWriter writer, string? comment = null, bool indent = true, bool useBlockComment = false)
		{
			if (indent) {
				writer.Write (Indent);
			}

			if (useBlockComment) {
				writer.Write ("/* ");
			} else {
				writer.Write (LineCommentStart);
			}

			if (!String.IsNullOrEmpty (comment)) {
				writer.Write (' ');
				writer.Write (comment);
			}

			if (useBlockComment) {
				writer.Write (" */");
			}

			writer.WriteLine ();
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
			status.ArrayOfStructures = new List<StructureWriteContext> ();
			foreach (T obj in collection) {
				StructureWriteContext context = StartStructure ();
				status.ArrayOfStructures.Add (context);
				WriteStructure<T> (context, obj);
			}
		}

		public void WriteStructure<T> (StructureWriteContext status, T obj) where T: class
		{
			Type type = typeof(T);
			if (!structureReflectionCache.TryGetValue (type, out StructureReflectionCacheEntry entry)) {
				var providerAttr = type.GetCustomAttribute<NativeAssemblerStructContextDataProviderAttribute> ();
				NativeAssemblerStructContextDataProvider? contextDataProvider = null;

				if (providerAttr != null) {
					contextDataProvider = Activator.CreateInstance (providerAttr.Type) as NativeAssemblerStructContextDataProvider;
				}

				entry = new StructureReflectionCacheEntry {
					MemberInfos = GatherMembers<T> (),
					ContextDataProvider = contextDataProvider,
				};
				structureReflectionCache.Add (type, entry);
			}

			WriteStructure<T> (status, entry.MemberInfos, entry.ContextDataProvider, obj);
		}

		void WriteStructure<T> (StructureWriteContext status, List<StructureMemberInfo> members, NativeAssemblerStructContextDataProvider? contextDataProvider, T obj) where T: class
		{
			status.ReflectedType = typeof(T);
			foreach (StructureMemberInfo smi in members) {
				object? value = smi.Info switch {
					FieldInfo fi => fi.GetValue (obj),
					PropertyInfo pi => pi.GetValue (obj),
					_ => throw new InvalidOperationException ($"Unsupported member nativeType {smi.Info}")
				};

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
				} else if (smi.Type == typeof(string)) {
					if (!HandleAttributedStrings (smi, smi.Attr as NativeAssemblerStringAttribute, (string)(object)value!)) {
						WriteStringPointer (status, (string)(object)value!, global: false, label: null, comment: GetComment<T> (smi));
					}
				} else if (smi.Type == typeof(byte[])) {
					WriteArrayField<byte> (smi, value);
				} else if (smi.Type == typeof(int[])) {
					WriteArrayField<int> (smi, value);
				} else {
					throw new InvalidOperationException ($"Managed nativeType '{smi.Type}' is not supported");
				}
			}

			bool HandleAttributedStrings (StructureMemberInfo smi, NativeAssemblerStringAttribute? attr, string value)
			{
				if (attr == null) {
					return false;
				}

				if (attr.PointerToSymbol) {
					WritePointer (status, (string)(object)value!, comment: GetComment<T> (smi));
					return true;
				}

				if (!attr.Inline) {
					return false;
				}

				uint maxLength = 0;
				if (attr.PadToMaxLength) {
					EnsureContextDataProvider ();
					maxLength = contextDataProvider.GetMaxInlineWidth (obj, smi.Info.Name);
				}

				WriteInlineString (status, value, fixedWidth: maxLength, comment: GetComment<T> (smi));
				return true;
			}

			void WriteField<MT> (StructureMemberInfo smi, object? value) where MT: struct
			{
				WriteData<MT> (status, (MT)(object)value!, comment: GetComment<MT> (smi));
			}

			void WriteArrayField<MT> (StructureMemberInfo smi, object? values) where MT: struct
			{
				WriteData<MT> (status, (MT[])(object)values!, comment: GetComment<MT> (smi));
			}

			string GetComment<MT> (StructureMemberInfo smi)
			{
				if (smi.Attr != null) {
					if (smi.Attr.UsesDataProvider) {
						EnsureContextDataProvider ();
						return contextDataProvider.GetComment (obj, smi.Info.Name);
					}

					if (!String.IsNullOrEmpty (smi.Attr.Comment)) {
						return smi.Attr.Comment;
					}
				}

				return smi.Info.Name;
			}

			void EnsureContextDataProvider ()
			{
				if (contextDataProvider == null) {
					throw new InvalidOperationException ($"Type '{status.ReflectedType}' requires NativeAssemblerStructContextDataProviderAttribute to specify the context data provider");
				}
			}
		}

		void WriteSymbolType (TextWriter writer, string symbolName, SymbolType type, bool local, string? comment = null, bool useBlockComment = false)
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

			WriteDirectiveWithComment (writer, ".type", comment, useBlockComment, symbolName, $"{TypeLead}{symbolType}");
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

		public string WriteEmptySymbol (SymbolType symbolType, string symbolName, bool local = true, bool skipLabelCounter = false)
		{
			return WriteEmptySymbol (Output, symbolType, symbolName, local, skipLabelCounter);
		}

		public string WriteEmptySymbol (TextWriter writer, SymbolType symbolType, string symbolName, bool local = true, bool skipLabelCounter = false)
		{
			WriteSymbolType (writer, symbolName, symbolType, local);

			string label = local ? MakeLocalLabel (symbolName, skipCounter: skipLabelCounter) : symbolName;
			WriteSymbolLabel (writer, label);
			WriteSymbolSize (writer, label, 0);

			return label;
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

		public string WriteSymbol (StructureWriteContext status, string symbolName, bool local = true, bool useBlockComment = false, bool alreadyInSection = false, bool skipLabelCounter = false)
		{
			return WriteSymbol (Output, status, symbolName, local, useBlockComment, alreadyInSection);
		}

		public string WriteSymbol (TextWriter writer, StructureWriteContext status, string symbolName, bool local = true, bool useBlockComment = false, bool alreadyInSection = false, bool skipLabelCounter = false)
		{
			string label = local ? MakeLocalLabel (symbolName, skipCounter: skipLabelCounter) : symbolName;
			writer.WriteLine ();
			if (status.ReflectedType != null) {
				WriteCommentLine (writer, useBlockComment: useBlockComment);
				WriteCommentLine (writer, $"Generated from instance of: {status.ReflectedType.AssemblyQualifiedName}", useBlockComment: useBlockComment);
				WriteCommentLine (writer, useBlockComment: useBlockComment);
			}

			bool isArray;
			if (status.ArrayOfStructures != null) {
				if (status.ArrayOfStructures.Count == 0) {
					if (!alreadyInSection) {
						WriteDataSection (writer);
					}
					WriteEmptySymbol (writer, SymbolType.Object, label, local, skipLabelCounter);
					return label;
				}

				isArray = true;
			} else {
				isArray = false;
			}

			WriteSymbolType (writer, label, SymbolType.Object, local);
			if (!alreadyInSection) {
				WriteDataSection (writer);
			}

			StructureWriteContext c = isArray ? status.ArrayOfStructures![0] : status;
			WriteAlignmentDirective (writer, c.ByteAlignment);
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

			return label;

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

				padding = context.Size % context.ByteAlignment;
				if (padding > 0) {
					padding = context.ByteAlignment - padding;
					WriteDirective (writer, ".zero", padding);
					context.Size += padding;
				}

				return context.Size;
			}
		}

		public string  WriteSymbol (LabeledStringSymbol str, bool local = true, bool ownSection = false)
		{
			return WriteSymbol (Output, str, local, ownSection);
		}

		public string WriteSymbol (TextWriter writer, LabeledStringSymbol str, bool local = true, bool ownSection = false)
		{
			if (ownSection) {
				WriteStringSection (writer, str.Label);
			}

			WriteSymbolType (writer, str.Label, SymbolType.Object, local);
			WriteSymbolLabel (writer, str.Label);
			WriteDirective (writer, ".asciz", QuoteString (str.Contents));
			WriteSymbolSize (writer, str.Label, (uint)Encoding.UTF8.GetByteCount (str.Contents) + 1);
			writer.WriteLine ();

			return str.Label;
		}

		public string WriteSymbol<T> (string symbolName, T value, bool local = true, bool hex = true, string? comment = null, bool useBlockComment = false) where T: struct
		{
			return WriteSymbol<T> (Output, symbolName, value, local, hex, comment, useBlockComment);
		}

		public string WriteSymbol<T> (TextWriter writer, string symbolName, T value, bool local = true, bool hex = true, string? comment = null, bool useBlockComment = false) where T: struct
		{
			WriteSymbolType (writer, symbolName, SymbolType.Object, local, comment, useBlockComment);
			NativeType nativeType = GetNativeType<T> ();
			WriteAlignmentDirective (writer, nativeType);

			string label = local ? MakeLocalLabel (symbolName, skipCounter: true) : symbolName;
			WriteSymbolLabel (writer, label);
			WriteData<T> (writer, value, hex);

			WriteSymbolSize (writer, label, nativeType.Size);

			return label;
		}

		public void WriteStringPointerSymbol (string symbolName, string symbolValue, bool global = true, bool alreadyInSection = false)
		{
			WriteStringPointerSymbol (Output, symbolName, symbolValue, global);
		}

		public void WriteStringPointerSymbol (TextWriter writer, string symbolName, string symbolValue, bool global = true, bool alreadyInSection = false)
		{
			string symbolLabel = global ? symbolName : MakeLocalLabel (symbolName, skipCounter: true);
			WriteSymbolType (writer, symbolLabel, SymbolType.Object, !global);
			if (!alreadyInSection) {
				WriteDataSection (writer, symbolLabel);
			}

			NativeType nativeType = GetPointerType ();
			WriteAlignmentDirective (writer, nativeType.Alignment);
			WriteSymbolLabel (writer, symbolLabel);

			string stringLabel = MakeLocalLabel (AutomaticStringSection);

			var sym = new LabeledStringSymbol (stringLabel, symbolValue, global);
			stringTable.Add (sym);

			WritePointer (writer, sym.Label);
			WriteSymbolSize (writer, symbolLabel, nativeType.Size);
		}

		public string WriteStringSymbol (string symbolName, string symbolValue, bool global = true)
		{
			return WriteStringSymbol (Output, symbolName, symbolValue, global);
		}

		public string WriteStringSymbol (TextWriter writer, string symbolName, string symbolValue, bool global = true)
		{
			return WriteSymbol (writer, new LabeledStringSymbol (symbolName, symbolValue, global), !global);
		}

		protected void WriteData (TextWriter writer, string typeName, string value, string? comment = null, bool useBlockComment = false)
		{
			WriteDirectiveWithComment (writer, typeName, comment, useBlockComment, value);
		}

		protected void WriteData (string typeName, string value, string? comment = null, bool useBlockComment = false)
		{
			WriteData (Output, typeName, value, comment, useBlockComment);
		}

		public uint WritePointer (string? label, string? comment = null, bool useBlockComment = false)
		{
			return WritePointer (Output, label, comment, useBlockComment);
		}

		public uint WritePointer (TextWriter writer, string? label, string? comment = null, bool useBlockComment = false)
		{
			WriteData (writer, GetNativeTypeName<IntPtr> (), label == null ? "0" : label, comment, useBlockComment);
			return GetNativeTypeSize<IntPtr> ();
		}

		public uint WritePointer (StructureWriteContext status, string? label, string? comment = null, bool useBlockComment = false)
		{
			status.TempWriter.GetStringBuilder ().Clear ();

			uint ret = WritePointer (status.TempWriter, label, comment, useBlockComment);
			AddStructureField (status, GetNativeType<IntPtr> (), status.TempWriter.ToString ());

			return ret;
		}

		public uint WriteStringPointer (string value, bool global = false, string? label = null, string? comment = null, bool useBlockComment = false)
		{
			return WriteStringPointer (Output, value, global, label, comment, useBlockComment);
		}

		public uint WriteStringPointer (TextWriter writer, string value, bool global = false, string? label = null, string? comment = null, bool useBlockComment = false)
		{
			if (global && String.IsNullOrEmpty (label)) {
				throw new ArgumentException (nameof (label), "Must not be null or empty for global symbols");
			}

			if (String.IsNullOrEmpty (label)) {
				label = MakeLocalLabel (AutomaticStringSection);
			}

			var sym = new LabeledStringSymbol (label, value, global);
			stringTable.Add (sym);

			return WritePointer (writer, sym.Label, comment, useBlockComment);
		}

		public uint WriteStringPointer (StructureWriteContext status, string value, bool global = false, string? label = null, string? comment = null, bool useBlockComment = false)
		{
			status.TempWriter.GetStringBuilder ().Clear ();

			uint ret = WriteStringPointer (status.TempWriter, value, global, label, comment, useBlockComment);
			AddStructureField (status, GetNativeType<IntPtr> (), status.TempWriter.ToString ());

			return ret;
		}

		public uint WriteInlineString (StructureWriteContext status, string value, uint fixedWidth = 0, string? comment = null, bool useBlockComment = false)
		{
			status.TempWriter.GetStringBuilder ().Clear ();

			uint ret = WriteInlineString (status.TempWriter, value, fixedWidth, comment, useBlockComment);
			AddStructureField (status, GetNativeType<byte> (), status.TempWriter.ToString (), arrayElements: ret);
			return ret;
		}

		public uint WriteInlineString (string value, uint fixedWidth = 0, string? comment = null, bool useBlockComment = false)
		{
			return WriteInlineString (Output, value, fixedWidth, comment, useBlockComment);
		}

		// fixedWidth includes the terminating 0
		public uint WriteInlineString (TextWriter writer, string value, uint fixedWidth = 0, string? comment = null, bool useBlockComment = false)
		{
			int byteCount = Encoding.UTF8.GetByteCount (value);
			if (fixedWidth > 0 && byteCount >= fixedWidth) {
				throw new InvalidOperationException ($"String is too long, maximum allowed length is {fixedWidth - 1} bytes, the string is {byteCount} bytes long");
			}

			WriteDirectiveWithComment (writer, ".ascii", comment, useBlockComment, QuoteString (value));
			if (fixedWidth == 0) {
				WriteDirective (writer, ".zero", 1);
				return (uint)(byteCount + 1);
			}

			int padding = (((int)fixedWidth) - byteCount);
			WriteDirectiveWithComment (writer, ".zero", $"byteCount == {byteCount}; fixedWidth == {fixedWidth}; returned size == {byteCount + padding}", padding);

			return (uint)(byteCount + padding);
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

		public virtual uint WriteData<T> (TextWriter writer, T value, bool hex = true, string? comment = null, bool useBlockComment = false) where T: struct
		{
			WriteData (writer, GetNativeTypeName<T> (), ToString (value, hex), comment, useBlockComment);
			return GetNativeTypeSize<T> ();
		}

		public virtual void WriteData<T> (StructureWriteContext status, T value, bool hex = true, string? comment = null, bool useBlockComment = false) where T: struct
		{
			status.TempWriter.GetStringBuilder ().Clear ();
			WriteData (status, status.TempWriter, value, hex, comment, useBlockComment);
		}

		protected void WriteData<T> (StructureWriteContext status, StringWriter sw, T value, bool hex = true, string? comment = null, bool useBlockComment = false) where T: struct
		{
			WriteData (sw, value, hex, comment, useBlockComment);
			AddStructureField (status, GetNativeType<T> (), sw.ToString ());
		}

		public virtual uint WriteData<T> (T[] value, bool hex = true, string? comment = null, bool useBlockComment = false) where T: struct
		{
			return WriteData<T> (Output, value, hex, comment, useBlockComment);
		}

		public virtual uint WriteData<T> (TextWriter writer, T[] value, bool hex = true, string? comment = null, bool useBlockComment = false) where T: struct
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
			WriteEOL (writer, comment, useBlockComment);

			return (uint)(GetNativeTypeSize<T> () * value.Length);
		}

		public virtual void WriteData<T> (StructureWriteContext status, T[] value, bool hex = true, string? comment = null, bool useBlockComment = false) where T: struct
		{
			Type type = typeof(T);
			if (type == typeof(byte) || type == typeof(sbyte)) {
				status.TempWriter.GetStringBuilder ().Clear ();

				WriteData (status.TempWriter, value, hex, comment, useBlockComment);
				AddStructureField (status, GetNativeType<T> (), status.TempWriter.ToString (), (uint)value.Length);
				return;
			}

			bool first = true;
			foreach (T v in value) {
				WriteData (status, v, hex, first ? comment : null, useBlockComment);
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
			if (nativeType.Alignment > status.ByteAlignment) {
				status.ByteAlignment = nativeType.Alignment;
			}

			if (arrayElements > 0) {
				status.Size += nativeType.Size * arrayElements;
			} else {
				status.Size += nativeType.Size;
			}
			status.Fields.Add (new StructureField (nativeType, contents, arrayElements));
		}

		public virtual uint WriteData<T> (T value, bool hex = true, string? comment = null, bool useBlockComment = false) where T: struct
		{
			return WriteData<T> (Output, value, hex, comment, useBlockComment);
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

			WriteSection (writer, sectionName, flags, SectionType.Data);
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
				if (flags.HasFlag (SectionFlags.Allocatable)) {
					list.Append ("a");
				}

				if (flags.HasFlag (SectionFlags.GnuMbind)) {
					list.Append ("d");
				}

				if (flags.HasFlag (SectionFlags.Excluded)) {
					list.Append ("e");
				}

				if (flags.HasFlag (SectionFlags.ReferencesOtherSection)) {
					list.Append ("o");
				}

				if (flags.HasFlag (SectionFlags.Writable)) {
					list.Append ("w");
				}

				if (flags.HasFlag (SectionFlags.Executable)) {
					list.Append ("x");
				}

				if (flags.HasFlag (SectionFlags.Mergeable)) {
					list.Append ("M");
				}

				if (flags.HasFlag (SectionFlags.HasCStrings)) {
					list.Append ("S");
				}

				if (flags.HasFlag (SectionFlags.GroupMember)) {
					list.Append ("G");
				}

				if (flags.HasFlag (SectionFlags.ThreadLocalStorage)) {
					list.Append ("T");
				}

				if (flags.HasFlag (SectionFlags.Retained)) {
					list.Append ("R");
				}

				if (flags.HasFlag (SectionFlags.Number) || flags.HasFlag (SectionFlags.Custom)) {
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

		void WriteAlignmentDirective (TextWriter writer, NativeType nativeType)
		{
			WriteAlignmentDirective (writer, nativeType.Alignment);
		}

		void WriteAlignmentDirective (TextWriter writer, uint byteAlignment)
		{
			WriteDirective (writer, ".p2align", Log2 (byteAlignment));
		}

		public void WriteDirective (TextWriter writer, string name, params object[]? args)
		{
			WriteDirectiveWithComment (writer, name, comment: null, useBlockComment: false, args: args);
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
			WriteDirectiveWithComment (writer, name, comment, useBlockComment: false, args: args);
		}

		public void WriteDirectiveWithComment (TextWriter writer, string name, string? comment, bool useBlockComment, params object[]? args)
		{
			WriteDirectiveName (writer, name);

			if (args != null && args.Length > 0) {
				writer.Write (Indent);
				WriteCommaSeparatedList (writer, args);
			}

			WriteEOL (writer, comment, useBlockComment);
		}

		public void WriteDirectiveWithComment (string name, string? comment, params object[]? args)
		{
			WriteDirectiveWithComment (name, comment, useBlockComment: false, args: args);
		}

		public void WriteDirectiveWithComment (string name, string? comment, bool useBlockComment, params object[]? args)
		{
			WriteDirectiveWithComment (Output, name, comment, useBlockComment, args);
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

		public string MakeLocalLabel (string labelNamespaceOrSymbolName, bool skipCounter = false)
		{
			string label;

			if (skipCounter) {
				label = labelNamespaceOrSymbolName;
			} else {
				if (!localLabels.TryGetValue (labelNamespaceOrSymbolName, out ulong counter)) {
					counter = 0;
					localLabels[labelNamespaceOrSymbolName] = counter;
				} else {
					localLabels[labelNamespaceOrSymbolName] = ++counter;
				}
				label = $"{labelNamespaceOrSymbolName}.{counter}";
			}

			return $".L.{label}";
		}

		static double Log2 (double x)
		{
  #if NETCOREAPP
			return Math.Log2 (x);
  #else
			return Math.Log (x, 2);
  #endif
		}
	}
}
