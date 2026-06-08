using System;
using System.Collections.Generic;
using System.Threading;
using System.Xml;

using Xamarin.Installer.AndroidSDK.Common;
using Xamarin.Installer.Common;

namespace Xamarin.Installer.AndroidSDK.Xamarin
{
	class XamarinPackage : BasePackage, IEquatable<XamarinPackage>
	{
		public override Channel Channel => Repository.GetChannel ("channel-0");
		public override AndroidComponentType ComponentType { get; }
		public Uri OriginalManifestUri { get; set; }

		public XamarinPackage (Repository repository, Uri manifestURL, IXmlLineInfo location, IList<Archive> archives, AndroidComponentType componentType) : base (repository, manifestURL, location, archives)
		{
			ComponentType = componentType;
			PopulatePlatformArchives (false);
		}

		public static XamarinPackage Clone (XamarinPackage source)
                {
			if (source == null)
				return null;

			// Archive instance aren't cloned because we want them to be shared amongst all the SDK instances and as the list is read-only, there's no
                        // need to clone it either.
			return new XamarinPackage (source.Repository, source.ManifestURL, source.Location, source.OriginalArchives, source.ComponentType) {
				IsEssential = source.IsEssential,
				IgnoreFailure = source.IgnoreFailure,
				Present = source.Present,
				NeedsUpdate = source.NeedsUpdate,
				Path = source.Path,
				FileSystemPath = source.FileSystemPath,
				Obsolete = source.Obsolete,
				Info = source.Info,
				Revision = source.Revision,
				InstalledRevision = source.InstalledRevision,
				DisplayName = source.DisplayName,
				Dependencies = source.Dependencies,
				LicenseID = source.LicenseID,
				ForceInstallation = source.ForceInstallation
			};
		}

		public override void RefreshMetadata (IAndroidComponent component, bool ignoreInstalledState = true)
		{
			RefreshMetadata (component as XamarinPackage);
		}

		void RefreshMetadata (XamarinPackage rp, bool ignoreInstalledState = true)
		{
			if (rp == null)
				throw new ArgumentNullException (nameof (rp));

			MetadataChanged (CreateMetadata (rp), true, ignoreInstalledState);
		}

		protected override PackageMetadata CreateMetadata (BasePackage component)
		{
			return new PackageMetadata (component);
		}

		public bool Equals (XamarinPackage other)
		{
			if (other == null)
				return false;

			return base.Equals (other as BasePackage);
		}

		public override bool Equals (object obj)
		{
			return Equals (obj as XamarinPackage);
		}

		public override int GetHashCode ()
		{
			return base.GetHashCode ();
		}

		protected override void LogError (string message)
		{
			Logger.Error (message);
		}

		protected override void LogWarning (string message)
		{
			Logger.Warning (message);
		}

		protected override void LogInfo (string message)
		{
			Logger.Info (message);
		}

		protected override void LogDebug (string message)
		{
			Logger.Debug (message);
		}
	}
}
