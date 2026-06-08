//
//  Authors:
//    Marek Habersack <grendel@twistedcode.net>
//
//  Copyright (c) 2017, Microsoft, Inc (http://microsoft.com)
//
//  All rights reserved.
//
using System;

namespace Xamarin.Installer.AndroidSDK.Common
{
	/// <summary>
	/// Android component status. <seealso cref="AndroidSdkInstance.ComponentStatusChanged"/>,
	/// <seealso cref="IAndroidComponent.StatusChanged"/>
	/// </summary>
	public enum AndroidComponentStatus
	{
		/// <summary>
		/// Component detection started
		/// </summary>
		DetectionStarted,

		/// <summary>
		/// Component detection completed
		/// </summary>
		DetectionEnded,

		/// <summary>
		/// Component installation started
		/// </summary>
		InstallationStarted,

		/// <summary>
		/// Component installation ended
		/// </summary>
		InstallationEnded,

		/// <summary>
		/// Component archive unpacking started
		/// </summary>
		UnpackingStarted,

		/// <summary>
		/// Component archive unpacking ended
		/// </summary>
		UnpackingEnded,

		/// <summary>
		/// Component removal started
		/// </summary>
		RemovalStarted,

		/// <summary>
		/// Component removal ended
		/// </summary>
		RemovalEnded,

		/// <summary>
		/// Component verification started
		/// </summary>
		VerificationStarted,

		/// <summary>
		/// Component verification started
		/// </summary>
		VerificationEnded,

		/// <summary>
		/// Component metadata (presence, version etc) changed
		/// </summary>
		MetadataUpdated
	}
}
