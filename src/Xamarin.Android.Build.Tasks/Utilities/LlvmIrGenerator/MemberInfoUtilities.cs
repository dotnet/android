using System;
using System.Reflection;

namespace Xamarin.Android.Tasks.LLVMIR
{
	static class MemberInfoUtilities
	{
		public static bool IsNativePointer (this MemberInfo mi)
		{
			return mi.GetCustomAttribute <NativePointerAttribute> () != null;
		}
	}
}
