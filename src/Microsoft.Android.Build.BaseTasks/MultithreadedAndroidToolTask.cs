using System;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.Android.Build.Tasks
{
	// Each concrete task must also declare MSBuildMultiThreadableTaskAttribute.
	// Unlike AndroidToolTask, this base does not capture the process working directory.
	// ToolTask implements IMultiThreadableTask and starts the tool with TaskEnvironment.
	public abstract class MultithreadedAndroidToolTask : ToolTask
	{
		public abstract string TaskPrefix { get; }

		readonly StringBuilder toolOutput = new StringBuilder ();

		public override bool Execute ()
		{
			try {
				bool taskResult = RunTask ();
				if (!taskResult && toolOutput.Length > 0) {
					Log.LogUnhandledToolError (TaskPrefix, toolOutput.ToString ().Trim ());
				}
				toolOutput.Clear ();
				return taskResult;
			} catch (Exception ex) {
				Log.LogUnhandledException (TaskPrefix, ex, TaskEnvironment);
				return false;
			}
		}

		protected override void LogEventsFromTextOutput (string singleLine, MessageImportance messageImportance)
		{
			base.LogEventsFromTextOutput (singleLine, messageImportance);
			toolOutput.AppendLine (singleLine);
		}

		public virtual bool RunTask () => base.Execute ();
	}
}
