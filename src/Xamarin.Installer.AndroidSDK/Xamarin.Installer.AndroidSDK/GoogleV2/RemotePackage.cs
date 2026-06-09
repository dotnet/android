//
//  Authors:
//    Marek Habersack <grendel@twistedcode.net>
//
//  Copyright (c) 2017, Microsoft Corp. (http://microsoft.com)
//
//  All rights reserved.
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

using Xamarin.Installer.AndroidSDK.Common;
using Xamarin.Installer.Common;

using IOPath = System.IO.Path;

namespace Xamarin.Installer.AndroidSDK.GoogleV2
{
	public class RemotePackage : BasePackage, IEquatable<RemotePackage>
	{
		IParserErrorHandler errorHandler;

		public override AndroidComponentType ComponentType => GetComponentType (Info);
		public override Channel Channel => Repository.GetChannel (ChannelID);
		public string ChannelID { get; set; }

		public bool IncludeAllArchives { get; set; } // For Xamarin manifest generator this is 'true'

		public RemotePackage (Repository repository, Uri manifestURL, IXmlLineInfo location, IParserErrorHandler errorHandler, IList<Archive> archives) : base (repository, manifestURL, location, archives)
		{
			this.errorHandler = errorHandler ?? throw new ArgumentNullException (nameof (errorHandler));
		}

		// This is just a way to see in the logs whether the package is sane, apply some hacks to work around bugs in
		// Google manifests etc.
		public void Verify ()
		{
			OnStatusChange (AndroidComponentStatus.VerificationStarted);
			try {
				DoVerify ();
			} finally {
				OnStatusChange (AndroidComponentStatus.VerificationEnded);
			}
		}

		void DoVerify ()
		{
			string location = Location?.AsString () ?? String.Empty;
			string path = String.IsNullOrEmpty (Path) ? "Unknown" : Path;

			if (OriginalArchives?.Any () == true) {
				PopulatePlatformArchives (Info is AndroidComponentInfoSystemImage || IncludeAllArchives);
				if (Info is AndroidComponentInfoSystemImage && !Archives.Any ()) {
					// This is a workaround for a bug in the Google manifest which has the following archives entry:
					//
					// <archives>
					//  <archive>
					//  <!--Built on: Fri Sep 16 16:41:26 2016.-->
					//   <complete>
					//	  <size>238333358</size>
					//	  <checksum>7cf2ad756e54a3acfd81064b63cb0cb9dff2798d</checksum>
					//    <url>armeabi-v7a-23_r06.zip</url>
					//	 </complete>
					//	 <host-os>windows</host-os>
					//  </archive>
					// </archives>
					//
					// which marks the archive as valid only on Windows. The issue is that system images are not
					// specific to any host OS since they contain Android installation to be executed inside the
					// emulator. That's why we ignore the host OS check for system images here.
					Archives = new List<Archive> { OriginalArchives.First () }.AsReadOnly ();
				}
			}

			if (Archives == null || Archives.Count == 0)
				Logger.Warning ($"Package loaded from {ManifestURL} {location}, with path '{path}' has no valid archives (Host OS: {AndroidSDKContext.Instance.Platform})");
		}

		public static RemotePackage Clone (RemotePackage source)
		{
			if (source == null)
				return null;

			// Archive instance aren't cloned because we want them to be shared amongst all the SDK instances and as the list is read-only, there's no
			// need to clone it either.
			var cloned = new RemotePackage (source.Repository, source.ManifestURL, source.Location, source.errorHandler, source.OriginalArchives) {
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
				ChannelID = source.ChannelID,
				LicenseID = source.LicenseID,
				ForceInstallation = source.ForceInstallation,
			};
			cloned.Verify ();

			return cloned;
		}

		protected override PackageMetadata CreateMetadata (BasePackage component)
		{
			return new RemotePackageMetadata (component as RemotePackage);
		}

		protected override bool MetadataChanged (PackageMetadata metadata, bool update = false, bool ignoreInstalledState = false)
		{
			if (!base.MetadataChanged (metadata, update, ignoreInstalledState))
				return false;

			return MetadataChanged (metadata as RemotePackageMetadata, update, ignoreInstalledState);
		}

		public override void RefreshMetadata (IAndroidComponent component, bool ignoreInstalledState = true)
		{
			RefreshMetadata (component as RemotePackage);
		}

		void RefreshMetadata (RemotePackage rp, bool ignoreInstalledState = true)
		{
			if (rp == null)
				throw new ArgumentNullException (nameof (rp));

			MetadataChanged (CreateMetadata (rp), true, ignoreInstalledState);
		}

		bool MetadataChanged (RemotePackageMetadata metadata, bool update, bool ignoreInstalledState)
		{
			if (metadata == null)
				throw new ArgumentNullException (nameof (metadata));

			bool haveChanges = false;
			if (String.Compare (metadata.ChannelID, ChannelID, StringComparison.Ordinal) != 0) {
				if (update) {
					ChannelID = metadata.ChannelID;
					haveChanges = true;
				} else
					return true;
			}

			if (String.Compare (metadata.LicenseID, LicenseID, StringComparison.Ordinal) != 0) {
				if (update) {
					LicenseID = metadata.LicenseID;
					haveChanges = true;
				} else
					return true;
			}

			return haveChanges;
		}

		AndroidComponentType GetComponentType (AndroidComponentInfo info)
		{
			if (info is AndroidComponentInfoAddon)
				return AndroidComponentType.Addon;

			if (info is AndroidComponentInfoExtra)
				return AndroidComponentType.Extra;

			if (info is AndroidComponentInfoGeneric)
				return AndroidComponentType.Generic;

			if (info is AndroidComponentInfoMaven)
				return AndroidComponentType.Maven;

			if (info is AndroidComponentInfoPlatform)
				return AndroidComponentType.Platform;

			if (info is AndroidComponentInfoSource)
				return AndroidComponentType.Source;

			if (info is AndroidComponentInfoSystemImage)
				return AndroidComponentType.SystemImage;

			errorHandler.Debug ($"Unknown component info type '{info?.GetType ()}'");
			return AndroidComponentType.Unknown;
		}

		public override bool MatchesTo (IAndroidComponent other)
		{
			if (!(other is RemotePackage))
				return false;

			return base.MatchesTo (other);
		}


		public override bool BasicMetadataEquals (BasePackage other)
		{
			if (!base.BasicMetadataEquals (other))
				return false;

			return BasicMetadataEquals (other as RemotePackage);
		}

		bool BasicMetadataEquals (RemotePackage other)
		{
			if (String.Compare (ChannelID, other.ChannelID, StringComparison.Ordinal) != 0)
				return false;

			return true;
		}

		public bool Equals (RemotePackage other)
		{
			if (other == null)
				return false;

			return base.Equals (other as BasePackage);
		}

		public override bool Equals (object obj)
		{
			return Equals (obj as RemotePackage);
		}

		public override int GetHashCode ()
		{
			int hashCode = base.GetHashCode ();

			return hashCode.XorWith (ChannelID?.GetHashCode ());
		}

		protected override void LogError (string message)
		{
			errorHandler.Error (message);
		}

		protected override void LogWarning (string message)
		{
			errorHandler.Warning (message);
		}

		protected override void LogInfo (string message)
		{
			errorHandler.Info (message);
		}

		protected override void LogDebug (string message)
		{
			errorHandler.Debug (message);
		}
	}
}
