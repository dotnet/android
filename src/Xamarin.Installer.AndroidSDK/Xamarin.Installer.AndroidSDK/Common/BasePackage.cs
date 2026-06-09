//
//  Authors:
//    Marek Habersack <grendel@twistedcode.net>
//
//  Copyright (c) 2018, Microsoft Corp. (https://microsoft.com)
//
//  All rights reserved.
//
using Kajabity.Tools.Java;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using Xamarin.Installer.Common;
using IOPath = System.IO.Path;

namespace Xamarin.Installer.AndroidSDK.Common
{
    public abstract class BasePackage : IEquatable<BasePackage>, IAndroidComponent, IAndroidComponentInternal
    {
        protected Repository Repository { get; }
        protected IList<Archive> OriginalArchives { get; }

        public event EventHandler<AndroidComponentStatusChangeEventArgs> StatusChanged;

        public abstract AndroidComponentType ComponentType { get; }
        public Guid UniqueID { get; } = Guid.NewGuid();
        public bool IsEssential { get; set; }
        public bool IgnoreFailure { get; set; }
        public bool Present { get; set; }
        public bool NeedsUpdate { get; set; }
        public string Path { get; set; }
        public string FileSystemPath { get; set; }
        public bool Obsolete { get; set; }
        public bool Preview => GetPreview();
        public AndroidComponentInfo Info { get; set; }
        public AndroidRevision Revision { get; set; }
        public AndroidRevision InstalledRevision { get; set; }
        public string DisplayName { get; set; }
        public string DetailedDescription => GetDetailedDescription();
        public IList<Dependency> Dependencies { get; set; }
        public abstract Channel Channel { get; }
        public IList<Archive> Archives { get; protected set; }
        public License License => Repository.GetLicense(LicenseID);
        public string LicenseID { get; set; }
        public Uri ManifestURL { get; }
        public IXmlLineInfo Location { get; }
        public bool ForceInstallation { get; set; }

        public BasePackage(Repository repository, Uri manifestURL, IXmlLineInfo location, IList<Archive> archives)
        {
            Repository = repository ?? throw new ArgumentNullException(nameof(repository));
            ManifestURL = manifestURL ?? throw new ArgumentNullException(nameof(manifestURL));
            Location = location;
            OriginalArchives = archives;
            if (archives == null || archives.Count == 0)
                return;

            foreach (Archive archive in archives)
            {
                if (archive == null)
                    continue;
                archive.Owner = this;
            }
        }

        public IEnumerable<AndroidSDKPlatform> SupportedPlatforms
        {
            get
            {
                //todo OriginalArchives?
                foreach (var archive in Archives)
                    yield return archive.Platform;
            }
        }

        public bool IsPlatformSpecific
        {
            get
            {
                return !Archives.Any(a => a.Platform == AndroidSDKPlatform.Any || a.Platform == AndroidSDKPlatform.Unknown);
            }
        }

        /// <summary>
        /// NOTE: Using in ManifestGenerator only.
        /// </summary>
        /// <param name="platform"></param>
        public void ExcludePlatform(AndroidSDKPlatform platform)
        {
            Archives = Archives.Where(a => a.Platform != platform).ToList();
            Dependencies = Dependencies.Where(d => d.Platform != platform).ToList();
        }

        protected void PopulatePlatformArchives(bool includeAllArchives)
        {
            var alist = new List<Archive>();
            AndroidSDKPlatform platform = AndroidSDKContext.Instance.Platform;

            foreach (Archive archive in OriginalArchives)
            {
                if (archive == null || (!includeAllArchives && !archive.IsValidForSystem()))
                    continue;
                alist.Add(archive);
            }


            bool IsBitsValid(uint bits) => ((bits == 64) == CommonUtilities.Helpers.Is64BitOS);
            // Remove all archives with HostBits not matching the OS Bits
            // Apply only if there is any matching in the list
            if (!includeAllArchives && alist.Count > 1 && alist.Any(a => IsBitsValid(a.HostBits)))
            {
                alist.RemoveAll(a => !IsBitsValid(a.HostBits));
            }

            Archives = alist.AsReadOnly();
        }

        protected virtual string GetDetailedDescription()
        {
            string desc = $"{DisplayName} r{Revision}";
            if (!String.IsNullOrEmpty(Info?.DetailedDescription))
                return $"{desc} [{Info?.DetailedDescription}]";
            return desc;
        }

        protected void OnStatusChange(AndroidComponentStatus status)
        {
            if (StatusChanged == null)
                return;

            StatusChanged(this, new AndroidComponentStatusChangeEventArgs(status, this));
        }

        public void Remove(string androidSDKRoot)
        {
            OnStatusChange(AndroidComponentStatus.RemovalStarted);
            try
            {
                PackageMetadata metadata = CreateMetadata(this);

                DoRemove(androidSDKRoot, retries: 5, delayInSeconds: 5);

                PerformDetection(androidSDKRoot, true);
                RefreshMetadata(this, false);
            }
            finally
            {
                OnStatusChange(AndroidComponentStatus.RemovalEnded);
            }
        }

        void DoRemove(string androidSDKRoot, int retries, int delayInSeconds)
        {
            if (String.IsNullOrEmpty(androidSDKRoot))
                throw new ArgumentException("must not be null or empty", nameof(androidSDKRoot));

            var ex = default(Exception);
            var success = false;
            while (!success && retries > 0)
            {
                try
                {
                    DoRemove(androidSDKRoot);
                    success = true;
                }
                catch (UnauthorizedAccessException uae)
                {
                    ex = uae;
                    retries = retries - 1;
                    Thread.Sleep(TimeSpan.FromSeconds(delayInSeconds));
                    delayInSeconds = delayInSeconds + 3;
                }
            }

            if (!success && ex != null)
                throw ex;
        }

        void DoRemove(string androidSDKRoot)
        {
            if (String.IsNullOrEmpty(androidSDKRoot))
                throw new ArgumentException("must not be null or empty", nameof(androidSDKRoot));

            string packagePath = IOPath.Combine(androidSDKRoot, FileSystemPath);
            if (File.Exists(packagePath))
                File.Delete(packagePath);
            else if (Directory.Exists(packagePath))
                Directory.Delete(packagePath, true);
            else
                return;

            DeleteDirTreeIfEmpty(
                new DirectoryInfo(GetRealDirectoryName(androidSDKRoot)),
                new DirectoryInfo(GetRealDirectoryName(IOPath.GetDirectoryName(packagePath)))
            );
        }

        public abstract void RefreshMetadata(IAndroidComponent component, bool ignoreInstalledState = true);
        protected abstract PackageMetadata CreateMetadata(BasePackage component);

        public void PerformDetection(string androidSDKRoot, bool isRefresh = false)
        {
            if (String.IsNullOrEmpty(androidSDKRoot))
                throw new ArgumentException("must not be null or empty", nameof(androidSDKRoot));

            PackageMetadata metadata = isRefresh ? CreateMetadata(this) : null;
            string description = DetailedDescription;
            Present = false;
            InstalledRevision = null;
            ForceInstallation = false;
            OnStatusChange(AndroidComponentStatus.DetectionStarted);
            try
            {
                NeedsUpdate = Detect(androidSDKRoot, description);
            }
            catch (Exception ex)
            {
                LogInfo($"Component {description} detection failed with exception. Component will be marked as outdated.");
                LogInfo(ex.ToString());
                NeedsUpdate = true;
            }
            finally
            {
                OnStatusChange(AndroidComponentStatus.DetectionEnded);
                if (MetadataChanged(metadata))
                    OnStatusChange(AndroidComponentStatus.MetadataUpdated);
            }
        }

        // Returns `true` if the package is absent or is older than our version
        bool Detect(string androidSDKRoot, string description)
        {
            Present = false;
            string fullPath = IOPath.Combine(androidSDKRoot, FileSystemPath);
            if (!Directory.Exists(fullPath))
            {
                LogDebug($"Component {description} not present on the system");
                return true;
            }
            LogDebug($"Detecting component {description} in directory '{fullPath}'");

            // Treat the package as present only if we have any file with version information
            AndroidRevision instv = null;
            string manifestFile = IOPath.Combine(fullPath, "package.xml");
            if (File.Exists(manifestFile))
            {
                Present = true;
                instv = GetPackageRevision(manifestFile);
            }

            if (instv == null)
            {
                manifestFile = IOPath.Combine(fullPath, "source.properties");
                if (File.Exists(manifestFile))
                {
                    Present = true;
                    JavaProperties props = AndroidUtilities.ReadAndroidProperties(manifestFile);
                    string rev;
                    props.GetPkgRevision(out rev, out instv, DisplayName);
                }
            }

            // https://bugzilla.xamarin.com/show_bug.cgi?id=59714 AndroidComponentInfoSystemImage.Abi reads Default for a x86 image
            // Try to fix system image' package.xml -> abi as it might be wrong
            if (ComponentType == AndroidComponentType.SystemImage && Present)
            {
                AndroidComponentInfoSystemImage info = (AndroidComponentInfoSystemImage)Info;
                if (info != null
                    && (info.Abi == AndroidSystemImageAbi.X86
                        || info.Abi == AndroidSystemImageAbi.ARMV7a
                        || info.Abi == AndroidSystemImageAbi.ARM64V8a))
                {
                    manifestFile = IOPath.Combine(fullPath, "package.xml");
                    if (File.Exists(manifestFile))
                    {
                        string abi = GetAbi(manifestFile);

                        // Rewrite the entire manifest if the abi value differs
                        if (abi != null && abi != info.GetAbiManifestString())
                        {
                            LogWarning($"Found invalid abi \"{abi}\" (expected value: \"{info.GetAbiManifestString()}\") in \"{manifestFile}\", regenerating package.xml...");
                            try
                            {
                                GeneratePackageXml(manifestFile);
                            }
                            catch (Exception ex)
                            {
                                if (ex is IOException || ex is UnauthorizedAccessException)
                                {
                                    LogError($"Could not regenerate package.xml for \"{manifestFile}\" as the process might not have administrative privileges. You will need to fix the manifest manually.\nHere is the error details: {ex}");
                                    // Marking component as not installed
                                    Present = false;
                                    InstalledRevision = null;
                                    return true;
                                }
                                throw;
                            }
                        }
                    }
                }
            }

            if (instv == null)
            {
                LogInfo($"  Version information not found for component {description}");
                return true;
            }

            LogDebug($"  Found revision {instv} on the system");
            InstalledRevision = instv;

            return Revision > InstalledRevision;
        }

        protected AndroidRevision GetPackageRevision(string manifestFile)
        {
            try
            {
                XDocument doc = XDocument.Load(manifestFile);
                XElement revision = doc.Descendants("revision")?.FirstOrDefault();
                if (revision == null)
                    return null;

                // We should use RevisionParser but due to its requirements (parser context) it's rather
                // unwieldy, hence the code duplication here
                return new AndroidRevision(
                    ParseAsInt(revision.Element("major")),
                    ParseAsInt(revision.Element("minor")),
                    ParseAsInt(revision.Element("micro")),
                    ParseAsInt(revision.Element("preview"))
                );
            }
            catch (Exception ex)
            {
                LogDebug($"Exception caught trying to parse {manifestFile} for package version information.");
                LogDebug(ex.ToString());
                return null;
            }
        }

        protected string GetAbi(string manifestFile)
        {
            try
            {
                XDocument doc = XDocument.Load(manifestFile);
                XElement abi = doc.Descendants("abi")?.FirstOrDefault();
                if (abi == null)
                    return null;

                return abi.Value;
            }
            catch (Exception ex)
            {
                LogDebug($"Exception caught trying to parse {manifestFile} for abi information.");
                LogDebug(ex.ToString());
                return null;
            }
        }

        protected int ParseAsInt(XElement e)
        {
            string v = e?.Value?.Trim();
            if (String.IsNullOrEmpty(v))
                return -1;

            int ret;
            if (!Int32.TryParse(v, out ret))
                return -1;

            return ret;
        }

        protected string GetRealDirectoryName(string path)
        {
            if (!Directory.Exists(path))
                return path;

            string cwd = Environment.CurrentDirectory;
            try
            {
                Directory.SetCurrentDirectory(path);
                return Environment.CurrentDirectory;
            }
            finally
            {
                Directory.SetCurrentDirectory(cwd);
            }
        }

        protected void DeleteDirTreeIfEmpty(DirectoryInfo stopAt, DirectoryInfo di)
        {
            if (String.Compare(stopAt.FullName, di.FullName, StringComparison.Ordinal) == 0)
                return;

            if (di.EnumerateFileSystemInfos().Any())
                return;

            DirectoryInfo parent = di.Parent;
            di.Delete();
            DeleteDirTreeIfEmpty(stopAt, parent);
        }

        protected virtual bool MetadataChanged(PackageMetadata metadata, bool update = false, bool ignoreInstalledState = false)
        {
            if (metadata == null)
                return false;

            bool haveChanges = false;
            if (!metadata.Archives.AreEqual(Archives))
            {
                if (update)
                {
                    Archives = new List<Archive>(metadata.Archives).AsReadOnly();
                    haveChanges = true;
                }
                else
                    return true;
            }

            if (!metadata.Dependencies.AreEqual(Dependencies))
            {
                if (update)
                {
                    Dependencies = metadata.Dependencies != null ? new List<Dependency>(metadata.Dependencies).AsReadOnly() : null;
                    haveChanges = true;
                }
                else
                    return true;
            }

            if (String.Compare(metadata.DisplayName, DisplayName, StringComparison.Ordinal) != 0)
            {
                if (update)
                {
                    DisplayName = metadata.DisplayName;
                    haveChanges = true;
                }
                else
                    return true;
            }

            if (String.Compare(metadata.FileSystemPath, FileSystemPath, StringComparison.Ordinal) != 0)
            {
                if (update)
                {
                    FileSystemPath = metadata.FileSystemPath;
                    haveChanges = true;
                }
                else
                    return true;
            }

            if (!AreInfosEqual(Info, metadata.Info))
            {
                if (update)
                {
                    Info = metadata.Info;
                    haveChanges = true;
                }
                else
                    return true;
            }

            if (!ignoreInstalledState && metadata.InstalledRevision != InstalledRevision)
            {
                if (update)
                {
                    InstalledRevision = metadata.InstalledRevision;
                    haveChanges = true;
                }
                else
                    return true;
            }

            if (!ignoreInstalledState && metadata.NeedsUpdate != NeedsUpdate)
            {
                if (update)
                {
                    NeedsUpdate = metadata.NeedsUpdate;
                    haveChanges = true;
                }
                else
                    return true;
            }

            if (metadata.Obsolete != Obsolete)
            {
                if (update)
                {
                    Obsolete = Obsolete;
                    haveChanges = true;
                }
                else
                    return true;
            }

            if (String.Compare(metadata.Path, Path, StringComparison.OrdinalIgnoreCase) != 0)
            {
                if (update)
                {
                    Path = metadata.Path;
                    haveChanges = true;
                }
                else
                    return true;
            }

            if (!ignoreInstalledState && metadata.Present != Present)
            {
                if (update)
                {
                    Present = metadata.Present;
                    haveChanges = true;
                }
                else
                    return true;
            }

            if (metadata.Revision != Revision)
            {
                if (update)
                {
                    Revision = metadata.Revision;
                    haveChanges = true;
                }
                else
                    return true;
            }

            return haveChanges;
        }

        public void Install(string archivePath, string androidSDKRoot,
            InstallationProgressEventArgs.InstallationProgressActionDelegate progressCallback = null)
        {
            OnStatusChange(AndroidComponentStatus.InstallationStarted);
            try
            {
                DoInstall(archivePath, androidSDKRoot, progressCallback);
            }
            finally
            {
                OnStatusChange(AndroidComponentStatus.InstallationEnded);
            }
        }

        void DoInstall(string archivePath, string androidSDKRoot,
            InstallationProgressEventArgs.InstallationProgressActionDelegate progressCallback = null)
        {
            if (String.IsNullOrEmpty(archivePath))
                throw new ArgumentException("must not be null or empty", nameof(archivePath));
            if (String.IsNullOrEmpty(androidSDKRoot))
                throw new ArgumentException("must not be null or empty", nameof(androidSDKRoot));

            string targetDirectory = IOPath.Combine(androidSDKRoot, FileSystemPath);
            OnStatusChange(AndroidComponentStatus.UnpackingStarted);

            bool success = false;
            try
            {
                success = InstallArchive(DetailedDescription, archivePath, targetDirectory, progressCallback);
            }
            catch (Exception ex)
            {
                LogError($"Archive '{archivePath}' installation failed with error: {ex}");
                throw;
            }
            finally
            {
                OnStatusChange(AndroidComponentStatus.UnpackingEnded);
            }

            if (success)
                GeneratePackageXml(IOPath.Combine(targetDirectory, "package.xml"));
            else
                throw new InvalidOperationException("Archive installation failed.");
        }

        bool InstallArchive(string description, string archivePath, string targetDirectory,
            InstallationProgressEventArgs.InstallationProgressActionDelegate progressCallback = null)
        {
            //return false; // this is a test
            //throw new Exception("A test InstallArchive exception.");
            var rnd = new Random();
            string componentUnzippedPath;

            int moveSplitPercents = DirectorySizeMonitoringTimer.CalculateMoveProgressSplit(archivePath);
            int unzipSplitPercents = 100 - moveSplitPercents;

            // LogDebug ($"unzipSplitPercents: {unzipSplitPercents} | moveSplitPercents: {moveSplitPercents}");

            // A small race here but I think we can accept the risks :)
            string temporaryPath = IOPath.Combine(IOPath.GetTempPath(), IOPath.GetRandomFileName());
            try
            {
                componentUnzippedPath = CommonUtilities.Helpers.Unzip(temporaryPath, archivePath, AndroidSDKContext.Instance.UserName, (progress) =>
                {
                    try
                    {
                        progressCallback?.Invoke((float)DirectorySizeMonitoringTimer.Remap(progress, 0, 100, 0, unzipSplitPercents));
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"[InstallArchive] progress callback exception: {ex}");
                    }
                }).RemoveTrailingDirectorySeparator();
            }
            catch (Exception ex)
            {
                LogError($"Exception caught while unzipping '{archivePath}' in '{temporaryPath}'. {ex}");
                throw;
            }

            LogDebug($"Android component '{description}' unpacked in directory '{componentUnzippedPath}'");
            LogDebug($"Android component destination path: {targetDirectory}");
            string oldDestinationPath = null;

            if (Directory.Exists(targetDirectory))
            {
                oldDestinationPath = targetDirectory + ".old" + rnd.Next(Int32.MaxValue);
                LogInfo($"Moving old directory '{targetDirectory}' to '{oldDestinationPath}'");

                // We'll be on the same filesystem/volume, so we can just use this instead of recursive copy+delete
                Directory.Move(targetDirectory, oldDestinationPath);
            }

            bool ret = true;
            Exception exMoveDirectory = null;
            try
            {
                int idx = targetDirectory.LastIndexOf(IOPath.DirectorySeparatorChar);
                string leadingPath;
                if (idx > 1)
                    leadingPath = targetDirectory.Substring(0, idx);
                else
                    leadingPath = targetDirectory;

                if (!Directory.Exists(leadingPath))
                {
                    LogInfo($"Creating Android component's parent path '{leadingPath}'");
                    Directory.CreateDirectory(leadingPath);
                }
                LogInfo("Moving Android component to its destination path.");

                using (var monitoringTimer = new DirectorySizeMonitoringTimer(targetDirectory, componentUnzippedPath, (progress) =>
                {
                    try
                    {
                        progressCallback?.Invoke(progress);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"[InstallArchive] progress callback exception: {ex}");
                    }
                }, remapFrom: unzipSplitPercents, remapTo: 100f))
                {
                    CommonUtilities.MoveDirectory(componentUnzippedPath, targetDirectory, overwrite: true);
                }
            }
            catch (Exception e)
            {
                exMoveDirectory = e;
                ret = false;
                LogError($"Failure to move Android component to its destination path. {e}");
                if (!String.IsNullOrEmpty(oldDestinationPath) && Directory.Exists(oldDestinationPath))
                {
                    LogError("Attempting to clean up.");
                    LogError($"Trying to remove directory '{targetDirectory}'");
                    CommonUtilities.DeleteDirectoryRecursively(targetDirectory);

                    try
                    {
                        CommonUtilities.MoveDirectory(oldDestinationPath, targetDirectory);
                    }
                    catch (Exception ex)
                    {
                        // ignore
                        LogError($"Failed to rename directory '{oldDestinationPath}' to '{targetDirectory}'. {ex}");
                    }
                }
            }

            bool logError = false;
            Exception rex = null;
            try
            {
                CommonUtilities.DeleteDirectoryRecursively(temporaryPath);
                if (!String.IsNullOrEmpty(oldDestinationPath))
                    logError = !CommonUtilities.DeleteDirectoryRecursively(oldDestinationPath);
            }
            catch (Exception e)
            {
                logError = true;
                ret = false;
                rex = e;
                // ignore
            }
            if (logError)
            {
                string message = $"Failed to remove old destination directory '{oldDestinationPath}'. User will have to remove it manually.";
                if (rex != null)
                    LogError($"{message}. {rex}");
                else
                    LogError(message);
            }

            if (exMoveDirectory != null)
                throw exMoveDirectory;

            return ret;
        }

        protected void GeneratePackageXml(string path)
        {
            LogDebug($"Generating {path}");

            var settings = new XmlWriterSettings
            {
                CloseOutput = true,
                Encoding = Encoding.UTF8,
                Indent = false,
                NamespaceHandling = NamespaceHandling.OmitDuplicates,
                NewLineHandling = NewLineHandling.None,
                OmitXmlDeclaration = false
            };

            using (var writer = XmlWriter.Create(path, settings))
            {
                writer.WriteStartDocument(true);
                // TODO: check if the namespace exists
                writer.WriteStartElement("sdk", "repository", Repository.GetNamespaceUri("sdk"));
                if (Repository.Namespaces != null)
                {
                    foreach (var kvp in Repository.Namespaces)
                    {
                        if (String.Compare("xsi", kvp.Key, StringComparison.Ordinal) == 0)
                            continue;
                        writer.WriteAttributeString("xmlns", kvp.Key, null, kvp.Value.NamespaceName);
                    }
                }

                writer.WriteStartElement("license");
                writer.WriteAttributeString("id", LicenseID ?? String.Empty);
                writer.WriteAttributeString("type", License?.Type ?? String.Empty);
                writer.WriteString(License?.Text ?? String.Empty);
                writer.WriteEndElement(); // license

                writer.WriteStartElement("localPackage");
                writer.WriteAttributeString("path", Path);
                writer.WriteAttributeString("obsolete", Obsolete ? "true" : "false");

                Info?.WritePackageXmlInfo(writer, Repository);

                writer.WriteStartElement("revision");
                if (Revision != null)
                {
                    WriteRevisionPart(writer, "major", Revision.Major);
                    WriteRevisionPart(writer, "minor", Revision.Minor);
                    WriteRevisionPart(writer, "micro", Revision.Micro);
                    WriteRevisionPart(writer, "preview", Revision.Preview);
                }
                writer.WriteEndElement(); // revision

                writer.WriteStartElement("display-name");
                writer.WriteString(DisplayName);
                writer.WriteEndElement(); // display-name

                writer.WriteStartElement("uses-license");
                writer.WriteAttributeString("ref", LicenseID ?? String.Empty);
                writer.WriteEndElement(); // uses-license

                if (Dependencies != null && Dependencies.Count > 0)
                {
                    writer.WriteStartElement("dependencies");
                    foreach (Dependency dep in Dependencies)
                    {
                        if (dep == null)
                            continue;
                        writer.WriteStartElement("dependency");
                        writer.WriteAttributeString("path", dep.Path ?? String.Empty);
                        writer.WriteEndElement(); //dependency
                    }
                    writer.WriteEndElement(); // dependencies
                }

                writer.WriteEndElement(); // localPackage

                writer.WriteEndElement(); // repository
                writer.WriteEndDocument();
            }
        }

        void WriteRevisionPart(XmlWriter writer, string name, int value)
        {
            if (value < 0)
                return;

            writer.WriteStartElement(name);
            writer.WriteValue(value);
            writer.WriteEndElement();
        }

        public virtual bool MatchesTo(IAndroidComponent other)
        {
            if (!(other is BasePackage))
                return false;

            if (ReferenceEquals(this, other))
                return true;

            if (IsPlatformSpecific && !SupportedPlatforms.SequenceEqual((other as BasePackage).SupportedPlatforms))
                return false;

            return String.Compare(Path, other.Path, StringComparison.Ordinal) == 0 &&
                        Revision == other.Revision &&
                        Channel == other.Channel;
        }

        public virtual bool BasicMetadataEquals(BasePackage other)
        {
            if (other == null)
                return false;

            if (ReferenceEquals(this, other))
                return true;

            // We compare only the properties that actually matter
            if (String.Compare(Path, other.Path, StringComparison.Ordinal) != 0)
                return false;

            if (!AreInfosEqual(Info, other.Info))
                return false;

            if (Obsolete != other.Obsolete)
                return false;

            if (!Revision.Equals(other.Revision))
                return false;

            return true;
        }

        bool AreInfosEqual(AndroidComponentInfo info1, AndroidComponentInfo info2)
        {
            // If only one of them is null return false
            if ((info1 == null) != (info2 == null))
                return false;

            if (info1 != null && !info1.Equals(info2))
                return false;

            return true;
        }

        // It's very simple in this case, we're not interested in deep comparison
        // of the two instances, but rather whether they are uniquely identified -
        // that is they have a different path+version combination.
        public virtual bool Equals(BasePackage other)
        {
            if (!BasicMetadataEquals(other))
                return false;

            if (UniqueID != other.UniqueID)
                return false;

            if (!Dependencies.AreEqual(other.Dependencies))
                return false;

            if (!Archives.AreEqual(other.Archives))
                return false;

            return true;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as BasePackage);
        }

        public override int GetHashCode()
        {
            int hashCode = base.GetHashCode();

            hashCode = hashCode.XorWith(UniqueID.GetHashCode());
            hashCode = hashCode.XorWith(Path?.GetHashCode());
            hashCode = hashCode.XorWith(Info?.GetHashCode());
            hashCode = hashCode.XorWith(Obsolete.GetHashCode());
            hashCode = hashCode.XorWith(Revision?.GetHashCode());
            hashCode = hashCode.XorWith(Dependencies?.GetHashCode());
            return hashCode.XorWith(Archives?.GetHashCode());
        }

        protected abstract void LogError(string message);
        protected abstract void LogWarning(string message);
        protected abstract void LogInfo(string message);
        protected abstract void LogDebug(string message);

        private bool GetPreview()
        {
            if (Info is AndroidComponentInfoPlatform platform)
            {
                return platform.Preview;
            }
            return false;
        }
    }
}
