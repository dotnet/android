#nullable enable

using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;

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
			static  readonly    JniMethodInfo           Class_forName;
			static  readonly    JniObjectReference      Class_reference;

			static Types ()
			{
				using (var t = new JniType ("java/lang/Class")) {
					Class_reference = t.PeerReference.NewGlobalRef ();
					Class_getName   = t.GetInstanceMethod ("getName", "()Ljava/lang/String;");
					Class_forName   = t.GetStaticMethod ("forName", "(Ljava/lang/String;ZLjava/lang/ClassLoader;)Ljava/lang/Class;");
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
#if FEATURE_JNIENVIRONMENT_JI_PINVOKES || FEATURE_JNIENVIRONMENT_JI_FUNCTION_POINTERS
				if (TryRawFindClass (info.EnvironmentPointer, classname, out var c, out var thrown)) {
					var r   = new JniObjectReference (c, JniObjectReferenceType.Local);
					JniEnvironment.LogCreateLocalRef (r);
					return r;
				}
				RawExceptionClear (info.EnvironmentPointer);

				var findClassThrown     = new JniObjectReference (thrown, JniObjectReferenceType.Local);
				LogCreateLocalRef (findClassThrown);
				var pendingException    = info.Runtime.GetExceptionForThrowable (ref findClassThrown, JniObjectReferenceOptions.CopyAndDispose);

				if (Class_forName.IsValid) {
					var java    = info.ToJavaName (classname);
					var __args  = stackalloc JniArgumentValue [3];
					__args [0]  = new JniArgumentValue (java);
					__args [1]  = new JniArgumentValue (true);  // initialize the class
					__args [2]  = new JniArgumentValue (info.Runtime.ClassLoader);

					c = RawCallStaticObjectMethodA (info.EnvironmentPointer, out thrown, Class_reference.Handle, Class_forName.ID, (IntPtr) __args);
					JniObjectReference.Dispose (ref java);
					if (thrown == IntPtr.Zero) {
						(pendingException as IJavaPeerable)?.Dispose ();
						var r = new JniObjectReference (c, JniObjectReferenceType.Local);
						JniEnvironment.LogCreateLocalRef (r);
						return r;
					}
					RawExceptionClear (info.EnvironmentPointer);

					if (pendingException != null) {
						JniEnvironment.References.RawDeleteLocalRef (info.EnvironmentPointer, thrown);
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
#endif  // !(FEATURE_JNIENVIRONMENT_JI_PINVOKES || FEATURE_JNIENVIRONMENT_JI_FUNCTION_POINTERS)
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

			static bool TryRawFindClass (IntPtr env, string classname, out IntPtr klass, out IntPtr thrown)
			{
#if FEATURE_JNIENVIRONMENT_JI_PINVOKES
				klass = NativeMethods.java_interop_jnienv_find_class (env, out thrown, classname);
				if (thrown == IntPtr.Zero) {
					return true;
				}
#endif  // !FEATURE_JNIENVIRONMENT_JI_PINVOKES
#if FEATURE_JNIENVIRONMENT_JI_FUNCTION_POINTERS
				var _classname_ptr = Marshal.StringToCoTaskMemUTF8 (classname);
				klass   = JniNativeMethods.FindClass (env, _classname_ptr);
				thrown  = JniNativeMethods.ExceptionOccurred (env);
				Marshal.ZeroFreeCoTaskMemUTF8 (_classname_ptr);
				if (thrown == IntPtr.Zero) {
					return true;
				}
#endif  // !FEATURE_JNIENVIRONMENT_JI_FUNCTION_POINTERS
				return false;
			}

			static void RawExceptionClear (IntPtr env)
			{
#if FEATURE_JNIENVIRONMENT_JI_PINVOKES
				// If the Java-side exception stack trace is *lost* a'la 89a5a229,
				// change `false` to `true` and rebuild+re-run.
#if false
				NativeMethods.java_interop_jnienv_exception_describe (env);
#endif  // FEATURE_JNIENVIRONMENT_JI_PINVOKES

				NativeMethods.java_interop_jnienv_exception_clear (env);
#elif FEATURE_JNIENVIRONMENT_JI_FUNCTION_POINTERS
				// If the Java-side exception stack trace is *lost* a'la 89a5a229,
				// change `false` to `true` and rebuild+re-run.
#if false
				JniNativeMethods.ExceptionDescribe (env);
#endif
				JniNativeMethods.ExceptionClear (env);
#endif  // FEATURE_JNIENVIRONMENT_JI_FUNCTION_POINTERS
			}

			static IntPtr RawCallStaticObjectMethodA (IntPtr env, out IntPtr thrown, IntPtr clazz, IntPtr jmethodID, IntPtr args)
			{
#if FEATURE_JNIENVIRONMENT_JI_PINVOKES
				return NativeMethods.java_interop_jnienv_call_static_object_method_a (env, out thrown, clazz, instance, jmethodID, args);
#elif FEATURE_JNIENVIRONMENT_JI_FUNCTION_POINTERS
				var r   = JniNativeMethods.CallStaticObjectMethodA (env, clazz, jmethodID, args);
				thrown  = JniNativeMethods.ExceptionOccurred (env);
				return r;
#else   // FEATURE_JNIENVIRONMENT_JI_FUNCTION_POINTERS
				return IntPtr.Zero;
#endif  // FEATURE_JNIENVIRONMENT_JI_FUNCTION_POINTERS
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
				if ((numMethods < 0) ||
						(numMethods > (methods?.Length ?? 0))) {
					throw new ArgumentOutOfRangeException (nameof (numMethods), numMethods,
							$"`numMethods` must be between 0 and `methods.Length` ({methods?.Length ?? 0})!");
				}
#if DEBUG && NETCOREAPP
				for (int i = 0; methods != null && i < numMethods; ++i) {
					var m   = methods [i];
					if (m.Marshaler != null && m.Marshaler.GetType ().GenericTypeArguments.Length != 0) {
						var method  = m.Marshaler.Method;
						Debug.WriteLine ($"JNIEnv::RegisterNatives() given a generic delegate type `{m.Marshaler.GetType()}`.  .NET Core doesn't like this.");
						Debug.WriteLine ($"  Java: {m.Name}{m.Signature}");
						Debug.WriteLine ($"  Marshaler Type={m.Marshaler.GetType ().FullName} Method={method.DeclaringType?.FullName}.{method.Name}");
					}
				}
#endif  // DEBUG && NETCOREAPP

				int r   = _RegisterNatives (type, methods ?? Array.Empty<JniNativeMethodRegistration>(), numMethods);

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

