using System;
using System.IO;
using Microsoft.Build.Framework;

using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	class CompressedAssemblyInfo
	{
		const string CompressedAssembliesInfoKey = "__CompressedAssembliesInfo";

		public uint FileSize { get; }
		public uint DescriptorIndex { get; }
		public AndroidTargetArch TargetArch { get; }
		public string AssemblyName { get; }

		public CompressedAssemblyInfo (uint fileSize, uint descriptorIndex, AndroidTargetArch targetArch, string assemblyName)
		{
			FileSize = fileSize;
			DescriptorIndex = descriptorIndex;
			TargetArch = targetArch;
			AssemblyName = assemblyName;
		}

		public static string GetKey (string projectFullPath)
		{
			if (String.IsNullOrEmpty (projectFullPath))
				throw new ArgumentException ("must be a non-empty string", nameof (projectFullPath));

			return $"{CompressedAssembliesInfoKey}:{projectFullPath}";
		}

		public static string GetDictionaryKey (ITaskItem assembly)
		{
			string abi = MonoAndroidHelper.GetAssemblyAbi (assembly);
			// Prefer %(DestinationSubPath) if set
			var path = assembly.GetMetadata ("DestinationSubPath");
			if (!string.IsNullOrEmpty (path)) {
				return Path.Combine (abi, path);
			}
			// MSBuild sometimes only sets %(DestinationSubDirectory)
			var subDirectory = assembly.GetMetadata ("DestinationSubDirectory");
			return Path.Combine (abi, subDirectory, Path.GetFileName (assembly.ItemSpec));
		}
	}
}
