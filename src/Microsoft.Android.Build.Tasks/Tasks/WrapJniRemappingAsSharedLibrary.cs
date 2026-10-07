#nullable enable

using System;
using System.IO;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Xamarin.Android.Tasks;
using Properties = Xamarin.Android.Tasks.Properties;

namespace Microsoft.Android.Tasks;

public class WrapJniRemappingAsSharedLibrary : AndroidTask
{
	public override string TaskPrefix => "WJRSL";

	internal const string LibraryName = "libandroid_runtime_blobs.so";
	internal const string PayloadSymbol = "xajr_payload";

	[Required]
	public string InputFile { get; set; } = "";

	[Required]
	public string OutputFile { get; set; } = "";

	[Required]
	public string RuntimeIdentifier { get; set; } = "";

	public override bool RunTask ()
	{
		string temporary = OutputFile + "." + Guid.NewGuid ().ToString ("N") + ".tmp";
		try {
			string? directory = Path.GetDirectoryName (OutputFile);
			if (!directory.IsNullOrEmpty ())
				Directory.CreateDirectory (directory);
			WriteOutputFile (temporary);
			File.Move (temporary, OutputFile, overwrite: true);
		} catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is NotSupportedException) {
			Log.LogCodedError ("XA4331", Properties.Resources.XA4331, InputFile, OutputFile, ex.Message);
		} finally {
			File.Delete (temporary);
		}
		return !Log.HasLoggedErrors;
	}

	protected virtual void WriteOutputFile (string outputFile)
	{
		using var input = File.OpenRead (InputFile);
		using var output = File.Create (outputFile);
		AssemblyStoreElfWriter.Write (input, output, MonoAndroidHelper.RidToArch (RuntimeIdentifier),
			LibraryName, PayloadSymbol);
	}
}
