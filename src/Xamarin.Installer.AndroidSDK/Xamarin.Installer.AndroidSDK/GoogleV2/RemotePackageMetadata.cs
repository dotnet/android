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

using Xamarin.Installer.AndroidSDK.Common;

namespace Xamarin.Installer.AndroidSDK.GoogleV2
{
	class RemotePackageMetadata : PackageMetadata
	{
		public string ChannelID { get; set; }

		public RemotePackageMetadata (RemotePackage rp) : base (rp)
		{
			if (rp == null)
				throw new ArgumentNullException (nameof (rp));
			ChannelID = rp.ChannelID;
		}
	}
}
