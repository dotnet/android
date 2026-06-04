using System;
using System.Collections.Generic;

namespace Xamarin.Installer.AndroidSDK.Common
{
    public interface IJdkComponent
    {
        Guid UniqueID { get; }

        string DisplayName { get; }

        bool Obsolete { get; }

        bool Preview { get; }

        string LicenseID { get; }

        PackageVendor Vendor { get; }

        AndroidRevision Revision { get; }

        /// <summary>
        /// Gets the component archives
        /// </summary>
        /// <value>Component archives</value>
        IList<JdkArchive> Archives { get; }

        IEnumerable<JdkArchive> GetValidArchivesForSystem();
    }
}
