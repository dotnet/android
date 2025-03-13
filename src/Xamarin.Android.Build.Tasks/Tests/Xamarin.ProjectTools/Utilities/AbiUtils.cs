using System;

using Xamarin.Android.Tasks;

namespace Xamarin.ProjectTools
{
	public static class AbiUtils
	{
		public static string AbiToRuntimeIdentifier (string androidAbi)
		{
			return MonoAndroidHelper.AbiToRidMaybe (androidAbi);
		}
	}
}
