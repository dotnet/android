using Microsoft.Build.Framework;
using System;
using Xamarin.Android.BuildTools.PrepTasks;

namespace Xamarin.Android.Tools.BootstrapTasks
{
	public class Ant : PathToolTask
	{
		public string Arguments { get; set; }

		public string JavaSdkDirectory { get; set; }

		public string WorkingDirectory { get; set; }

		protected override string ToolBaseName {
			get { return "ant"; }
		}

		public override bool Execute ()
		{
			Log.LogMessage (MessageImportance.Low, $"Task {nameof (Ant)}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (Arguments)}: {Arguments}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (JavaSdkDirectory)}: {JavaSdkDirectory}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (WorkingDirectory)}: {WorkingDirectory}");

			if (!string.IsNullOrEmpty (JavaSdkDirectory))
				Environment.SetEnvironmentVariable ("JAVA_HOME", JavaSdkDirectory);

			base.Execute ();

			return !Log.HasLoggedErrors;
		}

		protected override string GenerateCommandLineCommands ()
		{
			return Arguments;
		}

		protected override string GetWorkingDirectory ()
		{
			return WorkingDirectory;
		}
	}
}
