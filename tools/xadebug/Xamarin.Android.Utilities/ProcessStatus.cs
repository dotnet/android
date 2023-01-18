namespace Xamarin.Android.Utilities;

class ProcessStatus
{
	public int ExitCode   { get; } = -1;
	public bool Exited    { get; } = false;
	public bool Success   { get; } = false;

	public ProcessStatus ()
	{}

	public ProcessStatus (int exitCode, bool exited, bool success)
	{
		ExitCode = exitCode;
		Exited = exited;
		Success = success;
	}
}
