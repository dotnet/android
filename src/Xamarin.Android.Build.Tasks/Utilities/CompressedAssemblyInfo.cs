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

	class DSOAssemblyInfo
	{
		/// <summary>
		/// Size of the loadable assembly data (after decompression, if compression is enabled).
		/// </summary>
		public ulong DataSize             { get; }

		/// <summary>
		/// Size of the compressed assembly data or `0` if assembly is uncompressed.
		/// </summary>
		public ulong CompressedDataSize   { get; }

		public string InputFile           { get; }

		/// <summary>
		/// Name of the assembly, including culture prefix if it's a satellite assembly. Must include the
		/// extension.
		/// </summary>
		public string Name                { get; }

		/// <summary>
		/// <paramref name="name"/> is the original assembly name, including culture prefix (e.g. `en_US/`) if it is a
		/// satellite assembly.  <paramref name="inputFile"/> should be the full path to the input file.
		/// <paramref name="dataSize"/> gives the original file size, while <paramref name="compressedDataSize"/> specifies
		/// data size after compression, or `0` if file isn't compressed.
		/// </summary>
		public DSOAssemblyInfo (string name, string inputFile, ulong dataSize, ulong compressedDataSize)
		{
			Name = name;
			InputFile = inputFile;
			DataSize = dataSize;
			CompressedDataSize = compressedDataSize;
		}
	}
}
