using System;
using System.IO;
using Microsoft.Build.Framework;

namespace Xamarin.Android.Tasks
{
	class CompressedAssemblyInfo
	{
		const string CompressedAssembliesInfoKey = "__CompressedAssembliesInfo";

		public uint FileSize { get; }
		public uint DescriptorIndex { get; set; }

		public CompressedAssemblyInfo (uint fileSize)
		{
			FileSize = fileSize;
			DescriptorIndex = 0;
		}

		public static string GetKey (string projectFullPath)
		{
			if (String.IsNullOrEmpty (projectFullPath))
				throw new ArgumentException ("must be a non-empty string", nameof (projectFullPath));

			return $"{CompressedAssembliesInfoKey}:{projectFullPath}";
		}

		public static string GetDictionaryKey (ITaskItem assembly)
		{
			// Prefer %(DestinationSubPath) if set
			var path = assembly.GetMetadata ("DestinationSubPath");
			if (!string.IsNullOrEmpty (path)) {
				return path;
			}
			// MSBuild sometimes only sets %(DestinationSubDirectory)
			var subDirectory = assembly.GetMetadata ("DestinationSubDirectory");
			return Path.Combine (subDirectory, Path.GetFileName (assembly.ItemSpec));
		}
	}
}
