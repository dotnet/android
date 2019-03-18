
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.BuildTools.PrepTasks
{
	public class ParseExternalGitDependencies : Task
	{
		[Required]
		public string ExternalFilePath { get; set; }
		
		/* %(ExternalGitDependencies.Owner)     - Repo owner
		 * %(ExternalGitDependencies.Name)      - Repo name
		 * %(ExternalGitDependencies.Branch)    - Branch name
		 * %(ExternalGitDependencies.Identity)  - Commit hash
		 */
		[Output]
		public ITaskItem[] ExternalGitDependencies { get; set; }

		static readonly Regex externalRegex = new Regex (
			@"^(?<comment>\#\s*)?(?<owner>.*)\/(?<repo>.*):(?<branch>.*)@(?<commit>.*)$",
			RegexOptions.Compiled);

		public override bool Execute ()
		{
			if (!File.Exists (ExternalFilePath)) {
				Log.LogError($"Unable to find dependency file at: {ExternalFilePath}");
				return false;
			}

			string[] unparsedExternals = File.ReadAllLines (ExternalFilePath);
			var externals = new List<TaskItem> (unparsedExternals.Length);

			foreach (string external in unparsedExternals) {
				Match match = externalRegex.Match (external);
				if (match != null && match.Success) {
					if (match.Groups["comment"].Success) {
						// Ignore matching lines which start with '#'.
						continue;
					}
					var e = new TaskItem (match.Groups["commit"].Value);
					e.SetMetadata ("Owner", match.Groups["owner"].Value);
					e.SetMetadata ("Name", match.Groups["repo"].Value);
					e.SetMetadata ("Branch", match.Groups["branch"].Value);
					externals.Add (e);
				}
			}

			ExternalGitDependencies = externals.ToArray ();
			return !Log.HasLoggedErrors;
		}
	}
}
