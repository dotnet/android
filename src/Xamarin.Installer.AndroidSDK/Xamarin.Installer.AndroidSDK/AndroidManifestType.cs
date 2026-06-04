//
//  Author:
//    Marek Habersack <grendel@twistedcode.net>
//
//  Copyright (c) 2017, Microsoft, Inc
//
//  All rights reserved.
//
using System;

namespace Xamarin.Installer.AndroidSDK
{
	/// <summary>
	/// Android manifest type.
	/// </summary>
	public enum AndroidManifestType
	{
		/// <summary>
		/// New (as of 2016) Google repository manifest format (e.g. http://dl-ssl.google.com/android/repository/repository2-1.xml)
		/// </summary>
		GoogleV2,

		/// <summary>
		/// Xamarin manifest format (https://aka.ms/AndroidManifestFeed/d17-12)
		/// </summary>
		Xamarin,

		/// <summary>
		/// Doesn't access the Internet at all. Instead it looks for local repositories and attempts to read information about
		/// all the installed components. The returned information can (and will) be incomplete - information about channels is
		/// unavailable for locally installed packages, as well as download URLs of the archives. Care must be taken when
		/// accessing <see cref="t:Xamarin.Installer.AndroidSDK.Common.IAndroidComponent"/> properties so that no null reference exceptions are thrown.
		/// </summary>
		Local,
	}
}
