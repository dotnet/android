using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Xml;

using Xamarin.Installer.Common;

namespace Xamarin.Installer.AndroidSDK.Common
{
	interface IAndroidComponentDescriptor
	{
		Uri BaseURL { get; }
		AndroidRevision Revision { get; }
		string PreviewRevision { get; }
		string Description { get; }
		Uri DescriptionUrl { get; }
		string UsesLicense { get; }
		bool IsObsolete { get; }
		bool IsPreview { get; }
		IList<AndroidAddonDependencyInfo> AddonsInfo { get; }

		string Name { get; }
		bool IsEssential { get; }

		IAndroidComponent CreateComponent(AndroidSDKInstaller installer);
		IAndroidArchive GetArchive(string os, string arch, uint osbits, bool quiet = false);
	}
}
