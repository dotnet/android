using System.Collections.Generic;

namespace ApplicationUtility;

class AssemblyStoreAspectState : BasicAspectState
{
	public FormatBase Format { get; }

	public AssemblyStoreAspectState (FormatBase format)
		: base (success: true)
	{
		Format = format;
	}
}
