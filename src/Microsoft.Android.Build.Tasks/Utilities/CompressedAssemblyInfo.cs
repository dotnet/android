#nullable enable
using System;
using System.IO;
using Microsoft.Build.Framework;

using Xamarin.Android.Tasks;

namespace Microsoft.Android.Tasks
{
	class CompressedAssemblyInfo
	{
		const string CompressedAssembliesInfoKey = "__CompressedAssembliesInfo";

		public uint DescriptorIndex { get; }

		public CompressedAssemblyInfo (uint descriptorIndex)
		{
			DescriptorIndex = descriptorIndex;
		}

		public static string GetKey (string projectFullPath)
		{
			ArgumentNullException.ThrowIfNull (projectFullPath);
			if (projectFullPath.IsNullOrEmpty ())
				throw new ArgumentException ("must be a non-empty string", nameof (projectFullPath));

			return $"{CompressedAssembliesInfoKey}:{projectFullPath}";
		}

		public static string GetDictionaryKey (ITaskItem assembly)
		{
			ArgumentNullException.ThrowIfNull (assembly);
			// Prefer %(DestinationSubPath) if set
			var path = assembly.GetMetadata ("DestinationSubPath");
			if (!path.IsNullOrEmpty ()) {
				return path;
			}
			// MSBuild sometimes only sets %(DestinationSubDirectory)
			var subDirectory = assembly.GetMetadata ("DestinationSubDirectory");
			return Path.Combine (subDirectory, Path.GetFileName (assembly.ItemSpec));
		}
	}
}
