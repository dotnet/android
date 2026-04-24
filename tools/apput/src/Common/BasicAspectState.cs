namespace ApplicationUtility;

/// <summary>
/// Simple <see cref="IAspectState"/> implementation that carries only a success/failure flag.
/// </summary>
class BasicAspectState : IAspectState
{
	public bool Success { get; }

	public BasicAspectState (bool success)
	{
		Success = success;
	}
}
