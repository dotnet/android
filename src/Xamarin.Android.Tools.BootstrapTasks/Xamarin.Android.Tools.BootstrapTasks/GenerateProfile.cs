using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tools.BootstrapTasks
{
	public class GenerateProfile : Task
	{
		[Required]
		public ITaskItem[] Files { get; set; }

		[Required]
		public ITaskItem OutputFile { get; set; }

		public override bool Execute ()
		{
			Log.LogMessage (MessageImportance.Low, "Task GenerateProfile");
			Log.LogMessage (MessageImportance.Low, "\tOutputFile :  {0}", OutputFile);
			Log.LogMessage (MessageImportance.Low, "\tFiles : ");
			foreach (var file in Files) {
				Log.LogMessage (MessageImportance.Low, "\t\t{0}", file.ItemSpec);
			}

			var sb = new StringBuilder ();
			sb.AppendLine ("using System.Collections.Generic;");
			sb.AppendLine ();
			sb.AppendLine ("namespace Xamarin.Android.Tasks {");
			sb.AppendLine ("\tpublic partial class Profile {");
			sb.AppendLine ("\t\t// KEEP THIS SORTED ALPHABETICALLY, CASE-INSENSITIVE");
			sb.AppendLine ("\t\tpublic static readonly string [] SharedRuntimeAssemblies = new []{");
			foreach (var file in Files.Select(x => Path.GetFileName (x.ItemSpec)).Distinct().OrderBy(x => x)) {
				sb.AppendFormat ("\t\t\t\"{0}\"," + Environment.NewLine, file);
			}
			sb.AppendLine ("\t\t};");
			sb.AppendLine ("\t}");
			sb.AppendLine ("}");

			var newContents = sb.ToString ();
			var curContents = "";
			if (File.Exists (OutputFile.ItemSpec)) {
				curContents = File.ReadAllText (OutputFile.ItemSpec);
			}
			if (newContents != curContents) {
				File.WriteAllText (OutputFile.ItemSpec, sb.ToString ());
			}

			return !Log.HasLoggedErrors;
		}
	}
}

