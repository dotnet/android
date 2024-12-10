#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xamarin.Tools.Zip;

namespace Xamarin.Android.Tasks;

// This temporary class has a nonsensical API to allow it to be a drop-in replacement
// for ZipArchiveEx. This allows us to refactor with smaller diffs that can be
// reviewed easier. This class should not exist in this form in the final state.
public class ZipArchiveFileListBuilder : IDisposable
{
	public List<ITaskItem> ApkFiles { get; } = [];

	public ZipArchiveFileListBuilder (string archive, FileMode filemode)
	{
	}

	public void Dispose ()
	{
		// No-op
	}

	public void Flush ()
	{
		// No-op
	}

	public void AddFileAndFlush (string filename, string archiveFileName, CompressionMethod compressionMethod)
	{
		var item = new TaskItem (filename);

		item.SetMetadata ("ArchivePath", archiveFileName);
		item.SetMetadata ("Compression", compressionMethod.ToString ());

		ApkFiles.Add (item);
	}

	public void AddJavaEntryAndFlush (string javaFilename, string javaEntryName, string archiveFileName)
	{
		// An item's ItemSpec must be unique so use both the jar file name and the entry name
		var item = new TaskItem ($"{javaFilename}#{javaEntryName}");
		item.SetMetadata ("ArchivePath", archiveFileName);
		item.SetMetadata ("JavaArchiveEntry", javaEntryName);

		ApkFiles.Add (item);
	}

	public void FixupWindowsPathSeparators (Action<string, string> onRename)
	{
		// No-op
	}

	public bool SkipExistingFile (string file, string fileInArchive, CompressionMethod compressionMethod)
	{
		return false;
	}

	public bool SkipExistingEntry (ZipEntry sourceEntry, string fileInArchive)
	{
		return false;
	}
}
