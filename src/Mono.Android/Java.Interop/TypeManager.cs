using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using Android.Runtime;

namespace Java.Interop {

	static class TypeManagerMapDictionaries
	{
		static Dictionary<string, Type>? _jniToManaged;
		static Dictionary<Type, string>? _managedToJni;

		public static readonly object AccessLock = new object ();

		//
		// Access to both properties MUST be done after taking lock on accessLock!
		//
		public static Dictionary<string, Type> JniToManaged {
			get {
				if (_jniToManaged == null)
					_jniToManaged = new Dictionary<string, Type> (StringComparer.Ordinal);
				return _jniToManaged;
			}
		}

		public static Dictionary<Type, string> ManagedToJni {
			get {
				if (_managedToJni == null)
					_managedToJni = new Dictionary<Type, string> ();
				return _managedToJni;
			}
		}
	}

	public static partial class TypeManager {
		internal static string GetClassName (IntPtr class_ptr)
		{
			IntPtr ptr = RuntimeNativeMethods.monodroid_TypeManager_get_java_class_name (class_ptr);
			string ret = Marshal.PtrToStringAnsi (ptr)!;
			RuntimeNativeMethods.monodroid_free (ptr);

			return ret;
		}

		internal static string? GetJniTypeName (Type type)
		{
			lock (TypeManagerMapDictionaries.AccessLock) {
				if (TypeManagerMapDictionaries.ManagedToJni.TryGetValue (type, out var jni))
					return jni;
			}
			return null;
		}

		class TypeNameComparer : IComparer<string> {
			public int Compare (string? x, string? y)
			{
				if (object.ReferenceEquals (x, y))
					return 0;
				if (x == null)
					return -1;
				if (y == null)
					return 1;

				int xe = x.IndexOf (':');
				int ye = y.IndexOf (':');

				int r  = string.CompareOrdinal (x, 0, y, 0, System.Math.Max (xe, ye));
				if (r != 0)
					return r;

				if (xe >= 0 && ye >= 0)
					return xe - ye;

				if (xe < 0)
					return x.Length - ye;

				return xe - y.Length;
			}
		}

		static readonly TypeNameComparer JavaNameComparer = new TypeNameComparer ();

		public static string? LookupTypeMapping (string[] mappings, string javaType)
		{
			int i = Array.BinarySearch (mappings, javaType, JavaNameComparer);
			if (i < 0)
				return null;
			int c = mappings [i].IndexOf (':');
			return mappings [i].Substring (c+1);
		}

		public static void RegisterType (string java_class, Type t)
		{
			string jniFromType = JNIEnv.GetJniName (t);
			lock (TypeManagerMapDictionaries.AccessLock) {
				if (!TypeManagerMapDictionaries.JniToManaged.TryGetValue (java_class, out var lookup)) {
					TypeManagerMapDictionaries.JniToManaged.Add (java_class, t);
					if (String.Compare (jniFromType, java_class, StringComparison.OrdinalIgnoreCase) != 0) {
						TypeManagerMapDictionaries.ManagedToJni.Add (t, java_class);
					}
				} else if (t != typeof (Java.Interop.TypeManager)) {
					// skip the registration and output a warning
					Logger.Log (LogLevel.Warn, "monodroid", FormattableString.Invariant ($"Type Registration Skipped for {java_class} to {t} "));
				}

			}
		}

		const string TypeRegistrationNotSupported =
			"Java package type registration is no longer supported. Java-to-managed type resolution now goes through the trimmable type map.";

		// The package-based Java-to-managed type registration fallback was removed
		// (https://github.com/dotnet/android/issues/11663); type resolution now goes
		// through the trimmable type map, and the generator no longer emits
		// the `Java.Interop.__TypeRegistrations` class that called these methods. These
		// shipped public APIs are retained for binary compatibility but now throw, as
		// they can no longer register anything.
		public static void RegisterPackage (string package, Converter<string, Type> lookup)
		{
			throw new NotSupportedException (TypeRegistrationNotSupported);
		}

		public static void RegisterPackages (string[] packages, Converter<string, Type?>[] lookups)
		{
			throw new NotSupportedException (TypeRegistrationNotSupported);
		}

	}
}
