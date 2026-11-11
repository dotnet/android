// Copyright (C) 2011 Xamarin, Inc. All rights reserved.
#nullable enable

using System;
using System.IO;
using System.Threading;
using System.Text;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Xamarin.Android.Tools;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks {

	public abstract class Aapt2 : AsyncTask {

		private const int MAX_PATH = 260;
		private static readonly int DefaultMaxAapt2Daemons = 6;
		Dictionary<string, string>? _resource_name_case_map;

		Dictionary<string, string> resource_name_case_map => _resource_name_case_map ??= MonoAndroidHelper.ItemGroupToCaseMap (ResourceCaseMap);

		public ITaskItem [] ResourceCaseMap { get; set; } = [];

		protected virtual int ProcessorCount => Environment.ProcessorCount;

		public int DaemonMaxInstanceCount { get; set; }

		public bool DaemonKeepInDomain { get; set; }

		public ITaskItem []? ResourceDirectories { get; set; }

		public ITaskItem? AndroidManifestFile { get; set; }

		public string? SupportedOSPlatformVersion { get; set; }

		public string? ResourceSymbolsTextFile { get; set; }

		protected string ToolName { get { return OS.IsWindows ? "aapt2.exe" : "aapt2"; } }

		public string? ToolPath { get; set; }

		public string? ToolExe { get; set; }

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
			return Path.Combine (ToolPath, ToolExe.IsNullOrEmpty () ? ToolName : ToolExe);
		}

		protected virtual int GetRequiredDaemonInstances ()
		{
			return 1;
		}

		// This is set in Execute()
		Aapt2Daemon daemon = null!;

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
			if (singleLine.IsNullOrEmpty ())
				return true;

			var match = AndroidRunToolTask.AndroidErrorRegex.Match (singleLine.Trim ());
			string file = "";

			if (match.Success) {
				file = match.Groups ["file"].Value;
				int line = 0;
				if (!match.Groups ["line"]?.Value.IsNullOrEmpty () == true)
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
						if (!newfile.IsNullOrEmpty ()) {
							file = newfile;
							break;
						}
					}
				}

				bool manifestError = false;
				if (AndroidManifestFile != null && string.Equals (Path.GetFileName (file), Path.GetFileName (AndroidManifestFile.ItemSpec), StringComparison.OrdinalIgnoreCase)) {
					manifestError = true;
				}

				// Strip any "Error:" text from aapt's output
				if (message.StartsWith ("error: ", StringComparison.InvariantCultureIgnoreCase))
					message = message.Substring ("error: ".Length);

				if (level.Contains ("error") || (line != 0 && !file.IsNullOrEmpty ())) {
					var errorCode = GetErrorCodeForFile (message, file);
					if (manifestError)
						LogCodedError (errorCode, AndroidManifestFile?.ItemSpec ?? "", 0, string.Format (Xamarin.Android.Tasks.Properties.Resources.AAPTManifestError, message.TrimEnd('.')));
					else
						LogCodedError (errorCode, file, line, AddAdditionalErrorText (errorCode, message));
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
			foreach (var (code, pattern) in error_codes)
				if (message.IndexOf (pattern, StringComparison.OrdinalIgnoreCase) >= 0)
					return code;

			return "APT2000";
		}

		static readonly (string Code, string Pattern) [] error_codes = [
			("APT2001", "already defined a <flag>"),
			("APT2002", "already defined an <enum>"),
			("APT2003", "can't map package ID"),
			("APT2004", "android:name in <uses-feature> must not be empty"),
			("APT2005", "android-sdk is missing minSdkVersion attribute"),
			("APT2006", "Artifact does not have a name and no global name template defined"),
			("APT2007", "Asked to print artifacts without providing a configurations"),
			("APT2008", "tag must be a valid Java class name"),
			("APT2009", "tag must be a valid Java package name"),
			("APT2010", "tag must not be empty"),
			("APT2011", "attribute coreApp must be a boolean"),
			("APT2012", "attribute 'featureSplit' used in <manifest> but 'android:isFeatureSplit"),
			("APT2013", "attribute 'package' in <manifest> tag is not a valid Android package name"),
			("APT2014", "attribute 'package' in <manifest> tag must not be a reference"),
			("APT2015", "attribute 'split' in <manifest> tag is not a"),
			("APT2016", "<attr> tag must have a 'name' attribute"),
			("APT2017", "<bag> must have a 'type' attribute"),
			("APT2018", "can not define a <flag>"),
			("APT2019", "can not define an <enum>"),
			("APT2020", "cannot define both android:name and android:glEsVersion"),
			("APT2021", "cannot merge entry"),
			("APT2022", "cannot merge type"),
			("APT2023", "can't assign ID"),
			("APT2024", "can't extract text into its own resource"),
			("APT2025", "can't include static library when not building a static lib"),
			("APT2026", "can't specify --package-id when not building a regular app"),
			("APT2027", "target split ambiguous"),
			("APT2028", "corrupt resources.arsc"),
			("APT2029", "corrupt resource table"),
			("APT2030", "corrupt ResTable_header chunk"),
			("APT2031", "corrupt ResTable_package chunk"),
			("APT2032", "corrupt ResTable_package"),
			("APT2033", "corrupt ResTable_type chunk"),
			("APT2034", "corrupt ResTable_typeSpec chunk"),
			("APT2035", "Could not determine split APK artifact name"),
			("APT2036", "Could not find the root element in the XML document"),
			("APT2037", "could not identify format of APK"),
			("APT2038", "Could not lookup required ABIs"),
			("APT2039", "Could not lookup required Android SDK version"),
			("APT2040", "Could not lookup required device features"),
			("APT2041", "Could not lookup required locales"),
			("APT2042", "Could not lookup required OpenGL texture formats"),
			("APT2043", "Could not lookup required screen densities"),
			("APT2044", "Could not parse ABI value"),
			("APT2045", "could not parse array item"),
			("APT2046", "Could not parse config descriptor for empty screen-density-group"),
			("APT2047", "Could not parse config descriptor for screen-density"),
			("APT2048", "Could not parse config file"),
			("APT2049", "could not parse style item"),
			("APT2050", "Could not process XML document"),
			("APT2051", "could not update AndroidManifest.xml for output artifact"),
			("APT2052", "could not validate post processing configuration"),
			("APT2053", "for external package"),
			("APT2054", "duplicate overlayable declaration for resource"),
			("APT2055", "duplicate quantity"),
			("APT2056", "duplicate symbol"),
			("APT2057", "duplicate value for resource"),
			("APT2058", "empty symbol"),
			("APT2059", "failed assigning IDs"),
			("APT2060", "failed deduping resources"),
			("APT2061", "failed linking file resources"),
			("APT2062", "failed linking references"),
			("APT2063", "failed moving private attributes"),
			("APT2064", "failed opening zip"),
			("APT2065", "failed parsing input"),
			("APT2066", "failed parsing overlays"),
			("APT2067", "failed processing manifest"),
			("APT2068", "failed reading png"),
			("APT2069", "failed reading stable ID file"),
			("APT2070", "failed removing resources with no defaults"),
			("APT2071", "failed stripping products"),
			("APT2072", "failed to allocate info ptr"),
			("APT2073", "failed to allocate read ptr"),
			("APT2074", "failed to allocate write info ptr"),
			("APT2075", "failed to allocate write ptr"),
			("APT2076", "failed to copy file"),
			("APT2077", "failed to create archive"),
			("APT2078", "failed to create directory"),
			("APT2079", "failed to create libpng read png_info"),
			("APT2080", "failed to create libpng read png_struct"),
			("APT2081", "failed to create libpng write png_info"),
			("APT2082", "failed to create libpng write png_struct"),
			("APT2083", "failed to create Split AndroidManifest.xml"),
			("APT2084", "failed to deserialize proto XML"),
			("APT2085", "failed to deserialize proto"),
			("APT2086", "failed to deserialize resource table"),
			("APT2087", "failed to extract data from AndroidManifest.xml"),
			("APT2088", "failed to find"),
			("APT2089", "failed to finish entry"),
			("APT2090", "failed to finish writing data"),
			("APT2091", "failed to flatten resource table"),
			("APT2092", "failed to load APK"),
			("APT2093", "failed to load include path"),
			("APT2094", "failed to merge resource table"),
			("APT2095", "failed to mmap file"),
			("APT2096", "failed to open APK"),
			("APT2097", "failed to open directory"),
			("APT2098", "failed to open file"),
			("APT2099", "failed to open resources.arsc"),
			("APT2100", "failed to open resources.pb"), // lgtm [csharp/responsible-ai/ml-training-and-serialization-files-referenced] These are not the droids you are looking for. Not ML data training files.
			("APT2101", "failed to open"),
			("APT2102", "failed to parse binary XML"),
			("APT2103", "failed to parse binary"),
			("APT2104", "failed to parse file as binary XML"),
			("APT2105", "failed to parse file as proto XML"),
			("APT2106", "failed to parse proto XML"),
			("APT2107", "failed to parse table"),
			("APT2108", "Failed to parse the output artifact list"),
			("APT2109", "failed to parse value for resource"),
			("APT2110", "failed to parse whitelist from config file"),
			("APT2111", "failed to read compiled header"),
			("APT2112", "failed to read proto"),
			("APT2113", "failed to read"),
			("APT2114", "Failed to rewrite"),
			("APT2115", "failed to serialize AndroidManifest.xml"),
			("APT2116", "failed to serialize file"),
			("APT2117", "failed to serialize the resource table"),
			("APT2118", "failed to serialize to binary XML"),
			("APT2119", "failed to serialize to proto XML"),
			("APT2120", "Failed to strip versioned resources"),
			("APT2121", "failed to write entry data"),
			("APT2122", "failed to write png"),
			("APT2123", "failed to write resource table"),
			("APT2124", "failed to write"),
			("APT2125", "failed versioning styles"),
			("APT2126", "file not found"),
			("APT2127", "files given but --dir specified"),
			("APT2128", "file signature does not match PNG signature"),
			("APT2129", "flattening failed"),
			("APT2130", "for configuration"),
			("APT2131", "'id' is ignored within <public-group>"),
			("APT2132", "illegal nested XLIFF 'g' tag"),
			("APT2133", "incompatible package"),
			("APT2134", "in <item> within an <overlayable>"),
			("APT2135", "inline XML resources must have a single root"),
			("APT2136", "inner element must either be a resource reference or empty"),
			("APT2137", "in <public>"),
			("APT2138", "invalid android:minSdkVersion"),
			("APT2139", "invalid android:revisionCode"),
			("APT2140", "invalid android:versionCode"),
			("APT2141", "Invalid attribute:"),
			("APT2142", "invalid config"),
			("APT2143", "invalid density"),
			("APT2144", "invalid file path"),
			("APT2145", "invalid Java identifier"),
			("APT2146", "invalid 'max' value"),
			("APT2147", "invalid 'min' value"),
			("APT2148", "invalid namespace prefix"),
			("APT2149", "invalid package name"),
			("APT2150", "invalid preferred density"),
			("APT2151", "invalid resource ID"),
			("APT2152", "invalid resource name"),
			("APT2153", "invalid resources.pb"), // lgtm [csharp/responsible-ai/ml-training-and-serialization-files-referenced] These are not the droids you are looking for. Not ML data training files.
			("APT2154", "invalid split name"),
			("APT2155", "invalid split parameter"),
			("APT2156", "invalid static library"),
			("APT2157", "invalid type name"),
			("APT2158", "Invalid value for flag --output-format"),
			("APT2159", "invalid value for 'formatted'. Must be a boolean"),
			("APT2160", "invalid value for 'translatable'. Must be a boolean"),
			("APT2161", "invalid value for type"),
			("APT2162", "invalid XML attribute"),
			("APT2163", "is an invalid format"),
			("APT2164", "is not a valid integer"),
			("APT2165", "<item> in <plural> has invalid value"),
			("APT2166", "<item> in <plurals> requires attribute"),
			("APT2167", "<item> must have a 'name' attribute"),
			("APT2168", "<item> must have a 'type' attribute"),
			("APT2169", "<item> within an <overlayable> tag must have a 'name' attribute"),
			("APT2170", "<item> within an <overlayable> tag must have a 'type' attribute"),
			("APT2171", "<manifest> must have a 'package' attribute"),
			("APT2172", "manifest must have a versionCode attribute"),
			("APT2173", "<manifest> tag is missing 'package' attribute"),
			("APT2174", "'min' and 'max' can only be used when format='integer'"),
			("APT2175", "missing '='"),
			("APT2176", "missing key string pool"),
			("APT2177", "missing minSdkVersion from <uses-sdk>"),
			("APT2178", "missing 'name' attribute"),
			("APT2179", "Missing placeholder for artifact"),
			("APT2180", "missing type string pool"),
			("APT2181", "missing <uses-sdk> from <manifest>"),
			("APT2182", "multiple default products defined for resource"),
			("APT2183", "must be an integer"),
			("APT2184", "have overlapping version-code-order attributes"),
			("APT2185", "No AndroidManifest."),
			("APT2186", "no attribute 'name' found for tag <"),
			("APT2187", "no attribute 'value' found for tag"),
			("APT2188", "no default product defined for resource"),
			("APT2189", "no definition for declared symbol"),
			("APT2190", "no file associated with"),
			("APT2191", "No label found for element "),
			("APT2192", "no <manifest> root tag defined"),
			("APT2193", "No package name."),
			("APT2194", "no package with ID 0x7f found in static library"),
			("APT2195", "no root tag defined"),
			("APT2196", "no root XML tag found"),
			("APT2197", "no suitable parent for inheriting attribute"),
			("APT2198", "not a valid png file"),
			("APT2199", "not a valid png file"),
			("APT2200", "not a valid string"),
			("APT2201", "not enough data for PNG signature"),
			("APT2202", "No version-code-order found for element"),
			("APT2203", "only one of --shared-lib, --static-lib, or --proto_format can be defined"),
			("APT2204", "Output directory is required when using a configuration file"),
			("APT2205", "<overlayable> has invalid policy"),
			("APT2206", "package 'android' can only be built as a regular app"),
			("APT2207", "package ID is too big"),
			("APT2208", "is too long"),
			("APT2209", "Placeholder present but no value for artifact"),
			("APT2210", "Placeholder present multiple times"),
			("APT2211", "plain text not allowed here"),
			("APT2212", "PNG image dimensions are too large"),
			("APT2213", "previous declaration here"),
			("APT2214", "<public-group> must have a 'first-id' attribute"),
			("APT2215", "<public-group> must have a 'type' attribute"),
			("APT2216", "<public> must have a 'name' attribute"),
			("APT2217", "<public> must have a 'type' attribute"),
			("APT2218", "resource file cannot be a directory"),
			("APT2219", "name cannot contain '.' other than for"),
			("APT2220", "has invalid entry name"),
			("APT2221", "has same ID"),
			("APT2222", "resource previously defined here"),
			("APT2223", "does not override an existing resource"),
			("APT2224", "has a conflicting value for"),
			("APT2225", "was filtered out but no product variant remains"),
			("APT2226", "ResTable_type has invalid id"),
			("APT2227", "ResTable_typeSpec has invalid id"),
			("APT2228", "ResTable_typeSpec has too many entries"),
			("APT2229", "ResTable_typeSpec too small to hold entries"),
			("APT2230", "root element must be <resources>"),
			("APT2231", "root tag must be <manifest>"),
			("APT2232", "selection of product"),
			("APT2233", "is already taken by resource"),
			("APT2234", "static library has no package"),
			("APT2235", "invalid package ID 0x%02x"),
			("APT2236", "unknown chunk of type 0x%02x"),
			("APT2237", "string too large to encode using UTF-16"),
			("APT2238", "string too large to encode using UTF-8"),
			("APT2239", "The configuration and command line to filter artifacts do not match"),
			("APT2240", "but resource already has ID"),
			("APT2241", "but package"),
			("APT2242", "but type"),
			("APT2243", "'type' is ignored within <public-group>"),
			("APT2244", "file passed as argument. Must be"),
			("APT2245", "Unexpected element in ABI group"),
			("APT2246", "Unexpected element in gl-texture element"),
			("APT2247", "Unexpected element in GL texture group"),
			("APT2248", "Unexpected root_element in device feature group"),
			("APT2249", "Unexpected root_element in screen density group"),
			("APT2250", "unknown command"),
			("APT2251", "Unknown namespace found on root element"),
			("APT2252", "unknown tag"),
			("APT2253", "<uses-feature> must have either android:name or android:glEsVersion attribute"),
			("APT2254", "xml parser error"),
			("APT2255", "versionCode is invalid"),
			("APT2256", "missing:"),
			("APT2257", "in <public-group>"),
			("APT2258", "invalid"),
			("APT2259", "is incompatible with attribute"),
			("APT2260", "not found"),
			("APT2261", "file failed to compile"),
			("APT2262", "unexpected element <activity> found in <manifest>"),
			("APT2263", "found in <manifest>"),  // unexpected element <xxxxx> found in <manifest>
			("APT2264", "The system cannot find the file specified. (2)"), // Windows Long Path error from aapt2
			("APT2265", "The system cannot find the file specified. (2)"), // Windows non-ASCII characters error from aapt2
			("APT2266", "in <data> tag has value of") //  error: attribute ‘android:path’ in <data> tag has value of ‘code/fooauth://com.foo.foo, it must start with a leading slash ‘/’.
		];
	}
}
