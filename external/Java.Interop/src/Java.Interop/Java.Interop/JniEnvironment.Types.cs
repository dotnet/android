#nullable enable

using System;
using System.Diagnostics;
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

			public static JniObjectReference FindClass (string classname)
			{
				return TryFindClass (classname, throwOnError: true);
			}

			static unsafe JniObjectReference TryFindClass (string classname, bool throwOnError)
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

				// If the Java-side exception stack trace is *lost* a'la 89a5a229,
				// change `false` to `true` and rebuild+re-run.
#if false
				NativeMethods.java_interop_jnienv_exception_describe (info.EnvironmentPointer);
#endif

				NativeMethods.java_interop_jnienv_exception_clear (info.EnvironmentPointer);

				var findClassThrown     = new JniObjectReference (thrown, JniObjectReferenceType.Local);
				LogCreateLocalRef (findClassThrown);
				var pendingException    = info.Runtime.GetExceptionForThrowable (ref findClassThrown, JniObjectReferenceOptions.CopyAndDispose);

				if (info.Runtime.ClassLoader_LoadClass != null) {
					var java    = info.ToJavaName (classname);
					var __args  = stackalloc JniArgumentValue [1];
					__args [0]  = new JniArgumentValue (java);

					c = NativeMethods.java_interop_jnienv_call_object_method_a (info.EnvironmentPointer, out thrown, info.Runtime.ClassLoader.Handle, info.Runtime.ClassLoader_LoadClass.ID, (IntPtr) __args);
					JniObjectReference.Dispose (ref java);
					if (thrown == IntPtr.Zero) {
						(pendingException as IJavaPeerable)?.Dispose ();
						var r = new JniObjectReference (c, JniObjectReferenceType.Local);
						JniEnvironment.LogCreateLocalRef (r);
						return r;
					}
					NativeMethods.java_interop_jnienv_exception_clear (info.EnvironmentPointer);

					if (pendingException != null) {
						NativeMethods.java_interop_jnienv_delete_local_ref (info.EnvironmentPointer, thrown);
					}
					else {
						var loadClassThrown = new JniObjectReference (thrown, JniObjectReferenceType.Local);
						LogCreateLocalRef (loadClassThrown);
						pendingException    = info.Runtime.GetExceptionForThrowable (ref loadClassThrown, JniObjectReferenceOptions.CopyAndDispose);
					}
				}

				if (!throwOnError) {
					(pendingException as IJavaPeerable)?.Dispose ();
					return default;
				}
				throw pendingException!;
#endif  // !FEATURE_JNIENVIRONMENT_JI_PINVOKES
#if FEATURE_JNIOBJECTREFERENCE_SAFEHANDLES
				var c       = info.Invoker.FindClass (info.EnvironmentPointer, classname);
				var thrown  = info.Invoker.ExceptionOccurred (info.EnvironmentPointer);
				if (thrown.IsInvalid) {
					JniEnvironment.LogCreateLocalRef (c);
					return new JniObjectReference (c, JniObjectReferenceType.Local);
				}
				info.Invoker.ExceptionClear (info.EnvironmentPointer);
				var findClassThrown     = new JniObjectReference (thrown, JniObjectReferenceType.Local);
				LogCreateLocalRef (findClassThrown);
				var pendingException    = info.Runtime.GetExceptionForThrowable (ref findClassThrown, JniObjectReferenceOptions.CopyAndDispose);

				var java    = info.ToJavaName (classname);
				var __args  = stackalloc JniArgumentValue [1];
				__args [0]  = new JniArgumentValue (java);

				c       = info.Invoker.CallObjectMethodA (info.EnvironmentPointer, info.Runtime.ClassLoader.SafeHandle, info.Runtime.ClassLoader_LoadClass.ID, __args);
				JniObjectReference.Dispose (ref java);
				thrown  = info.Invoker.ExceptionOccurred (info.EnvironmentPointer);
				if (ignoreThrown.IsInvalid) {
					(pendingException as IJavaPeerable)?.Dispose ();
					var r   = new JniObjectReference (c, JniObjectReferenceType.Local);
					JniEnvironment.LogCreateLocalRef (r);
					return r;
				}
				info.Invoker.ExceptionClear (info.EnvironmentPointer);
				if (pendingException != null) {
					thrown.Dispose ();
					throw pendingException;
				}
				var loadClassThrown     = new JniObjectReference (thrown, JniObjectReferenceType.Local);
				LogCreateLocalRef (loadClassThrown);
				pendingException    = info.Runtime.GetExceptionForThrowable (ref loadClassThrown, JniObjectReferenceOptions.CopyAndDispose);
				if (!throwOnError) {
					(pendingException as IJavaPeerable)?.Dispose ();
					return default;
				}
				throw pendingException!;
#endif  // !FEATURE_JNIOBJECTREFERENCE_SAFEHANDLES
			}

#if NET
			public static bool TryFindClass (string classname, out JniObjectReference instance)
			{
				if (classname == null)
					throw new ArgumentNullException (nameof (classname));
				if (classname.Length == 0)
					throw new ArgumentException ("'classname' cannot be a zero-length string.", nameof (classname));

				instance = TryFindClass (classname, throwOnError: false);
				return instance.IsValid;
			}
#endif  // NET

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
#if DEBUG && NETCOREAPP
				foreach (var m in methods) {
					if (m.Marshaler.GetType ().GenericTypeArguments.Length != 0) {
						var method  = m.Marshaler.Method;
						Debug.WriteLine ($"JNIEnv::RegisterNatives() given a generic delegate type `{m.Marshaler.GetType()}`.  .NET Core doesn't like this.");
						Debug.WriteLine ($"  Java: {m.Name}{m.Signature}");
						Debug.WriteLine ($"  Marshaler Type={m.Marshaler.GetType ().FullName} Method={method.DeclaringType?.FullName}.{method.Name}");
					}
				}
#endif  // DEBUG && NETCOREAPP

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

