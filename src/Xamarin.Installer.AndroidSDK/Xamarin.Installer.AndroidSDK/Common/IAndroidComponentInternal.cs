//
//  Authors:
//    Marek Habersack <marhab@microsoft.com>
//
//  Copyright (c) 2018, Microsoft, Inc
//
//  All rights reserved.
//
using System;
using System.Collections.Generic;

namespace Xamarin.Installer.AndroidSDK.Common
{
	interface IAndroidComponentInternal
	{
		void Remove (string androidSDKRoot);
		void PerformDetection (string androidSDKRoot, bool isRefresh = false);
		void RefreshMetadata (IAndroidComponent component, bool ignoreInstalledState = true);
	}
}
