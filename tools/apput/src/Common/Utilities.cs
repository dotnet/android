using System;
using System.IO;
using System.Text;

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

	// TODO: review all the call sites, now that `rewindStream` default is different
	public static BinaryReader GetReaderAndRewindStream (Stream stream, bool rewindStream = true)
	{
		if (rewindStream) {
			stream.Seek (0, SeekOrigin.Begin);
		}

		return new BinaryReader (stream, Encoding.UTF8, leaveOpen: true);
	}

	public static BasicAspectState GetFailureAspectState (string message)
	{
		Log.Debug (message);
		return new BasicAspectState (false);
	}

	public static string ToStringOrNull<T> (T? reference) => reference == null ? "<NULL>" : reference.ToString () ?? "[unknown]";

	public static string GetZipEntryFileName (string entryName)
	{
		int idx = entryName.LastIndexOf ('/');
		if (idx <= 0) {
			return entryName;
		}

		if (idx == entryName.Length - 1) {
			return String.Empty;
		}

		return entryName.Substring (idx + 1);
	}

	public static string SizeToString (ulong val)
	{
		// TODO: return both bytes and a "human readable" value (kb, mb etc)
		// TODO: format the byte size according to the current culture
		return val.ToString ();
	}
}
