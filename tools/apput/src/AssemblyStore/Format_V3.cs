using System.IO;

namespace ApplicationUtility;

class Format_V3 : FormatBase
{
	public const uint HeaderSize = 5 * sizeof(uint);
	public const uint IndexEntrySize32 = sizeof(uint) + sizeof(uint) + sizeof(byte);
	public const uint IndexEntrySize64 = sizeof(ulong) + sizeof(uint) + sizeof(byte);
	public const uint AssemblyDescriptorSize = 7 * sizeof(uint);

	public Format_V3 (Stream storeStream, string? description)
		: base (storeStream, description)
	{}

	protected override IAspectState ValidateInner ()
	{
		Log.Debug ("AssemblyStore/Format_V3: validating store format.");
		if (Header == null || Header.EntryCount == null || Header.IndexEntryCount == null || Header.IndexSize == null) {
			return ValidationFailed ($"AssemblyStore/Format_V3: invalid header data.");
		}

		if (Descriptors == null || Descriptors.Count == 0) {
			return ValidationFailed ($"AssemblyStore/Format_V3: no descriptors read.");
		}

		// TODO: validate stream size
		// TODO: populate
		return new AssemblyStoreAspectState (true);

		BasicAspectState ValidationFailed (string message)
		{
			Log.Debug (message);
			return new BasicAspectState (false);
		}
	}
}
