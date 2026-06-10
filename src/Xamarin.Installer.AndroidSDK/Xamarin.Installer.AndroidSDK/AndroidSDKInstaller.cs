using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Kajabity.Tools.Java;

using Xamarin.AndroidTools;
using Xamarin.Installer.AndroidSDK.Common;
using Xamarin.Installer.AndroidSDK.Xamarin;
using Xamarin.Installer.AndroidSDK.GoogleV2;
using Xamarin.Installer.AndroidSDK.LocalSDK;
using Xamarin.Installer.Common;
using Xamarin.Installer.AndroidSDK.Manager;
using System.Threading.Tasks;
using System.Threading;

namespace Xamarin.Installer.AndroidSDK
{
	/// <summary>
	/// Android SDK installer class is the class to be used by the client software to perform all the 
	/// operations of updating or installing the SDK.
	/// </summary>
	public class AndroidSDKInstaller : IParserErrorHandler
	{
		List <AndroidSdkInstance> discoveredInstances;
		StringComparer filesystemComparer;
		StringComparison filesystemComparison;
		Uri _manifestURL;
		Uri _googleAddonsListURL;
		Uri _googleRepositoryBaseURL;
		bool _useManifestCaching;

		/// <summary>
		/// Initializes a new instance of the <see cref="AndroidSDKInstaller"/> class. The caller is
		/// required to pass an instance of class that implements the <see cref="IHelpers"/> interface.
		/// </summary>
		/// <param name="helpers">Instance of a class implementing the IHelpers interface</param>
		/// <param name="manifestType"><c>AndroidManifestType.Xamarin</c> if the installer is to use the 
		/// Xamarin Android SDK manifest, <c>AndroidManifestType.GoogleV1</c> or <c>AndroidManifestType.GoogleV2</c> 
		/// to use the official Google Android SDK manifest</param>
		/// <param name="manifestURL">URL of the repository manifest (optional)</param>
		/// <param name="googleAddonsListURL">Google repository only: URL of the addon manifest (optional)</param>
		/// <param name="googleRepositoryBaseURL">Google repository only: base URL of the repository</param>
		/// <param name="useManifestCaching">If <c>true</c>, load local manifest when offline</param>
		/// <param name="licensesStorage">Custom licenses storage (optional)</param>
		public AndroidSDKInstaller (IHelpers helpers, AndroidManifestType manifestType = AndroidManifestType.Xamarin, Uri manifestURL = null, Uri googleAddonsListURL = null, Uri googleRepositoryBaseURL = null, bool useManifestCaching = false, ILicensesStorage licensesStorage = null)
		{
			CommonUtilities.Helpers = helpers ?? new Helper();
			AndroidManifestType = manifestType;

			_manifestURL = manifestURL;
			_googleAddonsListURL = googleAddonsListURL;
			_googleRepositoryBaseURL = googleRepositoryBaseURL;
			_useManifestCaching = useManifestCaching;

			LicensesStorage = licensesStorage ?? new AndroidLicensesStorage();

			CreateRepository (manifestType, manifestURL, googleAddonsListURL, googleRepositoryBaseURL, useManifestCaching);

			filesystemComparer = helpers.IsCaseSensitiveFileSystem ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
			filesystemComparison = helpers.IsCaseSensitiveFileSystem ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
		}
		
		public Repository Repository { get; private set; }

		public ILicensesStorage LicensesStorage { get; private set; }

		/// <summary>
		/// Raised whenever the installer wants to show a message to the user. Used only by the <code>Install*</code>
		/// family of methods.
		/// </summary>
		public event EventHandler <InstallationProgressEventArgs> InstallationProgress;

		/// <summary>
		/// Gets the repository manifest URL as detected and used by the current installer instance.
		/// </summary>
		/// <value>The repository manifest URL</value>
		public Uri RepositoryManifestURL {
			get { return Repository?.ManifestURL; }
		}

		/// <summary>
		/// After calling <see cref="Discover"/> this list will contain all the instances of Android SDK
		/// detected on the system. Note that if there are no instances found a default one will be created
		/// and stored in this list.
		/// </summary>
		/// <value>The discovered Android SDK instances.</value>
		public IList <AndroidSdkInstance> DiscoveredSdkInstances {
			get { return discoveredInstances; }
		}

		/// <summary>
		/// Gets list of all the channels retrieved from the various repository manifests. Currently all of the
		/// Android SDK manifests defined the same 4 channels (<seealso cref="T:Channel"/>) but it is possible that
		/// there might be differences between manifests in the future.
		/// </summary>
		/// <value>The channels.</value>
		public IList <Channel> Channels {
			get { return Repository?.Channels?.Values?.ToList (); }
		}

		/// <summary>
		/// Sets or gets the Android manifest type for this instance (see <see cref="AndroidManifestType"/>)
		/// </summary>
		public AndroidManifestType AndroidManifestType { get; }

		/// <summary>
		/// Gets the repository state. If <c>true</c>, it is using local manifest,
		/// and should be updated as soon as connection is established.
		/// </summary>
		public bool IsOffline => Repository.IsOffline;

		void CreateRepository (AndroidManifestType manifestType, Uri manifestURL, Uri googleAddonsListURL, Uri googleRepositoryBaseURL, bool useManifestCaching)
		{
			switch (manifestType) {
				case AndroidManifestType.Xamarin:
					Repository = new XamarinRepository (manifestURL, useManifestCaching);
					break;

				case AndroidManifestType.GoogleV2:
					Repository = new GoogleV2Repository (this, manifestURL, googleAddonsListURL, googleRepositoryBaseURL, useManifestCaching);
					break;

				case AndroidManifestType.Local:
					Repository = new LocalSDKRepository (this);
					break;

				default:
					throw new InvalidOperationException ($"Unsupported manifest type {manifestType}");
			}
		}

		/// <summary>
		/// Given a full filesystem path attempts to find and return a corresponding <see cref="AndroidSdkInstance"/> instance.
		/// </summary>
		/// <returns>The instance corresponding to the path.</returns>
		/// <param name="forPath">Full path for which to find a corresponding SDK instance. If null, default path will be used.</param>
		public AndroidSdkInstance FindInstance (string forPath)
		{
			forPath = forPath ?? AndroidSdk.AndroidSdkPath;
			
			if (String.IsNullOrEmpty (forPath))
				throw new ArgumentException ("must not be an empty string", nameof (forPath));

			string path = Path.GetFullPath (forPath);
			if (discoveredInstances == null || discoveredInstances.Count == 0) {
				return null;
			}

			return discoveredInstances.FirstOrDefault (sdk => String.Compare (sdk.Path, path, filesystemComparison) == 0);
		}

		/// <summary>
		/// <para>
		/// Refreshes the state of all the components given a particular Android SDK <paramref name="instance"/>. By default the method
		/// doesn't re-download or re-parse the manifest(s) but instead it performs detection of each of the components in the
		/// instance, thus updating their state. If, however, <paramref name="localOnly"/> is set to <c>true</c> the repository manifests,
		/// if any, will be fetched and reparsed.
		/// </para>
		/// <para>
		/// If <paramref name="localOnly"/> is <c>true</c> then the <paramref name="instance"/> may have its set of components changed - new
		/// ones can be added and old ones can be changed (only their metadata will change, the component instance won't change).
		/// <seealso cref="IAndroidComponent.StatusChanged"/>
		/// </para>
		/// <para>
		/// If <paramref name="performFullDetection"/> is <c>true</c> then the method will start a full detection cycle. This means that the
		/// <paramref name="instance"/> will be modified - all of its components will be replaced with fresh instances. This is to ensure that
		/// the metadata in the components is 100% accurate. By default full detection is not performed.
		/// </para>
		/// </summary>
		/// <param name="instance">Android SDK instance</param>
		/// <param name="localOnly">Perform update from the local filesystem, if <c>true</c>, update from remote manifests otherwise</param>
		/// <param name="performFullDetection">If <c>true</c>, perform full detection of all the components after refreshing the instance</param>
		public void RefreshState (AndroidSdkInstance instance, bool localOnly = true, bool performFullDetection = false)
		{
			if (instance == null)
				throw new ArgumentNullException (nameof (instance));
			if (!localOnly)
			{
				try
				{
					Repository.Parse();
				}
				catch (Exception e)
				{
					LogParserError("AndroidSDK.RefreshState", e);
					return;
				}
			}
			Repository.Refresh (instance);

			if (performFullDetection)
				DetectSdkComponents (instance);
		}

		/// <summary>
		/// Downloads the current Android SDK repository manifest (<see cref="RepositoryManifestURL"/>) and uses it to disover/detect
		/// all instances of Android SDK on the system. It uses the system-dependent default location as well as the locations stored
		/// in <see cref="AndroidSdk.AllAndroidSdkPaths"/>. Additionally, it can include extra paths passed in
		/// the <paramref name="sdkLocations"/> list. If no Android SDK instance is detected on the system a default one will be created.
		/// All the discovered instances are returned by the <see cref="DiscoveredSdkInstances"/> property.
		/// 
		/// If <paramref name="fromScratch"/> is set to true (the default), the discovered instances replace the ones currently stored in
		/// the list returned by the <see cref="DiscoveredSdkInstances"/> property. Otherwise the newly discovered instances are appended
		/// to the list.
		/// 
		/// The method returns <c>true</c> if detection was successful, <c>false</c> to indicate a "soft" error (one which lets the caller
		/// retry the operation, most likely a networking issue) and it can throw exceptions for "hard" errors.
		/// </summary>
		/// <param name="sdkLocations">Custom Android SDK locations (optional).</param>
		/// <param name="fromScratch">Perform detection from scratch if set to <c>true</c> (optional)</param>
		/// <param name="instanceComponentStatusChangeHandler">Handler to be called whenever any component in the SDK instance changes its state</param>
		public bool Discover (List <string> sdkLocations = null, bool fromScratch = true, EventHandler <AndroidComponentStatusChangeEventArgs> instanceComponentStatusChangeHandler = null)
		{
			if (fromScratch || !Repository.Parsed) {
				if (fromScratch)
					CreateRepository (AndroidManifestType, _manifestURL, _googleAddonsListURL, _googleRepositoryBaseURL, _useManifestCaching);
				try
				{
					Repository.Parse();
				}
				catch (Exception e)
				{
					LogParserError("AndroidSDK.Discover", e);
					return false;
				}
			}

			var sdkInstances = new Dictionary<string, AndroidSdkInstance> (filesystemComparer);
			if (!fromScratch && discoveredInstances != null) {
				discoveredInstances.ForEach (instance => {
					if (instance == null || sdkInstances.ContainsKey (instance.Path))
						return;
					sdkInstances [instance.Path] = instance;
				});
			}

			var sdkPaths = new List <string> ();

#pragma warning disable CS0612 // Type or member is obsolete
			if (AndroidSdk.AllAndroidSdkPaths != null)
				sdkPaths.AddRange (AndroidSdk.AllAndroidSdkPaths);
#pragma warning restore CS0612 // Type or member is obsolete

			if (sdkLocations != null)
				sdkPaths.AddRange (sdkLocations);


			sdkPaths = sdkPaths.Distinct ().ToList ();

			string defaultPath = AndroidSdk.AndroidSdkPath ?? sdkPaths.FirstOrDefault ();
			foreach (string path in sdkPaths) {
				if (String.IsNullOrEmpty (path))
					continue;
				string asdkPath = Path.GetFullPath (path);
				if (sdkInstances.ContainsKey (asdkPath))
					continue;

				var sdkInstance = new AndroidSdkInstance {
					Path = asdkPath,
					IsDefault = String.Compare (path, defaultPath, filesystemComparison) == 0
				};
				if (instanceComponentStatusChangeHandler != null) {
					sdkInstance.ComponentStatusChanged -= instanceComponentStatusChangeHandler;
					sdkInstance.ComponentStatusChanged += instanceComponentStatusChangeHandler;
				}

				DetectSdkComponents (sdkInstance);

				sdkInstances [asdkPath] = sdkInstance;
			}

			if (fromScratch || discoveredInstances == null)
				discoveredInstances = sdkInstances.Values.ToList ();
			else {
				sdkInstances.Values.ToList ().ForEach (instance => {
					if (discoveredInstances.Contains (instance))
						return;
					discoveredInstances.Add (instance);
				});
			}
			if (discoveredInstances.Count == 1)
				discoveredInstances [0].Selected = true;
			else if (discoveredInstances.Any () && !discoveredInstances.Any (item => item.Selected)) {
				// Make sure we have a selected sdk in any case as the interaction is optional
				// Select default location if its valid, select sdk with latest version otherwise
				var defaultInstance = discoveredInstances.FirstOrDefault (item => item.IsDefault);
				if (defaultInstance != null && (defaultInstance.Version.Major > 0 || !discoveredInstances.Any (item => item.Version.Major > 0)))
					defaultInstance.Selected = true;
				else
					discoveredInstances.OrderByDescending (item => item.Version).First ().Selected = true;
			}

			return true;
		}

		/// <summary>
		/// Gets the license with the ID specified in <paramref name="licenseId"/>. The ID usually comes from
		/// <see cref="F:IAndroidComponent.UsesLicense"/> 
		/// </summary>
		/// <returns>The license identified by <paramref name="licenseId"/> or an empty string if license with this ID is not found.</returns>
		/// <param name="licenseId">License identifier. Required, must not be <c>null</c></param>
		public string GetLicense (string licenseId)
		{
			if (licenseId == null)
				throw new ArgumentNullException (nameof (licenseId));

			return Repository.GetLicense (licenseId)?.Text;
		}

		/// <summary>
		/// Returns a list of licenses for all the components passed in the <paramref name="componentsToInstall"/>
		/// parameter or, if it's <c>null</c> or empty, for all the outdated components in the specified
		/// <paramref name="instance"/>.
		/// </summary>
		/// <returns>List of licenses or <c>null</c> if no licenses were available</returns>
		/// <param name="instance">Android SDK instance.</param>
		/// <param name="componentsToInstall">List of components to install (optional)</param>
		public IList <License> GetLicenses (AndroidSdkInstance instance, IList<IAndroidComponent> componentsToInstall = null)
		{
			if (instance == null)
				throw new ArgumentNullException (nameof (instance));

			IList < IAndroidComponent > components = null;
			if (componentsToInstall == null || componentsToInstall.Count == 0)
				components = instance.Components.AllOutdated ();

			if (components == null || components.Count == 0)
				return null;
			
			var ret = new List<License> ();
			foreach (IAndroidComponent component in components) {
				License license = component?.License;
				if (license == null || ret.Contains (license))
					continue;
				ret.Add (license);
			}

			return ret.Count > 0 ? ret : null;
		}

		/// <summary>
		/// Gets a list of download items for all of the components passed in the <paramref name="components"/> parameter. After a
		/// successful download the calling party must set <see cref="m:Archive.DownloadedFilePath"/> to the on-disk path of the
		/// downloaded file thus marking the <see cref="t:Archive"/> instance as installable.
		/// </summary>
		/// <returns>List of archives to download</returns>
		/// <param name="components">List of components to get download items for</param>
		public IList <Archive> GetDownloadItems (IList <IAndroidComponent> components)
		{
			if (components == null || components.Count == 0)
				return null;

			var ret = new List <Archive> ();
			foreach (IAndroidComponent component in components.Where (c => c != null && c.Archives != null && c.Archives.Count > 0)) {
				ret.AddRange (component.Archives.Where (a => a != null && a.Url != null).Select (a => ResetForDownload (a)));
			}
			return ret;

			Archive ResetForDownload (Archive archive)
			{
				archive.DownloadedFilePath = null;
				return archive;
			}
		}

		/// <summary>
		/// Gets a list of download items for all the outdated or missing components in the passed <paramref name="instance"/>. After a
		/// successful download the calling party must set <see cref="m:Archive.DownloadedFilePath"/> to the on-disk path of the
		/// downloaded file thus marking the <see cref="t:Archive"/> instance as installable. The method considers <see cref="AndroidSdkInstance.ComponentsForInstallation"/>
		/// before <see cref="AndroidSdkInstance.Components"/>
		/// </summary>
		/// <returns>List of archives to download</returns>
		/// <param name="instance">Android SDK instance</param>
		/// <param name="includeMissing">If <c>true</c>, include missing components</param>
		public IList<Archive> GetDownloadItems (AndroidSdkInstance instance, bool includeMissing = false)
		{
			if (instance == null)
				return null;
			IList<IAndroidComponent> components = instance.ComponentsForInstallation ?? instance.Components;
			return GetDownloadItems (includeMissing ? components?.AllOutdatedOrMissing () : components?.AllOutdated ());
		}

		void OnInstallationProgress (bool isInitialEvent, float progress, string format, params object[] args)
		{
			if (InstallationProgress == null)
				return;

			string message;
			if (format == null)
				message = String.Empty;
			else if (args == null || args.Length == 0)
				message = format;
			else
				message = String.Format (format, args);
			InstallationProgress (this, new InstallationProgressEventArgs { Message = message, Progress = progress, IsInitialEvent = isInitialEvent });
		}

		/// <summary>
		/// <para>
		/// Gets the set of components available for installation given the particular <paramref name="instance"/>.
		/// If <paramref name="componentsToInstall"/> is <c>null</c> or empty, the outdated components from the
		/// <paramref name="instance"/> will be considered for inclusion in the set. If the <paramref name="allowedChannels"/>
		/// parameter is <c>null</c> or empty, only the default channel (stable) packages will be considered.
		/// </para>
		/// <para>
		/// Each component's dependencies will be added to the resulting set. It is possible that certain components
		/// (for instance build tools or emulators) will be found in the returned set more than once because they match
		/// the criteria (e.g. all the included versions are higher than the one detected on the system) or because they
		/// are dependent upon by one or more of the other components.
		/// </para>
		/// <para>
		/// Caller must decide which packages to eventually install if there are duplicates in the returned set.
		/// </para>
		/// </summary>
		/// <returns>List of components that are candidates for installation in the given SDK instance</returns>
		/// <param name="instance">Android SDK instance</param>
		/// <param name="componentsToInstall">Components to install.</param>
		/// <param name="allowedChannels">Allowed channels.</param>
		public IList<IAndroidComponent> GetInstallationSet (AndroidSdkInstance instance, IList<IAndroidComponent> componentsToInstall = null, HashSet<Channel> allowedChannels = null)
		{
			if (instance == null)
				throw new ArgumentNullException (nameof (instance));

			if (componentsToInstall == null || componentsToInstall.Count == 0)
				componentsToInstall = instance.Components.AllOutdated ();

			if (componentsToInstall.Count == 0)
				return null;

			if (allowedChannels == null || allowedChannels.Count == 0) {
				allowedChannels = new HashSet <Channel> {
					Repository.DefaultChannel
				};
		}

			var components = new List <IAndroidComponent> ();
			foreach (IAndroidComponent c in componentsToInstall) {
				AddComponentToSet (instance, c, components, allowedChannels);
			}

			return components;
		}

		/// <summary>
		/// Performs installation of all the components in the specified <paramref name="instance"/>. The <paramref name="componentsToInstall"/>
		/// parameter must not be null or empty and it must contain all the components the hosting application needs to install - no dependency
		/// checking is performed by this method. It is advised to call the <see cref="GetInstallationSet"/> method prior to calling this one.
		/// Before installing anything the method checks all the archives in all the passed components to make sure they are installable. 
		/// If any invalid archive is found no installation is done. This is to prevent partially successful installations, potentially breaking the SDK
		/// instance. If <paramref name="throwIfInvalidComponentsFound"/> is set to <c>true</c> and an invalid archive is found then the method
		/// will throw an exception, otherwise it will silently return without performing installation. All the issues are logged.
		/// </summary>
		/// <param name="instance">Android SDK instance.</param>
		/// <param name="componentsToInstall">Components to install</param>
		/// <param name="throwIfInvalidComponentsFound">Throw an exception if any component with invalid archives is found, if set to <c>true</c></param>
		/// <param name="monitor">Progress monitor</param>
		public void Install (AndroidSdkInstance instance, IList<IAndroidComponent> componentsToInstall, bool throwIfInvalidComponentsFound = true,
			IProgressMonitor monitor = null)
		{
			if (instance == null)
				throw new ArgumentNullException (nameof (instance));

			if (componentsToInstall == null)
				throw new ArgumentNullException (nameof (componentsToInstall));

			//throw new Exception("A test install inner exception.");

			if (componentsToInstall.Count == 0)
				return;

			// This is a separate step so that we can report all the issues at the same time as well as prevent
			// installing of any components if even a single archive isn't valid.
			bool haveArchiveIssues = false;
			foreach (IAndroidComponent c in componentsToInstall) {
				// If it happens simply ignore the component silently
				if (c == null || c.Archives == null || c.Archives.Count == 0)
					continue;

				bool haveInvalidArchives = false;
				foreach (Archive a in c.Archives) {
					if (a == null || !a.WasDownloaded || a.IsInstallable ())
						continue;
					haveInvalidArchives = true;
				}

				if (haveInvalidArchives)
					Logger.Warning ($"Android SDK component '{c.DetailedDescription}' reports issues with downloaded archive(s)");
			}

			if (haveArchiveIssues) {
				Logger.Error ($"Certain Android SDK components encountered issues with the downloaded archives");
				if (throwIfInvalidComponentsFound)
					throw new InvalidOperationException ("Found issues with one or more Android SDK Components. Unable to continue.");
				return;
			}

			foreach (IAndroidComponent c in componentsToInstall)
				InstallComponent (c, instance, monitor);
		}

		/// <summary>
		/// Removes the components listed in <paramref name="componentsToRemove"/> from the specified <paramref name="instance"/>.
		/// </summary>
		/// <param name="instance">Android SDK instance.</param>
		/// <param name="componentsToRemove">Components to remove</param>
		public void Remove (AndroidSdkInstance instance, IList<IAndroidComponent> componentsToRemove)
		{
			if (instance == null)
				throw new ArgumentNullException (nameof (instance));

			if (componentsToRemove == null)
				throw new ArgumentNullException (nameof (componentsToRemove));

			//throw new Exception("Uninstall inner test exception.");

			if (componentsToRemove.Count == 0)
				return;

			foreach (IAndroidComponent c in componentsToRemove) {
				if (c == null)
					continue;

				Repository.Remove (c, instance.Path);
			}
		}

		void AddComponentToSet (AndroidSdkInstance instance, IAndroidComponent component, List <IAndroidComponent> componentSet, HashSet<Channel> allowedChannels)
		{
			if (component == null) {
				Logger.Debug ("Skipping null component");
				return;
			}
			if (!component.ForceInstallation && component.Present && !component.NeedsUpdate) {
				Logger.Debug ($"Skipping component '{component.DetailedDescription}' as it is already present and up-to-date");
				return;
			}
			if (allowedChannels != null && !allowedChannels.Contains (component.Channel)) {
				var channels = string.Join (", ", allowedChannels.Select (c =>  c.Name));
				Logger.Debug ($"Skipping component '{component.DetailedDescription}' in channel '{component.Channel?.Name}' as it is not in the allowed channels ({channels})");
				return;
			}
			if (componentSet.Contains (component)) {
				Logger.Debug ($"Skipping component '{component.DetailedDescription}' as it is already in the set");
				return;
			}

			Logger.Debug ($"Adding component '{component.DetailedDescription}'");
			componentSet.Add (component);

			string hostOS = AndroidUtilities.GetPlatformOS ();
			if (component.Dependencies != null) {
				foreach (Dependency dep in component.Dependencies) {
					if (dep == null || String.IsNullOrEmpty (dep.Path))
						continue;
					Logger.Debug ($"{component.DetailedDescription} depends on: '{dep.Path}'{GetMinimumRevisionString (dep)}");
					IList <IAndroidComponent> deps = instance.Components.AllWithPath (dep.Path, dep.MinRevision);
					if (deps == null || deps.Count == 0) {
						Logger.Debug ($"{component.DetailedDescription}: dependency package '{dep.Path}' not found");
						continue;
					}
					string instances = GetPluralForm (deps.Count, "instance", "instances");
					Logger.Debug ($"{component.DetailedDescription}: found {deps.Count} {instances} of '{dep.Path}'");
					foreach (IAndroidComponent dc in deps) {
						if (dc == component)
							continue;
						AddComponentToSet (instance, dc, componentSet, allowedChannels);
					}
				}
			}
		}

		string GetPluralForm (int count, string singular, string plural)
		{
			return count == 1 ? singular : plural;
		}

		string GetMinimumRevisionString (Dependency dep)
		{
			if (dep?.MinRevision == null)
				return ", any revision";
			return $", minimum revision {dep.MinRevision}";
		}

		void InstallComponent (IAndroidComponent c, AndroidSdkInstance instance, IProgressMonitor monitor = null)
		{
			if (c == null || (!c.ForceInstallation && c.Present && !c.NeedsUpdate) || (c.Archives == null || c.Archives.Count == 0))
				return;
			var component = c as BasePackage;
			if (component == null)
				throw new InvalidOperationException ($"Internal error: unexpected component type {c.GetType ()}");

			int installSubComponentIndex = 0;
			bool isMonitorWithTotalProgress;
			if (monitor is MonitorWithTotalProgress wrapperMonitor)
				isMonitorWithTotalProgress = wrapperMonitor.IsMonitorWithTotalProgress;
			else if (monitor is IProgressMonitorWithTotalProgress) {
				monitor = new MonitorWithTotalProgress (monitor, 1);
				isMonitorWithTotalProgress = true;
			} else
				isMonitorWithTotalProgress = false;

			if (isMonitorWithTotalProgress && ((MonitorWithTotalProgress) monitor).IsMonitorWithTotalProgress) {
				((MonitorWithTotalProgress) monitor).State = MonitorWithTotalProgress.States.Install;
				((MonitorWithTotalProgress) monitor).SubComponentsCount = component.Archives.Count;
			}

			// In theory there should be just a single archive in each component, but we never know really...
			foreach (Archive archive in component.Archives) {
				// We can skip the checksum verification, it was done earlier
				// Don't check platform if the component is system-image
				if (!archive.IsInstallable (false, checkPlatform: !(component.Info is AndroidComponentInfoSystemImage)))
					continue;

				string progressMsgFormat = CommonUtilities.Helpers.GetString ("Installing {0}");
				string componentName = component.DetailedDescription;

				InstallationProgressEventArgs.InstallationProgressActionDelegate progressCallback =
					delegate (float progress) {
						//Logger.Debug($"[InstallationProgressActionDelegate] will report progress: {progress}");
						OnInstallationProgress (false, progress, progressMsgFormat, componentName);
						monitor?.ReportProgress ((long) progress);
					};

				OnInstallationProgress (true, 0f, progressMsgFormat, componentName);

				monitor?.BeginStep (String.Format (progressMsgFormat, componentName), 100);

				if (isMonitorWithTotalProgress && ((MonitorWithTotalProgress) monitor).IsMonitorWithTotalProgress) {
					((MonitorWithTotalProgress) monitor).SubComponentIndex = installSubComponentIndex++;
				}

				Repository.Install (component, archive.DownloadedFilePath, instance.Path, progressCallback);
				monitor?.EndStep (new AndroidSDKComponentInstallationResult (AndroidSDKComponentInstallationResult.States.InstalledSuccessfully));
			}
		}

		void DetectSdkComponents (AndroidSdkInstance sdkInstance)
		{
			if (String.IsNullOrEmpty (sdkInstance.Path))
				throw new InvalidOperationException ("Internal error: Android SDK instance without associated path");

			Logger.Debug ($"Detecting Android SDK in '{sdkInstance.Path}'");
			Repository.Detect (sdkInstance);

			var sourcePropsFileName = "source.properties";
			string path = Path.Combine (sdkInstance.Path, Constants.ComponentPaths.Tools, Constants.RequiredComponentVersions.RequiredCommandLineToolsVersion, sourcePropsFileName);
			if (!File.Exists (path)) {
				path = Path.Combine (sdkInstance.Path, Constants.ComponentPaths.ToolsObsolete, sourcePropsFileName);
			}

			JavaProperties props = AndroidUtilities.ReadAndroidProperties (path);

			if (!props.GetPkgRevision (out string origRev, out AndroidRevision instv)) {
				sdkInstance.Version = new AndroidRevision (0, 0, 0, 0);
				sdkInstance.VersionString = "r0";
				Logger.Debug ("Could not retrieve Pkg.Revision from {0}, r0 assumed", path);
				return;
			}
			sdkInstance.VersionString = "r" + origRev;
			sdkInstance.Version = instv;

			// The paths currently overlap with the contents of the tools package, but this can change hence the
			// code duplication
			List <string> sdkExistenceCheckPaths;
			switch (AndroidSDKContext.Instance.Platform) {
				case AndroidSDKPlatform.Linux:
				case AndroidSDKPlatform.Mac:
					sdkExistenceCheckPaths = new List<string> {
						Path.Combine (Constants.ComponentPaths.Tools, Constants.RequiredComponentVersions.RequiredCommandLineToolsVersion, "bin", "avdmanager"),
						Path.Combine (Constants.ComponentPaths.Tools, Constants.RequiredComponentVersions.RequiredCommandLineToolsVersion, "bin", "lint"),
						Path.Combine (Constants.ComponentPaths.Tools, Constants.RequiredComponentVersions.RequiredCommandLineToolsVersion, "bin", "sdkmanager"),
					};
					break;

				case AndroidSDKPlatform.Windows:
					sdkExistenceCheckPaths = new List<string> {
						Path.Combine (Constants.ComponentPaths.Tools, Constants.RequiredComponentVersions.RequiredCommandLineToolsVersion, "bin", "avdmanager.bat"),
						Path.Combine (Constants.ComponentPaths.Tools, Constants.RequiredComponentVersions.RequiredCommandLineToolsVersion, "bin", "lint.bat"),
						Path.Combine (Constants.ComponentPaths.Tools, Constants.RequiredComponentVersions.RequiredCommandLineToolsVersion, "bin", "sdkmanager.bat"),
					};
					break;

				default:
					throw new InvalidOperationException ("Unsupported platform");
			}

			bool someFound = AndroidUtilities.CheckWheterFilesExist (sdkInstance.Path, "Android SDK Core", sdkExistenceCheckPaths, out double percentFound);
			if (!someFound || percentFound < 100.0) {
				if (someFound)
					Logger.Info ("'Android SDK Core' has less than 100% of its files on disk. Update is necessary.");
			}
		}

		void IParserErrorHandler.OnRecoverableError (ParserErrorLevel recommendedLevel, Uri documentUrl, XElement element, string message)
		{
			switch (recommendedLevel) {
				case ParserErrorLevel.Debug:
					Logger.Debug (message);
					break;

				case ParserErrorLevel.Info:
					Logger.Info (message);
					break;

				case ParserErrorLevel.Error:
				case ParserErrorLevel.Warning:
					Logger.Warning (message);
					break;

				default:
					goto case ParserErrorLevel.Info;
			}
		}

		void IParserErrorHandler.OnFatalError (Uri documentUrl, XElement element, string message)
		{
			Logger.Error (message);
		}

		static void LogParserError(string whereItHappened, Exception e)
		{
			Logger.Error($"[{whereItHappened}]: Failure parsing Android manifest: {e.Message}");
			Logger.Error($"[{whereItHappened}]: This is usually due to bad XML in one or more of the SDK manifests.");
		}

		/// <summary>
		/// Returns if the license was accepted in this Android SDK
		/// </summary>
		/// <param name="instance">Android SDK instance.</param>
		/// <param name="license">license to check</param>
		/// <returns>true if the license is accepted, false otherwise</returns>
		public bool IsLicenseAccepted(AndroidSdkInstance instance, License license)
		{
			if (instance == null || license == null)
			{
				return false;
			}

			return this.LicensesStorage.IsLicenseAccepted(instance.Path, license);
		}

		/// <summary>
		/// Accept licenses
		/// </summary>
		/// <param name="instance">Android SDK instance.</param>
		/// <param name="licenses">list of licenses to accept</param>
		/// <param name="token">cancellation token</param>
		/// <param name="logPath">log file path</param>
		/// <param name="javaSdkPath">custom Java SDK path (optional)</param>
		/// <param name="throwsErrorIfValidationFailed">if <c>true</c>, throws an exception if license validation failed, otherwise it will just log the error and return</param>
		public Task AcceptLicensesAsync(AndroidSdkInstance instance, IEnumerable<License> licenses, CancellationToken token, string javaSdkPath = null, string logPath = null, bool throwsErrorIfValidationFailed = false)
		{
			if (instance != null) {
				var androidSdkPath = instance.Path;
				var javaPath = string.IsNullOrEmpty(javaSdkPath) ? AndroidSdk.JavaSdkPath : javaSdkPath;

				// try use the preferred cmdline-tools
				var cmdlineToolsRoot = Path.Combine(androidSdkPath, Constants.ComponentPaths.Tools);
				var cmdLineToolsPath = Path.Combine(cmdlineToolsRoot, Constants.RequiredComponentVersions.RequiredCommandLineToolsVersion);
				var cmdLineToolsPropertiesFilePath = Path.Combine(cmdLineToolsPath, "source.properties");
				if (!File.Exists(cmdLineToolsPropertiesFilePath))
				{
					// try use the latest cmdline-tools installed
					if (Directory.Exists(cmdlineToolsRoot))
					{
						cmdLineToolsPath = Directory.EnumerateDirectories(cmdlineToolsRoot).OrderBy(d => Path.GetFileName(d)).LastOrDefault();
					}
					else
					{
						cmdLineToolsPath = string.Empty;
					}
				}

				return LicensesStorage.AcceptLicensesAsync(androidSdkPath, javaPath, cmdLineToolsPath, licenses, token, logPath, throwsErrorIfValidationFailed);
			}

			return Task.CompletedTask;
		}
	}
}

