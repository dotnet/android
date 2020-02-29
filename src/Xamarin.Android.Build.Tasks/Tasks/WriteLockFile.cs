using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// Generates a warning if LockFile exists, registers the file to be deleted at end of build
	/// </summary>
	public class WriteLockFile : AndroidTask
	{
		public override string TaskPrefix => "WLF";

		[Required]
		public string LockFile { get; set; }

		public override bool RunTask ()
		{
			try {
				var path = Path.GetFullPath (LockFile);
				var key = new Tuple<string, string> (nameof (WriteLockFile), path); // Use the full path as part of the key

				// Check if already registered, for sanity
				var existing = BuildEngine4.GetRegisteredTaskObject (key, RegisteredTaskObjectLifetime.Build) as DeleteFileAfterBuild;
				if (existing == null) {
					if (File.Exists (path)) {
						Log.LogCodedWarning ("XA5302", Properties.Resources.XA5302, path);
					} else {
						Directory.CreateDirectory (Path.GetDirectoryName (path));
						File.WriteAllText (path, "");
					}

					BuildEngine4.RegisterTaskObject (key, new DeleteFileAfterBuild (path), RegisteredTaskObjectLifetime.Build, allowEarlyCollection: false);
				} else {
					Log.LogDebugMessage ("Lock file was created earlier in the build.");
				}
			} catch (Exception ex) {
				Log.LogDebugMessage ($"Exception in {nameof (WriteLockFile)}: {ex}");
			}

			// We want to always continue
			return true;
		}

		/// <summary>
		/// When using RegisterTaskObject and RegisteredTaskObjectLifetime.Build, MSBuild calls Dispose() at the end of the Build -- regardless of cancellation or failure
		/// </summary>
		class DeleteFileAfterBuild : IDisposable
		{
			readonly string path;

			public DeleteFileAfterBuild (string path) => this.path = path;

			public void Dispose ()
			{
				try {
					File.Delete (path);
				} catch {
					// We don't want anything to throw here, but it is too late to log the error
				}
			}
		}
	}
}
