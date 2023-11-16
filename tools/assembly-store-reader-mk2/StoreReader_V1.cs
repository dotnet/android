using System;
using System.IO;

namespace Xamarin.Android.AssemblyStore;

class StoreReader_V1 : AssemblyStoreReader
{
	public override string Description => "Assembly store v1";

	public StoreReader_V1 (Stream store, string path)
		: base (store, path)
	{}

	protected override bool IsSupported ()
	{
		return false;
	}

	protected override void Prepare ()
	{
	}
}
