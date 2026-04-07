namespace Microsoft.Android.Sdk.TrimmableTypeMap;

/// <summary>
/// Logging sink for typemap generation diagnostics.
/// </summary>
public interface ILogger
{
	/// <summary>
	/// Logs a low-importance diagnostic message.
	/// </summary>
	void LogMessage (string message);

	/// <summary>
	/// Logs a manifest-referenced Java type that could not be resolved to a scanned peer.
	/// </summary>
	void LogWarning (string typeName);
}

sealed class NullLogger : ILogger
{
	public static ILogger Instance { get; } = new NullLogger ();

	NullLogger ()
	{
	}

	public void LogMessage (string message)
	{
	}

	public void LogWarning (string typeName)
	{
	}
}
