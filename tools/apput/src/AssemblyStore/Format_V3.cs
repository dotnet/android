using System.IO;

namespace ApplicationUtility;

class Format_V3 : FormatBase
{
	public Format_V3 (Stream storeStream, string? description)
		: base (storeStream, description)
	{}

	public override IAspectState Validate ()
	{
		// TODO: validate that the store has correct format and populate the state below accordingly
		//       to save time later.
		return new AssemblyStoreAspectState (true);
	}
}
