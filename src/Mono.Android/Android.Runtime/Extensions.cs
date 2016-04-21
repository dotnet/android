using System;
using System.Reflection;

namespace Android.Runtime {

	public static class Extensions {

		public static TResult JavaCast<TResult> (this IJavaObject instance)
			where TResult : class, IJavaObject
		{
			return Java.Interop.JavaObjectExtensions.JavaCast<TResult>(instance);
		}
	}
}
