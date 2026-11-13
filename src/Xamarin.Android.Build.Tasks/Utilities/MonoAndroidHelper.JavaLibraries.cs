#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;

namespace Xamarin.Android.Tasks;

// Java library helpers shared by tasks in Xamarin.Android.Build.Tasks and Microsoft.Android.Build.Tasks.
partial class MonoAndroidHelper
{
	class SizeAndContentFileComparer : IEqualityComparer<FileInfo>, IEqualityComparer<ITaskItem>
	{
		readonly TaskEnvironment taskEnvironment;

		public SizeAndContentFileComparer (TaskEnvironment? taskEnvironment = null)
		{
			this.taskEnvironment = taskEnvironment ?? TaskEnvironment.Fallback;
		}

		public bool Equals (FileInfo? x, FileInfo? y)
		{
			if (x is null || y is null)
				return ReferenceEquals (x, y);
			if (x.Exists != y.Exists || x.Length != y.Length)
				return false;
			string xHash = Files.HashFile (x.FullName);
			string yHash = Files.HashFile (y.FullName);
			return xHash == yHash;
		}

		public int GetHashCode (FileInfo obj)
		{
			return (int) obj.Length;
		}

		public bool Equals (ITaskItem? x, ITaskItem? y)
		{
			if (x is null || y is null)
				return ReferenceEquals (x, y);
			return Equals (new FileInfo (taskEnvironment.GetAbsolutePath (x.ItemSpec)), new FileInfo (taskEnvironment.GetAbsolutePath (y.ItemSpec)));
		}

		public int GetHashCode (ITaskItem obj)
		{
			return GetHashCode (new FileInfo (taskEnvironment.GetAbsolutePath (obj.ItemSpec)));
		}
	}

	/// <summary>
	/// Expands `dir/pattern` items. Results keep the item's directory spelling so that
	/// <see cref="IsEmbeddedReferenceJar"/> still recognizes relative `__reference__` paths.
	/// </summary>
	public static IEnumerable<string> ExpandFiles (ITaskItem []? libraryProjectJars, TaskEnvironment taskEnvironment)
	{
		foreach (var item in libraryProjectJars ?? []) {
			string? directory = Path.GetDirectoryName (item.ItemSpec);
			if (directory.IsNullOrEmpty ())
				continue;
			var absoluteDirectory = taskEnvironment.GetAbsolutePath (directory);
			if (!Directory.Exists (absoluteDirectory))
				continue;
			foreach (var file in Directory.GetFiles (absoluteDirectory, Path.GetFileName (item.ItemSpec)))
				yield return Path.Combine (directory, Path.GetFileName (file));
		}
	}

	public static IEnumerable<ITaskItem> DistinctFilesByContent (IEnumerable<ITaskItem> filePaths, TaskEnvironment taskEnvironment)
	{
		return filePaths.Distinct (new SizeAndContentFileComparer (taskEnvironment));
	}

	public static IEnumerable<string> DistinctFilesByContent (IEnumerable<string> filePaths, TaskEnvironment taskEnvironment)
	{
		return filePaths.Select (p => new FileInfo (taskEnvironment.GetAbsolutePath (p))).ToArray ()
			.Distinct (new SizeAndContentFileComparer ()).Select (f => f.FullName).ToArray ();
	}

	public static bool IsEmbeddedReferenceJar (string jar)
	{
		return jar.StartsWith ("__reference__", StringComparison.Ordinal);
	}
}
