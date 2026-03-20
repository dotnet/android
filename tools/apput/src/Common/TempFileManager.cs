using System;
using System.Collections.Generic;
using System.IO;

namespace ApplicationUtility;

/// <summary>
/// Manages temporary files created during processing, ensuring they are cleaned up on exit.
/// </summary>
class TempFileManager : IDisposable
{
	readonly List<string> filePaths = new ();
	static TempFileManager? instance = new ();
	static readonly object instanceLock = new ();

	bool disposed;

	/// <summary>
	/// Registers a temporary file so it will be deleted during <see cref="Cleanup"/>.
	/// </summary>
	/// <param name="path">Path to the temporary file.</param>
	public static void RegisterFile (string path)
	{
		lock (instanceLock) {
			instance?.filePaths.Add (path);
		}
	}

	/// <summary>
	/// Deletes all previously registered temporary files.
	/// </summary>
	public static void Cleanup ()
	{
		lock (instanceLock) {
			try {
				instance?.Dispose ();
			} catch (Exception) {
				// Ignore
			} finally {
				instance = null;
			}
		}
	}

	protected virtual void Dispose (bool disposing)
	{
		if (disposed) {
			return;
		}

		try {
			DeleteFiles (filePaths);
		} catch (Exception) {
			// Ignore
		} finally {
			try {
				filePaths.Clear ();
			} catch (Exception) {
				// Ignore
			}
		}

		disposed = true;

	}

	static void DeleteFiles (List<string> filePaths)
	{
		if (filePaths.Count == 0) {
			return;
		}

		foreach (string path in filePaths) {
			try {
				if (!File.Exists (path)) {
					continue;
				}

				File.Delete (path);
			} catch (Exception) {
				// Ignore
			}
		}
	}

	public void Dispose ()
	{
		Dispose (disposing: true);
		GC.SuppressFinalize (this);
	}
}
