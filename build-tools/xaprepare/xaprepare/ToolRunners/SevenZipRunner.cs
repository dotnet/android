using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	partial class SevenZipRunner : ToolRunner
	{
		const double DefaultTimeout = 30; // minutes
		static readonly Version bsoepMinVersion = new Version (15, 5);

		// Just an educated guess.  The official download page had versions 19 and then 23+ available
		// and the 19 one didn't support the `-snld` switch
		static readonly Version snldMinVersion = new Version (20, 0);
		Version version;

		protected override string DefaultToolExecutableName => "7za";
		protected override string ToolName                  => "7zip";

		public SevenZipRunner (Context context, Log? log = null, string? toolPath = null)
			: base (context, log, toolPath ?? Context.Instance.Tools.SevenZipPath)
		{
			ProcessTimeout = TimeSpan.FromMinutes (DefaultTimeout);

			string vs = VersionString.Trim ();
			if (String.IsNullOrEmpty (vs) || !Version.TryParse (vs, out Version? ver) || ver == null) {
				version = new Version (0, 0);
			} else {
				version = ver;
			}
		}

		public async Task<bool> Extract (string archivePath, string outputDirectory, List<string>? extraArguments = null)
		{
			if (String.IsNullOrEmpty (archivePath))
				throw new ArgumentException ("must not be null or empty", nameof (archivePath));
			if (String.IsNullOrEmpty (outputDirectory))
				throw new ArgumentException ("must not be null or empty", nameof (outputDirectory));

			ProcessRunner runner = CreateProcessRunner ("x");

			AddStandardArguments (runner);
			AddArguments (runner, extraArguments);
			runner.AddQuotedArgument ($"-o{outputDirectory}");
			runner.AddQuotedArgument (archivePath);

			try {
				Log.StatusLine ($"Archive path: {archivePath}", ConsoleColor.White);
				return await RunTool (
					() => {
						using (TextWriter outputSink = SetupOutputSink (runner, $"7zip-extract.{Path.GetFileName (archivePath)}", "extracting archive")) {
							runner.WorkingDirectory = Path.GetDirectoryName (archivePath);
							StartTwiddler ();
							return runner.Run ();
						}
					}
				);
			} finally {
				StopTwiddler ();
			}
		}

		public async Task<bool> SevenZip (string outputArchivePath, string workingDirectory, List<string> inputFiles)
		{
			return await DoZip (outputArchivePath, workingDirectory, inputFiles, "7z", 9);
		}

		public async Task<bool> Zip (string outputArchivePath, string workingDirectory, List<string> inputFiles)
		{
			return await DoZip (outputArchivePath, workingDirectory, inputFiles, "zip", 9);
		}

		async Task<bool> DoZip (string outputArchivePath, string workingDirectory, List<string> inputFiles, string archiveFormat, uint compressionLevel)
		{
			if (String.IsNullOrEmpty (outputArchivePath))
				throw new ArgumentException ("must not be null or empty", nameof (outputArchivePath));
			if (String.IsNullOrEmpty (workingDirectory))
				throw new ArgumentException ("must not be null or empty", nameof (workingDirectory));
			if (inputFiles == null)
				throw new ArgumentNullException (nameof (inputFiles));

			var files = inputFiles.Where (f => !String.IsNullOrEmpty (f)).ToList ();
			if (files.Count == 0)
				throw new ArgumentException ("must not be an empty list", nameof (inputFiles));

			ProcessRunner runner = CreateProcessRunner ("a");
			AddStandardArguments (runner);
			runner.AddArgument ($"-t{archiveFormat}");
			runner.AddArgument ($"-mx={compressionLevel}"); // maximum compression (range: 0-9)
			runner.AddQuotedArgument (outputArchivePath);

			string responseFilePath = Path.GetTempFileName ();
			File.WriteAllLines (responseFilePath, files);
			runner.AddQuotedArgument ($"@{responseFilePath}");

			try {
				Log.StatusLine ($"Archive path: {outputArchivePath}", ConsoleColor.White);
				return await RunTool (
					() => {
						using (TextWriter outputSink = SetupOutputSink (runner, $"7zip-create-{archiveFormat}.{Path.GetFileName (outputArchivePath)}", $"creating {archiveFormat} archive")) {
							runner.WorkingDirectory = workingDirectory;
							StartTwiddler ();
							return runner.Run ();
						}
					}
				);
			} finally {
				StopTwiddler ();
				Utilities.DeleteFileSilent (responseFilePath);
			}
		}

		protected override TextWriter CreateLogSink (string? logFilePath)
		{
			return new OutputSink (Log, logFilePath);
		}

		void AddStandardArguments (ProcessRunner runner)
		{
			Log.DebugLine ($"7-zip standard arguments, for 7z version {version}");

			// Ignore some "dangerous" symbolic symlinks in the ZIP archives. This allows 7zip to unpack Android NDK archives
			// without error.  The option appears to be undocumented, but was mentioned by the 7zip author here:
			//
			//   https://sourceforge.net/p/sevenzip/discussion/45798/thread/187ce54fb0/
			//
			if (version >= snldMinVersion) {
				Log.DebugLine ("Adding option to ignore dangerous symlinks");
				runner.AddArgument ("-snld");
			}

			// Disable progress indicator (doesn't appear to have any effect with some versions of 7z)
			runner.AddArgument ("-bd");

			// These switches were added in 7zip 15.05
			if (version >= bsoepMinVersion) {
				// Write standard messages to stdout
				runner.AddArgument ("-bso1");

				// Write progress updates to stdout
				runner.AddArgument ("-bsp1");

				// Write errors to stderr
				runner.AddArgument ("-bse2");
			}

			// Answer 'yes' to all questions
			runner.AddArgument ("-y");
		}
	}
}
