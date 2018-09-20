using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Runtime.InteropServices;
using Java.Interop.Tools.TypeNameMappings;

using Android.Runtime;

namespace Java.Interop {

	public static partial class TypeManager {

		// Make this internal so that JNIEnv.Initialize can trigger the static
		// constructor so that JNIEnv.RegisterJNINatives() doesn't include 
		// the static constructor execution.

		// Lock on jniToManaged before accessing EITHER jniToManaged or managedToJni.
		internal static Dictionary<string, Type> jniToManaged = new Dictionary<string, Type> ();
		static Dictionary<Type, string> managedToJni = new Dictionary<Type, string> ();

		internal static IntPtr id_Class_getName;

		static TypeManager ()
		{
			var start = new DateTime ();
			if (Logger.LogTiming) {
				start = DateTime.UtcNow;
				Logger.Log (LogLevel.Info,
						"monodroid-timing",
						"TypeManager.cctor start: " + (start - new DateTime (1970, 1, 1)).TotalMilliseconds);
			}

			__TypeRegistrations.RegisterPackages ();

			if (Logger.LogTiming) {
				var end = DateTime.UtcNow;
				Logger.Log (LogLevel.Info,
						"monodroid-timing",
						"TypeManager.cctor time: " + (end - new DateTime (1970, 1, 1)).TotalMilliseconds + " [elapsed: " + (end - start).TotalMilliseconds + " ms]");
			}
		}

		internal static string GetClassName (IntPtr class_ptr)
		{
			return JNIEnv.GetString (JNIEnv.CallObjectMethod (class_ptr, JNIEnv.mid_Class_getName), JniHandleOwnership.TransferLocalRef).Replace (".", "/");
		}

		internal static string GetJniTypeName (Type type)
		{
			string jni;
			lock (jniToManaged) {
				if (managedToJni.TryGetValue (type, out jni))
					return jni;
			}
			return null;
		}

		class TypeNameComparer : IComparer<string> {
			public int Compare (string x, string y)
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

		public static string LookupTypeMapping (string[] mappings, string javaType)
		{
			int i = Array.BinarySearch (mappings, javaType, JavaNameComparer);
			if (i < 0)
				return null;
			int c = mappings [i].IndexOf (':');
			return mappings [i].Substring (c+1);
		}

		static Action<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, IntPtr> cb_activate;
		internal static Delegate GetActivateHandler ()
		{
			if (cb_activate == null)
				cb_activate = (Action<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, IntPtr>) JNINativeWrapper.CreateDelegate (
						(Action<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, IntPtr>) n_Activate);
			return cb_activate;
		}

#if JAVA_INTEROP
		internal static bool ActivationEnabled {
			get { return !JniEnvironment.WithinNewObjectScope; }
		}
#else   // !JAVA_INTEROP
		[ThreadStatic]
		static bool activation_disabled;

		internal static bool ActivationEnabled {
			get { return !activation_disabled; }
			set { activation_disabled = !value; }
		}
#endif	// !JAVA_INTEROP

		static Type[] GetParameterTypes (string signature)
		{
			if (String.IsNullOrEmpty (signature))
				return new Type[0];
			string[] typenames = signature.Split (':');
			Type[] result = new Type [typenames.Length];
			for (int i = 0; i < typenames.Length; i++)
				result [i] = Type.GetType (typenames [i], throwOnError:true);
			return result;
		}

		static void n_Activate (IntPtr jnienv, IntPtr jclass, IntPtr typename_ptr, IntPtr signature_ptr, IntPtr jobject, IntPtr parameters_ptr)
		{
			var o   = Java.Lang.Object.PeekObject (jobject);
			var ex  = o as IJavaObjectEx;
			if (ex != null) {
				if (!ex.NeedsActivation && !ex.IsProxy)
					return;
			}
			if (!ActivationEnabled) {
				if (Logger.LogGlobalRef) {
					Logger.Log (LogLevel.Info, "monodroid-gref",
							string.Format ("warning: Skipping managed constructor invocation for handle 0x{0} (key_handle 0x{1}). " +
								"Please use JNIEnv.StartCreateInstance() + JNIEnv.FinishCreateInstance() instead of " +
								"JNIEnv.NewObject() and/or JNIEnv.CreateInstance().",
								jobject.ToString ("x"), JNIEnv.IdentityHash (jobject).ToString ("x")));
				}
				return;
			}

			Type type = Type.GetType (JNIEnv.GetString (typename_ptr, JniHandleOwnership.DoNotTransfer), throwOnError:true);
			if (type.IsGenericTypeDefinition) {
				throw new NotSupportedException (
						"Constructing instances of generic types from Java is not supported, as the type parameters cannot be determined.",
						CreateJavaLocationException ());
			}
			Type[] ptypes = GetParameterTypes (JNIEnv.GetString (signature_ptr, JniHandleOwnership.DoNotTransfer));
			object[] parms = JNIEnv.GetObjectArray (parameters_ptr, ptypes);
			ConstructorInfo cinfo = type.GetConstructor (ptypes);
			if (cinfo == null) {
				throw CreateMissingConstructorException (type, ptypes);
			}
			if (o != null) {
				cinfo.Invoke (o, parms);
				return;
			}
			try {
				var activator = ConstructorBuilder.CreateDelegate (type, cinfo, ptypes);
				activator (jobject, parms);
			} catch (Exception e) {
				var m = string.Format ("Could not activate JNI Handle 0x{0} (key_handle 0x{1}) of Java type '{2}' as managed type '{3}'.",
						jobject.ToString ("x"), JNIEnv.IdentityHash (jobject).ToString ("x"), JNIEnv.GetClassNameFromInstance (jobject), type.FullName);
				Logger.Log (LogLevel.Warn, "monodroid", m);
				Logger.Log (LogLevel.Warn, "monodroid", CreateJavaLocationException ().ToString ());

				throw new NotSupportedException (m, e);
			}
		}

		static Exception CreateMissingConstructorException (Type type, Type[] ptypes)
		{
			var message = new System.Text.StringBuilder ();
			message.Append ("Unable to find ");
			if (ptypes.Length == 0)
				message.Append ("the default constructor");
			else {
				message.Append ("a constructor with signature (")
					.Append (ptypes [0].FullName);
				for (int i = 1; i < ptypes.Length; ++i)
					message.Append (", ").Append (ptypes [i].FullName);
				message.Append (")");
			}
			message.Append (" on type ").Append (type.FullName)
				.Append (".  Please provide the missing constructor.");
			return new NotSupportedException (message.ToString (), CreateJavaLocationException ());
		}

		static Exception CreateJavaLocationException ()
		{
			using (var loc = new Java.Lang.Error ("Java callstack:"))
				return new JavaLocationException (loc.ToString ());
		}

		[DllImport ("__Internal")]
		internal static extern IntPtr monodroid_typemap_java_to_managed (string java);

		internal static Type GetJavaToManagedType (string class_name)
		{
			var t = monodroid_typemap_java_to_managed (class_name);
			if (t != IntPtr.Zero)
				return Type.GetType (Marshal.PtrToStringAnsi (t));

			if (!JNIEnv.IsRunningOnDesktop) {
				return null;
			}

			var type    = (Type) null;
			int ls      = class_name.LastIndexOf ('/');
			var package = ls >= 0 ? class_name.Substring (0, ls) : "";
			List<Converter<string, Type>> mappers;
			if (packageLookup.TryGetValue (package, out mappers)) {
				foreach (Converter<string, Type> c in mappers) {
					type = c (class_name);
					if (type == null)
						continue;
					return type;
				}
			}
			if ((type = Type.GetType (JavaNativeTypeManager.ToCliType (class_name))) != null) {
				return type;
			}
			return null;
		}

		internal static IJavaObject CreateInstance (IntPtr handle, JniHandleOwnership transfer)
		{
			return CreateInstance (handle, transfer, null);
		}

		internal static IJavaObject CreateInstance (IntPtr handle, JniHandleOwnership transfer, Type targetType)
		{
			Type type = null;
			IntPtr class_ptr = JNIEnv.GetObjectClass (handle);
			string class_name = GetClassName (class_ptr);
			lock (jniToManaged) {
				while (class_ptr != IntPtr.Zero && !jniToManaged.TryGetValue (class_name, out type)) {

					type = GetJavaToManagedType (class_name);
					if (type != null) {
						jniToManaged.Add (class_name, type);
						break;
					}

					IntPtr super_class_ptr = JNIEnv.GetSuperclass (class_ptr);
					JNIEnv.DeleteLocalRef (class_ptr);
					class_ptr = super_class_ptr;
					class_name = GetClassName (class_ptr);
				}
			}
				
			JNIEnv.DeleteLocalRef (class_ptr);

			if (type == null) {
				JNIEnv.DeleteRef (handle, transfer);
				throw new NotSupportedException (
						string.Format ("Internal error finding wrapper class for '{0}'. (Where is the Java.Lang.Object wrapper?!)",
							JNIEnv.GetClassNameFromInstance (handle)),
						CreateJavaLocationException ());
			}

			if (targetType != null && !targetType.IsAssignableFrom (type))
				type = targetType;
				
			if (type.IsInterface || type.IsAbstract) {
				var invokerType = JavaObjectExtensions.GetHelperType (type, "Invoker");
				if (invokerType == null)
					throw new NotSupportedException ("Unable to find Invoker for type '" + type.FullName + "'. Was it linked away?",
							CreateJavaLocationException ());
				type = invokerType;
			}


			IJavaObject result = null;

			try {
				result = (IJavaObject) CreateProxy (type, handle, transfer);
				var ex = result as IJavaObjectEx;
				if (Runtime.IsGCUserPeer (result) && ex != null)
					ex.IsProxy = true;
			} catch (MissingMethodException e) {
				var key_handle  = JNIEnv.IdentityHash (handle);
				JNIEnv.DeleteRef (handle, transfer);
				throw new NotSupportedException (
						string.Format ("Unable to activate instance of type {0} from native handle 0x{1} (key_handle 0x{2}).",
							type, handle.ToString ("x"), key_handle.ToString ("x")),
						e);
			}
			return result;
 		}

		internal static object CreateProxy (Type type, IntPtr handle, JniHandleOwnership transfer)
		{
			// Skip Activator.CreateInstance() as that requires public constructors,
			// and we want to hide some constructors for sanity reasons.
			BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
			ConstructorInfo c = type.GetConstructor (flags, null, new[]{typeof (IntPtr), typeof (JniHandleOwnership)}, null);
			if (c == null) {
				throw new MissingMethodException (
						"No constructor found for " + type.FullName + "::.ctor(System.IntPtr, Android.Runtime.JniHandleOwnership)",
						CreateJavaLocationException ());
			}
			return c.Invoke (new object[]{handle, transfer});
		}

		public static void RegisterType (string java_class, Type t)
		{
			string jniFromType = JNIEnv.GetJniName (t);
			lock (jniToManaged) {
				Type lookup;
				if (!jniToManaged.TryGetValue (java_class, out lookup)) {
					jniToManaged.Add (java_class, t);
					if (jniFromType != java_class) {
						managedToJni.Add (t, java_class);
					}
				} else if (!JNIEnv.IsRunningOnDesktop || t != typeof (Java.Interop.TypeManager)) {
					// skip the registration and output a warning
					Logger.Log (LogLevel.Warn, "monodroid", string.Format ("Type Registration Skipped for {0} to {1} ", java_class, t.ToString()));
				}

			}
		}

		static Dictionary<string, List<Converter<string, Type>>> packageLookup = new Dictionary<string, List<Converter<string, Type>>> ();

		public static void RegisterPackage (string package, Converter<string, Type> lookup)
		{
			lock (packageLookup) {
				List<Converter<string, Type>> lookups;
				if (!packageLookup.TryGetValue (package, out lookups))
					packageLookup.Add (package, lookups = new List<Converter<string, Type>> ());
				lookups.Add (lookup);
			}
		}

		public static void RegisterPackages (string[] packages, Converter<string, Type>[] lookups)
		{
			if (packages == null)
				throw new ArgumentNullException ("packages");
			if (lookups == null)
				throw new ArgumentNullException ("lookups");
			if (packages.Length != lookups.Length)
				throw new ArgumentException ("`packages` and `lookups` arrays must have same number of elements.");

			lock (packageLookup) {
				for (int i = 0; i < packages.Length; ++i) {
					string package                  = packages [i];
					Converter<string, Type> lookup  = lookups [i];

					List<Converter<string, Type>> _lookups;
					if (!packageLookup.TryGetValue (package, out _lookups))
						packageLookup.Add (package, _lookups = new List<Converter<string, Type>> ());
					_lookups.Add (lookup);
				}
			}
		}

		[Register ("mono/android/TypeManager", DoNotGenerateAcw = true)]
		internal class JavaTypeManager : Java.Lang.Object
		{
			[Register ("activate", "(Ljava/lang/String;Ljava/lang/String;Ljava/lang/Object;[Ljava/lang/Object;)V", "")]
			static void n_Activate (IntPtr jnienv, IntPtr jclass, IntPtr typename_ptr, IntPtr signature_ptr, IntPtr jobject, IntPtr parameters_ptr)
			{
				TypeManager.n_Activate (jnienv, jclass, typename_ptr, signature_ptr, jobject, parameters_ptr);
			}

			internal static Delegate GetActivateHandler ()
			{
				return TypeManager.GetActivateHandler ();
			}
		}
	}
}
