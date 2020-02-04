#nullable enable

using System;
using System.Collections.Generic;
using System.Text;

namespace Java.Interop
{
	partial class JniEnvironment {
		static partial class Types {

			readonly    static  KeyValuePair<string, string>[]  BuiltinMappings = new KeyValuePair<string, string>[] {
				new KeyValuePair<string, string>("byte",       "B"),
				new KeyValuePair<string, string>("boolean",    "Z"),
				new KeyValuePair<string, string>("char",       "C"),
				new KeyValuePair<string, string>("double",     "D"),
				new KeyValuePair<string, string>("float",      "F"),
				new KeyValuePair<string, string>("int",        "I"),
				new KeyValuePair<string, string>("long",       "J"),
				new KeyValuePair<string, string>("short",      "S"),
				new KeyValuePair<string, string>("void",       "V"),
			};

			static  readonly    JniMethodInfo           Class_getName;

			static Types ()
			{
				using (var t = new JniType ("java/lang/Class")) {
					Class_getName   = t.GetInstanceMethod ("getName", "()Ljava/lang/String;");
				}
			}

			public static unsafe JniObjectReference FindClass (string classname)
			{
				if (classname == null)
					throw new ArgumentNullException (nameof (classname));
				if (classname.Length == 0)
					throw new ArgumentException ("'classname' cannot be a zero-length string.", nameof (classname));

				var info    = JniEnvironment.CurrentInfo;
#if FEATURE_JNIENVIRONMENT_JI_PINVOKES
				IntPtr thrown;
				var c   = NativeMethods.java_interop_jnienv_find_class (info.EnvironmentPointer, out thrown, classname);
				if (thrown == IntPtr.Zero) {
					var r   = new JniObjectReference (c, JniObjectReferenceType.Local);
					JniEnvironment.LogCreateLocalRef (r);
					return r;
				}

				NativeMethods.java_interop_jnienv_exception_clear (info.EnvironmentPointer);
				var e   = new JniObjectReference (thrown, JniObjectReferenceType.Local);
				LogCreateLocalRef (e);

				if (info.Runtime.ClassLoader_LoadClass != null) {
					var java    = info.ToJavaName (classname);
					var __args  = stackalloc JniArgumentValue [1];
					__args [0]  = new JniArgumentValue (java);

					IntPtr ignoreThrown;
					c = NativeMethods.java_interop_jnienv_call_object_method_a (info.EnvironmentPointer, out ignoreThrown, info.Runtime.ClassLoader.Handle, info.Runtime.ClassLoader_LoadClass.ID, (IntPtr) __args);
					JniObjectReference.Dispose (ref java);
					if (ignoreThrown == IntPtr.Zero) {
						JniObjectReference.Dispose (ref e);
						var r = new JniObjectReference (c, JniObjectReferenceType.Local);
						JniEnvironment.LogCreateLocalRef (r);
						return r;
					}
					NativeMethods.java_interop_jnienv_exception_clear (info.EnvironmentPointer);
					NativeMethods.java_interop_jnienv_delete_local_ref (info.EnvironmentPointer, ignoreThrown);

				}

				throw info.Runtime.GetExceptionForThrowable (ref e, JniObjectReferenceOptions.CopyAndDispose)!;
#endif  // !FEATURE_JNIENVIRONMENT_JI_PINVOKES
#if FEATURE_JNIOBJECTREFERENCE_SAFEHANDLES
				var c       = info.Invoker.FindClass (info.EnvironmentPointer, classname);
				var thrown  = info.Invoker.ExceptionOccurred (info.EnvironmentPointer);
				if (thrown.IsInvalid) {
					JniEnvironment.LogCreateLocalRef (c);
					return new JniObjectReference (c, JniObjectReferenceType.Local);
				}
				info.Invoker.ExceptionClear (info.EnvironmentPointer);
				LogCreateLocalRef (thrown);

				var java    = info.ToJavaName (classname);
				var __args  = stackalloc JniArgumentValue [1];
				__args [0]  = new JniArgumentValue (java);

				c                   = info.Invoker.CallObjectMethodA (info.EnvironmentPointer, info.Runtime.ClassLoader.SafeHandle, info.Runtime.ClassLoader_LoadClass.ID, __args);
				JniObjectReference.Dispose (ref java);
				var ignoreThrown    = info.Invoker.ExceptionOccurred (info.EnvironmentPointer);
				if (ignoreThrown.IsInvalid) {
					thrown.Dispose ();
					JniEnvironment.LogCreateLocalRef (c);
					return new JniObjectReference (c, JniObjectReferenceType.Local);
				}
				info.Invoker.ExceptionClear (info.EnvironmentPointer);
				LogCreateLocalRef (ignoreThrown);
				ignoreThrown.Dispose ();
				var e   = new JniObjectReference (thrown, JniObjectReferenceType.Local);
				throw info.Runtime.GetExceptionForThrowable (ref e, JniObjectReferenceOptions.CopyAndDispose);
#endif  // !FEATURE_JNIOBJECTREFERENCE_SAFEHANDLES
			}

			public static JniType? GetTypeFromInstance (JniObjectReference instance)
			{
				if (!instance.IsValid)
					return null;

				var lref = JniEnvironment.Types.GetObjectClass (instance);
				if (lref.IsValid)
					return new JniType (ref lref, JniObjectReferenceOptions.CopyAndDispose);
				return null;
			}

			public static string? GetJniTypeNameFromInstance (JniObjectReference instance)
			{
				if (!instance.IsValid)
					return null;

				var lref = GetObjectClass (instance);
				try {
					return GetJniTypeNameFromClass (lref);
				}
				finally {
					JniObjectReference.Dispose (ref lref, JniObjectReferenceOptions.CopyAndDispose);
				}
			}

			public static string? GetJniTypeNameFromClass (JniObjectReference type)
			{
				if (!type.IsValid)
					return null;

				var s = JniEnvironment.InstanceMethods.CallObjectMethod (type, Class_getName);
				return JavaClassToJniType (Strings.ToString (ref s, JniObjectReferenceOptions.CopyAndDispose)!);
			}

			static string JavaClassToJniType (string value)
			{
				for (int i = 0; i < BuiltinMappings.Length; ++i) {
					if (value == BuiltinMappings [i].Key)
						return BuiltinMappings [i].Value;
				}
				return value.Replace ('.', '/');
			}

			public static void RegisterNatives (JniObjectReference type, JniNativeMethodRegistration [] methods)
			{
				RegisterNatives (type, methods, methods == null ? 0 : methods.Length);
			}

			public static void RegisterNatives (JniObjectReference type, JniNativeMethodRegistration [] methods, int numMethods)
			{
				int r   = _RegisterNatives (type, methods, numMethods);

				if (r != 0) {
					throw new InvalidOperationException (
							string.Format ("Could not register native methods for class '{0}'; JNIEnv::RegisterNatives() returned {1}.", GetJniTypeNameFromClass (type), r));
				}
			}

			public static void UnregisterNatives (JniObjectReference type)
			{
				int r   = _UnregisterNatives (type);

				if (r != 0) {
					throw new InvalidOperationException (
							string.Format ("Could not unregister native methods for class '{0}'; JNIEnv::UnregisterNatives() returned {1}.", GetJniTypeNameFromClass (type), r));
				}
			}
		}
	}
}

