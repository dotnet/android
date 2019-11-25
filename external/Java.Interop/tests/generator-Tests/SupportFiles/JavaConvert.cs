using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

using Android.Runtime;

namespace Java.Interop {

	static class JavaConvert {
	
		public static T FromJniHandle<T>(IntPtr handle, JniHandleOwnership transfer)
		{
			throw new NotImplementedException ();
		}

		public static T FromJniHandle<T>(IntPtr handle, JniHandleOwnership transfer, out bool set)
		{
			throw new NotImplementedException ();
		}

		internal static string GetJniClassForType (Type type)
		{
			throw new NotImplementedException ();
		}

		public static object FromJniHandle (IntPtr handle, JniHandleOwnership transfer)
		{
			throw new NotImplementedException ();
		}

		public static T FromJavaObject<T>(IJavaObject value)
		{
			throw new NotImplementedException ();
		}

		public static T FromJavaObject<T>(IJavaObject value, out bool set)
		{
			throw new NotImplementedException ();
		}

		public static object FromJavaObject (IJavaObject value)
		{
			throw new NotImplementedException ();
		}

		public static IJavaObject ToJavaObject<T>(T value)
		{
			throw new NotImplementedException ();
		}

		public static TReturn WithLocalJniHandle<TValue, TReturn>(TValue value, Func<IntPtr, TReturn> action)
		{
			throw new NotImplementedException ();
		}

		public static TReturn WithLocalJniHandle<TReturn>(object value, Func<IntPtr, TReturn> action)
		{
			throw new NotImplementedException ();
		}
	}
}


