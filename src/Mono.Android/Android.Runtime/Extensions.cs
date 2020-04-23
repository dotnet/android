using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Android.Runtime {

	public static class Extensions {

		[return: MaybeNull]
		public static TResult JavaCast<TResult> (this IJavaObject? instance)
			where TResult : class, IJavaObject
		{
			return Java.Interop.JavaObjectExtensions.JavaCast<TResult>(instance);
		}
	}
}
