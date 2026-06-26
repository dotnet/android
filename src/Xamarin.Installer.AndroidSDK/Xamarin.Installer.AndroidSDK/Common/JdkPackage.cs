#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace Xamarin.Installer.AndroidSDK.Common
{
    public class JdkPackage : IJdkComponent
    {
        public JdkPackage(string displayName, bool obsolete, bool preview, string licenseId, PackageVendor vendor, AndroidRevision revision, List<JdkArchive> archives)
        {
            DisplayName = displayName;
            Obsolete = obsolete;
            Preview = preview;
            Vendor = vendor;
            Revision = revision;
            Archives = archives;
            LicenseID = licenseId;
        }

        public Guid UniqueID { get; } = Guid.NewGuid();

        public string DisplayName { get; }

        public bool Obsolete { get; }

        public bool Preview { get; }

        public string LicenseID { get; }

        public PackageVendor Vendor { get; }

        public AndroidRevision Revision { get; }

        public IList<JdkArchive> Archives { get; }

        public IEnumerable<JdkArchive> GetValidArchivesForSystem()
        {
            return Archives.Where(x => x.IsValidForSystem());
        }
    }
}
