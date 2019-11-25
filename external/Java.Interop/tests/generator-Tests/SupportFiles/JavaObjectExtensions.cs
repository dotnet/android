using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Android.Runtime;

namespace Java.Interop {

	public static class JavaObjectExtensions {

		[Obsolete ("Use Android.Runtime.JavaCollection.ToLocalJniHandle()")]
		public static JavaCollection ToInteroperableCollection (this ICollection instance)
		{
			throw new NotImplementedException ();
		}

		[Obsolete ("Use Android.Runtime.JavaCollection<T>.ToLocalJniHandle()")]
		public static JavaCollection<T> ToInteroperableCollection<T> (this ICollection<T> instance)
		{
			throw new NotImplementedException ();
		}

		[Obsolete ("Use Android.Runtime.JavaList.ToLocalJniHandle()")]
		public static JavaList ToInteroperableCollection (this IList instance)
		{
			throw new NotImplementedException ();
		}

		[Obsolete ("Use Android.Runtime.JavaList<T>.ToLocalJniHandle()")]
		public static JavaList<T> ToInteroperableCollection<T> (this IList<T> instance)
		{
			throw new NotImplementedException ();
		}

		[Obsolete ("Use Android.Runtime.JavaDictionary.ToLocalJniHandle()")]
		public static JavaDictionary ToInteroperableCollection (this IDictionary instance)
		{
			throw new NotImplementedException ();
		}

		[Obsolete ("Use Android.Runtime.JavaDictionary<K, V>.ToLocalJniHandle()")]
		public static JavaDictionary<K,V> ToInteroperableCollection<K,V> (this IDictionary<K,V> instance)
		{
			throw new NotImplementedException ();
		}

		public static TResult JavaCast<TResult> (this IJavaObject instance)
			where TResult : class, IJavaObject
		{
			throw new NotImplementedException ();
		}

		internal static TResult _JavaCast<TResult> (this IJavaObject instance)
		{
			throw new NotImplementedException ();
		}

		// typeof(Foo) -> FooSuffix
		// typeof(Foo<>) -> FooSuffix`1
		internal static Type GetHelperType (Type type, string suffix)
		{
			throw new NotImplementedException ();
		}
	}
}

