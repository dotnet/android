namespace ApplicationUtility;

/// <summary>
/// Carries the validated <see cref="FormatBase"/> instance from <see cref="AssemblyStore.ProbeAspect"/>
/// to <see cref="AssemblyStore.LoadAspect"/>.
/// </summary>
class AssemblyStoreAspectState : BasicAspectState
{
	public FormatBase Format { get; }

	public AssemblyStoreAspectState (FormatBase format)
		: base (success: true)
	{
		Format = format;
	}
}
