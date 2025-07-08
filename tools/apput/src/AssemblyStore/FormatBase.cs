using System.IO;

namespace ApplicationUtility;

abstract class FormatBase
{
	protected Stream StoreStream { get; }
	protected string? Description { get; }

	protected FormatBase (Stream storeStream, string? description)
	{
		this.StoreStream = storeStream;
		this.Description = description;
	}

	public abstract IAspectState Validate ();
}
