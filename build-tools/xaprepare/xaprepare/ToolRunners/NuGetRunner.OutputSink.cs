using System;
using System.Text;

namespace Xamarin.Android.Prepare
{
	partial class NuGetRunner
	{
		class OutputSink : ToolRunner.ToolOutputSink
		{
			const string DefaultIndent = "  ";

			string indent;
			string packageBullet;

			public override Encoding Encoding => Encoding.Default;

			public OutputSink (Log log, string logFilePath, string indent = null)
				: base (log, logFilePath)
			{
				this.indent = indent ?? DefaultIndent;
				packageBullet = Context.Instance.Characters.Package;
			}

			public override void WriteLine (string value)
			{
				const string RestoringPackagePrefix = "Restoring NuGet package";
				const string AllPackagesRestoredPrefix = "All packages listed in";

				base.WriteLine (value);
				if (String.IsNullOrEmpty (value))
					return;

				string consoleMessage = null;
				if (value.StartsWith (RestoringPackagePrefix, StringComparison.OrdinalIgnoreCase)) {
					consoleMessage = $"{packageBullet} {value.Substring (RestoringPackagePrefix.Length).Trim ().TrimEnd ('.')}";
				} else if (value.StartsWith (AllPackagesRestoredPrefix, StringComparison.OrdinalIgnoreCase)) {
					consoleMessage = value.Trim ();
				}

				if (consoleMessage == null)
					return;

				Log.StatusLine ($"{indent}{consoleMessage}", ConsoleColor.White);
			}
		}
	}
}
