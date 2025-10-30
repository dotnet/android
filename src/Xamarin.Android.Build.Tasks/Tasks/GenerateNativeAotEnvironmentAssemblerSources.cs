using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

public class GenerateNativeAotEnvironmentAssemblerSources : AndroidTask
{
	public override string TaskPrefix => "GNAEAS";

	[Required]
	public ITaskItem[] OutputSources { get; set; } = [];

	[Required]
	public string RID { get; set; } = "";

	public bool Debug { get; set; }
	public ITaskItem[]? Environments { get; set; }
	public string? HttpClientHandlerType { get; set; }

	public override bool RunTask ()
	{
		var envBuilder = new EnvironmentBuilder (Log);
		envBuilder.Read (Environments);

		if (Debug) {
			envBuilder.AddDefaultDebugBuildLogLevel ();
		}
		envBuilder.AddHttpClientHandlerType (HttpClientHandlerType);

		string abi = MonoAndroidHelper.RidToAbi (RID);
		AndroidTargetArch targetArch = MonoAndroidHelper.RidToArch (RID);

		// There can be only one, since we run in the inner build
		ITaskItem? outputFile = GenerateNativeAotLibraryLoadAssemblerSources.FindOutputFile (OutputSources, abi: abi, rid: RID);
		Log.LogDebugMessage ($"Environment variables file to generate: {outputFile.ItemSpec}");

		string environmentLlFilePath = outputFile.ItemSpec;
		var generator = new NativeAotEnvironmentNativeAssemblyGenerator (Log, envBuilder);
		LLVMIR.LlvmIrModule environmentModule = generator.Construct ();
		using var environmentWriter = MemoryStreamPool.Shared.CreateStreamWriter ();
		bool fileFullyWritten = false;
		try {
			generator.Generate (environmentModule, targetArch, environmentWriter, environmentLlFilePath!);
			environmentWriter.Flush ();
			Files.CopyIfStreamChanged (environmentWriter.BaseStream, environmentLlFilePath!);
			fileFullyWritten = true;
		} finally {
			// Log partial contents for debugging if generation failed
			if (!fileFullyWritten) {
				MonoAndroidHelper.LogTextStreamContents (Log, $"Partial contents of file '{environmentLlFilePath}'", environmentWriter.BaseStream);
			}
		}

		return !Log.HasLoggedErrors;
	}
}
