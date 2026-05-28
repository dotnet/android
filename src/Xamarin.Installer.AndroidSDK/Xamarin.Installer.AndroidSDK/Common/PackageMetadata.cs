//
//  Authors:
//    Marek Habersack <grendel@twistedcode.net>
//
//  Copyright (c) 2018, Microsoft Corp. (https://microsoft.com)
//
//  All rights reserved.
//
using System;
using System.Collections.Generic;

namespace Xamarin.Installer.AndroidSDK.Common
{
	public class PackageMetadata
	{
		public bool Present { get; set; }
		public bool NeedsUpdate { get; set; }
		public string Path { get; set; }
		public string FileSystemPath { get; set; }
		public bool Obsolete { get; set; }
		public bool Preview => GetPreview ();
		public AndroidComponentInfo Info { get; set; }
		public AndroidRevision Revision { get; set; }
		public AndroidRevision InstalledRevision { get; set; }
		public string DisplayName { get; set; }
		public IList<Dependency> Dependencies { get; set; }
		public IList<Archive> Archives { get; set; }
		public string LicenseID { get; set; }

		public PackageMetadata (IAndroidComponent component)
		{
			if (component == null)
				throw new ArgumentNullException (nameof (component));

			Present = component.Present;
			NeedsUpdate = component.NeedsUpdate;
			Path = component.Path;
			FileSystemPath = component.FileSystemPath;
			Obsolete = component.Obsolete;
			Info = component.Info;
			Revision = component.Revision;
			InstalledRevision = component.InstalledRevision;
			DisplayName = component.DisplayName;
			Dependencies = component.Dependencies != null ? new List<Dependency> (component.Dependencies) : null;
			Archives = component.Archives != null ? new List<Archive> (component.Archives) : null;
			LicenseID = component.LicenseID;
		}

		bool GetPreview ()
		{
			if (Info is AndroidComponentInfoPlatform platform) {
				return platform.Preview;
			}
			return false;
		}
	}
}
