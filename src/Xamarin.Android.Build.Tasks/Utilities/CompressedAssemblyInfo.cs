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
			var key = Path.GetFileName (assembly.ItemSpec);
			var abiDirectory = assembly.GetMetadata ("AbiDirectory");
			if (!string.IsNullOrEmpty (abiDirectory)) {
				key = abiDirectory + "/" + key;
			}
			return key;
		}
	}
}
