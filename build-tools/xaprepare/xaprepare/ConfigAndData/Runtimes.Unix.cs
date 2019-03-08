using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Xamarin.Android.Prepare
{
	partial class Runtimes
	{
		partial void AddUnixBundleItems (List<BundleItem> bundleItems)
		{
			bundleItems.AddRange (UnixBundleItems);
		}
	}
}
