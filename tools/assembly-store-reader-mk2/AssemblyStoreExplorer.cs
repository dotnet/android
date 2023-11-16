using System;
using System.Collections.Generic;
using System.IO;

using Xamarin.Android.Tools;

namespace Xamarin.Android.AssemblyStore;

class AssemblyStoreExplorer
{
	readonly AssemblyStoreReader reader;

	public string StorePath              { get; }
	public AndroidTargetArch? TargetArch        => reader.TargetArch;
	public uint AssemblyCount                   => reader.AssemblyCount;
	public uint IndexEntryCount                 => reader.IndexEntryCount;
	public IList<AssemblyStoreItem>? Assemblies => reader.Assemblies;
	public bool Is64Bit                         => reader.Is64Bit;

	public AssemblyStoreExplorer (FileInfo storeInfo)
	{
		Stream storeStream = storeInfo.OpenRead ();
		var storeReader = AssemblyStoreReader.Create (storeStream, storeInfo.FullName);
		if (storeReader == null) {
			storeStream.Dispose ();
			throw new NotSupportedException ($"Format of assembly store '{storeInfo.FullName}' is unsupported");
		}

		reader = storeReader;
		StorePath = storeInfo.FullName;
	}
}
