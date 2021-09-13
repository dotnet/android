using System;
using System.IO;

namespace Xamarin.Android.Tasks
{
	class BlobAssemblyInfo
	{
		public string FilesystemAssemblyPath { get; }
		public string ArchiveAssemblyPath { get; }
		public string DebugInfoPath { get; private set; }
		public string ConfigPath { get; private set; }
		public string Abi { get; }

		public BlobAssemblyInfo (string filesystemAssemblyPath, string archiveAssemblyPath, string abi)
		{
			if (String.IsNullOrEmpty (filesystemAssemblyPath)) {
				throw new ArgumentException ("must not be null or empty", nameof (filesystemAssemblyPath));
			}

			if (String.IsNullOrEmpty (archiveAssemblyPath)) {
				throw new ArgumentException ("must not be null or empty", nameof (archiveAssemblyPath));
			}

			FilesystemAssemblyPath = filesystemAssemblyPath;
			ArchiveAssemblyPath = archiveAssemblyPath;
			Abi = abi;
		}

		public void SetDebugInfoPath (string path)
		{
			DebugInfoPath = GetExistingPath (path);
		}

		public void SetConfigPath (string path)
		{
			ConfigPath = GetExistingPath (path);
		}

		string GetExistingPath (string path)
		{
			if (String.IsNullOrEmpty (path) || !File.Exists (path)) {
				return String.Empty;
			}

			return path;
		}
	}
}
