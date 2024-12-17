#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks;

class PackageFileListBuilder
{
	public List<ITaskItem> Items { get; } = [];

	public void AddItem (string filepath, string archivePath, string? javaEntryName = null)
	{
		var item = new TaskItem (filepath);
		item.SetMetadata ("ArchivePath", archivePath);

		if (javaEntryName is not null)
			item.SetMetadata ("JavaArchiveEntry", javaEntryName);

		Items.Add (item);
	}

	public ITaskItem [] ToArray () => Items.ToArray ();
}
