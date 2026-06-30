//
//  Authors:
//    Marek Habersack <grendel@twistedcode.net>
//
//  opyright (c) 2017, Microsoft Corp. (http://microsoft.com)
//
//  All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Xamarin.Installer.AndroidSDK.Common;
using Xamarin.Installer.Common;

namespace Xamarin.Installer.AndroidSDK
{
    public abstract class Repository
    {
        public string Name { get; }
        public Uri ManifestURL { get; protected set; }
        public IList<IAndroidComponent> Components { get; }
        public IList<IJdkComponent> JdkComponents { get; } = new List<IJdkComponent>();
        public bool Parsed { get; protected set; }
        public IDictionary<string, License> Licenses { get; }
        public IDictionary<string, Channel> Channels { get; protected set; }
        public Channel DefaultChannel { get; protected set; }
        public Dictionary<string, XNamespace> Namespaces { get; protected set; }

        /// <summary>
        /// The repository is considered Offline if manifest failed to download.
        /// </summary>
        /// <value></value>
        public bool IsOffline { get; protected set; }

        /// <summary>
        /// Used to cache and retrieve cached manifest.
        /// </summary>
        /// <value></value>
        protected ILocalManifestProvider ManifestCacher { get; set; }

        protected Repository(string name, Uri manifestURL) : this(name)
        {
            ManifestURL = manifestURL ?? throw new ArgumentNullException(nameof(manifestURL));
        }

        protected Repository(string name)
        {
            Components = new List<IAndroidComponent>();
            Licenses = new Dictionary<string, License>(StringComparer.Ordinal);
            Channels = new Dictionary<string, Channel>(StringComparer.Ordinal);
            Name = String.IsNullOrEmpty(name) ? "Unnamed Repository" : name;
        }

        public void Remove(IAndroidComponent component, string androidSDKRoot)
        {
            if (component == null)
                throw new ArgumentNullException(nameof(component));

            androidSDKRoot = androidSDKRoot?.Trim();
            if (String.IsNullOrEmpty(androidSDKRoot))
                throw new ArgumentException("must not be null or empty", nameof(androidSDKRoot));

            var rp = EnsureBasePackage(component);
            Logger.Info($"Removing Android SDK component: {rp.DetailedDescription} ({rp.Path})");
            rp.Remove(androidSDKRoot);
        }

        public void Install(IAndroidComponent component, string archivePath, string androidSDKRoot,
            InstallationProgressEventArgs.InstallationProgressActionDelegate progressCallback = null)
        {
            if (component == null)
                throw new ArgumentNullException(nameof(component));

            archivePath = archivePath?.Trim();
            if (String.IsNullOrEmpty(archivePath))
                throw new ArgumentException("must not be null or empty", nameof(archivePath));

            androidSDKRoot = androidSDKRoot?.Trim();
            if (String.IsNullOrEmpty(androidSDKRoot))
                throw new ArgumentException("must not be null or empty", nameof(androidSDKRoot));

            InstallComponent(component, archivePath, androidSDKRoot, progressCallback);
        }

        protected virtual void InstallComponent(IAndroidComponent component, string archivePath, string androidSDKRoot,
            InstallationProgressEventArgs.InstallationProgressActionDelegate progressCallback = null)
        {
            var rp = EnsureBasePackage(component);
            Logger.Info($"Installing Android SDK component: {rp.DetailedDescription} ({rp.Path})");
            try
            {
                rp.Install(archivePath, androidSDKRoot, progressCallback);
            }
            catch (Exception ex)
            {
                Logger.Exception($"Installation of Android SDK component '{rp.DetailedDescription}' failed with exception.", ex);
                if (component.IgnoreFailure)
                    Logger.Info($"Ignoring exception for Android SDK component '{rp.DetailedDescription}' - it is not critical for development.");
                else
                    throw;
            }
        }

        public void Detect(AndroidSdkInstance sdkInstance)
        {
            if (sdkInstance == null)
                throw new ArgumentNullException(nameof(sdkInstance));

            sdkInstance.Components = Components.Where(c => c != null).Select(c => c.Clone()).ToList();
            DetectInternal(sdkInstance);
        }

        public void Refresh(AndroidSdkInstance sdkInstance)
        {
            if (sdkInstance == null)
                throw new ArgumentNullException(nameof(sdkInstance));

            if (Components == null || Components.Count == 0 || sdkInstance.Components == null | sdkInstance.Components.Count == 0)
                return;

            foreach (IAndroidComponent c in Components)
            {
                if (c == null)
                    continue;

                IAndroidComponent match = FindMatchedComponent(c, sdkInstance.Components);

                if (match == null)
                {
                    sdkInstance.Components.Add(c);
                    continue;
                }

                BasePackage remotePackage = EnsureBasePackage(c);
                BasePackage instancePackage = EnsureBasePackage(match);
                instancePackage.PerformDetection(sdkInstance.Path, true);
                instancePackage.RefreshMetadata(remotePackage);
            }

            var componentsToRemove = sdkInstance.Components
                .Where(c => FindMatchedComponent(c, Components) == null)
                .ToArray();
            foreach (var c in componentsToRemove)
                sdkInstance.Components.Remove(c);
        }

        IAndroidComponent FindMatchedComponent(IAndroidComponent c, IList<IAndroidComponent> componentList)
        {
            return componentList.FirstOrDefault(component => component?.MatchesTo(c) == true);
        }

        BasePackage EnsureBasePackage(IAndroidComponent c)
        {
            var ret = c as BasePackage;
            if (ret == null)
                throw new InvalidOperationException($"Internal error: unsupported component type {c.GetType()}");

            return ret;
        }

        protected virtual void DetectInternal(AndroidSdkInstance sdkInstance)
        {
            if (sdkInstance.Components.Count == 0)
            {
                Logger.Info($"Unable to perform detection in '{Name}', no known components");
                return;
            }

            foreach (IAndroidComponent c in sdkInstance.Components)
            {
                DetectComponent(sdkInstance, c);
            }
        }

        protected virtual void DetectComponent(AndroidSdkInstance sdkInstance, IAndroidComponent c)
        {
            EnsureBasePackage(c)?.PerformDetection(sdkInstance.Path);
        }

        public Channel GetChannel(string channelId)
        {
            return GetDictionaryEntry(Channels, channelId);
        }

        public License GetLicense(string licenseId)
        {
            return GetDictionaryEntry(Licenses, licenseId);
        }

        protected T GetDictionaryEntry<T>(IDictionary<string, T> dict, string entryId) where T : class
        {
            if (dict == null)
                return null;

            entryId = entryId?.Trim();
            if (String.IsNullOrEmpty(entryId))
                return null;

            T ret;
            if (!dict.TryGetValue(entryId, out ret))
                return null;

            return ret;
        }

        protected void CopyDictionary<K, V>(IDictionary<K, V> source, IDictionary<K, V> destination)
        {
            if (source == null)
                return;
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));

            destination.Clear();
            foreach (var kvp in source)
                destination.Add(kvp.Key, kvp.Value);
        }

        public string GetNamespaceUri(string nsName)
        {
            if (String.IsNullOrEmpty(nsName) || Namespaces == null)
                return String.Empty;

            XNamespace ns;
            if (Namespaces.TryGetValue(nsName, out ns) && ns != null)
                return ns.NamespaceName ?? String.Empty;

            return String.Empty;
        }

        public abstract void Parse();

        /// <summary>
        /// Loads online manifest using <c>ManifestURL</c>;
        /// If it fails, tries to load local cached manifest;
        /// If local manifest is loaded, sets <c>IsOffline</c> to <c>true</c> and returns it.
        /// </summary>
        /// <returns></returns>
        protected string LoadManifest()
        {
            string manifest = null;
            Exception exception = null;
            try
            {
                if (CommonUtilities.Helpers.DownloadToString(ManifestURL, out manifest) && manifest != null)
                {
                    IsOffline = false;
                    ManifestCacher?.SaveManifest(manifest);
                    return manifest;
                }

            }
            catch (Exception ex)
            {
                exception = ex;
            }

            // In case something throwed during manifest caching.
            if (manifest != null)
                return manifest;

            IsOffline = true;
            Logger.Error($"Failed to download {Name} manifest from {ManifestURL}. {exception}");

            // If download from primary URL fails, attempt to use cached manifest
            if (ManifestCacher != null)
            {
                Logger.Info("Trying to load cached manifest...");

                manifest = ManifestCacher.GetManifest();
                if (manifest != null)
                {
                    Logger.Info("Using cached manifest...");
                    return manifest;
                }
                else
                {
                    Logger.Info("Unable to use cached manifest.");
                }
            }

            // If both primary URL and cached manifest fail, try fallback URL
            var fallbackUrl = GetFallbackManifestUrl();
            if (fallbackUrl != null)
            {
                Logger.Info("Trying to load fallback  manifest...");
                if (CommonUtilities.Helpers.DownloadToString(fallbackUrl, out manifest) && manifest != null)
                {

                    IsOffline = false;
                    Logger.Info("Successfully loaded the fallback manifest file");
                    return manifest;
                }
            }
            if (exception != null)
                throw exception;

            return null;
        }

        /// <summary>
        /// Parses manifest xml string into XDocument;
        /// Re-throws if XDocument.Load throws;
        /// Logs manfiest if the parsing failes.
        /// </summary>
        /// <param name="manifestName"></param>
        /// <param name="manifest"></param>
        /// <returns>Returns XDocument</returns>
        protected XDocument ParseManifest(string manifestName, string manifest)
        {
            using (System.IO.Stream input = new System.IO.MemoryStream(Encoding.UTF8.GetBytes(manifest)))
            {
                return ParseManifest(manifestName, input);
            }
        }

        /// <summary>
        /// Parses manifest xml stream into XDocument;
        /// Re-throws if XDocument.Load throws;
        /// Logs manfiest if the parsing failes.
        /// </summary>
        /// <param name="manifestName"></param>
        /// <param name="manifestStream"></param>
        /// <param name="loadOptions"></param>
        /// <returns>Returns XDocument</returns>
        protected XDocument ParseManifest(string manifestName, System.IO.Stream manifestStream, LoadOptions loadOptions = LoadOptions.None)
        {
            try
            {
                return XDocument.Load(manifestStream, LoadOptions.SetLineInfo);
            }
            catch (Exception ex)
            {
                Logger.Exception($"Could not parse {manifestName} xml. For more info, check the logged manifest file", ex);

                Logger.LogManifest(manifestName, manifestStream);
                throw;
            }
        }

        public virtual Uri GetFallbackManifestUrl()
        {
            return null;
        }
    }
}
