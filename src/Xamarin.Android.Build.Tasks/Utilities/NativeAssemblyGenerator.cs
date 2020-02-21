using System;
using System.IO;
using System.Text;

namespace Xamarin.Android.Tasks
{
	abstract class NativeAssemblyGenerator
	{
		uint structureByteCount;
		bool structureIsPacked = false;
		bool writingStructure = false;

		protected string Indent { get; } = "\t";
		protected NativeAssemblerTargetProvider TargetProvider { get; }
		protected string TypemapsIncludeFile { get; }
		protected string SharedIncludeFile { get; }
		public string MainSourceFile { get; }

		protected NativeAssemblyGenerator (NativeAssemblerTargetProvider targetProvider, string baseFilePath, bool sharedIncludeUsesAbiPrefix = false)
		{
			if (targetProvider == null)
				throw new ArgumentNullException (nameof (targetProvider));
			if (String.IsNullOrEmpty (baseFilePath))
				throw new ArgumentException ("must not be null or empty", nameof (baseFilePath));

			TargetProvider = targetProvider;
			if (sharedIncludeUsesAbiPrefix)
				SharedIncludeFile = $"{baseFilePath}.{targetProvider.AbiName}-shared.inc";
			else
				SharedIncludeFile = $"{baseFilePath}.shared.inc";
			TypemapsIncludeFile = $"{baseFilePath}.{targetProvider.AbiName}-managed.inc";
			MainSourceFile = $"{baseFilePath}.{targetProvider.AbiName}.s";
		}

		public void Write (StreamWriter output)
		{
			if (output == null)
				throw new ArgumentNullException (nameof (output));

			WriteFileHeader (output);
			WriteSymbols (output);
			WriteFileFooter (output);
			output.Flush ();
		}

		protected virtual void WriteSymbols (StreamWriter output)
		{}

		protected void WriteEndLine (StreamWriter output, string comment = null, bool indent = true)
		{
			if (!String.IsNullOrEmpty (comment)) {
				WriteComment (output, comment, indent);
			}
			output.WriteLine ();
		}

		protected void WriteComment (StreamWriter output, string comment, bool indent = true)
		{
			if (indent)
				output.Write (Indent);
			output.Write ("/* ");
			output.Write (comment);
			output.Write (" */");
		}

		protected void WriteCommentLine (StreamWriter output, string comment, bool indent = true)
		{
			WriteComment (output, comment, indent);
			output.WriteLine ();
		}

		protected virtual void WriteFileHeader (StreamWriter output)
		{
			TargetProvider.WriteFileHeader (output, Indent);
			output.Write (Indent);
			output.Write (".file");
			output.Write (Indent);
			output.Write ('"');
			output.Write (Path.GetFileName (MainSourceFile));
			output.WriteLine ('"');
		}

		protected virtual void WriteFileFooter (StreamWriter output)
		{}

		protected virtual void WriteSection (StreamWriter output, string sectionName, bool hasStrings, bool writable)
		{
			output.Write (Indent);
			output.Write (".section");
			output.Write (Indent);
			output.Write (sectionName);
			output.Write (",\"a"); // a - section is allocatable
			if (hasStrings)
				output.Write ("MS"); // M - section is mergeable, S - section contains zero-terminated strings
			else if (writable)
				output.Write ('w');

			output.Write ("\",");
			output.Write (TargetProvider.TypePrefix);
			output.Write ("progbits");
			if (hasStrings)
				output.Write (",1");
			output.WriteLine ();
		}

		// `alignBits` indicates the number of lowest bits that have to be cleared to 0 in order to align the
		// following data structure, thus `2` would mean "align to 4 bytes", `3' would be "align to 8 bytes"
		// etc. In general, if the data field contains a pointer the alignment should be to the platform's
		// native pointer size, if not it should be 2 (for 4-byte alignment) in the case of the targets we
		// support. Alignment is not necessary for standalone fields (i.e. not parts of a structure)
		protected void WriteSymbol <T> (StreamWriter output, T symbolValue, string symbolName, ulong size, uint alignBits, bool isGlobal, bool isObject, bool alwaysWriteSize)
		{
			WriteSymbol (output, TargetProvider.MapType<T>(), QuoteValue (symbolValue), symbolName, size, alignBits, isGlobal, isObject, alwaysWriteSize);
		}

		protected void WriteSymbol (StreamWriter output, string symbolType, string symbolValue, string symbolName, ulong size, uint alignBits, bool isGlobal, bool isObject, bool alwaysWriteSize)
		{
			if (isObject) {
				output.Write (Indent);
				output.Write (".type");
				output.Write (Indent);
				output.Write (symbolName);
				output.Write (", ");
				output.Write (TargetProvider.TypePrefix);
				output.WriteLine ("object");
			}
			if (alignBits > 0) {
				output.Write (Indent);
				output.Write (".p2align");
				output.Write (Indent);
				output.WriteLine (alignBits);
			}
			if (isGlobal) {
				output.Write (Indent);
				output.Write (".global");
				output.Write (Indent);
				output.WriteLine (symbolName);
			}
			if (!String.IsNullOrEmpty (symbolName)) {
				output.Write (symbolName);
				output.WriteLine (':');
			}
			if (!String.IsNullOrEmpty (symbolType) && !String.IsNullOrEmpty (symbolValue)) {
				output.Write (Indent);
				output.Write (symbolType);
				output.Write (Indent);
				output.WriteLine (symbolValue);
			}
			if (alwaysWriteSize || size > 0) {
				output.Write (Indent);
				output.Write (".size");
				output.Write (Indent);
				output.Write (symbolName);
				output.Write (", ");
				output.WriteLine (size);
			}
		}

		protected void WriteSymbol (StreamWriter output, string label, uint size, bool isGlobal, bool isObject, bool alwaysWriteSize)
		{
			WriteSymbol (output, null, null, label, size, alignBits: 0, isGlobal: isGlobal, isObject: isObject, alwaysWriteSize: alwaysWriteSize);
		}

		protected void WriteSymbol (StreamWriter output, string label, uint size, uint alignBits, bool isGlobal, bool isObject, bool alwaysWriteSize)
		{
			WriteSymbol (output, null, null, label, size, alignBits, isGlobal: isGlobal, isObject: isObject, alwaysWriteSize: alwaysWriteSize);
		}

		protected void WriteSymbol (StreamWriter output, string symbolName, uint alignBits, bool packed, bool isGlobal, bool alwaysWriteSize, Func<uint> structureWriter)
		{
			WriteStructureSymbol (output, symbolName, alignBits, isGlobal);
			uint size = WriteStructure (output, packed, structureWriter);
			WriteStructureSize (output, symbolName, size, alwaysWriteSize);
		}

		protected void WriteStructureSymbol (StreamWriter output, string symbolName, uint alignBits, bool isGlobal)
		{
			output.Write (Indent);
			output.Write (".type");
			output.Write (Indent);
			output.Write (symbolName);
			output.Write (", ");
			output.Write (TargetProvider.TypePrefix);
			output.WriteLine ("object");

			if (alignBits > 0) {
				output.Write (Indent);
				output.Write (".p2align");
				output.Write (Indent);
				output.WriteLine (alignBits);
			}
			if (isGlobal) {
				output.Write (Indent);
				output.Write (".global");
				output.Write (Indent);
				output.WriteLine (symbolName);
			}
			if (!String.IsNullOrEmpty (symbolName)) {
				output.Write (symbolName);
				output.WriteLine (':');
			}
		}

		protected void WriteStructureSize (StreamWriter output, string symbolName, uint size, bool alwaysWriteSize = false)
		{
			if (size == 0 && !alwaysWriteSize)
				return;

			if (String.IsNullOrEmpty (symbolName))
				throw new ArgumentException ("symbol name must be non-empty in order to write structure size", nameof (symbolName));

			output.Write (Indent);
			output.Write (".size");
			output.Write (Indent);
			output.Write (symbolName);
			output.Write (", ");
			output.WriteLine (size);
		}

		protected uint WriteStructure (StreamWriter output, bool packed, Func<uint> structureWriter)
		{
			writingStructure = true;
			structureByteCount = 0;
			structureIsPacked = packed;
			uint size = structureWriter != null ? structureWriter () : 0u;
			writingStructure = false;

			return size;
		}

		protected virtual string QuoteValue <T> (T value)
		{
			if (typeof(T) == typeof(string))
				return $"\"{value}\"";

			if (typeof(T) == typeof(bool))
				return (bool)((object)value) ? "1" : "0";

			return $"{value}";
		}

		protected uint WritePointer (StreamWriter output, string targetName = null, string label = null, bool isGlobal = false)
		{
			uint fieldSize = UpdateSize (output, targetName ?? String.Empty);
			WriteSymbol (output, TargetProvider.PointerFieldType, targetName ?? "0", label, size: 0, alignBits: 0, isGlobal: isGlobal, isObject: false, alwaysWriteSize: false);
			return fieldSize;
		}

		uint WriteDataPadding (StreamWriter output, uint nextFieldSize, uint sizeSoFar)
		{
			if (!writingStructure || structureIsPacked || nextFieldSize == 1)
				return 0;

			uint modulo;
			if (TargetProvider.Is64Bit) {
				modulo = nextFieldSize < 8 ? 4u : 8u;
			} else {
				modulo = 4u;
			}

			uint alignment = sizeSoFar % modulo;
			if (alignment == 0)
				return 0;

			uint nbytes = modulo - alignment;
			return WriteDataPadding (output, nbytes);
		}

		protected uint WriteDataPadding (StreamWriter output, uint nbytes)
		{
			if (nbytes == 0)
				return 0;

			output.Write (Indent);
			output.Write (".zero");
			output.Write (Indent);
			output.WriteLine (nbytes);
			return nbytes;
		}

		protected uint WriteData (StreamWriter output, string value, string label, bool isGlobal = false)
		{
			if (String.IsNullOrEmpty (label))
				throw new ArgumentException ("must not be null or empty", nameof (label));
			if (value == null)
				value = String.Empty;

			WriteSection (output, $".rodata.{label}", hasStrings: true, writable: false);
			WriteSymbol (output, value, isGlobal ? label : MakeLocalLabel (label), size: (ulong)(value.Length + 1), alignBits: 0, isGlobal: isGlobal, isObject: true, alwaysWriteSize: true);
			return TargetProvider.GetTypeSize (value);
		}

		protected uint WriteAsciiData (StreamWriter output, string value, uint padToWidth = 0)
		{
			if (value == null)
				value = String.Empty;

			uint size = (uint)output.Encoding.GetByteCount (value);
			if (size > 0) {
				output.Write (Indent);
				output.Write (".ascii");
				output.Write (Indent);
				output.Write ('"');
				output.Write (value);
				output.WriteLine ('"');
			} else if (padToWidth == 0) {
				return WriteDataPadding (output, 1);
			}

			if (padToWidth > size)
				size += WriteDataPadding (output, padToWidth - size);

			return size;
		}

		protected string MakeLocalLabel (string label)
		{
			return $".L.{label}";
		}

		uint UpdateSize <T> (StreamWriter output, T[] value)
		{
			uint typeSize = TargetProvider.GetTypeSize <T> ();
			uint fieldSize = typeSize * (uint)value.Length;

			fieldSize += WriteDataPadding (output, fieldSize, structureByteCount);
			structureByteCount += fieldSize;

			return fieldSize;
		}

		uint UpdateSize <T> (StreamWriter output, T value)
		{
			uint fieldSize;

			Type t = typeof(T);
			if (t == typeof (string))
				fieldSize = TargetProvider.GetPointerSize ();
			else
				fieldSize = TargetProvider.GetTypeSize (value);

			fieldSize += WriteDataPadding (output, fieldSize, structureByteCount);
			structureByteCount += fieldSize;

			return fieldSize;
		}

		protected uint WriteData (StreamWriter output, byte[] value, string label = null, bool isGlobal = false)
		{
			uint fieldSize = UpdateSize (output, value);
			var symbolValue = new StringBuilder ();
			bool first = true;
			foreach (byte b in value) {
				if (!first)
					symbolValue.Append (", ");
				else
					first = false;

				symbolValue.Append ($"0x{b:x02}");
			}
			WriteSymbol (output, TargetProvider.MapType<byte> (), symbolValue.ToString (), symbolName: label, size: 0, alignBits: 0, isGlobal: isGlobal, isObject: false, alwaysWriteSize: false);

			return fieldSize;
		}

		protected uint WriteData (StreamWriter output, byte value, string label = null, bool isGlobal = false)
		{
			uint fieldSize = UpdateSize (output, value);
			WriteSymbol (output, value, label, size: 0, alignBits: 0, isGlobal: isGlobal, isObject: false, alwaysWriteSize: false);
			return fieldSize;
		}

		protected uint WriteData (StreamWriter output, Int32 value, string label = null, bool isGlobal = false)
		{
			uint fieldSize = UpdateSize (output, value);
			WriteSymbol (output, value, label, size: 0, alignBits: 0, isGlobal: isGlobal, isObject: false, alwaysWriteSize: false);
			return fieldSize;
		}

		protected uint WriteData (StreamWriter output, UInt32 value, string label = null, bool isGlobal = false)
		{
			uint fieldSize = UpdateSize (output, value);
			WriteSymbol (output, value, label, size: 0, alignBits: 0, isGlobal: isGlobal, isObject: false, alwaysWriteSize: false);
			return fieldSize;
		}

		protected uint WriteData (StreamWriter output, Int64 value, string label = null, bool isGlobal = false)
		{
			uint fieldSize = UpdateSize (output, value);
			WriteSymbol (output, value, label, size: 0, alignBits: 0, isGlobal: isGlobal, isObject: false, alwaysWriteSize: false);
			return fieldSize;
		}

		protected uint WriteData (StreamWriter output, UInt64 value, string label = null, bool isGlobal = false)
		{
			uint fieldSize = UpdateSize (output, value);
			WriteSymbol (output, value, label, size: 0, alignBits: 0, isGlobal: isGlobal, isObject: false, alwaysWriteSize: false);
			return fieldSize;
		}

		protected uint WriteData (StreamWriter output, bool value, string label = null, bool isGlobal = false)
		{
			uint fieldSize = UpdateSize (output, value);
			WriteSymbol (output, value, label, size: 0, alignBits: 0, isGlobal: isGlobal, isObject: false, alwaysWriteSize: false);
			return fieldSize;
		}
	}
}
