namespace ApplicationUtility;

/// <summary>
/// An empty interface which can be used by the aspect detection mechanism to
/// preserve some state between <see cref="IAspect.ProbeAspect"/> and
/// <see cref="IAspect.LoadAspect"/> calls, to optimize resource usage.
/// </summary>
public interface IAspectState
{
	/// <summary>
	/// Indicates that whatever method returned instance of this interface, the operation was
	/// successful if `true`.
	/// </summary>
	bool Success { get; }
}
