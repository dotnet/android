using System;
using System.IO;

namespace ApplicationUtility;

class Utilities
{
	public static void DeleteFile (string path, bool quiet = true)
	{
		try {
			File.Delete (path);
		} catch (Exception ex) {
			Log.Debug ($"Failed to delete file '{path}'.", ex);
			if (!quiet) {
				throw;
			}
		}
	}

	public static void CloseAndDeleteFile (FileStream stream, bool quiet = true)
	{
		string path = stream.Name;
		try {
			stream.Close ();
		} catch (Exception ex) {
			Log.Debug ($"Failed to close file stream.", ex);
			if (!quiet) {
				throw;
			}
		}

		DeleteFile (path);
	}
}
