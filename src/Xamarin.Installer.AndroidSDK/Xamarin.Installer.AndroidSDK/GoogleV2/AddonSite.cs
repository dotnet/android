//
//  Authors:
//    Marek Habersack <grendel@twistedcode.net>
//
//  Copyright (c) 2017, Marek Habersack
//
//  All rights reserved.
//
using System;

namespace Xamarin.Installer.AndroidSDK.GoogleV2
{
	public class AddonSite
	{
		public AddonSiteType Type { get; }
		public string DisplayName { get; }
		public Uri Url { get; }

		public AddonSite (AddonSiteType type, string displayName, Uri url)
		{
			if (url == null)
				throw new ArgumentNullException (nameof (url));
			DisplayName = displayName ?? "Unnamed";
			Url = url;
		}
	}
}
