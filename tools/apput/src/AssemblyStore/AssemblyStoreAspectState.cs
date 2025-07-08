using System.Collections.Generic;

namespace ApplicationUtility;

class AssemblyStoreAspectState : BasicAspectState
{
	public AssemblyStoreHeader Header { get; }
	public AssemblyStoreIndex Index { get; }

	public AssemblyStoreAspectState (bool success)
		: base (success)
	{}
}
