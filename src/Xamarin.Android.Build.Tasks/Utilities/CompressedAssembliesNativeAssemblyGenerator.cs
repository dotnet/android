using System.Collections.Generic;
using System.IO;

namespace Xamarin.Android.Tasks
{
    class CompressedAssembliesNativeAssemblyGenerator : NativeAssemblyGenerator
    {
	    const string CompressedAssembliesField = "compressed_assemblies";
	    const string DescriptorsField = "compressed_assembly_descriptors";

	    IDictionary<string, CompressedAssemblyInfo> assemblies;
	    string dataIncludeFile;

	    public CompressedAssembliesNativeAssemblyGenerator (IDictionary<string, CompressedAssemblyInfo> assemblies, NativeAssemblerTargetProvider targetProvider, string baseFilePath)
	        : base (targetProvider, baseFilePath)
        {
	        this.assemblies = assemblies;
	        dataIncludeFile = $"{baseFilePath}-data.inc";
        }

	    protected override void WriteSymbols (StreamWriter output)
	    {
		    if (assemblies == null || assemblies.Count == 0) {
			    WriteCompressedAssembliesStructure (output, 0, null);
			    return;
		    }

		    string label = MakeLocalLabel (DescriptorsField);
		    using (var dataOutput = MemoryStreamPool.Shared.CreateStreamWriter (output.Encoding)) {
			    uint size = 0;

			    output.Write (Indent);
			    output.Write (".include");
			    output.Write (Indent);
			    output.Write ('"');
			    output.Write (Path.GetFileName (dataIncludeFile));
			    output.WriteLine ('"');
			    output.WriteLine ();

			    WriteDataSection (output, DescriptorsField);
			    WriteStructureSymbol (output, label, alignBits: TargetProvider.MapModulesAlignBits, isGlobal: false);
			    foreach (var kvp in assemblies) {
				    string assemblyName = kvp.Key;
				    CompressedAssemblyInfo info = kvp.Value;

				    string dataLabel = GetAssemblyDataLabel (info.DescriptorIndex);
				    WriteCommSymbol (dataOutput, dataLabel, info.FileSize, 16);
				    dataOutput.WriteLine ();

				    size += WriteStructure (output, packed: false, structureWriter: () => WriteDescriptor (output, assemblyName, info, dataLabel));
			    }
			    WriteStructureSize (output, label, size);

			    dataOutput.Flush ();
			    MonoAndroidHelper.CopyIfStreamChanged (dataOutput.BaseStream, dataIncludeFile);
		    }

		    WriteCompressedAssembliesStructure (output, (uint)assemblies.Count, label);
	    }

	    uint WriteDescriptor (StreamWriter output, string assemblyName, CompressedAssemblyInfo info, string dataLabel)
	    {
		    WriteCommentLine (output, $"{info.DescriptorIndex}: {assemblyName}");

		    WriteCommentLine (output, "uncompressed_file_size");
		    uint size = WriteData (output, info.FileSize);

		    WriteCommentLine (output, "loaded");
		    size += WriteData (output, false);

		    WriteCommentLine (output, "data");
		    size += WritePointer (output, dataLabel);

		    output.WriteLine ();
		    return size;
	    }

	    string GetAssemblyDataLabel (uint index)
	    {
		    return $"compressed_assembly_data_{index}";
	    }

	    void WriteCompressedAssembliesStructure (StreamWriter output, uint count, string descriptorsLabel)
	    {
		    WriteDataSection (output, CompressedAssembliesField);
		    WriteSymbol (output, CompressedAssembliesField, TargetProvider.GetStructureAlignment (true), packed: false, isGlobal: true, alwaysWriteSize: true, structureWriter: () => {
			    // Order of fields and their type must correspond *exactly* to that in
			    // src/monodroid/jni/xamarin-app.h CompressedAssemblies structure
			    WriteCommentLine (output, "count");
			    uint size = WriteData (output, count);

			    WriteCommentLine (output, "descriptors");
			    size += WritePointer (output, descriptorsLabel);

			    return size;
		    });
	    }

	    void WriteDataSection (StreamWriter output, string tag)
	    {
		    WriteSection (output, $".data.{tag}", hasStrings: false, writable: true);
	    }
    }
}
