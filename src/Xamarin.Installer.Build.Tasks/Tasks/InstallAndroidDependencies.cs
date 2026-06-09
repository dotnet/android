using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Android.Build.Tasks;
using Xamarin.Installer.AndroidSDK;
using Xamarin.Installer.AndroidSDK.Common;
using Xamarin.Installer.AndroidSDK.Manager;
using Xamarin.Installer.Build.Tasks.Properties;
using Xamarin.Installer.Common;
using System.Threading;

namespace Xamarin.Installer.Build.Tasks
{
	public class InstallAndroidDependencies : AsyncTask, ILogAdapter
	{
		public override string TaskPrefix => "IAD";

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

		List<string> temporaryFiles = new List<string> ();

		public string ManifestType { get; set; }

		public async override Task RunTaskAsync ()
		{
			if (Dependencies?.Length == 0 && JavaDependencies?.Length == 0) {
				return;
			}

			Logger.LogAdapter = this;

			try {
				using (var installationContext = ResolveInstallationContext())
				{
					if (installationContext == null)
					{
						return;
					}

					var token = installationContext.CancellationToken;
					
					var success = await InstallJavaSDKAsync(installationContext, token);
					if (!success || token.IsCancellationRequested)
					{
						return;
					}
					
					success = await InstallAndroidSDKAsync(installationContext, token);
					if (!success || token.IsCancellationRequested)
					{
						return;
					}
				}
			}
			catch (TaskCanceledException ex)
			{
				Exception(Resources.Task_Cancelled, ex, TimeoutInMinutes);
			} catch (Exception ex) {
				Exception (Resources.Task_Failed, ex);
			} finally {
				Logger.LogAdapter = null;

				// removes temp files
				foreach (var temp in temporaryFiles) {
					if (File.Exists (temp)) {
						try {
							Debug ("Deleting temporary file '{0}'", temp);
							File.Delete (temp);
						} catch (Exception ex) {
							Debug ("Error deleting temporary file '{0}'", ex, temp);
						}
					}
				}

				Complete ();
			}
		}

		InstallationContext ResolveInstallationContext()
		{
			var manifestType = AndroidManifestType.Xamarin;

			if (!string.IsNullOrEmpty(ManifestType) &&
				!Enum.TryParse<AndroidManifestType>(ManifestType, out manifestType))
			{
				Warning(Resources.Unsupported_Manifest_Type, ManifestType);
				manifestType = AndroidManifestType.Xamarin;
			}

			string path = AndroidSdkPath;
			if (string.IsNullOrEmpty(path))
			{
				var androidHome = Environment.GetEnvironmentVariable("ANDROID_HOME");
				path = string.IsNullOrEmpty(androidHome) ? Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT") : androidHome;
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

			// Resolve JavaSdkPath
			string resolvedJavaSdkPath = JavaSdkPath;
			if (string.IsNullOrEmpty(resolvedJavaSdkPath))
			{
				resolvedJavaSdkPath = Environment.GetEnvironmentVariable("JAVA_HOME");
			}
			if (string.IsNullOrEmpty(resolvedJavaSdkPath))
			{
				resolvedJavaSdkPath = Environment.GetEnvironmentVariable("JDK_HOME");
			}
			if (string.IsNullOrEmpty(resolvedJavaSdkPath))
			{
				resolvedJavaSdkPath = GetDefaultJavaSdkPath();
			}

			var installer = new AndroidSDKInstaller(new Helper(), manifestType);
			installer.Discover(new List<string> { path });

			var sdkInstance = installer.FindInstance(path);
			if (sdkInstance == null)
			{
				Error(Resources.AndroidSdk_Not_Found, path);
				return null;
			}

			IList<IAndroidComponent> installationSet = new List<IAndroidComponent>();
			var unknownInputs = new List<AndroidDependencyInput>();
			var components = new List<IAndroidComponent>();
			foreach (var dependency in Dependencies)
			{
				string dependencyPath = dependency.ItemSpec.Replace('/', ';');
				string version = dependency.GetMetadata("Version");
				var v = !string.IsNullOrWhiteSpace(version) ? new AndroidRevision(version) : null;

				/*
				* It's possible a component is in multiple channels, the channel list is of the form:
				* <channel id="channel-0">stable</channel>
				* <channel id="channel-1">beta</channel>
				* <channel id="channel-2">dev</channel>
				* <channel id="channel-3">canary</channel>
				*/
				IAndroidComponent matchingComponent = sdkInstance.Components
					.Where (c => c.Path == dependencyPath && c.Revision == (v ?? c.Revision))
					.OrderBy (c => c.Channel?.ID ?? "channel-99") // null sorted last
					.FirstOrDefault ();
				if (matchingComponent != null)
				{
					if (!matchingComponent.Present && !matchingComponent.Obsolete) {
						Debug ($"Adding dependency '{dependencyPath}/{version}'.");
						components.Add (matchingComponent);
					} else {
						Debug ($"Skipping dependency '{dependencyPath}/{version}' as it is already installed or obsolete.");
					}
				}
				else
				{
					Debug ($"Unknown dependency: '{dependencyPath}/{version}'");

					unknownInputs.Add(new AndroidDependencyInput
					{
						Path = dependencyPath,
						Version = version,
					});
				}
			}

			if (components.Count > 0)
			{
				installationSet = installer.GetInstallationSet(sdkInstance, components);
			}

			return new InstallationContext(TimeoutInMinutes, CancellationToken)
			{
				HttpClient = new HttpClient(),
				Installer = installer,
				SdkInstance = sdkInstance,
				InstallationSet = installationSet,
				Components = components,
				UnknownInputs = unknownInputs,
				JavaSdkPath = resolvedJavaSdkPath
			};
		}

		async Task<bool> InstallAndroidSDKAsync(InstallationContext installationContext, CancellationToken cancellationToken)
		{
			var sdkInstance = installationContext.SdkInstance;
			var components = installationContext.Components;

			if (components.Count > 0)
			{
				foreach (var component in components)
				{
					Debug("Dependency found: {0}", component.DisplayName);
				}

				var installationSet = installationContext.InstallationSet;
				foreach (var component in installationSet)
				{
					Debug("Dependency to be installed: {0}", component.DisplayName);
				}

				if (!AcceptAndroidSDKLicenses)
				{
					Error(Resources.License_Not_Accepted);
					return false;
				}

				var installer = installationContext.Installer;
				var downloads = installer.GetDownloadItems(installationSet);
				if (downloads == null)
				{
					Error(Resources.Required_Downloadable_Mismatch, installationSet.Count);
					return false;
				}

				await Task.WhenAll(downloads.Select(d => DownloadAsync(installationContext.HttpClient, d, cancellationToken)));

				if (!cancellationToken.IsCancellationRequested)
				{
					installer.Install(sdkInstance, installationSet);
				}

				await AcceptLicensesAsync(installationContext, cancellationToken);
			}
			else
			{
				Debug("No Android SDK components need to be installed.");
			}

			if (!cancellationToken.IsCancellationRequested)
			{
				var unknownInputs = installationContext.UnknownInputs;
				if (unknownInputs.Any())
				{
					var sdkManagerDirectory = Directory.EnumerateFiles(
						Path.Combine(sdkInstance.Path, "cmdline-tools"), "sdkmanager*", SearchOption.AllDirectories)
						.LastOrDefault(s => s.IndexOf("bin", StringComparison.OrdinalIgnoreCase) != -1) ?? "sdkmanager";
					foreach (var unresolved in unknownInputs)
					{
						Warning(Resources.Required_Not_Installed, unresolved.Path, sdkManagerDirectory);
					}
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

			var javaInstaller = new JavaDependencyInstaller(new Helper())
			{
				JdkPath = installationContext.JavaSdkPath,
			};

			var requestedJdkVersion = new AndroidRevision(JavaDependencies?.FirstOrDefault(j => j.ItemSpec == "jdk")?.GetMetadata("Version") ?? string.Empty);
			bool didParseExistingJdkVersion = javaInstaller.GetJdkRevision(out AndroidRevision currentVersion);
			if (currentVersion.Major >= requestedJdkVersion.Major && didParseExistingJdkVersion)
			{
				Info($"Requested Java SDK with major version '{requestedJdkVersion.Major}' (or greater) is already installed at '{installationContext.JavaSdkPath}'.");
				return true;
			}

			Info($"Attempting to install Java SDK version '{requestedJdkVersion}' to '{installationContext.JavaSdkPath}'.");

			if (!javaInstaller.IsJdkPathValid())
			{
				if (didParseExistingJdkVersion)
				{
					Error(Resources.Invalid_JavaSdkDirectory_ExistingInstall, installationContext.JavaSdkPath, currentVersion.Major, requestedJdkVersion.Major);
				}
				else
				{
					Error(Resources.Invalid_JavaSdkDirectory, installationContext.JavaSdkPath);
				}

				return false;
			}

			javaInstaller.Discover();
			var jdkToInstall = javaInstaller.GetFirstValidJdkArchiveWithVersion(requestedJdkVersion);
			if (jdkToInstall == null)
			{
				Error(Resources.Invalid_JavaSdkVersion, requestedJdkVersion);
				return false;
			}

			await DownloadAsync(installationContext.HttpClient, jdkToInstall, cancellationToken);

			if (!cancellationToken.IsCancellationRequested)
			{
				javaInstaller.InstallJdk(jdkToInstall);
			}

			return true;
		}

		async Task AcceptLicensesAsync(InstallationContext installationContext, CancellationToken cancellationToken)
		{
			if (AcceptAndroidSDKLicenses)
			{
				var installationSet = installationContext.InstallationSet;

				//Trying to mimic Android SDK's prompt, let's at least log all the licenses
				foreach (var component in installationSet)
				{
					if (cancellationToken.IsCancellationRequested)
					{
						return;
					}    

					LogMessage(component.License.Text);
				}

				try
				{
					Debug($"Accepting all SDK package licenses, via $({nameof(AcceptAndroidSDKLicenses)})");

					var licenses = installationSet.Select(component => component.License);

					var installer = installationContext.Installer;
					var sdkInstance = installationContext.SdkInstance;

					var logPath = string.Empty; // TODO: set a file path 
					await installer.AcceptLicensesAsync(sdkInstance, licenses, cancellationToken, javaSdkPath: installationContext.JavaSdkPath, logPath: logPath, throwsErrorIfValidationFailed: true);

					Debug($"All SDK package licenses have been accepted");
				}
				catch (Exception e)
				{
					// Exception is not propagated to ensure complete the installation process
					Warning(Resources.Licenses_Acceptance_Failed, e.Message);
					Debug(Resources.Licenses_Acceptance_Failed, e, e.Message);
				}
			}
		}

		async Task DownloadAsync(HttpClient httpClient, Archive archive, CancellationToken cancellationToken)
		{
			Info ("Downloading archive from '{0}'", archive.Url);
			using (var response = await httpClient.GetAsync (archive.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)) {
				response.EnsureSuccessStatusCode();
				var fileLength = response.Content.Headers.ContentLength.Value;
				var path = Path.GetTempFileName ();
				temporaryFiles.Add (path);
				using (var fileStream = File.OpenWrite (path)) {
					using (var httpStream = await response.Content.ReadAsStreamAsync ()) {
						var buffer = new byte[16 * 1024];
						int bytesRead;
						double bytesWritten = 0;
						double previousProgress = 0;
						while ((bytesRead = httpStream.Read (buffer, 0, buffer.Length)) > 0) {
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
					}
				}
				Info ("Wrote '{0}' to '{1}'", archive.Url, path);
				archive.DownloadedFilePath = path;
			}
		}

		public void Action (string format, params object [] parms)
		{
			LogDebugMessage (format, parms);
		}

		public void Debug (string format, params object [] parms)
		{
			LogDebugMessage (format, parms);
		}

		public void Debug (string format, Exception ex, params object [] parms)
		{
			LogDebugMessage (format, parms);
			LogDebugMessage ("[Exception]: {0}", ex);
		}

		public void Error (string format, params object [] parms)
		{
			LogError (format, parms);
		}

		public void Exception (string format, Exception ex, params object [] parms)
		{
			LogError (format, parms);
			this.LogUnhandledException (TaskPrefix, ex);
		}

		public void Info (string format, params object [] parms)
		{
			LogMessage (format, parms);
		}

		public void Warning (string format, params object [] parms)
		{
			LogWarning (format, parms);
		}

		public void SetOperationStatus (OperationStatus status) { }

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
				return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Android", "Sdk");
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
			{
				return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Android", "sdk");
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			{
				return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Android", "Sdk");
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
			if (!string.IsNullOrEmpty(requestedJdkVersion))
			{
				var revision = new AndroidRevision(requestedJdkVersion);
				if (revision.Major > 0)
				{
					majorVersion = revision.Major;
				}
			}

			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Android", $"jdk-{majorVersion}");
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
			{
				return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Android", $"microsoft-{majorVersion}.jdk");
			}
			// Linux: no default, require explicit JAVA_HOME or JavaSdkPath
			return null;
		}
	}

	public class InstallationContext : IDisposable
	{
		private CancellationTokenSource cancellationTokenSource;
		public InstallationContext(int timeoutInMinutes, CancellationToken cancellationToken)
		{
			cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			cancellationTokenSource.CancelAfter(TimeSpan.FromMinutes(timeoutInMinutes));
			Components = new List<IAndroidComponent>();
			UnknownInputs = new List<AndroidDependencyInput>();
			InstallationSet = new List<IAndroidComponent>();
		}

		public HttpClient HttpClient { get; set; }

		public AndroidSDKInstaller Installer { get; set; }

		public AndroidSdkInstance SdkInstance { get; set; }

		public List<IAndroidComponent> Components { get; internal set; }

		public List<AndroidDependencyInput> UnknownInputs { get; internal set; }

		public IList<IAndroidComponent> InstallationSet { get; set; }

		public string JavaSdkPath { get; set; }

		public CancellationToken CancellationToken => cancellationTokenSource.Token;

		void IDisposable.Dispose()
		{
			HttpClient?.Dispose();
			cancellationTokenSource?.Dispose();
		}
	}

	public class AndroidDependencyInput
	{
		public string Path { get; set; }
		public string Version { get; set; }
	}
}
