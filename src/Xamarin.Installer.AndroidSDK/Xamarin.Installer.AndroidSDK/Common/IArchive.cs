using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Xamarin.Installer.AndroidSDK.Common
{
    public interface IArchive
    {
        /// <summary>
        /// Gets the archive size
        /// </summary>
        /// <value>Archive size</value>
        ulong Size { get; }

        /// <summary>
        /// Gets the SHA1 checksum of the archive
        /// </summary>
        /// <value>SHA1 checksum</value>
        string Checksum { get; }

        /// <summary>
        /// Gets the archive URL
        /// </summary>
        /// <value>Archive URL</value>
        Uri Url { get; }

        /// <summary>
        /// Gets the target (host) operating system of the archive (if any)
        /// </summary>
        /// <value>Host operating system</value>
        string HostOS { get; }

        /// <summary>
        /// Mapping of <see cref="HostOS"/> into one of the <see cref="AndroidSDKPlatform"/> enumeration members
        /// </summary>
        /// <value>Archive platform.</value>
        AndroidSDKPlatform Platform { get; }

        /// <summary>
        /// Native word size of the host operating system.
        /// </summary>
        /// <value>Host operating system word size</value>
        uint HostBits { get; }

        /// <summary>
        /// CPU Architecture of the host operating system.
        /// </summary>
        /// <value>Host operating CPU architecture</value>
        string HostArch { get; }
    }
}
