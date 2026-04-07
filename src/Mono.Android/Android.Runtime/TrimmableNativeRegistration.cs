#nullable enable

using System;
using Microsoft.Android.Runtime;

namespace Android.Runtime;

static class TrimmableNativeRegistration
{
	internal static void ActivateInstance (IntPtr self, Type targetType)
	{
		TrimmableTypeMap.ActivateInstance (self, targetType);
	}
}
