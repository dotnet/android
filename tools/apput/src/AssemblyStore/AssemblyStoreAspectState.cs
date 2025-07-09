using System.Collections.Generic;

namespace ApplicationUtility;

class AssemblyStoreAspectState : BasicAspectState
{
	public AssemblyStoreHeader Header { get; }
	public FormatBase Format { get; }

	public AssemblyStoreAspectState (bool success)
		: base (success)
	{}
}
