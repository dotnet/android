//
//  Authors:
//    Marek Habersack <grendel@twistedcode.net>
//
//  Copyright (c) 2017, Microsoft Corp. (http://microsoft.com)
//
//  All rights reserved.
//
using System;

namespace Xamarin.Installer.AndroidSDK.Common
{
	/// <summary>
	/// Helper interface representing a component's API level
	/// </summary>
	public interface IAndroidApiLevel
	{
		/// <summary>
		/// Gets the API level.
		/// </summary>
		/// <value>The API level.</value>
		string ApiLevel { get; }
	}
}
