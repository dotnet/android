using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tools.BootstrapTasks
{
	public class Which : Task
	{
		[Required]
		public  ITaskItem           Program             { get; set; }

		public  ITaskItem[]         Directories         { get; set; }

		public  bool                Required            { get; set; }

		[Output]
		public  ITaskItem           Location            { get; set; }

		static  readonly    string[]    FileExtensions = new []{
			null,
			".bat",
			".cmd",
			".com",
			".exe",
		};

		public override bool Execute ()
		{
			string[]    paths   = Directories?.Select (d => d.ItemSpec).ToArray ();
			if (paths == null || paths.Length == 0) {
				paths    = Environment.GetEnvironmentVariable ("PATH").Split (Path.PathSeparator);
			}

			Log.LogMessage (MessageImportance.Low, $"Task {nameof (Which)}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (Program)}: {Program}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (Directories)}:");
			foreach (var p in paths) {
				Log.LogMessage (MessageImportance.Low, $"    {p}");
			}
			Log.LogMessage (MessageImportance.Low, $"  {nameof (Required)}: {Required}");

			foreach (var path in paths) {
				var p   = Path.Combine (path, Program.ItemSpec);
				foreach (var ext in FileExtensions) {
					var e   = Path.ChangeExtension (p, ext);
					if (File.Exists (e)) {
						Location = new TaskItem (e);
						break;
					}
				}
				if (Location != null)
					break;
			}

			if (Location == null && Required) {
				Log.LogError ("Could not find required program '{0}'.", Program.ItemSpec);
			}

			Log.LogMessage (MessageImportance.Low, $"  [Output] {nameof (Location)}: {Location?.ItemSpec}");

			return !Log.HasLoggedErrors;
		}
	}
}

