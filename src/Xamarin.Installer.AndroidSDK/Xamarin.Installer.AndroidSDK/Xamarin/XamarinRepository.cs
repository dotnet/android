using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Xml.XPath;
using Xamarin.Installer.AndroidSDK.Common;
using Xamarin.Installer.Common;

namespace Xamarin.Installer.AndroidSDK.Xamarin
{
    class XamarinRepository : Repository
    {
        readonly Dictionary<Type, Func<string, string, XElement, AndroidComponentInfo>> infoParsers;
        readonly Dictionary<Type, AndroidComponentType> infoToComponentType;

        public XamarinRepository(Uri manifestURL = null, bool cacheManifest = false) : base("Xamarin Repository")
        {
            this.ManifestURL = manifestURL ?? GetDefaultManifestURL();

            infoParsers = new Dictionary<Type, Func<string, string, XElement, AndroidComponentInfo>> {
                [typeof (AndroidComponentInfoPlatform)] = ParsePlatformPackageInfo,
                [typeof (AndroidComponentInfoSystemImage)] = ParseSystemImageInfo,
                [typeof (AndroidComponentInfoAddon)] = ParseAddonInfo,
                [typeof (AndroidComponentInfoExtra)] = ParseExtraInfo,
            };

            infoToComponentType = new Dictionary<Type, AndroidComponentType> {
                [typeof (AndroidComponentInfoPlatform)] = AndroidComponentType.Platform,
                [typeof (AndroidComponentInfoSystemImage)] =  AndroidComponentType.SystemImage,
                [typeof (AndroidComponentInfoAddon)] =  AndroidComponentType.Addon,
                [typeof (AndroidComponentInfoExtra)] =  AndroidComponentType.Extra,
            };

            var defaultChannel = new Channel("channel-0", "stable");
            DefaultChannel = defaultChannel;
            Channels.Add("channel-0", defaultChannel);

            if (cacheManifest)
                ManifestCacher = LocalManifestProvider.CreateXamarinManifestProvider();
        }

        Uri GetDefaultManifestURL()
        {
            Logger.Debug($"Android Manifest Version: {Constants.RequiredComponentVersions.AndroidManifestFeedVersion}");
            var androidManifestFeedVersion = Constants.RequiredComponentVersions.AndroidManifestFeedVersion?.Replace(".", "-");
            return new Uri($"https://aka.ms/AndroidManifestFeed/d{androidManifestFeedVersion}"); 
        }

        public override void Parse()
        {
            ParseInternal();
            Parsed = true;
        }

        void ParseInternal()
        {
            string manifest = LoadManifest();
            if (string.IsNullOrEmpty(manifest))
            {
                Logger.Warning($"Android manifest should not be empty! Manifest URL: {ManifestURL}");
                // todo: should we throw here?
                return;
            }

            Uri baseURL = Helpers.GetBaseURL(ManifestURL);
            Logger.Debug($"Xamarin manifest base URL: {baseURL}");

            Components?.Clear();

            XDocument doc = ParseManifest("XamarinAndroidManifest", manifest);

            var namespaces = new Dictionary<string, XNamespace>(StringComparer.Ordinal);
            doc.Root.GetNamespaces(ManifestURL, ref namespaces);
            Namespaces = namespaces;

            XElement root = doc.Root;

            var licenses = new Dictionary<string, License>(StringComparer.Ordinal);
            foreach (XElement element in root.XPathSelectElements("//license[string-length (@id) > 0]"))
            {
                string licenseId = element.GetAttributeValue("id", documentUrl: ManifestURL);
                if (licenses.ContainsKey(licenseId))
                    continue;
                string licenseType = element.GetAttributeValue("type", required: false, documentUrl: ManifestURL);
                licenses.Add(licenseId, new License(licenseId, licenseType, element.Value));
            }
            if (licenses.Count > 0)
                CopyDictionary(licenses, Licenses);

            ParsePackages<AndroidComponentInfoGeneric>("platform tools", root.XPathSelectElements("./platform-tools"));

            // Obsolete. Using for backward compatibility.
            ParsePackages<AndroidComponentInfoGeneric>("tools", root.XPathSelectElements("./tools"));

            ParsePackages<AndroidComponentInfoGeneric>("cmdline-tools", root.XPathSelectElements("./cmdline-tools"));
            ParsePackages<AndroidComponentInfoGeneric>("build tools", root.XPathSelectElements("./build-tool"));
            ParsePackages<AndroidComponentInfoGeneric>("emulators", root.XPathSelectElements("./emulator"));
            ParsePackages<AndroidComponentInfoGeneric>("NDK", root.XPathSelectElements("./ndk"));
            ParsePackages<AndroidComponentInfoGeneric>("LLDB", root.XPathSelectElements("./lldb"));
            ParsePackages<AndroidComponentInfoGeneric>("patchers", root.XPathSelectElements("./patcher"));
            ParsePackages<AndroidComponentInfoPlatform>("platforms", root.XPathSelectElements("./platform"));
            ParsePackages<AndroidComponentInfoSystemImage>("system images", root.XPathSelectElements("./system-image"));
            ParsePackages<AndroidComponentInfoAddon>("addons", root.XPathSelectElements("./addon"));
            ParsePackages<AndroidComponentInfoExtra>("extras", root.XPathSelectElements("./extra"));
            ParseJdkPackages(root.XPathSelectElements("./jdk"));
        }

        void ParseJdkPackages(IEnumerable<XElement> elements)
        {
            foreach (XElement element in elements)
            {
                JdkComponents.Add(ParseJdkPackage(element));
            }
        }

        JdkPackage ParseJdkPackage(XElement element)
        {
            var description = element.GetAttributeValue("description", documentUrl: ManifestURL);
            var vendorId = element.GetAttributeValue("vendor-id", documentUrl: ManifestURL);
            var vendorDisplay = element.GetAttributeValue("vendor-display", documentUrl: ManifestURL);
            var vendor = CreateVendor(vendorId, vendorDisplay);
            var revision = new AndroidRevision(element.GetAttributeValue("revision", documentUrl: ManifestURL));
            string licenseId = element.GetAttributeValue("license", documentUrl: ManifestURL);
            bool obsolete = element.GetAttributeValue("obsolete", documentUrl: ManifestURL).AsBool();
            bool preview = element.GetAttributeValue("preview", documentUrl: ManifestURL).AsBool();

            var archives = new List<JdkArchive>();
            foreach (XElement urlElement in element.XPathSelectElements("./urls/url"))
            {
                var archive = ParseJDKArchive(urlElement);
                archives.Add(archive);
            }

            if (archives.Count == 0)
                throw new InvalidOperationException($"Package {element.Name} at {element.GetLocation(ManifestURL)} does not have any valid URLs");
            
            return new JdkPackage(description, obsolete, preview, licenseId, vendor, revision, archives);
        }

        void ParsePackages<TInfo>(string name, IEnumerable<XElement> elements) where TInfo : AndroidComponentInfo
        {
            Logger.Debug($"Parsing: {name}");
            if (!elements.Any())
            {
                Logger.Debug("No elements of this kind found");
                return;
            }

            AndroidComponentInfo info = null;
            Func<string, string, XElement, AndroidComponentInfo> infoParser;

            foreach (XElement element in elements)
            {
                string type = element.GetAttributeValue("original-type", documentUrl: ManifestURL);
                info = null;
                if (infoParsers.TryGetValue(typeof(TInfo), out infoParser) && infoParser != null)
                {
                    info = infoParser(name, type, element);
                }

                if (info == null)
                    info = new AndroidComponentInfoGeneric(type);

                Components.Add(ParseCommonPackageData(name, info, element));
            }
        }

        XamarinPackage ParseCommonPackageData(string name, AndroidComponentInfo info, XElement element)
        {
            // Ignored attributes (obsolete):
            //    preview
            //
            var revision = new AndroidRevision(element.GetAttributeValue("revision", documentUrl: ManifestURL));
            string path = element.GetAttributeValue("path", documentUrl: ManifestURL);
            string filesystemPath = element.GetAttributeValue("filesystem-path", documentUrl: ManifestURL);
            string description = element.GetAttributeValue("description", documentUrl: ManifestURL);
            string licenseId = element.GetAttributeValue("license", documentUrl: ManifestURL);
            bool obsolete = element.GetAttributeValue("path", documentUrl: ManifestURL).AsBool();
            Uri originalManifestUri = element.GetAttributeValue("manifest-url", documentUrl: ManifestURL).AsUri();

            var archives = new List<Archive>();
            foreach (XElement urlElement in element.XPathSelectElements("./urls/url"))
            {
                archives.Add(ParseArchive<Archive>(urlElement));
            }

            if (archives.Count == 0)
                throw new InvalidOperationException($"Package {element.Name} at {element.GetLocation(ManifestURL)} does not have any valid URLs");

            var dependencies = new List<Dependency>();
            foreach (XElement urlElement in element.XPathSelectElements("./dependencies/dependency"))
            {
                Dependency dep = ParseDependency(urlElement);
                if (dep.Platform != AndroidSDKPlatform.Any && dep.Platform != AndroidSDKPlatform.Unknown && dep.Platform != AndroidSDKContext.Instance.Platform)
                    continue;
                dependencies.Add(dep);
            }
            if (dependencies.Count == 0)
                dependencies = null;

            AndroidComponentType componentType;
            if (!infoToComponentType.TryGetValue(info.GetType(), out componentType))
                componentType = AndroidComponentType.Generic;

            return new XamarinPackage(this, ManifestURL, element.GetLineInfo(), archives, componentType)
            {
                Revision = revision,
                Path = path,
                FileSystemPath = filesystemPath,
                DisplayName = description,
                Obsolete = obsolete,
                OriginalManifestUri = originalManifestUri,
                LicenseID = licenseId,
                Info = info,
                Dependencies = dependencies,
            };
        }

        Dependency ParseDependency(XElement element)
        {
            string path = element.GetAttributeValue("path", documentUrl: ManifestURL);
            string minRevision = element.GetAttributeValue("min-revision", documentUrl: ManifestURL);
            string hostOS = element.GetAttributeValue("host-os", documentUrl: ManifestURL);

            return new Dependency(path, String.IsNullOrEmpty(minRevision) ? null : new AndroidRevision(minRevision))
            {
                Platform = String.IsNullOrEmpty(hostOS) ? AndroidSDKPlatform.Unknown : AndroidUtilities.GetPlatformFromOS(hostOS)
            };
        }

        JdkArchive ParseJDKArchive(XElement element)
        {
            string payloadFileName = element.GetAttributeValue("payloadFileName", documentUrl: ManifestURL);
            var archive = ParseArchive<JdkArchive>(element);
            archive.PayloadFileName = payloadFileName;
            return archive;
        }

        T ParseArchive<T>(XElement element) where T : ArchiveBase
        {
            ulong size = element.GetAttributeValue("size", documentUrl: ManifestURL).AsULong();
            string checksum = element.GetAttributeValue("checksum", documentUrl: ManifestURL);
            string checksumType = element.GetAttributeValue("checksum-type", required: false, documentUrl: ManifestURL);
            string hostOS = element.GetAttributeValue("host-os", documentUrl: ManifestURL);
            string hostArch = element.GetAttributeValue("host-arch", required: false, documentUrl: ManifestURL);
            uint hostBits = element.GetAttributeValue("host-bits", documentUrl: ManifestURL).AsUInt();
            Uri url = element.GetElementValue().AsUri(true);

            var archive = (T)Activator.CreateInstance(typeof(T), new object[] { hostOS });
            archive.Size = size;
            archive.Checksum = checksum;
            archive.ChecksumType = checksumType;
            archive.HostArch = hostArch;
            archive.HostBits = hostBits;
            archive.Url = url;

            return archive;
        }

        AndroidComponentInfo ParsePlatformPackageInfo(string name, string packageType, XElement element)
        {
            // Ignored attributes (obsolete):
            //    min-tools-rev
            //    layoutlib-revision
            //
            string apiLevel = element.GetAttributeValue("api", documentUrl: ManifestURL);
            string codeName = element.GetAttributeValue("codename", documentUrl: ManifestURL);
            string layoutLibApi = element.GetAttributeValue("layoutlib-api", documentUrl: ManifestURL);
            string description = element.GetAttributeValue("description", documentUrl: ManifestURL);
            string preview = element.GetAttributeValue("preview", documentUrl: ManifestURL);

            return new AndroidComponentInfoPlatform("platform", apiLevel, codeName, layoutLibApi)
            {
                Description = description,
                Preview = preview != "False",
            };
        }

        AndroidComponentInfo ParseSystemImageInfo(string name, string packageType, XElement element)
        {
            string apiLevel = element.GetAttributeValue("api", documentUrl: ManifestURL);
            string abi = element.GetAttributeValue("abi", documentUrl: ManifestURL);
            string codeName = element.GetAttributeValue("codename", documentUrl: ManifestURL);
            string tagId = element.GetAttributeValue("tag-id", documentUrl: ManifestURL);
            string tagDisplay = element.GetAttributeValue("tag-display", documentUrl: ManifestURL);
            string vendorId = element.GetAttributeValue("vendor-id", documentUrl: ManifestURL);
            string vendorDisplay = element.GetAttributeValue("vendor-display", documentUrl: ManifestURL);

            return new AndroidComponentInfoSystemImage(
                packageType,
                AndroidUtilities.StringToAbi(abi),
                abi,
                apiLevel,
                codeName,
                CreateTag(tagId, tagDisplay),
                CreateVendor(vendorId, vendorDisplay)
            );
        }

        AndroidComponentInfo ParseAddonInfo(string name, string packageType, XElement element)
        {
            string apiLevel = element.GetAttributeValue("api", documentUrl: ManifestURL);
            string codeName = element.GetAttributeValue("codename", documentUrl: ManifestURL);
            string tagId = element.GetAttributeValue("tag-id", documentUrl: ManifestURL);
            string tagDisplay = element.GetAttributeValue("tag-display", documentUrl: ManifestURL);
            string vendorId = element.GetAttributeValue("vendor-id", documentUrl: ManifestURL);
            string vendorDisplay = element.GetAttributeValue("vendor-display", documentUrl: ManifestURL);

            var libraries = new List<PackageLibrary>();
            foreach (XElement libElement in element.XPathSelectElements("./libraries/library"))
            {
                libraries.Add(ParseLibrary(libElement));
            }

            return new AndroidComponentInfoAddon(
                packageType,
                apiLevel,
                codeName,
                CreateTag(tagId, tagDisplay),
                CreateVendor(vendorId, vendorDisplay),
                libraries
            );
        }

        PackageLibrary ParseLibrary(XElement element)
        {
            string name = element.GetAttributeValue("name", documentUrl: ManifestURL);
            string localJarPath = element.GetAttributeValue("local-jar-path", documentUrl: ManifestURL);
            string description = element.GetAttributeValue("description", documentUrl: ManifestURL);

            return new PackageLibrary
            {
                Name = name,
                LocalJarPath = localJarPath,
                Description = description
            };
        }

        AndroidComponentInfo ParseExtraInfo(string name, string packageType, XElement element)
        {
            string vendorId = element.GetAttributeValue("vendor-id", documentUrl: ManifestURL);
            string vendorDisplay = element.GetAttributeValue("vendor-display", documentUrl: ManifestURL);

            return new AndroidComponentInfoExtra(packageType, CreateVendor(vendorId, vendorDisplay));
        }

        PackageTag CreateTag(string id, string display)
        {
            if (String.IsNullOrEmpty(id))
                return null;

            return new PackageTag(id, display);
        }

        PackageVendor CreateVendor(string id, string display)
        {
            if (String.IsNullOrEmpty(id))
                return null;

            return new PackageVendor(id, display);
        }

        public override Uri GetFallbackManifestUrl()
        {
            Logger.Debug($"Fallback Android Manifest Version: {Constants.RequiredComponentVersions.AndroidManifestFeedVersion}");
            var dirPathParent = new Uri(typeof(XamarinRepository).Assembly.Location);
            var manifestUri = new Uri(dirPathParent, $"AndroidManifestFeed_d{Constants.RequiredComponentVersions.AndroidManifestFeedVersion}.xml");

            Logger.Debug($"Fallback Manifest Url:: manifestUri: {manifestUri}");
            return manifestUri;
        }
    }
}
