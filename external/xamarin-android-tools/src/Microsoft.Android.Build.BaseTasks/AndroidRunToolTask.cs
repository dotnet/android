// https://github.com/xamarin/xamarin-android/blob/0134c2fb20f2f20127b24ef49177d9fe8226efdb/src/Xamarin.Android.Build.Tasks/Tasks/AndroidToolTask.cs#L9

using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;

namespace Microsoft.Android.Build.Tasks
{
	public abstract class AndroidRunToolTask : AndroidToolTask
	{
		protected static bool IsWindows = Path.DirectorySeparatorChar == '\\';

		protected abstract string DefaultErrorCode { get; }

		protected override void LogEventsFromTextOutput (string singleLine, MessageImportance messageImportance)
		{
			base.LogEventsFromTextOutput (singleLine, messageImportance);

			if (messageImportance != StandardErrorLoggingImportance)
				return;

			Log.LogFromStandardError (DefaultErrorCode, singleLine);
		}

		protected virtual Regex ErrorRegex {
			get { return AndroidErrorRegex; }
		}

		/* This gets pre-pended to any filenames that we get from error strings */
		protected string BaseDirectory { get; set; }

		// Aapt errors looks like this:
		//   res\layout\main.axml:7: error: No resource identifier found for attribute 'id2' in package 'android' (TaskId:22)
		//   Resources/values/theme.xml(2): error APT0000: Error retrieving parent for item: No resource found that matches the given name '@android:style/Theme.AppCompat'.
		//   Resources/values/theme.xml:2: error APT0000: Error retrieving parent for item: No resource found that matches the given name '@android:style/Theme.AppCompat'.
		//   res/drawable/foo-bar.jpg: Invalid file name: must contain only [a-z0-9_.]
		// Warnings can be like this
		//   aapt2 W 09-17 18:15:27 98796 12879433 ApkAssets.cpp:138] resources.arsc in APK 'android.jar' is compressed.
		// Look for them and convert them to MSBuild compatible errors.
		static Regex androidErrorRegex;
		public static Regex AndroidErrorRegex {
			get {
				if (androidErrorRegex == null)
					androidErrorRegex = new Regex (@"
^
( # start optional path followed by `:`
 (?<path>
  (?<file>.+[\\/][^:\(]+)
  (
   ([:](?<line>[\d ]+))
   |
   (\((?<line>[\d ]+)\))
  )?
 )
 \s*
 :
)?
( # optional warning|error|aapt2\sW|aapt2.exe\sW:
 \s*
 (?<level>(warning|error|aapt2\sW|aapt2.exe\sW)[^:]*)\s*
 :
)?
\s*
(?<message>.*)
$
", RegexOptions.Compiled | RegexOptions.ExplicitCapture | RegexOptions.IgnorePatternWhitespace | RegexOptions.IgnoreCase);
				return androidErrorRegex;
			}
		}

		protected static string QuoteString (string value)
		{
			return string.Format ("\"{0}\"", value);
		}
	}
}
