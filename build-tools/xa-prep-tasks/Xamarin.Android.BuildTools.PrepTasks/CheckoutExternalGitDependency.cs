using System;
using System.IO;
using Microsoft.Build.Framework;

namespace Xamarin.Android.BuildTools.PrepTasks
{
	public class CheckoutExternalGitDependency : Git
	{
		[Required]
		public ITaskItem ExternalGitDependency { get; set; }

		protected override bool LogTaskMessages {
			get { return false; }
		}

		string commit;
		string owner;
		string name;
		string singleLine;

		public override bool Execute ()
		{
			commit = ExternalGitDependency.ItemSpec;
			owner = ExternalGitDependency.GetMetadata ("Owner");
			name = ExternalGitDependency.GetMetadata ("Name");
			string destination = Path.Combine (GetWorkingDirectory (), name);

			if (!Directory.Exists (destination)) {
				Clone (destination);
			}

			WorkingDirectory.ItemSpec = destination;
			Fetch ();
			CheckoutCommit ();

			return !Log.HasLoggedErrors;
		}

		void Clone (string destination)
		{
			string ghToken = Environment.GetEnvironmentVariable("GH_AUTH_SECRET");
			if (!string.IsNullOrEmpty (ghToken)) {
				Arguments = $"clone https://{ghToken}@github.com/{owner}/{name} --progress \"{destination}\"";
			} else {
				if (IsHttps ()) {
					// Just use a plain git clone for https
					Arguments = $"clone https://github.com/{owner}/{name} --progress \"{destination}\"";
				} else {
					// Fallback to SSH URI
					Arguments = $"clone git@github.com:{owner}/{name} --progress \"{destination}\"";
				}
			}

			base.Execute ();
		}

		void Fetch ()
		{
			Arguments = $"fetch --all --no-recurse-submodules --progress";
			base.Execute ();
		}

		void CheckoutCommit ()
		{
			Arguments = $"checkout {commit} --force --progress";
			base.Execute ();
		}

		bool IsHttps ()
		{
			Arguments = "config --get remote.origin.url";
			base.Execute ();
			return singleLine != null && singleLine.Contains ("https://");
		}

		protected override void LogToolCommand(string message)
		{
			// Do nothing
		}

		protected override void LogEventsFromTextOutput (string singleLine, MessageImportance messageImportance)
		{
			if (!string.IsNullOrEmpty (singleLine))
				this.singleLine = singleLine;
			
			base.LogEventsFromTextOutput (singleLine, messageImportance);
		}
	}
}
