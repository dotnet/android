using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;

namespace Xamarin.Android.Tasks;

public partial class MonoAndroidHelper
{
	static readonly char [] DirectorySeparators = new [] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };

	public static ZipArchive ReadZipFile (string filename)
	{
		try {
			return Files.ReadZipFile (filename);
		} catch (InvalidDataException ex) {
			throw new InvalidDataException ($"There was an error opening {filename}. The file is probably corrupt. Try deleting it and building again. {ex.Message}", ex);
		}
	}

	/// <summary>
	/// Returns the relative path that should be used for an @(AndroidAsset) item
	/// </summary>
	public static string GetRelativePathForAndroidAsset (string assetsDirectory, ITaskItem androidAsset)
	{
		var path = androidAsset.GetMetadata ("Link");
		path = !string.IsNullOrWhiteSpace (path) ? path : androidAsset.ItemSpec;
		var head = string.Join ("\\", path.Split (DirectorySeparators).TakeWhile (s => !s.Equals (assetsDirectory, StringComparison.OrdinalIgnoreCase)));
		path = head.Length == path.Length ? path : path.Substring ((head.Length == 0 ? 0 : head.Length + 1) + assetsDirectory.Length).TrimStart (DirectorySeparators);
		return path;
	}
}
