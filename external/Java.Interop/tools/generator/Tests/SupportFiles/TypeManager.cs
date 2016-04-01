#pragma warning disable
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;

using Android.Runtime;

namespace Java.Interop {

	public static partial class TypeManager {

		// Make this internal so that JNIEnv.Initialize can trigger the static
		// constructor so that JNIEnv.RegisterJNINatives() doesn't include 
		// the static constructor execution.

		// Lock on jniToManaged before accessing EITHER jniToManaged or managedToJni.
		internal static Dictionary<string, Type> jniToManaged = new Dictionary<string, Type> ();

		internal static IntPtr id_Class_getName;

		static TypeManager ()
		{
			throw new NotImplementedException ();
		}

		internal static string GetClassName (IntPtr class_ptr)
		{
			throw new NotImplementedException ();
		}

		internal static string GetJniTypeName (Type type)
		{
			throw new NotImplementedException ();
		}

		class TypeNameComparer : IComparer<string> {
			public int Compare (string x, string y)
			{
				throw new NotImplementedException ();
			}
		}

		public static string LookupTypeMapping (string[] mappings, string javaType)
		{
			throw new NotImplementedException ();
		}

		internal static Delegate GetActivateHandler ()
		{
			throw new NotImplementedException ();
		}
		
		internal static bool ActivationEnabled {
			get;
			set;
		}

		static Type[] GetParameterTypes (string signature)
		{
			throw new NotImplementedException ();
		}

		static void n_Activate (IntPtr jnienv, IntPtr jclass, IntPtr typename_ptr, IntPtr signature_ptr, IntPtr jobject, IntPtr parameters_ptr)
		{
			throw new NotImplementedException ();
		}

		static Exception CreateMissingConstructorException (Type type, Type[] ptypes)
		{
			throw new NotImplementedException ();
		}

		static Exception CreateJavaLocationException ()
		{
			throw new NotImplementedException ();
		}

		internal static IJavaObject CreateInstance (IntPtr handle, JniHandleOwnership transfer)
		{
			throw new NotImplementedException ();
		}

		internal static IJavaObject CreateInstance (IntPtr handle, JniHandleOwnership transfer, Type targetType)
		{
			throw new NotImplementedException ();
 		}

		internal static object CreateProxy (Type type, IntPtr handle, JniHandleOwnership transfer)
		{
			throw new NotImplementedException ();
		}

		public static void RegisterType (string java_class, Type t)
		{
			throw new NotImplementedException ();
		}

		static Dictionary<string, List<Converter<string, Type>>> packageLookup = new Dictionary<string, List<Converter<string, Type>>> ();

		public static void RegisterPackage (string package, Converter<string, Type> lookup)
		{
			throw new NotImplementedException ();
		}

		public static void RegisterPackages (string[] packages, Converter<string, Type>[] lookups)
		{
			throw new NotImplementedException ();
		}
	}
}
#pragma warning restore
