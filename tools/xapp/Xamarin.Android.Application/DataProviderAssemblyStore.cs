using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Xamarin.Android.Application;

class DataProviderAssemblyStore : DataProvider
{
	[StructLayout (LayoutKind.Sequential, Pack = 1)]
	struct AssemblyStoreHeader
	{
		public uint magic;
		public uint version;
		public uint local_entry_count;
		public uint global_entry_count;
		public uint store_id;
	}

	public const uint ASSEMBLY_STORE_MAGIC = 0x41424158; // 'XABA', little-endian

	static readonly int headerSize = Marshal.SizeOf<AssemblyStoreHeader> ();

	Stream inputStream;
	string? inputPath;
	AssemblyStoreHeader header;

	public uint Version                 { get; }
	public uint LocalEntryCount         { get; }
	public uint GlobalEntryCount        { get; }
	public uint ID                      { get; }
	public bool IsArchSpecific          { get; }
	public string DetectedArchitecture  { get; }

	public DataProviderAssemblyStore (Stream inputStream, string? inputPath)
		: base (inputStream, inputPath)
	{
		this.inputStream = inputStream;
		this.inputPath = inputPath;

		using var reader = new BinaryReader (inputStream);
		var bytes = new byte[headerSize];
		int nread = reader.Read (bytes);
		if (nread != headerSize) {
			throw new InvalidOperationException ("Failed to read assembly store header");
		}

		GCHandle handle = GCHandle.Alloc (bytes, GCHandleType.Pinned);
		header = Marshal.PtrToStructure<AssemblyStoreHeader> (handle.AddrOfPinnedObject ());

		Version = header.version;
		LocalEntryCount = header.local_entry_count;
		GlobalEntryCount = header.global_entry_count;
		ID = header.store_id;

		IsArchSpecific = ID > 0;
		DetectedArchitecture = DetectArchitecture (inputPath);
	}

	public bool ExtractAssembly (string assemblyNameRegex, string outputDirectory, bool decompress)
	{
		inputStream.Seek (0, SeekOrigin.Begin);

		return false;
	}

	string DetectArchitecture (string? inputPath)
	{
		if (!IsArchSpecific || String.IsNullOrEmpty (inputPath)) {
			return String.Empty;
		}

		string? fileName = Path.GetFileName (inputPath);
		if (String.IsNullOrEmpty (fileName)) {
			return String.Empty;
		}

		// Detect arch from the name: assemblies.ARCH.blob
		string[] parts = fileName.Split ('.');
		if (parts.Length != 3) {
			return String.Empty;
		}

		if (String.Compare ("assemblies", parts[0], StringComparison.Ordinal) != 0 ||
		    String.Compare ("blob", parts[2], StringComparison.Ordinal) != 0) {
			return String.Empty;
		}

		return parts[1];
	}
}
