#nullable disable
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Android.Build.Tasks;
using Xamarin.Android.Tools;
using Xamarin.Installer.Build.Tasks.Properties;
using System.Threading;

namespace Xamarin.Installer.Build.Tasks
{
	/// <summary>
	/// Installs the JDK from Microsoft OpenJDK and the Android SDK packages with Google's Android CLI.
	/// The Android CLI is downloaded from Google and its SHA-256 is checked against the pins in Xamarin.Installer.Common.targets.
	/// </summary>
	[MSBuildMultiThreadableTask]
	public class InstallAndroidDependencies : MultithreadedAndroidTask, ICancelableTask
	{
		public override string TaskPrefix => "IAD";

		readonly CancellationTokenSource cancellation = new CancellationTokenSource ();
		BlockingCollection<Action> logQueue;

		public void Cancel () => cancellation.Cancel ();

		internal CancellationToken CancellationToken => cancellation.Token;

		[Required]
		public ITaskItem [] Dependencies { get; set; }

		[Required]
		public ITaskItem [] JavaDependencies { get; set; }

		[Required]
		public string AndroidSdkPath { get; set; }

		public string JavaSdkPath { get; set; }

		public bool AcceptAndroidSDKLicenses { get; set; }

		// Provide a default timeout value of 10 minutes if a value is not provided.
		public int TimeoutInMinutes { get; set; } = 10;

		public bool InstallJavaDependencies { get; set; } = true;

		public bool SkipSignCheck { get; set; }

		public string OpenJdkPublicKeyUrl { get; set; } = "";

		public string OpenJdkPublicKeySha256 { get; set; } = "";

		List<string> temporaryFiles = new List<string> ();

		/// <summary>
		/// The per-user cache directory for the pinned Android CLI version.
		/// </summary>
		public string AndroidCliDirectory { get; set; } = "";

		/// <summary>
		/// One item per host platform, for example `windows_x86_64`, with `Url` and `Sha256` metadata.
		/// </summary>
		public ITaskItem [] AndroidCliPackages { get; set; } = [];

		/// <summary>
		/// Uses this Android CLI executable instead of the pinned download.
		/// </summary>
		public string AndroidCliPath { get; set; } = "";

		// The installation runs on thread-pool continuations, so forward each message to the
		// thread that called Execute () instead of calling TaskLoggingHelper concurrently.
		public override bool RunTask ()
		{
			var queue = new BlockingCollection<Action> ();
			logQueue = queue;
			var work = Task.Run (RunTaskAsync);
			work.ContinueWith (_ => queue.CompleteAdding (), TaskScheduler.Default);
			foreach (var log in queue.GetConsumingEnumerable ())
				log ();
			work.GetAwaiter ().GetResult ();
			return !Log.HasLoggedErrors;
		}

		void Post (Action log)
		{
			var queue = logQueue;
			if (queue == null) {
				log ();
				return;
			}
			try {
				queue.Add (log);
			} catch (InvalidOperationException) {
				// Execute () has returned; MSBuild no longer accepts messages from this task.
			}
		}

		static string Format (string format, object [] parms) =>
			parms == null || parms.Length == 0 ? format : string.Format (CultureInfo.CurrentCulture, format, parms);

		void LogMessage (string message, MessageImportance importance = MessageImportance.Normal) =>
			Post (() => Log.LogMessage (importance, message));

		void LogCodedError (string code, string format, params object [] parms)
		{
			var message = Format (format, parms);
			Post (() => Log.LogError (null, code, null, null, 0, 0, 0, 0, message));
		}

		protected virtual async Task RunTaskAsync ()
		{
			if (Dependencies?.Length == 0 && JavaDependencies?.Length == 0) {
				return;
			}

			try {
				using (var installationContext = ResolveInstallationContext())
				{
					if (installationContext == null)
					{
						return;
					}

					var token = installationContext.CancellationToken;
					
					var success = await InstallJavaSDKAsync(installationContext, token);
					token.ThrowIfCancellationRequested ();
					if (!success)
					{
						return;
					}
					
					success = await InstallAndroidSDKAsync(installationContext, token);
					token.ThrowIfCancellationRequested ();
					if (!success)
					{
						return;
					}
				}
			}
			catch (OperationCanceledException ex)
			{
				Exception(Resources.Task_Cancelled, ex, TimeoutInMinutes);
			} catch (Exception ex) {
				Exception (Resources.Task_Failed, ex);
			} finally {
				// removes temp files
				foreach (var temp in temporaryFiles) {
					if (File.Exists (TaskEnvironment.GetAbsolutePath (temp))) {
						try {
							Debug ("Deleting temporary file '{0}'", temp);
							File.Delete (TaskEnvironment.GetAbsolutePath (temp));
						} catch (Exception ex) {
							Debug ("Error deleting temporary file '{0}'", ex, temp);
						}
					}
				}
			}
		}

		InstallationContext ResolveInstallationContext()
		{
			string path = AndroidSdkPath;
			if (string.IsNullOrEmpty(path))
			{
				var androidHome = TaskEnvironment.GetEnvironmentVariable("ANDROID_HOME");
				path = string.IsNullOrEmpty(androidHome) ? TaskEnvironment.GetEnvironmentVariable("ANDROID_SDK_ROOT") : androidHome;
			}

			if (string.IsNullOrEmpty(path))
			{
				path = GetDefaultAndroidSdkPath();
			}

			if (string.IsNullOrEmpty(path))
			{
				Error(Resources.AndroidSdk_Not_Set);
				return null;
			}
			var sdkPath = TaskEnvironment.GetAbsolutePath (path).GetCanonicalForm ();

			// Resolve JavaSdkPath
			string resolvedJavaSdkPath = JavaSdkPath;
			if (string.IsNullOrEmpty(resolvedJavaSdkPath))
			{
				resolvedJavaSdkPath = TaskEnvironment.GetEnvironmentVariable("JAVA_HOME");
			}
			if (string.IsNullOrEmpty(resolvedJavaSdkPath))
			{
				resolvedJavaSdkPath = TaskEnvironment.GetEnvironmentVariable("JDK_HOME");
			}
			if (string.IsNullOrEmpty(resolvedJavaSdkPath))
			{
				resolvedJavaSdkPath = GetDefaultJavaSdkPath();
			}
			AbsolutePath? javaSdkPath = string.IsNullOrEmpty(resolvedJavaSdkPath) ? null : TaskEnvironment.GetAbsolutePath (resolvedJavaSdkPath).GetCanonicalForm ();

			var components = new List<AndroidPackage>();
			foreach (var dependency in Dependencies)
			{
				var component = new AndroidPackage (dependency.ItemSpec, dependency.GetMetadata("Version"));
				if (!component.IsInstalled (sdkPath)) {
					Debug ($"Adding dependency '{component.InstallArgument}'.");
					components.Add (component);
				} else {
					Debug ($"Skipping dependency '{component.InstallArgument}' as it is already up-to-date.");
				}
			}

			return new InstallationContext(TimeoutInMinutes, CancellationToken)
			{
				HttpClient = new HttpClient(),
				SdkPath = sdkPath,
				Components = components,
				JavaSdkPath = javaSdkPath
			};
		}

		async Task<bool> InstallAndroidSDKAsync(InstallationContext installationContext, CancellationToken cancellationToken)
		{
			var sdkPath = installationContext.SdkPath;
			var components = installationContext.Components;

			if (components.Count > 0)
			{
				if (!AcceptAndroidSDKLicenses)
				{
					Error(Resources.License_Not_Accepted);
					return false;
				}

				var cli = await GetAndroidCliAsync (installationContext.HttpClient, cancellationToken);
				if (string.IsNullOrEmpty (cli))
				{
					return false;
				}

				Directory.CreateDirectory (sdkPath);
				var args = new List<string> { "--no-metrics", "--sdk=" + sdkPath.Value, "sdk", "install" };
				args.AddRange (components.Select (c => c.InstallArgument));
				Info ("Installing Android SDK packages to '{0}': {1}", sdkPath.Value, string.Join (" ", components.Select (c => c.InstallArgument)));

				var psi = TaskEnvironment.GetProcessStartInfo ();
				psi.FileName = cli;
				psi.UseShellExecute = false;
				psi.CreateNoWindow = true;
				psi.RedirectStandardOutput = true;
				psi.RedirectStandardError = true;
				foreach (var arg in args)
					psi.ArgumentList.Add (arg);
				Debug ("{0} {1}", psi.FileName, string.Join (" ", psi.ArgumentList));
				var output = new OutputTail ();
				int exitCode;
				// psi comes from TaskEnvironment.GetProcessStartInfo (), so it carries the project's environment and directory.
#pragma warning disable MSBuildTask0002
				using (var process = Process.Start (psi)) {
#pragma warning restore MSBuildTask0002
					try {
						// Stream the output so progress output does not use a lot of memory.
						await foreach (var line in process.ReadAllLinesAsync (cancellationToken))
							output.WriteLine (line.Content);
						await process.WaitForExitAsync (cancellationToken);
					} catch {
						process.Kill (entireProcessTree: true);
						throw;
					}
					exitCode = process.ExitCode;
				}
				if (exitCode != 0)
				{
					LogCodedError ("XA5308", Resources.XA5308, exitCode, output);
					return false;
				}
			}
			else
			{
				Debug("No Android SDK components need to be installed.");
			}

			if (!cancellationToken.IsCancellationRequested)
			{
				// The Android CLI can exit with 0 when it does not know a package, so check the SDK again.
				foreach (var unresolved in components.Where (c => !c.IsInstalled (sdkPath)))
				{
					Warning(Resources.Required_Not_Installed, unresolved.InstallArgument, sdkPath.Value);
				}
			}

			return true;
		}

		async Task<bool> InstallJavaSDKAsync(InstallationContext installationContext, CancellationToken cancellationToken)
		{
			// Install requested Java SDK
			if (!InstallJavaDependencies)
			{
				Info("Skipping Java SDK installation.");
				return true;
			}

			var javaInstaller = new JavaDependencyInstaller()
			{
				JdkPath = installationContext.JavaSdkPath,
			};

			var requestedJdkVersionText = JavaDependencies?.FirstOrDefault(j => j.ItemSpec == "jdk")?.GetMetadata("Version");
			if (string.IsNullOrEmpty(requestedJdkVersionText))
			{
				return true;
			}
			var requestedJdkVersion = ParseVersion(requestedJdkVersionText);
			if (requestedJdkVersion == null)
			{
				Error(Resources.Invalid_JavaSdkVersion, requestedJdkVersionText);
				return false;
			}
			bool didParseExistingJdkVersion = javaInstaller.GetJdkRevision(out Version currentVersion);
			if (currentVersion.Major >= requestedJdkVersion.Major && didParseExistingJdkVersion)
			{
				Info($"Requested Java SDK with major version '{requestedJdkVersion.Major}' (or greater) is already installed at '{installationContext.JavaSdkPath?.Value}'.");
				return true;
			}

			Info($"Attempting to install Java SDK version '{requestedJdkVersionText}' to '{installationContext.JavaSdkPath?.Value}'.");

			if (!javaInstaller.IsJdkPathValid())
			{
				if (didParseExistingJdkVersion)
				{
					Error(Resources.Invalid_JavaSdkDirectory_ExistingInstall, installationContext.JavaSdkPath?.Value, currentVersion.Major, requestedJdkVersion.Major);
				}
				else
				{
					Error(Resources.Invalid_JavaSdkDirectory, installationContext.JavaSdkPath?.Value);
				}

				return false;
			}

			var jdkToInstall = await javaInstaller.GetFirstValidJdkArchiveWithVersion(installationContext.HttpClient, requestedJdkVersionText, cancellationToken);
			if (jdkToInstall == null)
			{
				Error(Resources.Invalid_JavaSdkVersion, requestedJdkVersionText);
				return false;
			}

			await DownloadAsync(installationContext.HttpClient, jdkToInstall, cancellationToken);
			cancellationToken.ThrowIfCancellationRequested ();
			if (!await VerifyJavaArchiveAsync (installationContext.HttpClient, jdkToInstall, cancellationToken))
				return false;
			cancellationToken.ThrowIfCancellationRequested ();
			await javaInstaller.InstallJdk(jdkToInstall, cancellationToken);

			return true;
		}

		internal async Task<bool> VerifyJavaArchiveAsync (HttpClient httpClient, Archive archive, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested ();
			if (SkipSignCheck) {
				LogMessage ($"Microsoft OpenJDK signature verification was skipped for '{archive.Url}' because _SkipSignCheck=true. Use this workaround only if you have independently verified the archive.", MessageImportance.High);
				return true;
			}

			try {
				var keyUrl = string.IsNullOrEmpty (OpenJdkPublicKeyUrl) ? OpenJdkSignatureVerifier.PublicKeyUrl : OpenJdkPublicKeyUrl;
				if (!Uri.TryCreate (keyUrl, UriKind.Absolute, out var publicKeyUrl) ||
					(publicKeyUrl.Scheme != Uri.UriSchemeHttps && publicKeyUrl.Scheme != Uri.UriSchemeHttp))
					throw new InvalidDataException ("The OpenJDK public key URL must be an absolute HTTP or HTTPS URL.");
				var trustedKeySha256 = string.IsNullOrEmpty (OpenJdkPublicKeySha256) ? OpenJdkSignatureVerifier.PublicKeySha256 : OpenJdkPublicKeySha256;
				if (trustedKeySha256.Length != 64 || !trustedKeySha256.All (Uri.IsHexDigit))
					throw new InvalidDataException ("The OpenJDK public key SHA-256 hash must contain exactly 64 hexadecimal characters.");
				var signatureUrl = new UriBuilder (archive.Url);
				signatureUrl.Path += ".sig";
				var signature = await DownloadVerificationFileAsync (httpClient, signatureUrl.Uri, cancellationToken);
				var publicKey = await DownloadVerificationFileAsync (httpClient, publicKeyUrl, cancellationToken);
				using (var file = File.OpenRead (TaskEnvironment.GetAbsolutePath (archive.DownloadedFilePath)))
					OpenJdkSignatureVerifier.Verify (file, signature, publicKey, trustedKeySha256, cancellationToken);
				return true;
			} catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
				throw;
			} catch (Exception ex) when (ex is HttpRequestException || ex is IOException || ex is InvalidDataException ||
				ex is System.Security.Cryptography.CryptographicException || ex is FormatException || ex is OperationCanceledException) {
				LogCodedError ("XA5305", Resources.XA5305, archive.Url, ex.Message);
				return false;
			}
		}

		internal static async Task<byte []> DownloadVerificationFileAsync (HttpClient httpClient, Uri url, CancellationToken cancellationToken, ArrayPool<byte> bufferPool = null)
		{
			using (var response = await httpClient.GetAsync (url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)) {
				response.EnsureSuccessStatusCode ();
				using (var input = await response.Content.ReadAsStreamAsync ()) {
					const int maximumLength = 64 * 1024;
					var pool = bufferPool ?? ArrayPool<byte>.Shared;
					var buffer = pool.Rent (maximumLength + 1);
					try {
						int length = 0;
						int count;
						while ((count = await input.ReadAsync (buffer, length, buffer.Length - length, cancellationToken)) > 0) {
							length += count;
							if (length > maximumLength)
								throw new InvalidDataException ("The OpenJDK verification file exceeds 64 KiB.");
						}
						return buffer.AsSpan (0, length).ToArray ();
					} finally {
						pool.Return (buffer);
					}
				}
			}
		}

		async Task DownloadAsync(HttpClient httpClient, Archive archive, CancellationToken cancellationToken)
		{
			Info ("Downloading archive from '{0}'", archive.Url);
			using (var response = await httpClient.GetAsync (archive.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)) {
				response.EnsureSuccessStatusCode();
				var fileLength = response.Content.Headers.ContentLength.Value;
#pragma warning disable MSBuildTask0002 // A private scratch file; the node's temporary directory is acceptable until TaskEnvironment provides one.
				var path = Path.GetTempFileName ();
#pragma warning restore MSBuildTask0002
				temporaryFiles.Add (path);
				using (var fileStream = File.OpenWrite (TaskEnvironment.GetAbsolutePath (path))) {
					using (var httpStream = await response.Content.ReadAsStreamAsync ()) {
						const int bufferSize = 16 * 1024;
						var buffer = ArrayPool<byte>.Shared.Rent (bufferSize);
						try {
							int bytesRead;
							double bytesWritten = 0;
							double previousProgress = 0;
							while ((bytesRead = await httpStream.ReadAsync (buffer, 0, buffer.Length, cancellationToken).ConfigureAwait (false)) > 0) {
								fileStream.Write (buffer, 0, bytesRead);
								bytesWritten += bytesRead;
								// Log download progress roughly every 10%.
								var progress = bytesWritten / fileLength;
								if (progress - previousProgress > .10) {
									Info ($"Downloaded {progress:P0} of {Path.GetFileName (archive.Url.AbsolutePath)}");
									previousProgress = progress;
								}
							}
							fileStream.Flush ();
						} finally {
							ArrayPool<byte>.Shared.Return (buffer);
						}
					}
				}
				Info ("Wrote '{0}' to '{1}'", archive.Url, path);
				archive.DownloadedFilePath = path;
			}
		}

		public void Debug (string format, params object [] parms)
		{
			LogMessage (Format (format, parms), MessageImportance.Low);
		}

		public void Debug (string format, Exception ex, params object [] parms)
		{
			Debug (format, parms);
			Debug ("[Exception]: {0}", ex);
		}

		public void Error (string format, params object [] parms)
		{
			var message = Format (format, parms);
			Post (() => Log.LogError (message));
		}

		public void Exception (string format, Exception ex, params object [] parms)
		{
			Error (format, parms);
			Post (() => Log.LogUnhandledException (TaskPrefix, ex, TaskEnvironment));
		}

		public void Info (string format, params object [] parms)
		{
			LogMessage (Format (format, parms));
		}

		public void Warning (string format, params object [] parms)
		{
			var message = Format (format, parms);
			Post (() => Log.LogWarning (message));
		}

		string CombineWithEnvironmentDirectory (string variable, params string [] paths)
		{
			var directory = TaskEnvironment.GetEnvironmentVariable (variable);
			return string.IsNullOrEmpty (directory) ? null : Path.Combine ([directory, .. paths]);
		}

		/// <summary>
		/// Gets the default Android SDK path based on the current platform.
		/// Windows: %LocalAppData%\Android\Sdk
		/// macOS: ~/Library/Android/sdk
		/// Linux: ~/Android/Sdk
		/// </summary>
		string GetDefaultAndroidSdkPath()
		{
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				return CombineWithEnvironmentDirectory ("LOCALAPPDATA", "Android", "Sdk");
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
			{
				return CombineWithEnvironmentDirectory ("HOME", "Library", "Android", "sdk");
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			{
				return CombineWithEnvironmentDirectory ("HOME", "Android", "Sdk");
			}
			return null;
		}

		/// <summary>
		/// Gets the default Java SDK path based on the current platform.
		/// These paths are user-writable and discovered by dotnet/android-tools.
		/// Windows: %LocalAppData%\Android\jdk-{majorVersion}
		///   - Discovered via GetWindowsUserFileSystemJdks: https://github.com/dotnet/android-tools/commit/ebd3aaf34b6650b0d0b763f824d5ba3f2d6802e3
		/// macOS: ~/Library/Android/microsoft-{majorVersion}.jdk
		///   - Discovered via GetMacOSUserFileSystemJdks: https://github.com/dotnet/android-tools/commit/ebd3aaf34b6650b0d0b763f824d5ba3f2d6802e3
		/// Linux: No default - must set JAVA_HOME or pass $(JavaSdkPath) explicitly
		/// </summary>
		string GetDefaultJavaSdkPath()
		{
			var requestedJdkVersion = JavaDependencies?.FirstOrDefault(j => j.ItemSpec == "jdk")?.GetMetadata("Version");
			int majorVersion = 21; // Default if parsing fails
			var revision = ParseVersion(requestedJdkVersion);
			if (revision != null && revision.Major > 0)
			{
				majorVersion = revision.Major;
			}

			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				return CombineWithEnvironmentDirectory ("LOCALAPPDATA", "Android", $"jdk-{majorVersion}");
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
			{
				return CombineWithEnvironmentDirectory ("HOME", "Library", "Android", $"microsoft-{majorVersion}.jdk");
			}
			// Linux: no default, require explicit JAVA_HOME or JavaSdkPath
			return null;
		}

		async Task<string> GetAndroidCliAsync (HttpClient client, CancellationToken cancellationToken)
		{
			if (!string.IsNullOrEmpty (AndroidCliPath))
				return TaskEnvironment.GetAbsolutePath (AndroidCliPath).Value;

			var platform = GetAndroidCliPlatform ();
			var package = platform == null ? null : AndroidCliPackages?.FirstOrDefault (p => p.ItemSpec == platform);
			var url = package?.GetMetadata ("Url");
			var expectedSha256 = package?.GetMetadata ("Sha256");
			if (string.IsNullOrEmpty (url) || string.IsNullOrEmpty (expectedSha256) || string.IsNullOrEmpty (AndroidCliDirectory)) {
				LogCodedError ("XA5306", Resources.XA5306, RuntimeInformation.OSDescription, RuntimeInformation.OSArchitecture);
				return null;
			}

			var directory = TaskEnvironment.GetAbsolutePath (AndroidCliDirectory);
			var path = new AbsolutePath (RuntimeInformation.IsOSPlatform (OSPlatform.Windows) ? "android-cli.exe" : "android-cli", directory);
			if (File.Exists (path)) {
				if (HashMatches (path, expectedSha256))
					return path;
				Debug ("The cached Android CLI at '{0}' does not match the expected SHA-256 and will be downloaded again.", path.Value);
			}

			var archive = new Archive { Url = new Uri (url) };
			await DownloadAsync (client, archive, cancellationToken);

			var actualSha256 = ComputeSha256 (TaskEnvironment.GetAbsolutePath (archive.DownloadedFilePath));
			if (!string.Equals (actualSha256, expectedSha256, StringComparison.OrdinalIgnoreCase)) {
				LogCodedError ("XA5307", Resources.XA5307, url, expectedSha256, actualSha256);
				return null;
			}

			if (!OperatingSystem.IsWindows ()) {
				File.SetUnixFileMode (TaskEnvironment.GetAbsolutePath (archive.DownloadedFilePath),
					UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
					UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
					UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
			}

			Directory.CreateDirectory (directory);
			try {
				if (File.Exists (path))
					File.Delete (path);
				File.Move (TaskEnvironment.GetAbsolutePath (archive.DownloadedFilePath), path);
			} catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException) && File.Exists (path) && HashMatches (path, expectedSha256)) {
				// Another build put the same file in place first.
				Debug ("Using the Android CLI that another build put at '{0}'.", path.Value);
			}
			return path;
		}

		static string GetAndroidCliPlatform ()
		{
			string os = RuntimeInformation.IsOSPlatform (OSPlatform.Windows) ? "windows" : RuntimeInformation.IsOSPlatform (OSPlatform.OSX) ? "darwin" : RuntimeInformation.IsOSPlatform (OSPlatform.Linux) ? "linux" : null;
			string arch = RuntimeInformation.OSArchitecture switch {
				Architecture.X64 => "x86_64",
				Architecture.Arm64 => "arm64",
				_ => null,
			};
			return os == null || arch == null ? null : $"{os}_{arch}";
		}

		static bool HashMatches (AbsolutePath path, string expectedSha256) =>
			string.Equals (ComputeSha256 (path), expectedSha256, StringComparison.OrdinalIgnoreCase);

		static string ComputeSha256 (AbsolutePath path)
		{
			using (var sha256 = SHA256.Create ())
			using (var file = File.OpenRead (path))
				return BitConverter.ToString (sha256.ComputeHash (file)).Replace ("-", "");
		}

		/// <summary>
		/// Parses the leading digits and dots of a version, for example `21.0.8` or `37.0.0-rc1`.
		/// </summary>
		internal static Version ParseVersion (string value)
		{
			if (string.IsNullOrEmpty (value))
				return null;
			int length = 0;
			value = value.Trim ();
			while (length < value.Length && (char.IsDigit (value [length]) || value [length] == '.'))
				length++;
			var text = value.Substring (0, length).Trim ('.');
			if (text.Length == 0)
				return null;
			if (text.IndexOf ('.') < 0)
				text += ".0";
			return Version.TryParse (text, out var version) ? version : null;
		}

		internal static string ReadProperty (AbsolutePath path, string name)
		{
			if (!File.Exists (path))
				return null;
			foreach (var line in File.ReadLines (path)) {
				int index = line.IndexOf ('=');
				if (index > 0 && line.Substring (0, index).Trim () == name)
					return line.Substring (index + 1).Trim ();
			}
			return null;
		}

		/// <summary>
		/// An `@(AndroidDependency)` item, mapped to an Android CLI package name and SDK directory.
		/// </summary>
		internal sealed class AndroidPackage
		{
			readonly string directory;
			readonly Version version;

			public string InstallArgument { get; }

			public AndroidPackage (string path, string version)
			{
				path = path.Replace (';', '/');
				// The Android CLI has no `ndk-bundle` package. Use the side-by-side NDK instead.
				if (path == "ndk-bundle" && !string.IsNullOrEmpty (version))
					path = "ndk/" + version;
				directory = path;
				this.version = ParseVersion (version);

				var lastSegment = path.Substring (path.LastIndexOf ('/') + 1);
				InstallArgument = string.IsNullOrEmpty (version) || lastSegment == version ? path : $"{path}@{version}";
			}

			public bool IsInstalled (AbsolutePath sdkPath)
			{
				var properties = new AbsolutePath (Path.Combine (directory.Replace ('/', Path.DirectorySeparatorChar), "source.properties"), sdkPath);
				if (!File.Exists (properties))
					return false;
				if (version == null)
					return true;
				var installed = ParseVersion (ReadProperty (properties, "Pkg.Revision"));
				return installed != null && installed >= version;
			}
		}

		/// <summary>
		/// Keeps only the end of the process output, so that progress output does not use a lot of memory.
		/// </summary>
		internal sealed class OutputTail : TextWriter
		{
			const int MaximumLength = 4096;
			readonly StringBuilder text = new StringBuilder ();
			readonly object textLock = new object ();

			public override Encoding Encoding => Encoding.UTF8;

			public override void Write (char value)
			{
				lock (textLock) {
					text.Append (value);
					Trim ();
				}
			}

			public override void Write (char [] buffer, int index, int count)
			{
				lock (textLock) {
					text.Append (buffer, index, count);
					Trim ();
				}
			}

			void Trim ()
			{
				if (text.Length > MaximumLength)
					text.Remove (0, text.Length - MaximumLength);
			}

			public override string ToString ()
			{
				lock (textLock)
					return text.ToString ().Trim ();
			}
		}
	}

	public class InstallationContext : IDisposable
	{
		private CancellationTokenSource cancellationTokenSource;
		public InstallationContext(int timeoutInMinutes, CancellationToken cancellationToken)
		{
			cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			cancellationTokenSource.CancelAfter(TimeSpan.FromMinutes(timeoutInMinutes));
			Components = new List<InstallAndroidDependencies.AndroidPackage>();
		}

		public HttpClient HttpClient { get; set; }

		public AbsolutePath SdkPath { get; set; }

		internal List<InstallAndroidDependencies.AndroidPackage> Components { get; set; }

		public AbsolutePath? JavaSdkPath { get; set; }

		public CancellationToken CancellationToken => cancellationTokenSource.Token;

		void IDisposable.Dispose()
		{
			HttpClient?.Dispose();
			cancellationTokenSource?.Dispose();
		}
	}

	/// <summary>
	/// A downloaded file. A simplified copy of Archive from the removed Xamarin.Installer.AndroidSDK project.
	/// </summary>
	public class Archive
	{
		public Uri Url { get; set; }

		public string DownloadedFilePath { get; set; }
	}
}
