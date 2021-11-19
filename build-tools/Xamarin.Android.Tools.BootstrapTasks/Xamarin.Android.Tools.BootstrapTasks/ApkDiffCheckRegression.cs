using System;
using System.IO;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xamarin.Android.BuildTools.PrepTasks;


namespace Xamarin.Android.Tools.BootstrapTasks
{
	public class ApkDiffCheckRegression : ToolTask
	{
		[Required]
		public string ApkDescDirectory { get; set; }

		[Required]
		public string ApkDescSuffix { get; set; }

		[Required]
		public string ApkDiffTool { get; set; }

		[Required]
		public string Package { get; set; }

		[Required]
		public string ReferenceDescription { get; set; }

		[Required]
		public string TestResultDirectory { get; set; }

		protected override string ToolName => Path.GetFileName (ApkDiffTool);
		protected override string GenerateFullPathToTool () => ApkDiffTool;

		const int ApkSizeThreshold = 48*1024;
		const int AssemblySizeThreshold = 50*1024;

		StringBuilder logCopy = new StringBuilder ();

		protected override string GenerateCommandLineCommands ()
		{
			var apkDescFile = Path.Combine (ApkDescDirectory, $"{Path.GetFileNameWithoutExtension (Package)}{ApkDescSuffix}{Path.GetExtension (Package)}desc");
			var cmd = new CommandLineBuilder ();
			cmd.AppendSwitch ("-s");
			cmd.AppendSwitch ($"--save-description-2={apkDescFile}");
			cmd.AppendSwitch ("--descrease-is-regression");
			cmd.AppendSwitch ($"--test-apk-size-regression={ApkSizeThreshold}");
			cmd.AppendSwitch ($"--test-assembly-size-regression={AssemblySizeThreshold}");
			cmd.AppendFileNameIfNotNull (ReferenceDescription);
			cmd.AppendFileNameIfNotNull (Package);

			return cmd.ToString ();
		}

		protected override void LogEventsFromTextOutput (string singleLine, MessageImportance messageImportance)
		{
			base.LogEventsFromTextOutput (singleLine, messageImportance);

			logCopy.AppendLine (singleLine);
		}

		public override bool Execute ()
		{
			LogStandardErrorAsError = false;
			StandardOutputImportance = "Low";

			var succes = base.Execute ();
			if (ExitCode == 0)
				return succes;

			var errorMessage = $"apkdiff exited with error code: {ExitCode}.";
			var testResultPath = Path.Combine (TestResultDirectory, $"TestResult-apkdiff-{Path.GetFileNameWithoutExtension (ReferenceDescription)}.xml");
			ErrorResultsHelper.CreateErrorResultsFile (
				testResultPath,
				nameof (ApkDiffCheckRegression),
				"check apk size regression, context: https://github.com/xamarin/xamarin-android/blob/main/Documentation/project-docs/ApkSizeRegressionChecks.md",
				new Exception (errorMessage),
				$"apkdiff output:\n{logCopy}");

			return false;
		}
	}
}
