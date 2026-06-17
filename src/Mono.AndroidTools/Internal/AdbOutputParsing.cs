//
// AdbOutputParsing.cs
//
// Author:
//       Michael Hutchinson <mhutch@xamarin.com>
//
// Copyright (c) 2011 Xamarin Inc. (http://xamarin.com)
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Xml.Linq;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;

namespace Mono.AndroidTools.Internal
{
	static class AdbOutputParsing
	{
		public static List<AndroidInstalledPackage> ParsePackageList (string packageList)
		{
			return ParsePackageList (XDocument.Parse (packageList));
		}
		public static List<AndroidInstalledPackage> ParsePackageList (XDocument packageList)
		{
			return packageList.Element ("packages").Elements ("package").Select (e =>
				new AndroidInstalledPackage (
					(string) e.Attribute ("name"),
					(string) e.Attribute ("codePath"),
					(int) e.Attribute ("version")
				)).ToList ();
		}

		public static List<AndroidInstalledPackage> ParsePmPackageList (string output)
		{
			var list = new List<AndroidInstalledPackage> ();

			using (var sr = new StringReader (output)) {
				string s;

				while ((s = sr.ReadLine ()) != null) {
					s.Trim ();

					if (string.IsNullOrEmpty (s))
						continue;

					// Skip lines that do not begin with package:
					if (s.StartsWith ("package:", StringComparison.Ordinal)) {
						list.Add (new AndroidInstalledPackage (s));
					}
				}
			}

			return list;
		}

		public static Dictionary<string,string> ParseGetprop (string properties)
		{
			var props = new Dictionary<string,string> ();

			foreach (Match m in PropertyIdentifier.Matches (properties)) {
				props.Add (m.Groups["key"].Value, m.Groups["value"].Value);
			}

			return props;
		}

		static char [] space = new  [] { ' ' };
		static bool reportedBadPsOutput;

		public static int GetPackagePidFromPs (string packageName, string psOutput)
		{
			int pid = TryGetPackagePidFromPs (packageName, psOutput);
			if (pid < 0) {
				throw new AdbException ("'ps' output not recognized");
			}
			return pid;
		}

		public static int TryGetPackagePidFromPs (string packageName, string psOutput)
		{
			int pid;
			try {
				pid = GetPackagePidFromPsInternal (packageName, psOutput);
			} catch (Exception ex) {
				AndroidLogger.LogError ("Internal error parsing `ps' output", ex);
				pid = -1;
			}
			if (pid < 0) {
				if (!reportedBadPsOutput) {
					AndroidLogger.LogError ("`ps' output not recognized, full output:\n" + psOutput);
					reportedBadPsOutput = true;
				} else {
					AndroidLogger.LogError ("`ps' output not recognized");
				}
			}
			return pid;
		}

		static int GetPackagePidFromPsInternal (string packageName, string psOutput)
		{
			using (var sr = new StringReader (psOutput)) {
				string line = sr.ReadLine ();
				if (line == null)
					return -1;

				var checkStateColumn    = false;
				int pidIndex, packageNameIndex, truncWidth, expectedEntries;

				string [] header = line.Split (space, StringSplitOptions.RemoveEmptyEntries);
				if (header.Length == 8 && header[1] == "PID" && header[7] == "NAME") {
					//standard android shell
					pidIndex = 1;
					packageNameIndex = 8; //there's an extra " S " before it
					expectedEntries = 9;
					truncWidth = int.MaxValue;
				} else if (header.Length == 4 && header[0] == "PID" && header[3] == "COMMAND") {
					//busybox 4 col
					pidIndex = 0;
					packageNameIndex = 4; //there's an extra " {foo} " before it
					expectedEntries = 5;
					truncWidth = 79; //in some cases it seems busybox truncates its output at 79 chars
				} else if (header.Length == 5 && header[0] == "PID" && header[4] == "COMMAND") {
					//busybox 5 col
					pidIndex = 0;
					packageNameIndex = 5; //there's an extra " {foo} " before it
					expectedEntries = 6;
					truncWidth = 78; //in some cases it seems busybox truncates its output at 78 chars
				} else {
					checkStateColumn    = true;
					expectedEntries     = header.Length;
					truncWidth          = int.MaxValue;
					pidIndex            = Array.IndexOf (header, "PID");
					if (pidIndex < 0)
						return -1;
					packageNameIndex    = Array.IndexOf (header, "NAME");
					if (packageNameIndex < 0) {
						packageNameIndex    = Array.IndexOf (header, "COMMAND");
					}
					if (packageNameIndex < 0)
						return -1;
				}

				var linesWithExpectedNumberOfStats = 0;
				var linesWithoutExpectedNumberOfStats = 0;

				while ((line = sr.ReadLine ()) != null) {
					//if there's a possibility the line has been truncated
					bool maybeTruncated = line.Length == truncWidth;

					line = line.Trim ();
					if (line.Length == 0) {
						continue;
					}
					string[] stats = line.Split (space, StringSplitOptions.RemoveEmptyEntries);
					if (stats.Length < packageNameIndex) {
						linesWithoutExpectedNumberOfStats++;
						continue;
					}

					linesWithExpectedNumberOfStats++;

					if (checkStateColumn) {
						// If there's 1 more column than the header had, assume it's state
						if (stats.Length == expectedEntries + 1) {
							packageNameIndex++;
							expectedEntries++;
						}
						checkStateColumn = false;
					}

					// in the case that the line is not truncated we can search all of the columns for packageName
					// this will help us in the case when WCHAN is blank and the column index of the process name is out by one
					// if the line could be truncated, then we may or may not find packageName and we'll just have to hope that
					// the way we've been parsing will still work
					var pid = FindPackageNameInPsStats (stats, packageName, pidIndex);
					if (pid != 0) {
						return pid;
					}

					// Some devices have *system* processes either with names containing spaces within,
					// or nameless. Ignore them.
					if (stats.Length != expectedEntries) {
						continue;
					}

					string procPkgName = stats [packageNameIndex].Trim ();

					if (packageName == procPkgName) {
						if (Int32.TryParse (stats [pidIndex], out pid)) {
							return pid;
						}
						return -1;
					}

					//it might be truncated, try to recover the end of the package name from the {} section
					if (maybeTruncated && packageName.StartsWith (procPkgName, StringComparison.Ordinal)) {
						string trailing = stats [packageNameIndex - 1].Trim ();
						if (trailing[0] == '{' && trailing[trailing.Length - 1] == '}') {
							trailing = trailing.Substring (1, trailing.Length - 2);
							bool enoughChars = (trailing.Length + procPkgName.Length) >= packageName.Length;
							if (enoughChars && packageName.EndsWith (trailing, StringComparison.Ordinal)) {
								if (Int32.TryParse (stats [pidIndex], out pid)) {
									return pid;
								}
							}
						}
						return -1;
					}

				}

				// we've run out of output and we didn't find the app name
				// if we encountered more "invalid" lines than valid lines, signal that we don't understand the format
				if (linesWithoutExpectedNumberOfStats > linesWithExpectedNumberOfStats) {
					return -1;
				}
			}

			return 0;
		}

		/// <summary>
		/// Searches all of the column values for packageName and if it finds it, returns the PID from the PID column.
		/// If we find packageName
		/// </summary>
		static int FindPackageNameInPsStats (string[] stats, string packageName, int pidIndex)
		{
			foreach (var stat in stats) {
				if (stat.Trim () == packageName) {
					int pid;
					if (Int32.TryParse (stats [pidIndex], out pid)) {
						return pid;
					}

					// something's wrong, return error
					return -1;
				}
			}

			return 0;
		}

		public static List<AndroidDevice> ParseDeviceList (string response)
		{
			var devices = new List<AndroidDevice> ();
			using (var sr = new StringReader (response)) {
				string s;
				while ((s = sr.ReadLine ()) != null) {
					s = s.Trim ();
					if (s.Length == 0)
						continue;

					string[] data = s.Split (new char [] {' ', '\t'}, StringSplitOptions.RemoveEmptyEntries);
					if (data.Length < 2)
						continue;
					var device = new AndroidDevice (data[0].Trim (), data[1].Trim ());
					devices.Add (device);
					if (data.Length > 2) {
						device.LongOutput = string.Join (";", data.Skip(2));
					}
				}
			}
			return devices;
		}

		public static void CheckInstallSuccess (string output, string packageName)
		{
			// adb can return empty output for successful operations.
			// https://devdiv.visualstudio.com/DevDiv/_workitems/edit/602572
			if (string.IsNullOrEmpty (output)) {
				return;
			}
			using (var r = new StringReader (output)) {
				string line;
				while ((line = r.ReadLine ()) != null) {
					if (line.StartsWith ("Success", StringComparison.Ordinal)) {
						return;
					}

					//NOTE: Always omit the closing ], since errors can be of the form: [INSTALL_FAILED_NO_MATCHING_ABIS: Failed to extract native libraries, res=-113]
					//      However, depending on the error adb may print [INSTALL_FAILED_NO_MATCHING_ABIS], so we should support *both*.

					if (line.Contains ("[INSTALL_FAILED_INSUFFICIENT_STORAGE"))
						throw new InsufficientSpaceException (string.Format ("There is not enough storage space on the device to store package: {0}. Free up some space and try again.", packageName), output);
					if (line.Contains ("[INSTALL_FAILED_MEDIA_UNAVAILABLE"))
						throw new InsufficientSpaceException (string.Format ("There is not enough storage space on the device to store package: {0}. Free up some space or use an SD card and try again.", packageName), output);
					if (line.Contains ("[INSTALL_FAILED_ALREADY_EXISTS"))
						throw new PackageAlreadyExistsException (string.Format ("Package {0} already exists on device.", packageName), output: output, packageFile: packageName);
					if (line.Contains ("[INSTALL_FAILED_OLDER_SDK"))
						throw new SdkNotSupportedException ("The device does not support the minimum SDK level specified in the manifest.", output);
					if (line.Contains ("[INSTALL_PARSE_FAILED_INCONSISTENT_CERTIFICATES"))
						throw new RequiresUninstallException (line, output: output, packageFile: packageName);
					if (line.Contains("doesn't support runtime permissions"))
						throw new RequiresUninstallException(line, output: output, packageFile: packageName);
					if (line.Contains ("[INSTALL_PARSE_FAILED_NO_CERTIFICATES"))
						throw new InstallFailedException ("The package was not properly signed (NO_CERTIFICATES).", output);
					if (line.Contains ("[INSTALL_FAILED_CONTAINER_ERROR"))
						throw new InstallFailedException (string.Format ("Installation failed due to container error. This can be caused by lack of available space on the SD card or stale files left behind from previous installations."), output);
					if (line.Contains ("[INSTALL_FAILED_CPU_ABI_INCOMPATIBLE") || line.Contains ("[INSTALL_FAILED_NO_MATCHING_ABIS"))
						throw new IncompatibleCpuAbiException ("The package does not support the CPU architecture of this device.", output);
					if (line.Contains ("[INSTALL_FAILED_UPDATE_INCOMPATIBLE") || line.Contains ("[INSTALL_FAILED_VERSION_DOWNGRADE"))
						throw new RequiresUninstallException ("The installed package is incompatible. Please manually uninstall and try again.", output: output, packageFile: packageName);
					if (line.Contains ("[INSTALL_FAILED"))
						throw new InstallFailedException (line, output);
				}
			}

			throw new InstallFailedException ("Unexpected install output: " + output, output);
		}

		public static bool UninstallResult (string output)
		{
			using (var r = new StringReader (output)) {
				string line;
				while ((line = r.ReadLine ()) != null) {
					if (line.StartsWith ("Success", StringComparison.Ordinal)) {
						return true;
					}
				}
			}
			return false;
		}
		static readonly Regex ExtractLocation = new Regex (@"^Broadcast completed: result=0, data=""([^""]*)""$",
			RegexOptions.Multiline | RegexOptions.Compiled);

		static readonly Regex FallbackBroadcastData = new Regex(@"^.*data=""([^""]*)""$",
			RegexOptions.Multiline | RegexOptions.Compiled);

		/// <summary>
		/// RegEx for parsing getprop output
		/// first we look for `[.]: [` at the start of the line. 
		/// we then collect any character until we hit the 
		/// negative look ahead which matches the next property key pattern or the end of the string.
		///  `]\n[.]: [` or `\Z` 
		/// </summary>
		static readonly Regex PropertyIdentifier = new Regex (@"
	^\[(?<key>[^\]]+)\]:\s\[        # looks for `[.]: [` at the start of the line
	(?<value>(.|\n)*?)              # captures all characters afterwards until we hit the positive look ahead expression
	(?=                             # positive lookahead for…
	  (
	    (\](\r)?\n\[([^\]]+)\]:)    # end of value + start of key
	    |                           # or
	    (\](\r)?(\n$|\Z))           # end of string
	  )
	)
", 
			RegexOptions.Multiline | RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnorePatternWhitespace);

		public static string TryBroadcastResult (string output)
		{
			var m = ExtractLocation.Match (output.Trim ());
			if (m.Success) {
				return m.Groups [1].Value;
			}

			var m2 = FallbackBroadcastData.Match (output.Trim ());
			if (m2.Success) {
				return m2.Groups [1].Value;
			}
			return null;
		}

		public static string BroadcastResult (string output)
		{
			string ret = TryBroadcastResult (output);
			if (ret == null)
				throw new AdbException ("Broadcast failed");
			return ret;
		}

		static readonly string StartResultError = "Error: Activity not started";

		public static void CheckStartResult (string output, string activity)
		{
			if (output.Contains ("Error: Bad component name") || output.Contains ("does not exist."))
				throw new ActivityNotFoundException (string.Format ("Device could not find component named: {0}", activity));

			if (output.Contains (StartResultError))
				throw new AdbException (output);
		}

		public static List<AndroidLogCatEntry> ParseLogCat (string output)
		{
			var list = new List<AndroidLogCatEntry> ();

			using (var sr = new StringReader (output)) {
				string s;

				while ((s = sr.ReadLine ()) != null) {
					s.Trim ();

					if (string.IsNullOrEmpty (s))
						continue;

					var entry = ParseLogCatEntry (s);

					if (entry != null)
						list.Add (entry);
				}
			}

			return list;
		}

		public static AndroidLogCatEntry ParseLogCatEntry (string data)
		{
			if (data.StartsWith ("---", StringComparison.Ordinal))
				return null;

			try {
				var entry = new AndroidLogCatEntry ();

				data = data.TrimEnd ('\n', '\r');

				entry.Raw = data;
				entry.Tag = data.Split ('/', '(')[1].Trim ();
				entry.Type = ParseLogEntryType (data.Substring (19, 1)[0]);
				entry.Date = data.Substring (0, 18);
				entry.Pid = int.Parse (data.Split ('(', ')')[1].Trim ());
				entry.Message = data.Substring (data.IndexOf (')') + 3);

				return entry;
			} catch (Exception) {
				return null;
			}
		}

		private static LogEntryType ParseLogEntryType (char input)
		{
			switch (input) {
				case 'D':
					return LogEntryType.Debug;
				case 'I':
					return LogEntryType.Info;
				case 'W':
					return LogEntryType.Warning;
				case 'E':
				case 'F':
					return LogEntryType.Error;
				case 'V':
					return LogEntryType.Verbose;
			}

			throw new ArgumentException ("Unknown LogEntryType: " + input);
		}
	}
}
