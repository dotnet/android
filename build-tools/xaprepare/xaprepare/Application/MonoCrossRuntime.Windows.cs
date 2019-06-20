using System;
using System.IO;

namespace Xamarin.Android.Prepare
{
	partial class MonoCrossRuntime : MonoRuntime
	{
		partial void InitOS (Context context)
		{
			// On Windows we should not check cross runtimes because we don't build them - thus they must be
			// removed from the set of runtime files/bundle items or otherwise we'll get false negatives
			// similar to:
			//
			//   bin\Debug\lib\xamarin.android\xbuild\Xamarin\Android\Windows\cross-arm missing, skipping the rest of bundle item file scan
			//      Some bundle files are missing, download/rebuild/reinstall forced
			//
			if (!context.IsWindowsCrossAotAbi (Name))
				SupportedOnHostOS = false;
		}
	}
}
