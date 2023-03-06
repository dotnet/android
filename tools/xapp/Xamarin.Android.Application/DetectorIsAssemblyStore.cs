using System;
using System.IO;

namespace Xamarin.Android.Application;

class DetectorIsAssemblyStore : InputTypeDetector
{
	public override (bool accepted, InputReader? reader) Detect (string inputFilePath, InputTypeDetector? parent)
	{
		using var fs = File.OpenRead (inputFilePath);
		using var reader = new BinaryReader (fs);

		uint signature = reader.ReadUInt32 ();
		if (signature != DataProviderAssemblyStore.ASSEMBLY_STORE_MAGIC) {
			return (false, null);
		}

		return (true, new InputReaderAssemblyStore (inputFilePath));
	}
}
