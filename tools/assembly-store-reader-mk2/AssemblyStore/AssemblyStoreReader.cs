using System.Collections.Generic;
using System.IO;
using System.Text;

using Xamarin.Android.Tools;

namespace Xamarin.Android.AssemblyStore;

abstract class AssemblyStoreReader
{
	static readonly UTF8Encoding ReaderEncoding = new UTF8Encoding (false);

	protected Stream StoreStream                { get; }
	public abstract string Description          { get; }
	public string StorePath                     { get; }

	public AndroidTargetArch TargetArch         { get; protected set; } = AndroidTargetArch.Arm;
	public uint AssemblyCount                   { get; protected set; }
	public uint IndexEntryCount                 { get; protected set; }
	public IList<AssemblyStoreItem>? Assemblies { get; protected set; }
	public bool Is64Bit                         { get; protected set; }

	protected AssemblyStoreReader (Stream store, string path)
	{
		StoreStream = store;
		StorePath = path;
	}

	public static AssemblyStoreReader? Create (Stream store, string path)
	{
		AssemblyStoreReader? reader = MakeReaderReady (new StoreReader_V1 (store, path));
		if (reader != null) {
			return reader;
		}

		reader = MakeReaderReady (new StoreReader_V2 (store, path));
		if (reader != null) {
			return reader;
		}

		return null;
	}

	static AssemblyStoreReader? MakeReaderReady (AssemblyStoreReader reader)
	{
		if (!reader.IsSupported ()) {
			return null;
		}

		reader.Prepare ();
		return reader;
	}

	protected BinaryReader CreateReader () => new BinaryReader (StoreStream, ReaderEncoding, leaveOpen: true);

	protected abstract bool IsSupported ();
	protected abstract void Prepare ();
}
