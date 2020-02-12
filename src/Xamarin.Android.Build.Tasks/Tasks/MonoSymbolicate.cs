using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	public class MonoSymbolicate : AndroidToolTask
	{
		public override string TaskPrefix => "MSYM";

		protected override string ToolName => OS.IsWindows ? "mono-symbolicate.exe" : "mono-symbolicate";

		[Required]
		public string InputDirectory { get; set; }

		[Required]
		public string OutputDirectory { get; set; }

		protected override string GenerateFullPathToTool ()
		{
			return Path.Combine (ToolPath, ToolExe);
		}

		protected override string GenerateCommandLineCommands ()
		{
			var cmd = new CommandLineBuilder ();
			cmd.AppendSwitch ("store-symbols");
			cmd.AppendFileNameIfNotNull (OutputDirectory);
			cmd.AppendFileNameIfNotNull (InputDirectory);
			return cmd.ToString ();
		}

		/// <summary>
		/// mono-symbolicate tends to print:
		/// Warning: Directory obj\Release\android\assets contains Xamarin.Android.Arch.Core.Common.dll but no debug symbols file was found.
		/// </summary>
		static readonly Regex symbolsWarning = new Regex ("no debug symbols file was found", RegexOptions.IgnoreCase | RegexOptions.Compiled);

		protected override void LogEventsFromTextOutput (string singleLine, MessageImportance messageImportance)
		{
			if (symbolsWarning.IsMatch (singleLine)) {
				Log.LogMessage (messageImportance, singleLine);
			} else {
				base.LogEventsFromTextOutput (singleLine, messageImportance);
			}
		}
	}
}
