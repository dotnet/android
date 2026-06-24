//
//  Authors:
//    Marek Habersack <grendel@twistedcode.net>
//
//  Copyright (c) 2017, Microsoft, Inc
//
//  All rights reserved.
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

using Xamarin.Installer.Common;

namespace Xamarin.Installer.AndroidSDK.Common
{
    /// <summary>
    /// Describes a single package archive
    /// </summary>
    public class Archive : ArchiveBase, IEquatable<Archive>
    {
        /// <summary>
        /// Gets the collection of patches for this archive. This library currently does not support applying these
        /// patches, instead it always downloads the full archive from the specified URL
        /// </summary>
        /// <value>The list of patches (if any)</value>
        public IList<ArchivePatch> Patches { get; set; }

        /// <summary>
        /// Gets a value indicating whether this <see cref="T:Xamarin.Installer.AndroidSDK.Common.Archive"/> was downloaded.
        /// <c>true</c> will be returned only if <see cref="DownloadedFilePath"/> is set and refers to an existing file.
        /// Checksum is not verified when reading this property.
        /// </summary>
        /// <value><c>true</c> if archive was downloaded; otherwise, <c>false</c>.</value>
        public bool WasDownloaded => !String.IsNullOrEmpty(GetDownloadPath(false));

        /// <summary>
        /// Gets or sets the downloaded file path. The installer assembly resets this property to <c>null</c> when returning archive
        /// instance from <see cref="m:AndroidSDKInstaller.GetDownloadItems"/> but otherwise it is responsibilty of the client code
        /// to set the property to a valid path of a downloaded archive. <see cref="WasDownloaded"/>
        /// </summary>
        /// <value>The downloaded file path.</value>
        public string DownloadedFilePath { get; set; }

        /// <summary>
        /// Gets the Android component that owns this Archive, if any
        /// </summary>
        /// <value>The owner component.</value>
        public IAndroidComponent Owner { get; internal set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Xamarin.Installer.AndroidSDK.Common.Archive"/> class.
        /// </summary>
        /// <param name="hostOS">The host operating system.</param>
        public Archive(string hostOS) : base(hostOS)
        {
        }

        /// <summary>
        /// Verifies whether the downloaded archive is indeed found on disk and whether it passes checksum verification
        /// (if <paramref name="verifyChecksum"/> is <c>true</c>).
        /// </summary>
        /// <returns><c>true</c>, if downloaded archive is valid, <c>false</c> otherwise.</returns>
        /// <param name="verifyChecksum">If set to <c>true</c> verify the archive checksum.</param>
        public bool IsDownloadValid(bool verifyChecksum = true)
        {
            string filePath = GetDownloadPath();
            if (String.IsNullOrEmpty(filePath))
                return false;

            if (!verifyChecksum)
                return true;

            if (!VerifyChecksum(filePath, ChecksumType, Checksum, Url))
            {
                Logger.Warning($"Downloaded file '{DownloadedFilePath}' fails checksum verification for Android SDK archive '{Url}'");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Verifies <paramref name="filePath"/> against <paramref name="hash"/> using the algorithm
        /// named by <paramref name="algorithm"/>. The algorithm string is normalized (trimmed,
        /// lowercased, with a single hyphen stripped) so values such as "sha-1" / "SHA1" / "sha-256"
        /// are all accepted. A null or empty algorithm defaults to SHA-1 to match the historical
        /// manifest behavior. Unknown algorithms or hash-length mismatches log a warning and return
        /// <c>false</c> rather than silently falling back.
        /// </summary>
        internal static bool VerifyChecksum(string filePath, string algorithm, string hash, Uri url)
        {
            string normalized = (algorithm ?? string.Empty).Trim().ToLowerInvariant().Replace("-", string.Empty);
            string trimmedHash = hash?.Trim();

            if (string.IsNullOrEmpty(normalized) || normalized == "sha1")
            {
                if (!string.IsNullOrEmpty(trimmedHash) && trimmedHash.Length != 40)
                {
                    Logger.Warning($"Checksum for Android SDK archive '{url}' is declared as SHA-1 but its length ({trimmedHash.Length}) is not 40 hex characters");
                    return false;
                }
                return CheckSHA1(filePath, trimmedHash);
            }

            if (normalized == "sha256")
            {
                if (!string.IsNullOrEmpty(trimmedHash) && trimmedHash.Length != 64)
                {
                    Logger.Warning($"Checksum for Android SDK archive '{url}' is declared as SHA-256 but its length ({trimmedHash.Length}) is not 64 hex characters");
                    return false;
                }
                return CheckSHA256(filePath, trimmedHash);
            }

            Logger.Warning($"Unknown checksum algorithm '{algorithm}' declared for Android SDK archive '{url}'; refusing to verify");
            return false;
        }

        /// <summary>
        /// Checks whether the downloaded archive is valid (verifying the checksum if <paramref name="verifyChecksum"/> is <c>true</c>) and
        /// whether it can be installed on the current system. <seealso cref="IsDownloadValid"/>
        /// </summary>
        /// <returns><c>true</c>, if the archive is installable on the current system, <c>false</c> otherwise.</returns>
        /// <param name="verifyChecksum">If set to <c>true</c> verify checksum.</param>
        /// <param name="checkPlatform">If set to <c>true</c> verify archive host os.</param>
        public bool IsInstallable(bool verifyChecksum = true, bool checkPlatform = true)
        {
            return (!checkPlatform || IsValidForSystem()) && IsDownloadValid(verifyChecksum);
        }

        /// <summary>
        /// Checks whether archive's platform and host-arch are matching the current system.
        /// </summary>
        /// <returns></returns>
        public override bool IsValidForSystem()
        {
            if (!IsPlatformValid())
                return false;

            if (IsHostArchValid())
                return true;

            // custom logic: windows emulator only have x64 archives
            return Platform == AndroidSDKPlatform.Windows &&
                (string.IsNullOrEmpty(Owner?.FileSystemPath) || (Owner.FileSystemPath.StartsWith("emulator") && HostArch.StartsWith("x")));
        }

        string GetDownloadPath(bool logMissing = true)
        {
            string filePath = DownloadedFilePath?.Trim();
            if (String.IsNullOrEmpty(filePath))
                return null;

            if (!File.Exists(filePath))
            {
                if (logMissing)
                    Logger.Info($"Downloaded file '{DownloadedFilePath}' not found for Android SDK archive '{Url}'");
                return null;
            }

            return filePath;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA5350:Do Not Use Weak Cryptographic Algorithms", Justification = "Used to verify checksums provided by an external party.")]
        static bool CheckSHA1(string filePath, string sha1hash)
        {
            sha1hash = sha1hash?.Trim();
            if (String.IsNullOrEmpty(sha1hash) || sha1hash.Length != 40)
                return false;

            using (var sha1 = SHA1.Create())// CodeQL [SM02196] Receiving external information: Used to verify checksums provided by an external party
            { 
                using (var fs = File.OpenRead(filePath))
                {
                    byte[] hash = sha1.ComputeHash(fs);
                    string shash = BitConverter.ToString(hash).Replace("-", "");
                    return String.Compare(shash, sha1hash, StringComparison.OrdinalIgnoreCase) == 0;
                }
            }
        }

        static bool CheckSHA256(string filePath, string sha256hash)
        {
            sha256hash = sha256hash?.Trim();
            if (String.IsNullOrEmpty(sha256hash) || sha256hash.Length != 64)
                return false;

            using (var sha256 = SHA256.Create())
            {
                using (var fs = File.OpenRead(filePath))
                {
                    byte[] hash = sha256.ComputeHash(fs);
                    string shash = BitConverter.ToString(hash).Replace("-", "");
                    return String.Compare(shash, sha256hash, StringComparison.OrdinalIgnoreCase) == 0;
                }
            }
        }

        /// <summary>
        /// Determines whether the specified <see cref="t:Xamarin.Installer.AndroidSDK.Common.Archive"/> is equal to the current <see cref="T:Xamarin.Installer.AndroidSDK.Common.Archive"/>.
        /// </summary>
        /// <param name="other">The <see cref="t:Xamarin.Installer.AndroidSDK.Common.Archive"/> to compare with the current <see cref="T:Xamarin.Installer.AndroidSDK.Common.Archive"/>.</param>
        /// <returns><c>true</c> if the specified <see cref="t:Xamarin.Installer.AndroidSDK.Common.Archive"/> is equal to the current
        /// <see cref="T:Xamarin.Installer.AndroidSDK.Common.Archive"/>; otherwise, <c>false</c>.</returns>
        public bool Equals(Archive other)
        {
            if (!base.Equals(other))
                return false;

            if (Patches == null)
            {
                if (other.Patches != null)
                    return false;
            }
            else
            {
                if (other.Patches == null)
                    return false;
                if (!ComparePatches(other.Patches))
                    return false;
            }

            return true;
        }

        bool ComparePatches(IList<ArchivePatch> otherPatches)
        {
            if (Patches.Count != otherPatches.Count)
                return false;

            for (int i = 0; i < otherPatches.Count; i++)
            {
                ArchivePatch localPatch = Patches[i];
                ArchivePatch otherPatch = otherPatches[i];

                if (localPatch == null)
                {
                    if (otherPatch != null)
                        return false;
                    continue;
                }

                if (otherPatch == null)
                    return false;

                if (!localPatch.Equals(otherPatch))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Determines whether the specified <see cref="object"/> is equal to the current <see cref="T:Xamarin.Installer.AndroidSDK.Common.Archive"/>.
        /// </summary>
        /// <param name="obj">The <see cref="object"/> to compare with the current <see cref="T:Xamarin.Installer.AndroidSDK.Common.Archive"/>.</param>
        /// <returns><c>true</c> if the specified <see cref="object"/> is equal to the current
        /// <see cref="T:Xamarin.Installer.AndroidSDK.Common.Archive"/>; otherwise, <c>false</c>.</returns>
        public override bool Equals(Object obj)
        {
            return Equals(obj as Archive);
        }

        /// <summary>
        /// Serves as a hash function for a <see cref="T:Xamarin.Installer.AndroidSDK.Common.Archive"/> object.
        /// </summary>
        /// <returns>A hash code for this instance that is suitable for use in hashing algorithms and data structures such as a hash table.</returns>
        public override int GetHashCode()
        {
            int hashCode = base.GetHashCode();
            return hashCode.XorWith(Patches?.GetHashCode());
        }
    }
}
