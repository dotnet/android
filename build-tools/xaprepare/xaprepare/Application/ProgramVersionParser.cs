using System;
using System.IO;

namespace Xamarin.Android.Prepare
{
	abstract class ProgramVersionParser : AppObject
	{
		public const string DefaultVersionString = "0.0.0";

		/// <summary>
		///   Either a full path or just the name of the program to get the version of. In the latter case, <see
		///   cref="OS.Which"/> is used to find the program.
		/// </summary>
		public string ProgramName { get; }

		/// <summary>
		///   Number of the line in the version output on which the version string is found. If 0 (the default) all
		///   lines are checked.
		/// </summary>
		public uint VersionOutputLine { get; }

		/// <summary>
		///   Arguments to pass to <see cref="ProgramName"/> in order to obtain the version. Defaults to <c>null</c>
		///   since some programs show the version when there are no arguments passed to them.
		/// </summary>
		public string VersionArguments { get; }

		protected ProgramVersionParser (string programName, string versionArguments = null, uint versionOutputLine = 0, Log log = null)
			: base (log)
		{
			if (String.IsNullOrEmpty (programName))
				throw new ArgumentException ("must not be null or empty", nameof (programName));
			ProgramName = programName;
			VersionArguments = versionArguments;
			VersionOutputLine = versionOutputLine;
		}

		public virtual string GetVersion (Context context, string fullProgramPath)
		{
			if (context == null)
				throw new ArgumentNullException (nameof (context));

			string programPath = String.IsNullOrEmpty (fullProgramPath) ? ProgramName : fullProgramPath;
			if (Path.IsPathRooted (ProgramName))
				programPath = ProgramName;
			else if (ProgramName.IndexOf (Path.DirectorySeparatorChar) >= 0) {
				if (ProgramName [0] == Path.DirectorySeparatorChar) { // Might be the case on Windows
					programPath = Path.GetFullPath (ProgramName);
				} else {
					programPath = Path.Combine (BuildPaths.XamarinAndroidSourceRoot, ProgramName);
				}
			} else {
				programPath = context.OS.Which (ProgramName, required: false);
			}

			if (!Utilities.FileExists (programPath)) {
				Log.DebugLine ($"{fullProgramPath} does not exist, unable to obtain version");
				return DefaultVersionString;
			}

			string versionOutput = Utilities.GetStringFromStdout (programPath, VersionArguments);
			Log.DebugLine ($"{programPath} {VersionArguments} returned: {versionOutput}");

			return ParseVersion (versionOutput);
		}

		protected abstract string ParseVersion (string programOutput);
	}
}
