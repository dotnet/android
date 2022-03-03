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
		string fileName;
		List<IStructureInfo> structures = new List<IStructureInfo> ();

		protected abstract string DataLayout { get; }
		protected abstract int PointerSize { get; }
		protected abstract string Triple { get; }

		public TextWriter Output { get; }
		protected string Indent => "\t";
		protected LlvmIrMetadataManager MetadataManager { get; }

		protected LlvmIrGenerator (TextWriter output, string fileName)
		{
			Output = output;
			MetadataManager = new LlvmIrMetadataManager ();
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
					AndroidTargetArch.Arm => new Arm32LlvmIrGenerator (output, fileName),
					AndroidTargetArch.Arm64 => new Arm64LlvmIrGenerator (output, fileName),
					AndroidTargetArch.X86 => new X86LlvmIrGenerator (output, fileName),
					AndroidTargetArch.X86_64 => new X64LlvmIrGenerator (output, fileName),
					_ => throw new InvalidOperationException ($"Unsupported Android target ABI {arch}")
				};
			}
		}

		public static string MapManagedTypeToIR (Type type)
		{
			if (type == typeof (bool)) return "i8";
			if (type == typeof (byte)) return "i8";
			if (type == typeof (sbyte)) return "i8";
			if (type == typeof (short)) return "i16";
			if (type == typeof (ushort)) return "i16";
			if (type == typeof (int)) return "i32";
			if (type == typeof (uint)) return "i32";
			if (type == typeof (long)) return "i64";
			if (type == typeof (ulong)) return "i64";
			if (type == typeof (float)) return "float";
			if (type == typeof (double)) return "double";
			if (type == typeof (string)) return "i8*";

			throw new InvalidOperationException ($"Unsupported managed type {type}");
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
			Output.WriteLine ($"%struct.{name} = type {{");
		}

		public void WriteStructureDeclarationEnd ()
		{
			Output.WriteLine ("}");
			Output.WriteLine ();
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
			flagsFields.Add (MetadataManager.AddNumbered (LlvmIrModuleMergeBehavior.Max, "PIE Level", 2));
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

		public static string QuoteString (string value)
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

			return QuoteStringNoEscape (sb.ToString ());
		}
	}
}
