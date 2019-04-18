using System;
using System.IO;
using System.Text;

namespace Xamarin.Android.Tasks
{
	abstract class NativeAssemblyGenerator
	{
		uint structureByteCount;
		uint structureAlignBytes;
		bool writingStructure = false;

		protected string Indent { get; } = "\t";
		protected NativeAssemblerTargetProvider TargetProvider { get; }

		protected NativeAssemblyGenerator (NativeAssemblerTargetProvider targetProvider)
		{
			if (targetProvider == null)
				throw new ArgumentNullException (nameof (targetProvider));
			TargetProvider = targetProvider;
		}

		public void Write (StreamWriter output, string outputFileName)
		{
			if (output == null)
				throw new ArgumentNullException (nameof (output));

			WriteFileHeader (output, outputFileName);
			WriteSymbols (output);
			output.Flush ();
		}

		protected virtual void WriteSymbols (StreamWriter output)
		{
		}

		protected string HashToHex (byte[] dataHash)
		{
			var sb = new StringBuilder ();
			foreach (byte b in dataHash)
				sb.Append ($"{b:x02}");
			return sb.ToString ();
		}

		protected void WriteEndLine (StreamWriter output, string comment = null, bool indent = true)
		{
			if (!String.IsNullOrEmpty (comment)) {
				if (indent)
					output.Write (Indent);
				WriteComment (output, comment);
			}
			output.WriteLine ();
		}

		protected void WriteComment (StreamWriter output, string comment, bool indent = true)
		{
			output.Write ($"{(indent ? Indent : String.Empty)}/* {comment} */");
		}

		protected void WriteCommentLine (StreamWriter output, string comment, bool indent = true)
		{
			WriteComment (output, comment);
			output.WriteLine ();
		}

		protected virtual void WriteFileHeader (StreamWriter output, string outputFileName)
		{
			TargetProvider.WriteFileHeader (output, Indent);
			output.WriteLine ($"{Indent}.file{Indent}\"{Path.GetFileName (outputFileName)}\"");
		}

		protected virtual void WriteSection (StreamWriter output, string sectionName, bool hasStrings, bool writable)
		{
			output.Write ($"{Indent}.section{Indent}{sectionName},\"a"); // a - section is allocatable
			if (hasStrings)
				output.Write ("MS"); // M - section is mergeable, S - section contains zero-terminated strings
			else if (writable)
				output.Write ("w");

			output.Write ($"\",{TargetProvider.TypePrefix}progbits");
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
			if (isObject)
				output.WriteLine ($"{Indent}.type{Indent}{symbolName}, {TargetProvider.TypePrefix}object");
			if (alignBits > 0)
				output.WriteLine ($"{Indent}.p2align{Indent}{alignBits}");
			if (isGlobal)
				output.WriteLine ($"{Indent}.global{Indent}{symbolName}");
			if (!String.IsNullOrEmpty (symbolName))
				output.WriteLine ($"{symbolName}:");
			if (!String.IsNullOrEmpty (symbolType) && !String.IsNullOrEmpty (symbolValue))
				output.WriteLine ($"{Indent}{symbolType}{Indent}{symbolValue}");
			if (alwaysWriteSize || size > 0)
				output.WriteLine ($"{Indent}.size{Indent}{symbolName}, {size}");
		}

		protected void WriteSymbol (StreamWriter output, string label, uint size, bool isGlobal, bool isObject, bool alwaysWriteSize)
		{
			WriteSymbol (output, null, null, label, size, alignBits: 0, isGlobal: isGlobal, isObject: isObject, alwaysWriteSize: alwaysWriteSize);
		}

		protected void WriteSymbol (StreamWriter output, string symbolName, uint alignBits, uint fieldAlignBytes, bool isGlobal, bool alwaysWriteSize, Func<uint> structureWriter)
		{
			output.WriteLine ($"{Indent}.type{Indent}{symbolName}, {TargetProvider.TypePrefix}object");
			if (alignBits > 0)
				output.WriteLine ($"{Indent}.p2align{Indent}{alignBits}");
			if (isGlobal)
				output.WriteLine ($"{Indent}.global{Indent}{symbolName}");
			if (!String.IsNullOrEmpty (symbolName))
				output.WriteLine ($"{symbolName}:");

			writingStructure = true;
			structureByteCount = 0;
			structureAlignBytes = fieldAlignBytes;
			uint size = structureWriter != null ? structureWriter () : 0u;
			writingStructure = false;
			if (alwaysWriteSize || size > 0)
				output.WriteLine ($"{Indent}.size{Indent}{symbolName}, {size}");
		}

		protected virtual string QuoteValue <T> (T value)
		{
			if (typeof(T) == typeof(string))
				return $"\"{value}\"";

			if (typeof(T) == typeof(bool))
				return (bool)((object)value) ? "1" : "0";

			return $"{value}";
		}

		protected uint WritePointer (StreamWriter output, string targetName, string label = null, bool isGlobal = false)
		{
			if (String.IsNullOrEmpty (targetName))
				throw new ArgumentException ("must not be null or empty", nameof (targetName));

			uint fieldSize = UpdateSize (output, targetName);
			WriteSymbol (output, TargetProvider.PointerFieldType, targetName, label, size: 0, alignBits: 0, isGlobal: isGlobal, isObject: false, alwaysWriteSize: false);
			return fieldSize;
		}

		uint WriteDataPadding (StreamWriter output, uint nextFieldSize, uint sizeSoFar)
		{
			if (!writingStructure || structureAlignBytes <= 1 || nextFieldSize == 1)
				return 0;

			uint alignment = sizeSoFar % structureAlignBytes;
			if (alignment == 0)
				return 0;

			uint nbytes = structureAlignBytes - alignment;
			output.WriteLine ($"{Indent}.zero{Indent}{nbytes}");
			return nbytes;
		}

		protected uint WriteData (StreamWriter output, string value, string label, bool isGlobal = false)
		{
			if (String.IsNullOrEmpty (label))
				throw new ArgumentException ("must not be null or empty", nameof (label));
			if (value == null)
				value = String.Empty;

			WriteSection (output, $".rodata.{label}", hasStrings: true, writable: false);
			WriteSymbol (output, value, label, size: (ulong)(value.Length + 1), alignBits: 0, isGlobal: isGlobal, isObject: true, alwaysWriteSize: true);
			return TargetProvider.GetTypeSize (value);
		}

		uint UpdateSize <T> (StreamWriter output, T value)
		{
			uint fieldSize;

			if (typeof (T) == typeof (string))
				fieldSize = TargetProvider.GetPointerSize ();
			else
				fieldSize = TargetProvider.GetTypeSize (value);
			fieldSize += WriteDataPadding (output, fieldSize, structureByteCount);
			structureByteCount += fieldSize;

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
