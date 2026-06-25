using System;

namespace Xamarin.Installer.AndroidSDK.Common
{
    /// <summary>
    /// Describes a single package archive
    /// </summary>
    public abstract class ArchiveBase : IArchive
    {
        public ArchiveBase(string hostOS)
        {
            HostOS = hostOS;
            Platform = AndroidUtilities.GetPlatformFromOS(hostOS);
        }

        /// <summary>
        /// Gets the archive size
        /// </summary>
        /// <value>Archive size</value>
        public ulong Size { get; set; }

        /// <summary>
        /// Gets the checksum of the archive. The algorithm is identified by <see cref="ChecksumType"/>;
        /// when <see cref="ChecksumType"/> is null or empty the checksum is assumed to be SHA-1.
        /// </summary>
        /// <value>Checksum digest as a hex string</value>
        public string Checksum { get; set; }

        /// <summary>
        /// Gets the checksum algorithm declared by the manifest for <see cref="Checksum"/>,
        /// for example <c>sha1</c> or <c>sha256</c>. A null or empty value means SHA-1 (the historical default).
        /// </summary>
        /// <value>Checksum algorithm name</value>
        public string ChecksumType { get; set; }

        /// <summary>
        /// Gets the archive URL
        /// </summary>
        /// <value>Archive URL</value>
        public Uri Url { get; set; }

        /// <summary>
        /// Gets the target (host) operating system of the archive (if any)
        /// </summary>
        /// <value>Host operating system</value>
        public string HostOS { get; }

        /// <summary>
        /// Mapping of <see cref="HostOS"/> into one of the <see cref="AndroidSDKPlatform"/> enumeration members
        /// </summary>
        /// <value>Archive platform.</value>
        public AndroidSDKPlatform Platform { get; }

        /// <summary>
        /// Native word size of the host operating system.
        /// </summary>
        /// <value>Host operating system word size</value>
        public uint HostBits { get; set; }

        /// <summary>
        /// CPU Architecture of the host operating system.
        /// </summary>
        /// <value>Host operating CPU architecture</value>
        public string HostArch { get; set; }

        /// <summary>
        /// Checks whether archive's platform and host-arch are matching the current system.
        /// </summary>
        /// <returns></returns>
        public abstract bool IsValidForSystem();

        /// <summary>
        /// Checks whether archive's platform is matching the current system.
        /// </summary>
        /// <returns></returns>
        public virtual bool IsPlatformValid()
        {
            return (Platform == AndroidSDKPlatform.Any || Platform == AndroidSDKContext.Instance.Platform);
        }

        /// <summary>
        /// Checks whether archive's host-arch is matching the current system.
        /// </summary>
        /// <returns></returns>
        public virtual bool IsHostArchValid()
        {
            return string.IsNullOrEmpty(HostArch) || HostArch == AndroidSDKContext.Instance.HostArch;
        }

        /// <summary>
        /// Determines whether the specified <see cref="t:Xamarin.Installer.AndroidSDK.Common.Archive"/> is equal to the current <see cref="T:Xamarin.Installer.AndroidSDK.Common.Archive"/>.
        /// </summary>
        /// <param name="other">The <see cref="t:Xamarin.Installer.AndroidSDK.Common.Archive"/> to compare with the current <see cref="T:Xamarin.Installer.AndroidSDK.Common.Archive"/>.</param>
        /// <returns><c>true</c> if the specified <see cref="t:Xamarin.Installer.AndroidSDK.Common.Archive"/> is equal to the current
        /// <see cref="T:Xamarin.Installer.AndroidSDK.Common.Archive"/>; otherwise, <c>false</c>.</returns>
        public bool Equals(IArchive other)
        {
            if (other == null)
                return false;

            if (ReferenceEquals(this, other))
                return true;

            if (String.Compare(Checksum, other.Checksum, StringComparison.Ordinal) != 0)
                return false;

            if (HostBits != other.HostBits)
                return false;

            if (String.Compare(HostOS, other.HostOS, StringComparison.Ordinal) != 0)
                return false;

            if (Size != other.Size)
                return false;

            if (Url != other.Url)
                return false;

            return true;
        }

        /// <summary>
        /// Serves as a hash function for a <see cref="T:Xamarin.Installer.AndroidSDK.Common.Archive"/> object.
        /// </summary>
        /// <returns>A hash code for this instance that is suitable for use in hashing algorithms and data structures such as a hash table.</returns>
        public override int GetHashCode()
        {
            int hashCode = base.GetHashCode();
            hashCode = hashCode.XorWith(HostBits.GetHashCode());
            hashCode = hashCode.XorWith(HostOS?.GetHashCode());
            hashCode = hashCode.XorWith(Size.GetHashCode());
            return hashCode.XorWith(Url?.GetHashCode());
        }
    }
}
