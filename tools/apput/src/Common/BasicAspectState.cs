namespace ApplicationUtility;

class BasicAspectState : IAspectState
{
	public bool Success { get; }

	public BasicAspectState (bool success)
	{
		Success = success;
	}
}
