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

			static  readonly    JniInstanceMethodInfo   Class_getName;

			static Types ()
			{
				using (var t = new JniType ("java/lang/Class")) {
					Class_getName   = t.GetInstanceMethod ("getName", "()Ljava/lang/String;");
				}
			}

			public static unsafe JniObjectReference FindClass (string classname)
			{
				if (classname == null)
					throw new ArgumentNullException ("classname");
				if (classname.Length == 0)
					throw new ArgumentException ("'classname' cannot be a zero-length string.", nameof (classname));

				var info    = JniEnvironment.CurrentInfo;
#if FEATURE_JNIENVIRONMENT_JI_PINVOKES
				IntPtr thrown;
				var c   = NativeMethods.JavaInterop_FindClass (info.EnvironmentPointer, out thrown, classname);
				if (thrown == IntPtr.Zero) {
					var r   = new JniObjectReference (c, JniObjectReferenceType.Local);
					JniEnvironment.LogCreateLocalRef (r);
					return r;
				}
				NativeMethods.JavaInterop_ExceptionClear (info.EnvironmentPointer);
				var e   = new JniObjectReference (thrown, JniObjectReferenceType.Local);
				LogCreateLocalRef (e);

				var java    = info.ToJavaName (classname);
				var __args  = stackalloc JniArgumentValue [1];
				__args [0]  = new JniArgumentValue (java);

				IntPtr ignoreThrown;
				c   = NativeMethods.JavaInterop_CallObjectMethodA (info.EnvironmentPointer, out ignoreThrown, info.Runtime.ClassLoader.Handle, info.Runtime.ClassLoader_LoadClass.ID, __args);
				JniObjectReference.Dispose (ref java);
				if (ignoreThrown == IntPtr.Zero) {
					JniObjectReference.Dispose (ref e);
					var r   = new JniObjectReference (c, JniObjectReferenceType.Local);
					JniEnvironment.LogCreateLocalRef (r);
					return r;
				}
				NativeMethods.JavaInterop_ExceptionClear (info.EnvironmentPointer);
				NativeMethods.JavaInterop_DeleteLocalRef (info.EnvironmentPointer, ignoreThrown);
				throw info.Runtime.GetExceptionForThrowable (ref e, JniObjectReferenceOptions.DisposeSourceReference);
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
				throw info.Runtime.GetExceptionForThrowable (ref e, JniObjectReferenceOptions.DisposeSourceReference);
#endif  // !FEATURE_JNIOBJECTREFERENCE_SAFEHANDLES
			}

			public static JniType GetTypeFromInstance (JniObjectReference reference)
			{
				var lref = JniEnvironment.Types.GetObjectClass (reference);
				if (lref.IsValid)
					return new JniType (ref lref, JniObjectReferenceOptions.DisposeSourceReference);
				return null;
			}

			public static string GetJniTypeNameFromInstance (JniObjectReference reference)
			{
				var lref = GetObjectClass (reference);
				try {
					return GetJniTypeNameFromClass (lref);
				}
				finally {
					JniObjectReference.Dispose (ref lref, JniObjectReferenceOptions.DisposeSourceReference);
				}
			}

			public static string GetJniTypeNameFromClass (JniObjectReference reference)
			{
				var s = Class_getName.InvokeVirtualObjectMethod (reference);
				return JavaClassToJniType (Strings.ToString (ref s, JniObjectReferenceOptions.DisposeSourceReference));
			}

			static string JavaClassToJniType (string value)
			{
				for (int i = 0; i < BuiltinMappings.Length; ++i) {
					if (value == BuiltinMappings [i].Key)
						return BuiltinMappings [i].Value;
				}
				return value.Replace ('.', '/');
			}

			public static void RegisterNatives (JniObjectReference klass, JniNativeMethodRegistration [] methods, int numMethods)
			{
				int r   = _RegisterNatives (klass, methods, numMethods);

				if (r != 0) {
					throw new InvalidOperationException (string.Format ("Could not get JavaVM; JNIEnv::RegisterNatives() returned {0}.", r));
				}
			}

			public static void UnregisterNatives (JniObjectReference klass)
			{
				int r   = _UnregisterNatives (klass);

				if (r != 0) {
					throw new InvalidOperationException (string.Format ("Could not get JavaVM; JNIEnv::UnregisterNatives() returned {0}.", r));
				}
			}
		}
	}
}

