#nullable enable
using System.IO;
using System.IO.Compression;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;

namespace Microsoft.Android.Tasks;

[MSBuildMultiThreadableTask]
public class CreateJavaArchive : MultithreadedAndroidTask
{
	public override string TaskPrefix => "CJA";

	[Required]
	public string SourceDirectory { get; set; } = "";

	[Required]
	public string OutputFile { get; set; } = "";

	public override bool RunTask ()
	{
		using var archive = new ZipArchiveEx (TaskEnvironment.GetAbsolutePath (OutputFile), FileMode.OpenOrCreate, TaskEnvironment);
		archive.AddDirectory (TaskEnvironment.GetAbsolutePath (SourceDirectory), "", CompressionLevel.NoCompression);
		return !Log.HasLoggedErrors;
	}
}
