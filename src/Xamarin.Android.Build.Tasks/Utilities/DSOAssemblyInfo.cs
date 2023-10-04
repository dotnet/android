using System;

namespace Xamarin.Android.Tasks;

class DSOAssemblyInfo
{
	/// <summary>
	/// Size of the loadable assembly data (after decompression, if compression is enabled).
	/// </summary>
	public uint DataSize              { get; }

	/// <summary>
	/// Size of the compressed assembly data or `0` if assembly is uncompressed.
	/// </summary>
	public uint CompressedDataSize    { get; }

	/// <summary>
	/// The file data comes from, either the original assembly or its compressed copy
	/// </summary>
	public string InputFile           { get; }

	/// <summary>
	/// Name of the assembly, including culture prefix if it's a satellite assembly. Must include the extension.
	/// </summary>
	public string Name                { get; }

	/// <summary>
	/// Indicates whether assembly data is stored in a standalone shared library.
	/// </summary>
	public bool IsStandalone          { get; }

	public string? StandaloneDSOName  { get; }

	/// <summary>
	/// <paramref name="name"/> is the original assembly name, including culture prefix (e.g. `en_US/`) if it is a
	/// satellite assembly.  <paramref name="inputFile"/> should be the full path to the input file.
	/// <paramref name="dataSize"/> gives the original file size, while <paramref name="compressedDataSize"/> specifies
	/// data size after compression, or `0` if file isn't compressed.
	/// </summary>
	public DSOAssemblyInfo (string name, string inputFile, uint dataSize, uint compressedDataSize, bool isStandalone = false, string? dsoName = null)
	{
		if (isStandalone && String.IsNullOrEmpty (dsoName)) {
			throw new ArgumentException ("must not be null or empty for standalone assembly", nameof (dsoName));
		}

		Name = name;
		InputFile = inputFile;
		DataSize = dataSize;
		CompressedDataSize = compressedDataSize;
		IsStandalone = isStandalone;
		StandaloneDSOName = isStandalone ? dsoName : null;
	}
}
