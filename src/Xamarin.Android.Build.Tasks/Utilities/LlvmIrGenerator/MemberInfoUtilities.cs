using System;
using System.Reflection;

using Xamarin.Android.Tasks;

namespace Xamarin.Android.Tasks.LLVMIR
{
	static class MemberInfoUtilities
	{
		public static bool IsNativePointer (this MemberInfo mi)
		{
			return mi.GetCustomAttribute <NativePointerAttribute> () != null;
		}

		public static bool IsNativePointerToPreallocatedBuffer (this MemberInfo mi)
		{
			var attr = mi.GetCustomAttribute <NativePointerAttribute> ();
			if (attr == null) {
				return false;
			}

			return attr.PointsToPreAllocatedBuffer;
		}

		public static bool ShouldBeIgnored (this MemberInfo mi)
		{
			var attr = mi.GetCustomAttribute<NativeAssemblerAttribute> ();
			return attr != null && attr.Ignore;
		}

		public static bool UsesDataProvider (this MemberInfo mi)
		{
			var attr = mi.GetCustomAttribute<NativeAssemblerAttribute> ();
			return attr != null && attr.UsesDataProvider;
		}
	}
}
