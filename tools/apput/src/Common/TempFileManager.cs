namespace ApplicationUtility;

/// <summary>
/// Manages temporary files created during processing, ensuring they are cleaned up on exit.
/// </summary>
class TempFileManager
{
	/// <summary>
	/// Registers a temporary file so it will be deleted during <see cref="Cleanup"/>.
	/// </summary>
	/// <param name="path">Path to the temporary file.</param>
	public static void RegisterFile (string path)
	{
		// TODO: implement
	}

	/// <summary>
	/// Deletes all previously registered temporary files.
	/// </summary>
	public static void Cleanup ()
	{
		// TODO: implement
	}
}
