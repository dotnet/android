using System;
using Xamarin.Installer.AndroidSDK.Common;
using Xamarin.Installer.AndroidSDK.GoogleV2;
using Xamarin.Installer.AndroidSDK.Xamarin;

namespace Xamarin.Installer.AndroidSDK
{
	partial class Extensions
	{
		internal static IAndroidComponent Clone (this IAndroidComponent component)
		{
			if (component == null)
				return null;

			var rp = component as RemotePackage;
			if (rp != null)
				return RemotePackage.Clone (rp);

			var xp = component as XamarinPackage;
			if (xp != null)
				return XamarinPackage.Clone (xp);

			throw new InvalidOperationException ($"Unknown component type {component.GetType ()}");
		}
	}
}
