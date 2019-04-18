using System;
using System.IO;
using System.Text;

namespace Xamarin.Android.Tasks
{
	class TypeMappingNativeAssemblyGenerator : NativeAssemblyGenerator
	{
		NativeAssemblyDataStream dataStream;
		string dataFileName;
		uint dataSize;
		string mappingFieldName;

		public TypeMappingNativeAssemblyGenerator (NativeAssemblerTargetProvider targetProvider, NativeAssemblyDataStream dataStream, string dataFileName, uint dataSize, string mappingFieldName)
			: base (targetProvider)
		{
			if (dataStream == null)
				throw new ArgumentNullException (nameof (dataStream));
			if (String.IsNullOrEmpty (dataFileName))
				throw new ArgumentException ("must not be null or empty", nameof (dataFileName));
			if (String.IsNullOrEmpty (mappingFieldName))
				throw new ArgumentException ("must not be null or empty", nameof (mappingFieldName));

			this.dataStream = dataStream;
			this.dataFileName = dataFileName;
			this.dataSize = dataSize;
			this.mappingFieldName = mappingFieldName;
		}

		protected override void WriteFileHeader (StreamWriter output, string outputFileName)
		{
			// The hash is written to make sure the assembly file which includes the data one is
			// actually different whenever the data changes. Relying on mapping header values for this
			// purpose would not be enough since the only change to the mapping might be a single-character
			// change in one of the type names and we must be sure the assembly is rebuilt in all cases,
			// thus the SHA1.
			WriteEndLine (output, $"Data SHA1: {HashToHex (dataStream.GetStreamHash ())}", false);
			base.WriteFileHeader (output, outputFileName);
		}

		protected override void WriteSymbols (StreamWriter output)
		{
			WriteMappingHeader (output, dataStream, mappingFieldName);
			WriteCommentLine (output, "Mapping data");
			WriteSymbol (output, mappingFieldName, dataSize, isGlobal: true, isObject: true, alwaysWriteSize: true);
			output.WriteLine ($"{Indent}.include{Indent}\"{dataFileName}\"");
		}

		void WriteMappingHeader (StreamWriter output, NativeAssemblyDataStream dataStream, string mappingFieldName)
		{
			output.WriteLine ();
			WriteCommentLine (output, "Mapping header");
			WriteSection (output, $".data.{mappingFieldName}", hasStrings: false, writable: true);
			WriteSymbol (output, $"{mappingFieldName}_header", alignBits: 2, fieldAlignBytes: 4, isGlobal: true, alwaysWriteSize: true, structureWriter: () => {
				WriteCommentLine (output, "version");
				WriteData (output, dataStream.MapVersion);

				WriteCommentLine (output, "entry-count");
				WriteData (output, dataStream.MapEntryCount);

				WriteCommentLine (output, "entry-length");
				WriteData (output, dataStream.MapEntryLength);

				WriteCommentLine (output, "value-offset");
				WriteData (output, dataStream.MapValueOffset);
				return 16;
			});
			output.WriteLine ();
		}
	}
}
