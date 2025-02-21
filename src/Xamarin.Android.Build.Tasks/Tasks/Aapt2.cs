// Copyright (C) 2011 Xamarin, Inc. All rights reserved.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using System.Text;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Xamarin.Android.Tools;
using ThreadingTasks = System.Threading.Tasks;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks {

	public abstract class Aapt2 : AsyncTask {

		private const int MAX_PATH = 260;
		private static readonly int DefaultMaxAapt2Daemons = 6;
		protected Dictionary<string, string> _resource_name_case_map;

		Dictionary<string, string> resource_name_case_map => _resource_name_case_map ??= MonoAndroidHelper.LoadResourceCaseMap (BuildEngine4, ProjectSpecificTaskObjectKey);

		protected virtual int ProcessorCount => Environment.ProcessorCount;

		public int DaemonMaxInstanceCount { get; set; }

		public bool DaemonKeepInDomain { get; set; }

		public ITaskItem [] ResourceDirectories { get; set; }

		public ITaskItem AndroidManifestFile { get; set;}

		public string ResourceSymbolsTextFile { get; set; }

		protected string ToolName { get { return OS.IsWindows ? "aapt2.exe" : "aapt2"; } }

		public string ToolPath { get; set; }

		public string ToolExe { get; set; }

		/// <summary>
		/// Returns true if a filename starts with a . character.
		/// </summary>
		public static bool IsInvalidFilename (string path) =>
			Path.GetFileName (path).StartsWith (".", StringComparison.Ordinal);

		/// <summary>
		/// Returns <see langword="true"/> if <paramref name="c"/> is an ASCII
		/// character ([ U+0000..U+007F ]).
		/// </summary>
		/// <remarks>
		/// Per http://www.unicode.org/glossary/#ASCII, ASCII is only U+0000..U+007F.
		/// We cannot use Char.IsAscii cos we are .netstandard2.0
		/// Source https://github.com/dotnet/runtime/blob/1d1bf92fcf43aa6981804dc53c5174445069c9e4/src/libraries/System.Private.CoreLib/src/System/Char.cs#L91
		/// </remarks>
		public static bool IsAscii(char c) => (uint)c <= '\x007f';

		protected string ResourceDirectoryFullPath (string resourceDirectory)
		{
			return (Path.IsPathRooted (resourceDirectory) ? resourceDirectory : Path.Combine (WorkingDirectory, resourceDirectory)).TrimEnd ('\\');
		}

		protected string GetFullPath (string dir)
		{
			return (Path.IsPathRooted (dir) ? dir : Path.GetFullPath (Path.Combine (WorkingDirectory, dir)));
		}

		protected string GenerateFullPathToTool ()
		{
			return Path.Combine (ToolPath, string.IsNullOrEmpty (ToolExe) ? ToolName : ToolExe);
		}

		protected virtual int GetRequiredDaemonInstances ()
		{
			return 1;
		}

		Aapt2Daemon daemon;

		internal Aapt2Daemon Daemon => daemon;
		public override bool Execute ()
		{
			// Must register on the UI thread!
			// We don't want to use up ALL the available cores especially when
			// running in the IDE. So lets cap it at DefaultMaxAapt2Daemons (6).
			int maxInstances = Math.Min (Math.Max (1, ProcessorCount - 1), DefaultMaxAapt2Daemons);
			if (DaemonMaxInstanceCount == 0)
				DaemonMaxInstanceCount = maxInstances;
			else
				DaemonMaxInstanceCount = Math.Min (DaemonMaxInstanceCount, maxInstances);
			daemon  = Aapt2Daemon.GetInstance (BuildEngine4, LogDebugMessage, GenerateFullPathToTool (),
				DaemonMaxInstanceCount, GetRequiredDaemonInstances (), registerInDomain: DaemonKeepInDomain);
			return base.Execute ();
		}

		ConcurrentBag<long> jobs = new ConcurrentBag<long> ();

		protected long RunAapt (string [] args, string outputFile)
		{
			LogDebugMessage ($"Executing {string.Join (" ", args)}");
			long jobid = daemon.QueueCommand (args, outputFile, CancellationToken);
			jobs.Add (jobid);
			return jobid;
		}

		protected void ProcessOutput ()
		{
			Aapt2Daemon.Job[] completedJobs = Daemon.WaitForJobsToComplete (jobs);
			foreach (var job in completedJobs) {
				foreach (var line in job.Output) {
					if (!LogAapt2EventsFromOutput (line.Line, MessageImportance.Normal, job.Succeeded)) {
						break;
					}
				}
			}
		}

		protected bool LogAapt2EventsFromOutput (string singleLine, MessageImportance messageImportance, bool apptResult)
		{
			if (string.IsNullOrEmpty (singleLine))
				return true;

			var match = AndroidRunToolTask.AndroidErrorRegex.Match (singleLine.Trim ());
			string file = string.Empty;

			if (match.Success) {
				file = match.Groups ["file"].Value;
				int line = 0;
				if (!string.IsNullOrEmpty (match.Groups ["line"]?.Value))
					line = int.Parse (match.Groups ["line"].Value.Trim ()) + 1;
				var level = match.Groups ["level"].Value.ToLowerInvariant ();
				var message = match.Groups ["message"].Value;

				// Handle the following which is NOT an error :(
				// W/ResourceType(23681): For resource 0x0101053d, entry index(1341) is beyond type entryCount(733)
				// W/ResourceType( 3681): For resource 0x0101053d, entry index(1341) is beyond type entryCount(733)
				if (file.StartsWith ("W/", StringComparison.OrdinalIgnoreCase)) {
					LogCodedWarning (GetErrorCode (singleLine), singleLine);
					return true;
				}
				if (message.StartsWith ("unknown option", StringComparison.OrdinalIgnoreCase)) {
					// we need to filter out the remailing help lines somehow.
					LogCodedError ("APT0001", Properties.Resources.APT0001, message.Substring ("unknown option '".Length).TrimEnd ('.', '\''));
					return false;
				}
				if (LogNotesOrWarnings (message, singleLine, messageImportance))
					return true;
				if (level.Contains ("note")) {
					LogMessage (message, messageImportance);
					return true;
				}
				if (level.Contains ("warning") || level.StartsWith ($"{ToolName} W", StringComparison.OrdinalIgnoreCase)) {
					LogCodedWarning (GetErrorCode (singleLine), singleLine);
					return true;
				}

				// Try to map back to the original resource file, so when the user
				// double clicks the error, it won't take them to the obj/Debug copy
				if (ResourceDirectories != null) {
					foreach (var dir in ResourceDirectories) {
						var resourceDirectory = dir.ItemSpec;
						var resourceDirectoryFullPath = ResourceDirectoryFullPath (resourceDirectory);

						string newfile = MonoAndroidHelper.FixUpAndroidResourcePath (file, resourceDirectory, resourceDirectoryFullPath, resource_name_case_map);
						if (!string.IsNullOrEmpty (newfile)) {
							file = newfile;
							break;
						}
					}
				}

				bool manifestError = false;
				if (AndroidManifestFile != null && string.Compare (Path.GetFileName (file), Path.GetFileName (AndroidManifestFile.ItemSpec), StringComparison.OrdinalIgnoreCase) == 0) {
					manifestError = true;
				}

				// Strip any "Error:" text from aapt's output
				if (message.StartsWith ("error: ", StringComparison.InvariantCultureIgnoreCase))
					message = message.Substring ("error: ".Length);

				if (level.Contains ("error") || (line != 0 && !string.IsNullOrEmpty (file))) {
					var errorCode = GetErrorCodeForFile (message, file);
					if (manifestError)
						LogCodedError (errorCode, string.Format (Xamarin.Android.Tasks.Properties.Resources.AAPTManifestError, message.TrimEnd('.')), AndroidManifestFile.ItemSpec, 0);
					else
						LogCodedError (errorCode, AddAdditionalErrorText (errorCode, message), file, line);
					return true;
				}
			}

			if (!apptResult) {
				var message = string.Format ("{0} \"{1}\".", singleLine.Trim (), singleLine.Substring (singleLine.LastIndexOfAny (new char [] { '\\', '/' }) + 1));
				if (LogNotesOrWarnings (message, singleLine, messageImportance))
					return true;
				var errorCode = GetErrorCodeForFile (message, file);
				LogCodedError (errorCode, AddAdditionalErrorText (errorCode, message), ToolName);
			} else {
				LogCodedWarning (GetErrorCode (singleLine), singleLine);
			}
			return true;
		}

		bool LogNotesOrWarnings (string message, string singleLine, MessageImportance messageImportance)
		{
			if (message.Contains ("in APK") && message.Contains ("is compressed.")) {
				LogMessage (singleLine, messageImportance);
				return true;
			}
			else if (message.Contains ("fakeLogOpen")) {
				LogMessage (singleLine, messageImportance);
				return true;
			}
			else if (message.Contains ("note:")) {
				LogMessage (singleLine, messageImportance);
				return true;
			}
			else if (message.Contains ("warn:")) {
				LogCodedWarning (GetErrorCode (singleLine), singleLine);
				return true;
			}
			return false;
		}

		static bool IsFilePathToLong (string filePath)
		{
			if (OS.IsWindows && filePath.Length > MAX_PATH) {
				return true;
			}
			return false;
		}

		static protected bool IsPathOnlyASCII (string filePath)
		{
			if (!OS.IsWindows)
				return true;

			foreach (var c in filePath)
				if (!IsAscii (c))
					return false;
			return true;
		}

		static string AddAdditionalErrorText (string errorCode, string message)
		{
			var sb = new StringBuilder ();
			sb.AppendLine (message);
			switch (errorCode)
			{
				case "APT2264":
					sb.AppendLine (Xamarin.Android.Tasks.Properties.Resources.APT2264);
				break;
				case "APT2265":
					sb.AppendLine (Xamarin.Android.Tasks.Properties.Resources.APT2265);
				break;
			}
			return sb.ToString ();
		}

		static string GetErrorCodeForFile (string message, string filePath)
		{
			var errorCode = GetErrorCode (message);
			switch (errorCode)
			{
				case "APT2265":
					if (IsPathOnlyASCII (filePath) && IsFilePathToLong (filePath))
						errorCode = "APT2264";
				break;
			}
			return errorCode;
		}

		static string GetErrorCode (string message)
		{
			foreach (var tuple in error_codes)
				if (message.IndexOf (tuple.Item2, StringComparison.OrdinalIgnoreCase) >= 0)
					return tuple.Item1;

			return "APT2000";
		}

		static readonly List<Tuple<string, string>> error_codes = new List<Tuple<string, string>> () {
			Tuple.Create ("APT2001", "already defined a <flag>"),
			Tuple.Create ("APT2002", "already defined an <enum>"),
			Tuple.Create ("APT2003", "can't map package ID"),
			Tuple.Create ("APT2004", "android:name in <uses-feature> must not be empty"),
			Tuple.Create ("APT2005", "android-sdk is missing minSdkVersion attribute"),
			Tuple.Create ("APT2006", "Artifact does not have a name and no global name template defined"),
			Tuple.Create ("APT2007", "Asked to print artifacts without providing a configurations"),
			Tuple.Create ("APT2008", "tag must be a valid Java class name"),
			Tuple.Create ("APT2009", "tag must be a valid Java package name"),
			Tuple.Create ("APT2010", "tag must not be empty"),
			Tuple.Create ("APT2011", "attribute coreApp must be a boolean"),
			Tuple.Create ("APT2012", "attribute 'featureSplit' used in <manifest> but 'android:isFeatureSplit"),
			Tuple.Create ("APT2013", "attribute 'package' in <manifest> tag is not a valid Android package name"),
			Tuple.Create ("APT2014", "attribute 'package' in <manifest> tag must not be a reference"),
			Tuple.Create ("APT2015", "attribute 'split' in <manifest> tag is not a"),
			Tuple.Create ("APT2016", "<attr> tag must have a 'name' attribute"),
			Tuple.Create ("APT2017", "<bag> must have a 'type' attribute"),
			Tuple.Create ("APT2018", "can not define a <flag>"),
			Tuple.Create ("APT2019", "can not define an <enum>"),
			Tuple.Create ("APT2020", "cannot define both android:name and android:glEsVersion"),
			Tuple.Create ("APT2021", "cannot merge entry"),
			Tuple.Create ("APT2022", "cannot merge type"),
			Tuple.Create ("APT2023", "can't assign ID"),
			Tuple.Create ("APT2024", "can't extract text into its own resource"),
			Tuple.Create ("APT2025", "can't include static library when not building a static lib"),
			Tuple.Create ("APT2026", "can't specify --package-id when not building a regular app"),
			Tuple.Create ("APT2027", "target split ambiguous"),
			Tuple.Create ("APT2028", "corrupt resources.arsc"),
			Tuple.Create ("APT2029", "corrupt resource table"),
			Tuple.Create ("APT2030", "corrupt ResTable_header chunk"),
			Tuple.Create ("APT2031", "corrupt ResTable_package chunk"),
			Tuple.Create ("APT2032", "corrupt ResTable_package"),
			Tuple.Create ("APT2033", "corrupt ResTable_type chunk"),
			Tuple.Create ("APT2034", "corrupt ResTable_typeSpec chunk"),
			Tuple.Create ("APT2035", "Could not determine split APK artifact name"),
			Tuple.Create ("APT2036", "Could not find the root element in the XML document"),
			Tuple.Create ("APT2037", "could not identify format of APK"),
			Tuple.Create ("APT2038", "Could not lookup required ABIs"),
			Tuple.Create ("APT2039", "Could not lookup required Android SDK version"),
			Tuple.Create ("APT2040", "Could not lookup required device features"),
			Tuple.Create ("APT2041", "Could not lookup required locales"),
			Tuple.Create ("APT2042", "Could not lookup required OpenGL texture formats"),
			Tuple.Create ("APT2043", "Could not lookup required screen densities"),
			Tuple.Create ("APT2044", "Could not parse ABI value"),
			Tuple.Create ("APT2045", "could not parse array item"),
			Tuple.Create ("APT2046", "Could not parse config descriptor for empty screen-density-group"),
			Tuple.Create ("APT2047", "Could not parse config descriptor for screen-density"),
			Tuple.Create ("APT2048", "Could not parse config file"),
			Tuple.Create ("APT2049", "could not parse style item"),
			Tuple.Create ("APT2050", "Could not process XML document"),
			Tuple.Create ("APT2051", "could not update AndroidManifest.xml for output artifact"),
			Tuple.Create ("APT2052", "could not validate post processing configuration"),
			Tuple.Create ("APT2053", "for external package"),
			Tuple.Create ("APT2054", "duplicate overlayable declaration for resource"),
			Tuple.Create ("APT2055", "duplicate quantity"),
			Tuple.Create ("APT2056", "duplicate symbol"),
			Tuple.Create ("APT2057", "duplicate value for resource"),
			Tuple.Create ("APT2058", "empty symbol"),
			Tuple.Create ("APT2059", "failed assigning IDs"),
			Tuple.Create ("APT2060", "failed deduping resources"),
			Tuple.Create ("APT2061", "failed linking file resources"),
			Tuple.Create ("APT2062", "failed linking references"),
			Tuple.Create ("APT2063", "failed moving private attributes"),
			Tuple.Create ("APT2064", "failed opening zip"),
			Tuple.Create ("APT2065", "failed parsing input"),
			Tuple.Create ("APT2066", "failed parsing overlays"),
			Tuple.Create ("APT2067", "failed processing manifest"),
			Tuple.Create ("APT2068", "failed reading png"),
			Tuple.Create ("APT2069", "failed reading stable ID file"),
			Tuple.Create ("APT2070", "failed removing resources with no defaults"),
			Tuple.Create ("APT2071", "failed stripping products"),
			Tuple.Create ("APT2072", "failed to allocate info ptr"),
			Tuple.Create ("APT2073", "failed to allocate read ptr"),
			Tuple.Create ("APT2074", "failed to allocate write info ptr"),
			Tuple.Create ("APT2075", "failed to allocate write ptr"),
			Tuple.Create ("APT2076", "failed to copy file"),
			Tuple.Create ("APT2077", "failed to create archive"),
			Tuple.Create ("APT2078", "failed to create directory"),
			Tuple.Create ("APT2079", "failed to create libpng read png_info"),
			Tuple.Create ("APT2080", "failed to create libpng read png_struct"),
			Tuple.Create ("APT2081", "failed to create libpng write png_info"),
			Tuple.Create ("APT2082", "failed to create libpng write png_struct"),
			Tuple.Create ("APT2083", "failed to create Split AndroidManifest.xml"),
			Tuple.Create ("APT2084", "failed to deserialize proto XML"),
			Tuple.Create ("APT2085", "failed to deserialize proto"),
			Tuple.Create ("APT2086", "failed to deserialize resource table"),
			Tuple.Create ("APT2087", "failed to extract data from AndroidManifest.xml"),
			Tuple.Create ("APT2088", "failed to find"),
			Tuple.Create ("APT2089", "failed to finish entry"),
			Tuple.Create ("APT2090", "failed to finish writing data"),
			Tuple.Create ("APT2091", "failed to flatten resource table"),
			Tuple.Create ("APT2092", "failed to load APK"),
			Tuple.Create ("APT2093", "failed to load include path"),
			Tuple.Create ("APT2094", "failed to merge resource table"),
			Tuple.Create ("APT2095", "failed to mmap file"),
			Tuple.Create ("APT2096", "failed to open APK"),
			Tuple.Create ("APT2097", "failed to open directory"),
			Tuple.Create ("APT2098", "failed to open file"),
			Tuple.Create ("APT2099", "failed to open resources.arsc"),
			Tuple.Create ("APT2100", "failed to open resources.pb"), // lgtm [csharp/responsible-ai/ml-training-and-serialization-files-referenced] These are not the droids you are looking for. Not ML data training files.
			Tuple.Create ("APT2101", "failed to open"),
			Tuple.Create ("APT2102", "failed to parse binary XML"),
			Tuple.Create ("APT2103", "failed to parse binary"),
			Tuple.Create ("APT2104", "failed to parse file as binary XML"),
			Tuple.Create ("APT2105", "failed to parse file as proto XML"),
			Tuple.Create ("APT2106", "failed to parse proto XML"),
			Tuple.Create ("APT2107", "failed to parse table"),
			Tuple.Create ("APT2108", "Failed to parse the output artifact list"),
			Tuple.Create ("APT2109", "failed to parse value for resource"),
			Tuple.Create ("APT2110", "failed to parse whitelist from config file"),
			Tuple.Create ("APT2111", "failed to read compiled header"),
			Tuple.Create ("APT2112", "failed to read proto"),
			Tuple.Create ("APT2113", "failed to read"),
			Tuple.Create ("APT2114", "Failed to rewrite"),
			Tuple.Create ("APT2115", "failed to serialize AndroidManifest.xml"),
			Tuple.Create ("APT2116", "failed to serialize file"),
			Tuple.Create ("APT2117", "failed to serialize the resource table"),
			Tuple.Create ("APT2118", "failed to serialize to binary XML"),
			Tuple.Create ("APT2119", "failed to serialize to proto XML"),
			Tuple.Create ("APT2120", "Failed to strip versioned resources"),
			Tuple.Create ("APT2121", "failed to write entry data"),
			Tuple.Create ("APT2122", "failed to write png"),
			Tuple.Create ("APT2123", "failed to write resource table"),
			Tuple.Create ("APT2124", "failed to write"),
			Tuple.Create ("APT2125", "failed versioning styles"),
			Tuple.Create ("APT2126", "file not found"),
			Tuple.Create ("APT2127", "files given but --dir specified"),
			Tuple.Create ("APT2128", "file signature does not match PNG signature"),
			Tuple.Create ("APT2129", "flattening failed"),
			Tuple.Create ("APT2130", "for configuration"),
			Tuple.Create ("APT2131", "'id' is ignored within <public-group>"),
			Tuple.Create ("APT2132", "illegal nested XLIFF 'g' tag"),
			Tuple.Create ("APT2133", "incompatible package"),
			Tuple.Create ("APT2134", "in <item> within an <overlayable>"),
			Tuple.Create ("APT2135", "inline XML resources must have a single root"),
			Tuple.Create ("APT2136", "inner element must either be a resource reference or empty"),
			Tuple.Create ("APT2137", "in <public>"),
			Tuple.Create ("APT2138", "invalid android:minSdkVersion"),
			Tuple.Create ("APT2139", "invalid android:revisionCode"),
			Tuple.Create ("APT2140", "invalid android:versionCode"),
			Tuple.Create ("APT2141", "Invalid attribute:"),
			Tuple.Create ("APT2142", "invalid config"),
			Tuple.Create ("APT2143", "invalid density"),
			Tuple.Create ("APT2144", "invalid file path"),
			Tuple.Create ("APT2145", "invalid Java identifier"),
			Tuple.Create ("APT2146", "invalid 'max' value"),
			Tuple.Create ("APT2147", "invalid 'min' value"),
			Tuple.Create ("APT2148", "invalid namespace prefix"),
			Tuple.Create ("APT2149", "invalid package name"),
			Tuple.Create ("APT2150", "invalid preferred density"),
			Tuple.Create ("APT2151", "invalid resource ID"),
			Tuple.Create ("APT2152", "invalid resource name"),
			Tuple.Create ("APT2153", "invalid resources.pb"), // lgtm [csharp/responsible-ai/ml-training-and-serialization-files-referenced] These are not the droids you are looking for. Not ML data training files.
			Tuple.Create ("APT2154", "invalid split name"),
			Tuple.Create ("APT2155", "invalid split parameter"),
			Tuple.Create ("APT2156", "invalid static library"),
			Tuple.Create ("APT2157", "invalid type name"),
			Tuple.Create ("APT2158", "Invalid value for flag --output-format"),
			Tuple.Create ("APT2159", "invalid value for 'formatted'. Must be a boolean"),
			Tuple.Create ("APT2160", "invalid value for 'translatable'. Must be a boolean"),
			Tuple.Create ("APT2161", "invalid value for type"),
			Tuple.Create ("APT2162", "invalid XML attribute"),
			Tuple.Create ("APT2163", "is an invalid format"),
			Tuple.Create ("APT2164", "is not a valid integer"),
			Tuple.Create ("APT2165", "<item> in <plural> has invalid value"),
			Tuple.Create ("APT2166", "<item> in <plurals> requires attribute"),
			Tuple.Create ("APT2167", "<item> must have a 'name' attribute"),
			Tuple.Create ("APT2168", "<item> must have a 'type' attribute"),
			Tuple.Create ("APT2169", "<item> within an <overlayable> tag must have a 'name' attribute"),
			Tuple.Create ("APT2170", "<item> within an <overlayable> tag must have a 'type' attribute"),
			Tuple.Create ("APT2171", "<manifest> must have a 'package' attribute"),
			Tuple.Create ("APT2172", "manifest must have a versionCode attribute"),
			Tuple.Create ("APT2173", "<manifest> tag is missing 'package' attribute"),
			Tuple.Create ("APT2174", "'min' and 'max' can only be used when format='integer'"),
			Tuple.Create ("APT2175", "missing '='"),
			Tuple.Create ("APT2176", "missing key string pool"),
			Tuple.Create ("APT2177", "missing minSdkVersion from <uses-sdk>"),
			Tuple.Create ("APT2178", "missing 'name' attribute"),
			Tuple.Create ("APT2179", "Missing placeholder for artifact"),
			Tuple.Create ("APT2180", "missing type string pool"),
			Tuple.Create ("APT2181", "missing <uses-sdk> from <manifest>"),
			Tuple.Create ("APT2182", "multiple default products defined for resource"),
			Tuple.Create ("APT2183", "must be an integer"),
			Tuple.Create ("APT2184", "have overlapping version-code-order attributes"),
			Tuple.Create ("APT2185", "No AndroidManifest."),
			Tuple.Create ("APT2186", "no attribute 'name' found for tag <"),
			Tuple.Create ("APT2187", "no attribute 'value' found for tag"),
			Tuple.Create ("APT2188", "no default product defined for resource"),
			Tuple.Create ("APT2189", "no definition for declared symbol"),
			Tuple.Create ("APT2190", "no file associated with"),
			Tuple.Create ("APT2191", "No label found for element "),
			Tuple.Create ("APT2192", "no <manifest> root tag defined"),
			Tuple.Create ("APT2193", "No package name."),
			Tuple.Create ("APT2194", "no package with ID 0x7f found in static library"),
			Tuple.Create ("APT2195", "no root tag defined"),
			Tuple.Create ("APT2196", "no root XML tag found"),
			Tuple.Create ("APT2197", "no suitable parent for inheriting attribute"),
			Tuple.Create ("APT2198", "not a valid png file"),
			Tuple.Create ("APT2199", "not a valid png file"),
			Tuple.Create ("APT2200", "not a valid string"),
			Tuple.Create ("APT2201", "not enough data for PNG signature"),
			Tuple.Create ("APT2202", "No version-code-order found for element"),
			Tuple.Create ("APT2203", "only one of --shared-lib, --static-lib, or --proto_format can be defined"),
			Tuple.Create ("APT2204", "Output directory is required when using a configuration file"),
			Tuple.Create ("APT2205", "<overlayable> has invalid policy"),
			Tuple.Create ("APT2206", "package 'android' can only be built as a regular app"),
			Tuple.Create ("APT2207", "package ID is too big"),
			Tuple.Create ("APT2208", "is too long"),
			Tuple.Create ("APT2209", "Placeholder present but no value for artifact"),
			Tuple.Create ("APT2210", "Placeholder present multiple times"),
			Tuple.Create ("APT2211", "plain text not allowed here"),
			Tuple.Create ("APT2212", "PNG image dimensions are too large"),
			Tuple.Create ("APT2213", "previous declaration here"),
			Tuple.Create ("APT2214", "<public-group> must have a 'first-id' attribute"),
			Tuple.Create ("APT2215", "<public-group> must have a 'type' attribute"),
			Tuple.Create ("APT2216", "<public> must have a 'name' attribute"),
			Tuple.Create ("APT2217", "<public> must have a 'type' attribute"),
			Tuple.Create ("APT2218", "resource file cannot be a directory"),
			Tuple.Create ("APT2219", "name cannot contain '.' other than for"),
			Tuple.Create ("APT2220", "has invalid entry name"),
			Tuple.Create ("APT2221", "has same ID"),
			Tuple.Create ("APT2222", "resource previously defined here"),
			Tuple.Create ("APT2223", "does not override an existing resource"),
			Tuple.Create ("APT2224", "has a conflicting value for"),
			Tuple.Create ("APT2225", "was filtered out but no product variant remains"),
			Tuple.Create ("APT2226", "ResTable_type has invalid id"),
			Tuple.Create ("APT2227", "ResTable_typeSpec has invalid id"),
			Tuple.Create ("APT2228", "ResTable_typeSpec has too many entries"),
			Tuple.Create ("APT2229", "ResTable_typeSpec too small to hold entries"),
			Tuple.Create ("APT2230", "root element must be <resources>"),
			Tuple.Create ("APT2231", "root tag must be <manifest>"),
			Tuple.Create ("APT2232", "selection of product"),
			Tuple.Create ("APT2233", "is already taken by resource"),
			Tuple.Create ("APT2234", "static library has no package"),
			Tuple.Create ("APT2235", "invalid package ID 0x%02x"),
			Tuple.Create ("APT2236", "unknown chunk of type 0x%02x"),
			Tuple.Create ("APT2237", "string too large to encode using UTF-16"),
			Tuple.Create ("APT2238", "string too large to encode using UTF-8"),
			Tuple.Create ("APT2239", "The configuration and command line to filter artifacts do not match"),
			Tuple.Create ("APT2240", "but resource already has ID"),
			Tuple.Create ("APT2241", "but package"),
			Tuple.Create ("APT2242", "but type"),
			Tuple.Create ("APT2243", "'type' is ignored within <public-group>"),
			Tuple.Create ("APT2244", "file passed as argument. Must be"),
			Tuple.Create ("APT2245", "Unexpected element in ABI group"),
			Tuple.Create ("APT2246", "Unexpected element in gl-texture element"),
			Tuple.Create ("APT2247", "Unexpected element in GL texture group"),
			Tuple.Create ("APT2248", "Unexpected root_element in device feature group"),
			Tuple.Create ("APT2249", "Unexpected root_element in screen density group"),
			Tuple.Create ("APT2250", "unknown command"),
			Tuple.Create ("APT2251", "Unknown namespace found on root element"),
			Tuple.Create ("APT2252", "unknown tag"),
			Tuple.Create ("APT2253", "<uses-feature> must have either android:name or android:glEsVersion attribute"),
			Tuple.Create ("APT2254", "xml parser error"),
			Tuple.Create ("APT2255", "versionCode is invalid"),
			Tuple.Create ("APT2256", "missing:"),
			Tuple.Create ("APT2257", "in <public-group>"),
			Tuple.Create ("APT2258", "invalid"),
			Tuple.Create ("APT2259", "is incompatible with attribute"),
			Tuple.Create ("APT2260", "not found"),
			Tuple.Create ("APT2261", "file failed to compile"),
			Tuple.Create ("APT2262", "unexpected element <activity> found in <manifest>"),
			Tuple.Create ("APT2263", "found in <manifest>"),  // unexpected element <xxxxx> found in <manifest>
			Tuple.Create ("APT2264", "The system cannot find the file specified. (2)"), // Windows Long Path error from aapt2
			Tuple.Create ("APT2265", "The system cannot find the file specified. (2)"), // Windows non-ASCII characters error from aapt2
			Tuple.Create ("APT2266", "in <data> tag has value of") //  error: attribute ‘android:path’ in <data> tag has value of ‘code/fooauth://com.foo.foo, it must start with a leading slash ‘/’.
		};
	}
}
