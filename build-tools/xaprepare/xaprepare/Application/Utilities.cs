using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	static partial class Utilities
	{
		static readonly TimeSpan ExceptionRetryInitialDelay = TimeSpan.FromSeconds (30);
		static readonly TimeSpan WebRequestTimeout = TimeSpan.FromMinutes (60);
		static readonly int ExceptionRetries = 5;

		const string MSBuildPropertyListSeparator = ":";

		static readonly List<string> tarArchiveExtensions = new List<string> {
			".tar.gz",
			".tar.bz2",
			".tar",
		};

		static readonly Log Log = Log.Instance;
		static readonly Dictionary<string, string> versionCache = new Dictionary<string, string> (StringComparer.Ordinal);

		public static readonly Encoding UTF8NoBOM = new UTF8Encoding (false);

		public static bool ParseAndroidPkgRevision (string? v, out Version? version, out string? tag)
		{
			string? ver = v?.Trim ();
			version = null;
			tag = null;
			if (String.IsNullOrEmpty (ver))
				return false;

			if (ver!.IndexOf ('.') < 0)
				ver = $"{ver}.0";

			int tagIdx = ver.IndexOf ('-');
			if (tagIdx >= 0) {
				tag = ver.Substring (tagIdx + 1);
				ver = ver.Substring (0, tagIdx - 1);
			}

			if (Version.TryParse (ver, out version))
				return true;

			return false;
		}

		public static bool AbiChoiceChanged (Context context)
		{
			string cacheFile = Configurables.Paths.MonoRuntimesEnabledAbisCachePath;
			if (!File.Exists (cacheFile)) {
				Log.DebugLine ($"Enabled ABI cache file not found at {cacheFile}");
				return true;
			}

			var oldAbis = new HashSet<string> (StringComparer.Ordinal);
			foreach (string l in File.ReadAllLines (cacheFile)) {
				string line = l.Trim ();
				if (String.IsNullOrEmpty (line) || oldAbis.Contains (line))
					continue;
				oldAbis.Add (line);
			}

			HashSet<string>? currentAbis = null;
			FillCurrentAbis (context, ref currentAbis);

			if (currentAbis == null)
				return false;

			if (oldAbis.Count != currentAbis.Count)
				return true;

			foreach (string abi in oldAbis) {
				if (!currentAbis.Contains (abi))
					return true;
			}

			return false;
		}

		public static void SaveAbiChoice (Context context)
		{
			HashSet<string>? currentAbis = null;
			FillCurrentAbis (context, ref currentAbis);

			if (currentAbis == null || currentAbis.Count == 0) {
				Log.WarningLine ("Cannot save ABI choice, no current ABIs");
				return;
			}

			string cacheFile = Configurables.Paths.MonoRuntimesEnabledAbisCachePath;
			Log.DebugLine ($"Writing ABI cache file {cacheFile}");
			File.WriteAllLines (cacheFile, currentAbis);
		}

		static void FillCurrentAbis (Context context, ref HashSet<string>? currentAbis)
		{
			Utilities.AddAbis (context.Properties.GetRequiredValue (KnownProperties.AndroidSupportedTargetJitAbis).Trim (), ref currentAbis);
			Utilities.AddAbis (context.Properties.GetRequiredValue (KnownProperties.AndroidSupportedTargetAotAbis).Trim (), ref currentAbis);
			Utilities.AddAbis (context.Properties.GetRequiredValue (KnownProperties.AndroidSupportedHostJitAbis).Trim (), ref currentAbis);
		}

		public static void PropagateXamarinAndroidCecil (Context context)
		{
			const string CecilAssembly = "Xamarin.Android.Cecil.dll";

			CopyFile (
				Path.Combine (Configurables.Paths.InstallMSBuildDir, CecilAssembly),
				Path.Combine (Configurables.Paths.ExternalJavaInteropDir, "bin", context.Configuration, CecilAssembly)
			);
		}

		public static async Task<bool> BuildRemapRef (Context context, bool haveManagedRuntime, string managedRuntime, bool quiet = false)
		{
			if (!quiet)
				Log.StatusLine ("Building remap-assembly-ref");

			var msbuild = new MSBuildRunner (context);
			string projectPath = Path.Combine (Configurables.Paths.BuildToolsDir, "remap-assembly-ref", "remap-assembly-ref.csproj");
			bool result = await msbuild.Run (
				projectPath: projectPath,
				logTag: "remap-assembly-ref",
				binlogName: "build-remap-assembly-ref"
			);

			if (!result) {
				Log.ErrorLine ("Failed to build remap-assembly-ref");
				return false;
			}

			return true;
		}

		public static string ToXamarinAndroidPropertyValue (ICollection<string> coll)
		{
			if (coll == null)
				return String.Empty;

			return String.Join (MSBuildPropertyListSeparator, coll);
		}

		public static DownloadStatus SetupDownloadStatus (Context context, ulong totalDownloadSize, bool fancyLogging)
		{
			var downloadStatus = new DownloadStatus (totalDownloadSize, (DownloadStatus status) => Utilities.LogDownloadProgress (status, fancyLogging));

			if (!fancyLogging)
				downloadStatus.UpdateIntervalMS = 60000; // No need to spam the log too much
			return downloadStatus;
		}

		public static void LogDownloadProgress (DownloadStatus status, bool fancyLogging)
		{
			int curRow = ConsoleCursorTop;
			try {
				double percentComplete = Math.Round ((status.DownloadedSoFar * 100.0) / status.TotalSize, 2);

				Utilities.FormatSize (status.DownloadedSoFar, out decimal sizeValue, out string sizeUnit);
				Utilities.FormatSpeed (status.BytesPerSecond, out decimal speedValue, out string speedUnit);

				string progress = $"{percentComplete}% ({sizeValue} {sizeUnit} at {speedValue} {speedUnit})";
				if (!fancyLogging) {
					Log.StatusLine ($"Download progress: {progress}", ConsoleColor.White);
					return;
				}
				ConsoleSetCursorPosition (0, ConsoleWindowHeight - 1);
				Log.Status ($"{progress}             ", ConsoleColor.White);
			} catch (Exception ex) {
				Log.DebugLine ($"Failed to report progress: {ex.Message}");
				Log.DebugLine (ex.ToString ());
			} finally {
				ConsoleSetCursorPosition (0, curRow);
			}
		}

		public static void AddAbis (string abis, ref HashSet <string>? collection, bool warnAboutDuplicates = true)
		{
			if (collection == null)
				collection = new HashSet <string> (StringComparer.OrdinalIgnoreCase);

			if (abis.Length == 0)
				return;

			foreach (string abi in abis.Split (Configurables.Defaults.PropertyListSeparator, StringSplitOptions.RemoveEmptyEntries)) {
				if (collection.Contains (abi)) {
					if (warnAboutDuplicates)
						Log.DebugLine ($"Duplicate ABI name: {abi}");
					continue;
				}

				collection.Add (abi);
			}
		}

		public static async Task<bool> Unpack (string fullArchivePath, string destinationDirectory, bool cleanDestinatioBeforeUnpacking = false)
		{
			if (String.IsNullOrEmpty (fullArchivePath))
				throw new ArgumentNullException ("must not be null or empty", nameof (fullArchivePath));
			if (String.IsNullOrEmpty (destinationDirectory))
				throw new ArgumentNullException ("must not be null or empty", nameof (destinationDirectory));

			if (cleanDestinatioBeforeUnpacking)
				DeleteDirectorySilent (destinationDirectory);

			CreateDirectory (destinationDirectory);

			Log.DebugLine ($"Unpacking {fullArchivePath} to directory: {destinationDirectory}");
			bool useTar = false;
			foreach (string ext in tarArchiveExtensions) {
				if (fullArchivePath.EndsWith (ext, StringComparison.OrdinalIgnoreCase)) {
					useTar = true;
					break;
				}
			}

			if (useTar) {
				var tar = new TarRunner (Context.Instance);
				return await tar.Extract (fullArchivePath, destinationDirectory);
			}

			var sevenZip = new SevenZipRunner (Context.Instance);
			return await sevenZip.Extract (fullArchivePath, destinationDirectory);
		}

		public static string ShortenGitHash (string fullHash)
		{
			if (String.IsNullOrEmpty (fullHash))
				return String.Empty;

			if (fullHash.Length <= Configurables.Defaults.AbbreviatedHashLength)
				return fullHash;

			return fullHash.Substring (0, (int)Configurables.Defaults.AbbreviatedHashLength);
		}

		public static string GetDebugSymbolsPath (string executablePath)
		{
			if (String.IsNullOrEmpty (executablePath))
				throw new ArgumentException ("must not be null or empty", nameof (executablePath));

			string extension = Context.Instance.DebugFileExtension;
			if (String.Compare (".mdb", extension, StringComparison.Ordinal) == 0)
				return Path.Combine (executablePath, extension);

			string dirName = Path.GetDirectoryName (executablePath) ?? Environment.CurrentDirectory;
			return Path.Combine (dirName, $"{Path.GetFileNameWithoutExtension (executablePath)}{extension}");
		}

		public static (bool success, string version) GetProgramVersion (string programPath)
		{
			if (String.IsNullOrEmpty (programPath))
				throw new ArgumentException ("must not be null or empty", nameof (programPath));

			if (versionCache.TryGetValue (programPath, out string? version) && version != null) {
				return (true, version);
			}

			bool fetcherPresent;

			(fetcherPresent, version) = RunVersionFetcher (programPath, programPath);
			if (fetcherPresent)
				return (IsValidVersion (), version);

			if (programPath.IndexOf (Path.DirectorySeparatorChar) >= 0) {
				(fetcherPresent, version) = RunVersionFetcher (Path.GetFileName (programPath), programPath);
				if (fetcherPresent)
					return (IsValidVersion (), version);
			}

			return (false, String.Empty);

			bool IsValidVersion ()
			{
				bool valid = !String.IsNullOrEmpty (version);
				if (!valid)
					return false;

				versionCache [programPath] = version;
				return valid;
			}
		}

		static (bool fetcherPresent, string version) RunVersionFetcher (string program, string programPath)
		{
			Log.DebugLine ($"Attempting to find version fetcher for: {program} ({programPath})");
			if (Context.Instance.VersionFetchers.Fetchers.TryGetValue (program, out ProgramVersionParser? vp) && vp != null) {
				Log.DebugLine ("Fetcher found");
				string version = vp.GetVersion (Context.Instance, programPath);
				Log.DebugLine ($"{program} version: {version}");
				return (true, version);
			}

			Log.DebugLine ("Fetcher not found");
			return (false, String.Empty);
		}

		public static void DeleteDirectory (string directoryPath, bool ignoreErrors = false, bool recurse = true)
		{
			if (String.IsNullOrEmpty (directoryPath))
				throw new ArgumentException ("must not be null or empty", nameof (directoryPath));

			if (!Directory.Exists (directoryPath))
				return;

			try {
				Log.DebugLine ($"Deleting directory recursively: {directoryPath}");
				DeleteDirectoryWithRetry (directoryPath, recurse);
			} catch (Exception ex) {
				if (ignoreErrors) {
					Log.DebugLine ($"Failed to delete directory: {directoryPath}");
					Log.DebugLine (ex.ToString ());
					return;
				}

				throw;
			}
		}

		public static void DeleteDirectorySilent (string directoryPath, bool recurse = true)
		{
			if (String.IsNullOrEmpty (directoryPath))
				return;

			DeleteDirectory (directoryPath, ignoreErrors: true, recurse: true);
		}

		public static void DeleteFile (string filePath, bool ignoreErrors = false)
		{
			if (String.IsNullOrEmpty (filePath))
				throw new ArgumentException ("must not be null or empty", nameof (filePath));

			// In this case we don't care if the file is a dangling symlink or not (which FileExists checks for) - we
			// want to remove whatever exists.
			if (!File.Exists (filePath))
				return;

			try {
				Log.DebugLine ($"Deleting file: {filePath}");
				File.Delete (filePath);
			} catch (Exception ex) {
				if (ignoreErrors) {
					Log.DebugLine ($"Failed to delete file: {filePath}");
					Log.DebugLine (ex.ToString ());
					return;
				}

				throw;
			}
		}

		public static void DeleteFileSilent (string filePath)
		{
			if (String.IsNullOrEmpty (filePath))
				return;

			DeleteFile (filePath, true);
		}

		public static void CreateDirectory (string? directoryPath)
		{
			if (String.IsNullOrEmpty (directoryPath))
				throw new ArgumentException ("must not be null or empty", nameof (directoryPath));

			if (Directory.Exists (directoryPath))
				return;

			Log.DebugLine ($"Creating directory: {directoryPath}");
			Directory.CreateDirectory (directoryPath);
		}

		public static void CopyFilesSimple (IEnumerable<string> sourceFiles, string destinationDirectory, bool overwriteDestinationFile = true)
		{
			if (sourceFiles == null)
				throw new ArgumentNullException (nameof (sourceFiles));
			if (String.IsNullOrEmpty (destinationDirectory))
				throw new ArgumentException ("must not be null or empty", nameof (destinationDirectory));

			CreateDirectory (destinationDirectory);
			foreach (string src in sourceFiles) {
				CopyFileInternal (src, destinationDirectory, destinationFileName: String.Empty, overwriteDestinationFile: overwriteDestinationFile);
			}
		}

		public static void CopyFile (string sourceFilePath, string destinationFilePath, bool overwriteDestinationFile = true)
		{
			if (String.IsNullOrEmpty (sourceFilePath))
				throw new ArgumentException ("must not be null or empty", nameof (sourceFilePath));
			if (String.IsNullOrEmpty (destinationFilePath))
				throw new ArgumentException ("must not be null or empty", nameof (destinationFilePath));

			CopyFileInternal (sourceFilePath, Path.GetDirectoryName (destinationFilePath), Path.GetFileName (destinationFilePath), overwriteDestinationFile);
		}

		public static void CopyFileToDir (string sourceFilePath, string destinationDirectory, string? destinationFileName = null, bool overwriteDestinationFile = true)
		{
			if (String.IsNullOrEmpty (sourceFilePath))
				throw new ArgumentException ("must not be null or empty", nameof (sourceFilePath));
			if (String.IsNullOrEmpty (destinationDirectory))
				throw new ArgumentException ("must not be null or empty", nameof (destinationDirectory));

			CreateDirectory (destinationDirectory);
			CopyFileInternal (sourceFilePath, destinationDirectory, destinationFileName ?? String.Empty, overwriteDestinationFile);
		}

		static void CopyFileInternal (string? sourceFilePath, string? destinationDirectory, string? destinationFileName, bool overwriteDestinationFile)
		{
			if (String.IsNullOrEmpty (sourceFilePath)) {
				throw new ArgumentException ("must not be null or empty", nameof (sourceFilePath));
			}

			if (String.IsNullOrEmpty (destinationDirectory)) {
				throw new ArgumentException ("must not be null or empty", nameof (destinationDirectory));
			}

			string targetFileName;
			if (String.IsNullOrEmpty (destinationFileName))
				targetFileName = Path.GetFileName (sourceFilePath);
			else
				targetFileName = destinationFileName!;

			if (!FileExists (sourceFilePath!))
				throw new InvalidOperationException ($"Location '{sourceFilePath}' does not point to a file");

			CreateDirectory (destinationDirectory);
			string destFile = Path.Combine (destinationDirectory, targetFileName);
			Log.DebugLine ($"Copying file '{sourceFilePath}' to '{destFile}' (overwrite destination: {overwriteDestinationFile})");
			File.Copy (sourceFilePath, destFile, overwriteDestinationFile);
		}

		public static void FormatSpeed (ulong bytesPerSecond, out decimal value, out string unit)
		{
			SizeFormatter.FormatBytes (bytesPerSecond, out value, out unit);
			value = SignificantDigits (value, 3);
			unit += "/s";
		}

		public static void FormatSize (ulong dSize, out decimal value, out string unit)
		{
			SizeFormatter.FormatBytes (dSize, out value, out unit);
			value = SignificantDigits (value, 3);
		}

		public static string SizeToString (long dSize)
		{
			Utilities.FormatSize ((ulong)dSize, out decimal value, out string unit);
			return $"{value}{unit}";
		}

		// Creates numbers with maxDigitCount significant digits or less
		static decimal SignificantDigits (decimal number, int maxDigitCount)
		{
			decimal n = number;

			while (n > 1 && maxDigitCount > 0) {
				maxDigitCount--;
				n = n / 10;
			}

			return Math.Round (number, maxDigitCount);
		}

		public static async Task<(bool success, ulong size)> GetDownloadSize (Uri url)
		{
			(bool success, ulong size, HttpStatusCode _) = await GetDownloadSizeWithStatus (url);
			return (success, size);
		}

		public static HttpClient CreateHttpClient ()
		{
			var handler = new HttpClientHandler {
				CheckCertificateRevocationList = true,
			};

			return new HttpClient (handler);
		}

		public static async Task<(bool success, ulong size, HttpStatusCode status)> GetDownloadSizeWithStatus (Uri url)
		{
			TimeSpan delay = ExceptionRetryInitialDelay;
			for (int i = 0; i < ExceptionRetries; i++) {
				try {
					using (HttpClient httpClient = CreateHttpClient ()) {
						httpClient.Timeout = WebRequestTimeout;
						HttpResponseMessage resp = await httpClient.GetAsync (url, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait (true);
						if (!resp.IsSuccessStatusCode || !resp.Content.Headers.ContentLength.HasValue)
							return (false, 0, resp.StatusCode);

						return (true, (ulong) resp.Content.Headers.ContentLength.Value, resp.StatusCode);
					}
				} catch (Exception ex) {
					if (i < ExceptionRetries - 1) {
						WaitAWhile ($"GetDownloadSize {url}", i, ref ex, ref delay);
					}
				}
			}
			return (false, 0, HttpStatusCode.InternalServerError);
		}

		public static async Task<bool> Download (Uri url, string targetFile, DownloadStatus status)
		{
			if (url == null)
				throw new ArgumentNullException (nameof (url));
			if (String.IsNullOrEmpty (targetFile))
				throw new ArgumentException ("must not be null or empty", nameof (targetFile));
			if (status == null)
				throw new ArgumentNullException (nameof (status));

			TimeSpan delay = ExceptionRetryInitialDelay;
			bool succeeded = false;
			for (int i = 0; i < ExceptionRetries; i++) {
				try {
					await DoDownload (url, targetFile, status);
					succeeded = true;
					break;
				} catch (Exception ex) {
					if (i < ExceptionRetries - 1) {
						WaitAWhile ($"Download {url}", i, ref ex, ref delay);
					}
				}
			}

			return succeeded;
		}

		static async Task DoDownload (Uri url, string targetFile, DownloadStatus status)
		{
			using (HttpClient httpClient = CreateHttpClient ()) {
				httpClient.Timeout = WebRequestTimeout;
				Log.DebugLine ("Calling GetAsync");
				HttpResponseMessage resp = await httpClient.GetAsync (url, HttpCompletionOption.ResponseHeadersRead);
				Log.DebugLine ("GetAsync finished");

				resp.EnsureSuccessStatusCode ();
				string? dir = Path.GetDirectoryName (targetFile);
				CreateDirectory (dir);
				using (var fs = File.Open (targetFile, FileMode.Create, FileAccess.Write)) {
					using (var webStream = await resp.Content.ReadAsStreamAsync ()) {
						status.Start ();
						WriteWithProgress (fs, webStream, status);
					}
				}
			}
		}

		static void WriteWithProgress (FileStream fs, Stream webStream, DownloadStatus status)
		{
			var buf = new byte [16384];
			int nread;

			Log.DebugLine ("Downloading...");
			while ((nread = webStream.Read (buf, 0, buf.Length)) > 0) {
				status.Update ((ulong)nread);
				fs.Write (buf, 0, nread);
			}

			fs.Flush ();
		}

		public static void MoveDirectoryContentsRecursively (string sourceDir, string destinationDir, bool cleanDestinationDir = true, bool resetFileTimestamp = false, bool ignoreDeletionErrors = false)
		{
			if (String.IsNullOrEmpty (sourceDir))
				throw new ArgumentException ("must not be null or empty", nameof (sourceDir));
			if (String.IsNullOrEmpty (destinationDir))
				throw new ArgumentException ("must not be null or empty", nameof (destinationDir));

			if (!Path.IsPathRooted (sourceDir))
				sourceDir = Path.GetFullPath (sourceDir);

			if (!Path.IsPathRooted (destinationDir))
				destinationDir = Path.Combine (sourceDir, destinationDir);

			if (sourceDir.Equals (destinationDir, Context.Instance.OS.DefaultStringComparison))
				return;

			if (!Directory.Exists (sourceDir))
				throw new InvalidOperationException ($"Source directory {sourceDir} does not exist");

			if (cleanDestinationDir && Directory.Exists (destinationDir))
				DeleteDirectoryWithRetry (destinationDir, true);

			Log.DebugLine ($"Moving directory {sourceDir} to {destinationDir}");
			MoveDirectory (sourceDir, destinationDir, resetFileTimestamp);
			try {
				DeleteDirectoryWithRetry (sourceDir, true);
			} catch (Exception ex) {
				if (!ignoreDeletionErrors)
					throw;

				Log.DebugLine ($"Attempt to recursively delete directory {sourceDir} failed. Error was ignored, there might be some files left behind.");
				Log.DebugLine (ex.ToString ());
			}
		}

		static void MoveDirectory (string sourceDir, string destinationDir, bool resetFileTimestamp)
		{
			foreach (string entry in Directory.EnumerateFileSystemEntries (sourceDir)) {
				if (entry.Equals (destinationDir, Context.Instance.OS.DefaultStringComparison))
					continue;

				string destination;
				if (Directory.Exists (entry)) {
					destination = Path.Combine (destinationDir, Path.GetFileName (entry));
					if (File.Exists (destination))
						throw new InvalidOperationException ($"Destination '{destination}' exists and is not a directory");
					Log.DebugLine ($"Creating directory: {destination}");
					CreateDirectory (destination);
					MoveDirectory (entry, destination, resetFileTimestamp);
					continue;
				}

				destination = Path.Combine (destinationDir, Path.GetFileName (entry));
				string? dir = Path.GetDirectoryName (destination);
				CreateDirectory (dir);
				MoveFileWithRetry (entry, destination, resetFileTimestamp);
			}
		}

		public static void TouchFileWithRetry (string filePath)
		{
			TouchFileWithRetry (filePath, DateTime.UtcNow);
		}

		public static void TouchFileWithRetry (string filePath, DateTime stamp)
		{
			if (!File.Exists (filePath))
				return;

			Log.DebugLine ($"Resetting timestamp of {filePath}");
			TimeSpan delay = ExceptionRetryInitialDelay;
			Exception? ex = null;

			for (int i = 0; i < ExceptionRetries; i++) {
				try {
					// Don't attempt to set write/access time on linked files.
					var destFileInfo = new FileInfo (filePath);
					if (!destFileInfo.Attributes.HasFlag (FileAttributes.ReparsePoint)) {
						File.SetLastWriteTimeUtc (filePath, stamp);
					}
					break;
				} catch (Exception e) {
					ex = e;
				}
				WaitAWhile ($"Reset timestamp for {filePath}", i, ref ex, ref delay);
			}

			if (ex != null) {
				// No need to throw, timestamp reset is not critical
				Log.WarningLine ($"Failed to reset timestamp for file {filePath}");
				Log.WarningLine ($"Exception {ex.GetType()} thrown: {ex.Message}");
			}
		}

		public static void MoveFileWithRetry (string source, string destination, bool resetFileTimestamp = false)
		{
			TimeSpan delay = ExceptionRetryInitialDelay;
			Exception? ex = null;

			Log.DebugLine ($"Moving '{source}' to '{destination}'");
			for (int i = 0; i < ExceptionRetries; i++) {
				try {
					FileMove (source, destination);
					break;
				} catch (Exception e) {
					ex = e;
				}
				WaitAWhile ($"File move ({source} -> {destination})", i, ref ex, ref delay);
			}

			if (ex != null)
				throw ex;

			if (!resetFileTimestamp)
				return;

			TouchFileWithRetry (destination);
		}

		static void ResetFilePermissions (string directoryPath)
		{
			foreach (string file in Directory.EnumerateFiles (directoryPath, "*", SearchOption.AllDirectories)) {
				FileAttributes attrs = File.GetAttributes (file);
				if ((attrs & (FileAttributes.Hidden | FileAttributes.ReadOnly | FileAttributes.System)) == 0)
					continue;

				File.SetAttributes (file, FileAttributes.Normal);
			}
		}

		public static void DeleteDirectoryWithRetry (string directoryPath, bool recursive)
		{
			TimeSpan delay = ExceptionRetryInitialDelay;
			Exception? ex = null;
			bool tryResetFilePermissions = false;

			for (int i = 0; i < ExceptionRetries; i++) {
				ex = null;
				try {
					if (tryResetFilePermissions) {
						tryResetFilePermissions = false;
						ResetFilePermissions (directoryPath);
					}
					Log.DebugLine ($"Deleting directory {directoryPath} (recursively? {recursive})");
					Directory.Delete (directoryPath, recursive);
					return;
				} catch (IOException e) {
					ex = e;
				} catch (UnauthorizedAccessException e) {
					ex = e;
					tryResetFilePermissions = true;
				}

				WaitAWhile ($"Directory {directoryPath} deletion", i, ref ex, ref delay);
			}

			if (ex != null)
				throw ex;
		}

		static void WaitAWhile (string what, int which, ref Exception ex, ref TimeSpan delay)
		{
			Log.DebugLine ($"{what} attempt no. {which + 1} failed, retrying after delay of {delay}");
			if (ex != null)
				Log.DebugLine ($"Failure cause: {ex.Message}");
			Thread.Sleep (delay);
			delay = TimeSpan.FromMilliseconds (delay.TotalMilliseconds * 2);
		}

		public static List<Type> GetTypesWithCustomAttribute<T> (Assembly? assembly = null) where T: System.Attribute
		{
			Assembly asm = assembly ?? typeof (Utilities).Assembly;

			var types = new List<Type> ();
			foreach (Type type in asm.GetTypes ()) {
				if (type.GetCustomAttribute<T> (true) == null)
					continue;

				types.Add (type);
			}

			return types;
		}

		public static StreamWriter OpenStreamWriter (string outputPath, bool append = false, Encoding? encoding = null)
		{
			return new StreamWriter (OpenFileForWrite (outputPath, append), encoding ?? UTF8NoBOM);
		}

		public static FileStream OpenFileForWrite (string outputPath, bool append = true)
		{
			if (String.IsNullOrEmpty (outputPath))
				throw new ArgumentException ("must not be null or empty", nameof (outputPath));

			string? dir = Path.GetDirectoryName (outputPath);
			CreateDirectory (dir);

			if (File.Exists (outputPath))
				return File.Open (outputPath, append ? FileMode.Append : FileMode.Truncate, FileAccess.Write);

			return File.Open (outputPath, FileMode.Create, FileAccess.Write);
		}

		public static bool RunCommand (string command, params string[] arguments)
		{
			return RunCommand (command, workingDirectory: null, echoStderr: true, ignoreEmptyArguments: false, arguments: arguments);
		}

		public static bool RunCommand (string command, bool ignoreEmptyArguments, params string[] arguments)
		{
			return RunCommand (command, workingDirectory: null, echoStderr: true, ignoreEmptyArguments: ignoreEmptyArguments, arguments: arguments);
		}

		public static bool RunCommand (string command, string workingDirectory, bool ignoreEmptyArguments, params string[] arguments)
		{
			return RunCommand (command, workingDirectory, echoStderr: true, ignoreEmptyArguments: ignoreEmptyArguments, arguments: arguments);
		}

		public static bool RunCommand (string command, string? workingDirectory, bool echoStderr, bool ignoreEmptyArguments, params string[] arguments)
		{
			if (String.IsNullOrEmpty (command))
				throw new ArgumentException ("must not be null or empty", nameof (command));

			var runner = new ProcessRunner (command, ignoreEmptyArguments, arguments) {
				EchoStandardError = echoStderr,
				WorkingDirectory = workingDirectory,
			};

			return runner.Run ();
		}

		public static bool RunManagedCommand (string command, string workingDirectory, bool ignoreEmptyArguments, params string [] arguments)
		{
			return RunManagedCommand (command, workingDirectory, echoStderr: true, ignoreEmptyArguments: ignoreEmptyArguments, arguments: arguments);
		}

		// This is a managed assembly that needs to be run as 'dotnet foo.dll'
		public static bool RunManagedCommand (string command, string? workingDirectory, bool echoStderr, bool ignoreEmptyArguments, params string [] arguments)
		{
			if (string.IsNullOrEmpty (command))
				throw new ArgumentException ("must not be null or empty", nameof (command));

			var runner = new ProcessRunner ("dotnet", ignoreEmptyArguments, new [] { command }.Concat (arguments).ToArray ()) {
				EchoStandardError = echoStderr,
				WorkingDirectory = workingDirectory,
			};

			return runner.Run ();
		}

		public static string GetStringFromStdout (string command, params string?[] arguments)
		{
			return GetStringFromStdout (command, throwOnErrors: false, trimTrailingWhitespace: true, arguments: arguments);
		}

		public static string GetStringFromStdout (string command, bool throwOnErrors, params string?[] arguments)
		{
			return GetStringFromStdout (command, throwOnErrors, trimTrailingWhitespace: true, arguments: arguments);
		}

		public static string GetStringFromStdout (string command, bool throwOnErrors, bool trimTrailingWhitespace, params string?[] arguments)
		{
			return GetStringFromStdout (new ProcessRunner (command, arguments), throwOnErrors, trimTrailingWhitespace);
		}

		public static string GetStringFromStdout (string command, bool throwOnErrors, bool trimTrailingWhitespace, bool quietErrors, params string?[] arguments)
		{
			return GetStringFromStdout (new ProcessRunner (command, arguments), throwOnErrors, trimTrailingWhitespace, quietErrors);
		}

		public static string GetStringFromStdout (ProcessRunner runner, bool throwOnErrors = false, bool trimTrailingWhitespace = true, bool quietErrors = false)
		{
			using (var sw = new StringWriter ()) {
				runner.AddStandardOutputSink (sw);
				if (!runner.Run ()) {
					LogError ("did not exit cleanly");
					return String.Empty;
				}

				if (runner.ExitCode != 0) {
					LogError ($"failed with exit code {runner.ExitCode}");
					return String.Empty;
				}

				string ret = sw.ToString ();
				if (trimTrailingWhitespace)
					return ret.TrimEnd ();
				return ret;
			}

			void LogError (string message)
			{
				string msg = $"{runner.FullCommandLine}: {message}";
				if (throwOnErrors)
					throw new InvalidOperationException (msg);
				if (quietErrors)
					Log.DebugLine (msg);
				else
					Log.ErrorLine (msg);
			}
		}

		public static string GetRelativePath (string relativeTo, string path)
		{
			return Xamarin.Android.Tools.PathUtil.GetRelativePath (relativeTo, path, Context.Instance.OS.DefaultStringComparison);
		}
	}
}
