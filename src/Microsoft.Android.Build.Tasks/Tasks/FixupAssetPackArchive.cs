#nullable enable
using System.IO;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;

namespace Microsoft.Android.Tasks;

public class FixupAssetPackArchive : AndroidTask
{
	public override string TaskPrefix => "FAPA";

	[Required]
	public string ArchiveFile { get; set; } = "";

	public override bool RunTask ()
	{
		using var archive = new ZipArchiveEx (ArchiveFile, FileMode.Open);
		archive.MoveEntry ("AndroidManifest.xml", "manifest/AndroidManifest.xml");
		archive.DeleteEntry ("resources.pb");
		archive.FixupWindowsPathSeparators (Log);
		return !Log.HasLoggedErrors;
	}
}
