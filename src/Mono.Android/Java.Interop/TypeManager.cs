using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Java.Interop.Tools.TypeNameMappings;

using Android.Runtime;
using Microsoft.Android.Runtime;

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
		// Internal call for Mono runtime - registered by native code as Java.Interop.TypeManager::monodroid_typemap_java_to_managed
		[MethodImplAttribute (MethodImplOptions.InternalCall)]
		internal static extern Type monodroid_typemap_java_to_managed (string java_type_name);

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

		public static string? LookupTypeMapping (string[] mappings, string javaType)
		{
			int i = Array.BinarySearch (mappings, javaType, JavaNameComparer);
			if (i < 0)
				return null;
			int c = mappings [i].IndexOf (':');
			return mappings [i].Substring (c+1);
		}

		static _JniMarshal_PPLLLL_V? cb_activate;
		internal static Delegate GetActivateHandler ()
		{
			return cb_activate ??= new _JniMarshal_PPLLLL_V (n_Activate);
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

		[RequiresUnreferencedCode ("Dynamic activation uses Type.GetType() which cannot be statically analyzed.")]
		static Type[] GetParameterTypes (string? signature)
		{
			if (String.IsNullOrEmpty (signature))
				return Array.Empty<Type> ();
			string[] typenames = signature!.Split (':');
			Type[] result = new Type [typenames.Length];
			for (int i = 0; i < typenames.Length; i++)
				result [i] = Type.GetType (typenames [i], throwOnError:true)!;
			return result;
		}

		[global::System.Diagnostics.DebuggerDisableUserUnhandledExceptions]
		[RequiresUnreferencedCode ("Dynamic activation uses Type.GetType() which cannot be statically analyzed.")]
		static void n_Activate (IntPtr jnienv, IntPtr jclass, IntPtr typename_ptr, IntPtr signature_ptr, IntPtr jobject, IntPtr parameters_ptr)
		{
			if (!global::Java.Interop.JniEnvironment.BeginMarshalMethod (jnienv, out var __envp, out var __r))
				return;

			try {
				var o   = Java.Lang.Object.PeekObject (jobject);
				var ex  = o as IJavaPeerable;
				if (ex != null) {
					var state = ex.JniManagedPeerState;
					if (!state.HasFlag (JniManagedPeerStates.Activatable) && !state.HasFlag (JniManagedPeerStates.Replaceable))
						return;
				}
				if (!ActivationEnabled) {
					if (Logger.LogGlobalRef) {
						Logger.Log (LogLevel.Info, "monodroid-gref",
							FormattableString.Invariant ($"warning: Skipping managed constructor invocation for handle 0x{jobject:x} (key_handle 0x{JNIEnv.IdentityHash (jobject):x}). Please use JNIEnv.StartCreateInstance() + JNIEnv.FinishCreateInstance() instead of JNIEnv.NewObject() and/or JNIEnv.CreateInstance()."));
					}
					return;
				}

				Type type = Type.GetType (JNIEnv.GetString (typename_ptr, JniHandleOwnership.DoNotTransfer)!, throwOnError:true)!;
				if (type.IsGenericTypeDefinition) {
					throw new NotSupportedException (
							"Constructing instances of generic types from Java is not supported, as the type parameters cannot be determined.",
							CreateJavaLocationException ());
				}
				Type[] ptypes = GetParameterTypes (JNIEnv.GetString (signature_ptr, JniHandleOwnership.DoNotTransfer));
				var parms = JNIEnv.GetObjectArray (parameters_ptr, ptypes);
				var cinfo = type.GetConstructor (ptypes);
				if (cinfo == null) {
					throw CreateMissingConstructorException (type, ptypes);
				}
				if (o != null) {
					cinfo.Invoke (o, parms);
					return;
				}

				Activate (jobject, cinfo, parms);
			} catch (global::System.Exception __e) {
				__r.OnUserUnhandledException (ref __envp, __e);
				return;
			} finally {
				global::Java.Interop.JniEnvironment.EndMarshalMethod (ref __envp);
			}
		}

		[RequiresUnreferencedCode ("Dynamic activation cannot statically determine the type from ConstructorInfo.DeclaringType.")]
		internal static void Activate (IntPtr jobject, ConstructorInfo cinfo, object? []? parms)
		{
			try {
				var newobj = RuntimeHelpers.GetUninitializedObject (cinfo.DeclaringType!);
				if (newobj is IJavaPeerable peer) {
					peer.SetPeerReference (new JniObjectReference (jobject));
				} else {
					throw new InvalidOperationException ($"Unsupported type: '{newobj}'");
				}
				cinfo.Invoke (newobj, parms);
			} catch (Exception e) {
				var m = FormattableString.Invariant (
					$"Could not activate JNI Handle 0x{jobject:x} (key_handle 0x{JNIEnv.IdentityHash (jobject):x}) of Java type '{JNIEnv.GetClassNameFromInstance (jobject)}' as managed type '{cinfo?.DeclaringType?.FullName}'.");
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

		internal static Exception CreateJavaLocationException ()
		{
			using (var loc = new Java.Lang.Error ("Java callstack:"))
				return new JavaLocationException (loc.ToString ());
		}

		internal static Type? GetJavaToManagedType (string class_name)
		{
			if (JNIEnvInit.TypeMap?.TryGetTypesForJniName (class_name, out var types) == true) {
				foreach (var type in types) {
					return type;
				}
			}
			return null;
		}

		internal static IJavaPeerable? CreateInstance (IntPtr handle, JniHandleOwnership transfer)
		{
			return CreateInstance (handle, transfer, null);
		}

		internal static IJavaPeerable? CreateInstance (IntPtr handle, JniHandleOwnership transfer, Type? targetType)
		{
			return JNIEnvInit.TypeMap?.CreatePeer (handle, transfer, targetType);
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

		static Dictionary<string, List<Converter<string, Type?>>>? packageLookup;

		[MemberNotNull (nameof (packageLookup))]
		static void LazyInitPackageLookup ()
		{
			if (packageLookup == null)
				packageLookup = new Dictionary<string, List<Converter<string, Type?>>> (StringComparer.Ordinal);
		}

		public static void RegisterPackage (string package, Converter<string, Type> lookup)
		{
			LazyInitPackageLookup ();

			lock (packageLookup!) {
				if (!packageLookup.TryGetValue (package, out var lookups))
					packageLookup.Add (package, lookups = new List<Converter<string, Type?>> ());
				lookups.Add (lookup);
			}
		}

		public static void RegisterPackages (string[] packages, Converter<string, Type?>[] lookups)
		{
			LazyInitPackageLookup ();

			if (packages == null)
				throw new ArgumentNullException ("packages");
			if (lookups == null)
				throw new ArgumentNullException ("lookups");
			if (packages.Length != lookups.Length)
				throw new ArgumentException ("`packages` and `lookups` arrays must have same number of elements.");

			lock (packageLookup!) {
				for (int i = 0; i < packages.Length; ++i) {
					string package                  = packages [i];
					var lookup			= lookups [i];

					if (!packageLookup.TryGetValue (package, out var _lookups))
						packageLookup.Add (package, _lookups = new List<Converter<string, Type?>> ());
					_lookups.Add (lookup);
				}
			}
		}

		/// <summary>
		/// Gets the function pointer for the [UnmanagedCallersOnly] n_Activate_mm method.
		/// This is used by TrimmableTypeMap to provide the function pointer for
		/// framework JCWs that call TypeManager.Activate().
		/// </summary>
		internal static unsafe IntPtr GetActivateFunctionPointer ()
		{
			delegate* unmanaged<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, void> fn = &JavaTypeManager.n_Activate_mm;
			return (IntPtr)fn;
		}

		[Register ("mono/android/TypeManager", DoNotGenerateAcw = true)]
		internal class JavaTypeManager : Java.Lang.Object
		{
			[Register ("activate", "(Ljava/lang/String;Ljava/lang/String;Ljava/lang/Object;[Ljava/lang/Object;)V", "")]
			static void n_Activate (IntPtr jnienv, IntPtr jclass, IntPtr typename_ptr, IntPtr signature_ptr, IntPtr jobject, IntPtr parameters_ptr)
			{
				TypeManager.n_Activate (jnienv, jclass, typename_ptr, signature_ptr, jobject, parameters_ptr);
			}

			[UnmanagedCallersOnly]
			internal static void n_Activate_mm (IntPtr jnienv, IntPtr jclass, IntPtr typename_ptr, IntPtr signature_ptr, IntPtr jobject, IntPtr parameters_ptr)
			{
				// TODO: need a full wrapper code here, a'la JNINativeWrapper.CreateDelegate
				try {
					TypeManager.n_Activate (jnienv, jclass, typename_ptr, signature_ptr, jobject, parameters_ptr);
				} catch (Exception ex) {
					AndroidEnvironment.UnhandledException (ex);
				}
			}

			internal static Delegate GetActivateHandler ()
			{
				return TypeManager.GetActivateHandler ();
			}
		}
	}
}
