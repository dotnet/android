#nullable enable
using System.IO;
using System.IO.Compression;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;

namespace Microsoft.Android.Tasks;

public class CreateJavaArchive : AndroidTask
{
	public override string TaskPrefix => "CJA";

	[Required]
	public string SourceDirectory { get; set; } = "";

	[Required]
	public string OutputFile { get; set; } = "";

	public override bool RunTask ()
	{
		using var archive = new ZipArchiveEx (OutputFile, FileMode.OpenOrCreate);
		archive.AddDirectory (SourceDirectory, "", CompressionLevel.NoCompression);
		return !Log.HasLoggedErrors;
	}
}
