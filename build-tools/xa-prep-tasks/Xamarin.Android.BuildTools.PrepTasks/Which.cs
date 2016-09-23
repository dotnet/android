using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.BuildTools.PrepTasks
{
	public class Which : Task
	{
		[Required]
		public  ITaskItem           Program             { get; set; }

		public  ITaskItem[]         Directories         { get; set; }

		public  bool                Required            { get; set; }

		[Output]
		public  ITaskItem           Location            { get; set; }

		static  readonly    string[]    FileExtensions;

		static Which ()
		{
			var pathExt     = Environment.GetEnvironmentVariable ("PATHEXT");
			var pathExts    = pathExt?.Split (new char [] { Path.PathSeparator }, StringSplitOptions.RemoveEmptyEntries);
			FileExtensions  = new string [(pathExts?.Length ?? 0) + 1];
			FileExtensions [0] = null;
			if (pathExts != null) {
				Array.Copy (pathExts, 0, FileExtensions, 1, pathExt.Length);
			}
		}

		public static string GetProgramLocation (string programBasename, out string filename, string[] directories = null)
		{
			directories = directories ?? GetPathDirectories ();
			foreach (var d in directories) {
				var p   = GetProgramLocation (programBasename, d, out filename);
				if (p != null)
					return p;
			}
			filename    = programBasename;
			return null;
		}

		static string GetProgramLocation (string programBasename, string directory, out string filename)
		{
			foreach (var ext in FileExtensions) {
				filename    = Path.ChangeExtension (programBasename, ext);
				var p       = Path.Combine (directory, filename);
				if (File.Exists (p)) {
					return p;
				}
			}
			filename    = programBasename;
			return null;
		}

		static string[] GetPathDirectories ()
		{
			return Environment.GetEnvironmentVariable ("PATH")
				.Split (Path.PathSeparator);
		}

		public override bool Execute ()
		{
			string[]    paths   = Directories?.Select (d => d.ItemSpec).ToArray ();
			if (paths == null || paths.Length == 0) {
				paths   = GetPathDirectories ();
			}

			Log.LogMessage (MessageImportance.Low, $"Task {nameof (Which)}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (Program)}: {Program}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (Directories)}:");
			foreach (var p in paths) {
				Log.LogMessage (MessageImportance.Low, $"    {p}");
			}
			Log.LogMessage (MessageImportance.Low, $"  {nameof (Required)}: {Required}");

			string _;
			var e = GetProgramLocation (Program.ItemSpec, out _, paths);
			if (e != null) {
				Location = new TaskItem (e);
			}

			if (Location == null && Required) {
				Log.LogError ("Could not find required program '{0}'.", Program.ItemSpec);
			}

			Log.LogMessage (MessageImportance.Low, $"  [Output] {nameof (Location)}: {Location?.ItemSpec}");

			return !Log.HasLoggedErrors;
		}
	}
}

