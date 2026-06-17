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

		[UnconditionalSuppressMessage ("Trimming", "IL2057", Justification = "Type.GetType() can never statically know the string value from parameter 'signature'.")]
		static Type[] GetParameterTypes (string? signature)
		{
			using var operation = TrimmableTypeMapTelemetry.StartOperation ("typemap.llvm.activation.get_parameter_types");
			if (String.IsNullOrEmpty (signature))
				return Array.Empty<Type> ();
			string[] typenames = signature!.Split (':');
			if (operation.IsActive) {
				operation.SetTag ("parameter.count", typenames.Length);
			}
			Type[] result = new Type [typenames.Length];
			for (int i = 0; i < typenames.Length; i++) {
				using var resolveOperation = TrimmableTypeMapTelemetry.StartOperation ("typemap.llvm.activation.resolve_parameter_type");
				if (resolveOperation.IsActive) {
					resolveOperation.SetTag ("managed.type.name", typenames [i]);
				}
				result [i] = Type.GetType (typenames [i], throwOnError:true)!;
				if (resolveOperation.IsActive) {
					resolveOperation.SetTag ("managed.type", result [i].FullName);
				}
			}
			return result;
		}

		[global::System.Diagnostics.DebuggerDisableUserUnhandledExceptions]
		[UnconditionalSuppressMessage ("Trimming", "IL2057", Justification = "Type.GetType() can never statically know the string value from parameter 'typename_ptr'.")]
		static void n_Activate (IntPtr jnienv, IntPtr jclass, IntPtr typename_ptr, IntPtr signature_ptr, IntPtr jobject, IntPtr parameters_ptr)
		{
			using var operation = TrimmableTypeMapTelemetry.StartOperation ("typemap.llvm.activation");
			if (!global::Java.Interop.JniEnvironment.BeginMarshalMethod (jnienv, out var __envp, out var __r))
				return;

			try {
				IJavaPeerable? o;
				using (var peekOperation = TrimmableTypeMapTelemetry.StartOperation ("typemap.llvm.activation.peek_object")) {
					o = Java.Lang.Object.PeekObject (jobject);
					if (peekOperation.IsActive) {
						peekOperation.SetTag ("has.peer", o is not null);
						peekOperation.SetTag ("peer.type", o?.GetType ().FullName);
					}
				}
				var ex  = o;
				if (ex != null) {
					var state = ex.JniManagedPeerState;
					if (operation.IsActive) {
						operation.SetTag ("peer.state", state.ToString ());
					}
					if (!state.HasFlag (JniManagedPeerStates.Activatable) && !state.HasFlag (JniManagedPeerStates.Replaceable)) {
						if (operation.IsActive) {
							operation.SetTag ("skip", true);
							operation.SetTag ("reason", "existing-peer-not-activatable");
						}
						return;
					}
				}
				if (!ActivationEnabled) {
					if (Logger.LogGlobalRef) {
						Logger.Log (LogLevel.Info, "monodroid-gref",
							FormattableString.Invariant ($"warning: Skipping managed constructor invocation for handle 0x{jobject:x} (key_handle 0x{JNIEnv.IdentityHash (jobject):x}). Please use JNIEnv.StartCreateInstance() + JNIEnv.FinishCreateInstance() instead of JNIEnv.NewObject() and/or JNIEnv.CreateInstance()."));
					}
					if (operation.IsActive) {
						operation.SetTag ("skip", true);
						operation.SetTag ("reason", "activation-disabled");
					}
					return;
				}

				Type type;
				using (var typeOperation = TrimmableTypeMapTelemetry.StartOperation ("typemap.llvm.activation.resolve_type")) {
					var typeName = JNIEnv.GetString (typename_ptr, JniHandleOwnership.DoNotTransfer);
					if (typeOperation.IsActive) {
						typeOperation.SetTag ("managed.type.name", typeName);
					}
					type = Type.GetType (typeName!, throwOnError:true)!;
					if (typeOperation.IsActive) {
						typeOperation.SetTag ("managed.type", type.FullName);
					}
				}
				if (operation.IsActive) {
					operation.SetTag ("managed.type", type.FullName);
				}
				if (type.IsGenericTypeDefinition) {
					throw new NotSupportedException (
							"Constructing instances of generic types from Java is not supported, as the type parameters cannot be determined.",
							CreateJavaLocationException ());
				}
				Type[] ptypes = GetParameterTypes (JNIEnv.GetString (signature_ptr, JniHandleOwnership.DoNotTransfer));
				object? []? parms;
				using (var parametersOperation = TrimmableTypeMapTelemetry.StartOperation ("typemap.llvm.activation.get_object_array")) {
					if (parametersOperation.IsActive) {
						parametersOperation.SetTag ("parameter.count", ptypes.Length);
					}
					parms = JNIEnv.GetObjectArray (parameters_ptr, ptypes);
				}
				ConstructorInfo? cinfo;
				using (var constructorOperation = TrimmableTypeMapTelemetry.StartOperation ("typemap.llvm.activation.constructor_lookup")) {
					if (constructorOperation.IsActive) {
						constructorOperation.SetTag ("managed.type", type.FullName);
						constructorOperation.SetTag ("parameter.count", ptypes.Length);
					}
					cinfo = type.GetConstructor (ptypes);
					if (constructorOperation.IsActive) {
						constructorOperation.SetTag ("found", cinfo is not null);
					}
				}
				if (cinfo == null) {
					throw CreateMissingConstructorException (type, ptypes);
				}
				if (o != null) {
					using var invokeOperation = TrimmableTypeMapTelemetry.StartOperation ("typemap.llvm.activation.invoke_existing_peer");
					if (invokeOperation.IsActive) {
						invokeOperation.SetTag ("managed.type", type.FullName);
					}
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

		[UnconditionalSuppressMessage ("Trimming", "IL2072", Justification = "RuntimeHelpers.GetUninitializedObject() does not statically know the return value from ConstructorInfo.DeclaringType.")]
		internal static void Activate (IntPtr jobject, ConstructorInfo cinfo, object? []? parms)
		{
			using var operation = TrimmableTypeMapTelemetry.StartOperation ("typemap.llvm.activation.activate_uninitialized");
			if (operation.IsActive) {
				operation.SetTag ("managed.type", cinfo.DeclaringType?.FullName);
				operation.SetTag ("parameter.count", parms?.Length ?? 0);
			}
			try {
				var newobj = RuntimeHelpers.GetUninitializedObject (cinfo.DeclaringType!);
				if (operation.IsActive) {
					operation.SetTag ("created.uninitialized", true);
				}
				if (newobj is IJavaPeerable peer) {
					using (var peerOperation = TrimmableTypeMapTelemetry.StartOperation ("typemap.llvm.activation.set_peer_reference")) {
						if (peerOperation.IsActive) {
							peerOperation.SetTag ("managed.type", cinfo.DeclaringType?.FullName);
						}
						peer.SetPeerReference (new JniObjectReference (jobject));
					}
				} else {
					throw new InvalidOperationException ($"Unsupported type: '{newobj}'");
				}
				using var invokeOperation = TrimmableTypeMapTelemetry.StartOperation ("typemap.llvm.activation.invoke_constructor");
				if (invokeOperation.IsActive) {
					invokeOperation.SetTag ("managed.type", cinfo.DeclaringType?.FullName);
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

		static Exception CreateJavaLocationException ()
		{
			using (var loc = new Java.Lang.Error ("Java callstack:"))
				return new JavaLocationException (loc.ToString ());
		}

		[MethodImplAttribute(MethodImplOptions.InternalCall)]
		static extern Type monodroid_typemap_java_to_managed (string java_type_name);

		static Type monovm_typemap_java_to_managed (string java_type_name)
		{
			return monodroid_typemap_java_to_managed (java_type_name);
		}

		[UnconditionalSuppressMessage ("Trimming", "IL2026", Justification = "Value of java_type_name isn't statically known.")]
		static Type? clr_typemap_java_to_managed (string java_type_name)
		{
			bool result = RuntimeNativeMethods.clr_typemap_java_to_managed (java_type_name, out IntPtr managedAssemblyNamePointer, out uint managedTypeTokenId);
			if (!result || managedAssemblyNamePointer == IntPtr.Zero) {
				return null;
			}

			string managedAssemblyName = Marshal.PtrToStringAnsi (managedAssemblyNamePointer);
			Assembly assembly = Assembly.Load (managedAssemblyName);
			Type? ret = null;
			foreach (Module module in assembly.Modules) {
				ret = module.ResolveType ((int)managedTypeTokenId);
				if (ret != null) {
					break;
				}
			}

			if (Logger.LogAssembly) {
				Logger.Log (LogLevel.Info, "monodroid", $"Loaded type: {ret}");
			}

			return ret;
		}

		internal static Type? GetJavaToManagedType (string class_name)
		{
			lock (TypeManagerMapDictionaries.AccessLock) {
				return GetJavaToManagedTypeCore (class_name);
			}
		}

		static Type? GetJavaToManagedTypeCore (string class_name)
		{
			using var operation = TrimmableTypeMapTelemetry.StartOperation ("typemap.llvm.lookup.jni_name");
			if (operation.IsActive) {
				operation.SetTag ("jni.name", class_name);
			}
			if (TypeManagerMapDictionaries.JniToManaged.TryGetValue (class_name, out Type? type)) {
				if (operation.IsActive) {
					operation.SetTag ("cache.hit", true);
					operation.SetTag ("managed.type", type.FullName);
				}
				return type;
			}

			if (operation.IsActive) {
				operation.SetTag ("cache.hit", false);
			}

			if (RuntimeFeature.TrimmableTypeMap) {
				throw new System.Diagnostics.UnreachableException (
					$"{nameof (TypeManager)}.{nameof (GetJavaToManagedTypeCore)} should not be used when " +
					$"{nameof (RuntimeFeature.TrimmableTypeMap)} is enabled. The trimmable path should resolve " +
					$"types through {nameof (TrimmableTypeMapTypeManager)}.");
			} else if (RuntimeFeature.IsMonoRuntime) {
				using var uncachedOperation = TrimmableTypeMapTelemetry.StartOperation ("typemap.llvm.lookup.jni_name.uncached");
				if (uncachedOperation.IsActive) {
					uncachedOperation.SetTag ("jni.name", class_name);
					uncachedOperation.SetTag ("runtime", "monovm");
				}
				type = monovm_typemap_java_to_managed (class_name);
				if (uncachedOperation.IsActive) {
					uncachedOperation.SetTag ("managed.type", type?.FullName);
				}
			} else if (RuntimeFeature.IsCoreClrRuntime) {
				using var uncachedOperation = TrimmableTypeMapTelemetry.StartOperation ("typemap.llvm.lookup.jni_name.uncached");
				if (uncachedOperation.IsActive) {
					uncachedOperation.SetTag ("jni.name", class_name);
					uncachedOperation.SetTag ("runtime", "coreclr");
				}
				type = clr_typemap_java_to_managed (class_name);
				if (uncachedOperation.IsActive) {
					uncachedOperation.SetTag ("managed.type", type?.FullName);
				}
			} else {
				throw new NotSupportedException ("Internal error: unknown runtime not supported");
			}

			if (type != null) {
				if (operation.IsActive) {
					operation.SetTag ("managed.type", type.FullName);
				}
				TypeManagerMapDictionaries.JniToManaged.Add (class_name, type);
				return type;
			}

			// Miss message is logged in the native runtime
			if (Logger.LogAssembly)
				JNIEnv.LogTypemapTrace (new System.Diagnostics.StackTrace (true));
			return null;
		}

		internal static IJavaPeerable? CreateInstance (IntPtr handle, JniHandleOwnership transfer)
		{
			return CreateInstance (handle, transfer, null);
		}

		[UnconditionalSuppressMessage ("Trimming", "IL2067", Justification = "TypeManager.CreateProxy() does not statically know the value of the 'type' local variable.")]
		[UnconditionalSuppressMessage ("Trimming", "IL2072", Justification = "TypeManager.CreateProxy() does not statically know the value of the 'type' local variable.")]
		internal static IJavaPeerable? CreateInstance (IntPtr handle, JniHandleOwnership transfer, Type? targetType)
		{
			using var operation = TrimmableTypeMapTelemetry.StartOperation ("typemap.llvm.peer.create");
			if (operation.IsActive) {
				operation.SetTag ("target.type", targetType?.FullName);
			}
			Type? type = null;
			IntPtr class_ptr;
			using (var classOperation = TrimmableTypeMapTelemetry.StartOperation ("typemap.llvm.peer.get_object_class")) {
				class_ptr = JNIEnv.GetObjectClass (handle);
				if (classOperation.IsActive) {
					classOperation.SetTag ("class.handle", class_ptr);
				}
			}
			string? class_name;
			using (var nameOperation = TrimmableTypeMapTelemetry.StartOperation ("typemap.llvm.peer.get_class_name")) {
				class_name = GetClassName (class_ptr);
				if (nameOperation.IsActive) {
					nameOperation.SetTag ("jni.name", class_name);
				}
			}
			if (operation.IsActive) {
				operation.SetTag ("jni.name", class_name);
			}
			lock (TypeManagerMapDictionaries.AccessLock) {
				int depth = 0;
				while (class_ptr != IntPtr.Zero) {
					using (var resolveOperation = TrimmableTypeMapTelemetry.StartOperation ("typemap.llvm.peer.resolve_class")) {
						if (resolveOperation.IsActive) {
							resolveOperation.SetTag ("jni.name", class_name);
							resolveOperation.SetTag ("depth", depth);
						}
						type = GetJavaToManagedTypeCore (class_name);
						if (resolveOperation.IsActive) {
							resolveOperation.SetTag ("managed.type", type?.FullName);
							resolveOperation.SetTag ("resolved", type is not null);
						}
						if (type != null) {
							break;
						}
					}

					IntPtr super_class_ptr;
					using (var superOperation = TrimmableTypeMapTelemetry.StartOperation ("typemap.llvm.peer.get_superclass")) {
						if (superOperation.IsActive) {
							superOperation.SetTag ("jni.name", class_name);
							superOperation.SetTag ("depth", depth);
						}
						super_class_ptr = JNIEnv.GetSuperclass (class_ptr);
						if (superOperation.IsActive) {
							superOperation.SetTag ("has.superclass", super_class_ptr != IntPtr.Zero);
						}
					}
					JNIEnv.DeleteLocalRef (class_ptr);
					class_name = null;
					class_ptr = super_class_ptr;
					if (class_ptr != IntPtr.Zero) {
						using var superclassNameOperation = TrimmableTypeMapTelemetry.StartOperation ("typemap.llvm.peer.get_superclass_name");
						class_name = GetClassName (class_ptr);
						if (superclassNameOperation.IsActive) {
							superclassNameOperation.SetTag ("jni.name", class_name);
							superclassNameOperation.SetTag ("depth", depth + 1);
						}
					}
					depth++;
				}
			}

			if (class_ptr != IntPtr.Zero) {
				JNIEnv.DeleteLocalRef (class_ptr);
				class_ptr = IntPtr.Zero;
			}

			if (targetType != null &&
					(type == null ||
					 !targetType.IsAssignableFrom (type))) {
				type = targetType;
			}
			if (operation.IsActive) {
				operation.SetTag ("managed.type", type?.FullName);
			}

			if (type == null) {
				class_name = JNIEnv.GetClassNameFromInstance (handle);
				JNIEnv.DeleteRef (handle, transfer);
				throw new NotSupportedException (
						FormattableString.Invariant ($"Internal error finding wrapper class for '{class_name}'. (Where is the Java.Lang.Object wrapper?!)"),
						CreateJavaLocationException ());
			}

			if (type.IsInterface || type.IsAbstract) {
				var invokerType = JavaObjectExtensions.GetInvokerType (type);
				if (invokerType == null)
					throw new NotSupportedException ("Unable to find Invoker for type '" + type.FullName + "'. Was it linked away?",
							CreateJavaLocationException ());
				type = invokerType;
				if (operation.IsActive) {
					operation.SetTag ("invoker.type", type.FullName);
				}
			}

			JniTypeSignature typeSig;
			using (var signatureOperation = TrimmableTypeMapTelemetry.StartOperation ("typemap.llvm.peer.get_type_signature")) {
				if (signatureOperation.IsActive) {
					signatureOperation.SetTag ("managed.type", type.FullName);
				}
				typeSig  = JNIEnvInit.androidRuntime?.TypeManager.GetTypeSignature (type) ?? default;
				if (signatureOperation.IsActive) {
					signatureOperation.SetTag ("jni.simple_reference", typeSig.SimpleReference);
					signatureOperation.SetTag ("valid", typeSig.IsValid);
				}
			}
			if (!typeSig.IsValid || typeSig.SimpleReference == null) {
				throw new ArgumentException ($"Could not determine Java type corresponding to `{type.AssemblyQualifiedName}`.", nameof (targetType));
			}

			JniObjectReference typeClass = default;
			JniObjectReference handleClass = default;
			try {
				try {
					using var findClassOperation = TrimmableTypeMapTelemetry.StartOperation ("typemap.llvm.peer.find_type_class");
					if (findClassOperation.IsActive) {
						findClassOperation.SetTag ("jni.name", typeSig.SimpleReference);
					}
					typeClass = JniEnvironment.Types.FindClass (typeSig.SimpleReference);
				} catch (Exception e) {
					throw new ArgumentException ($"Could not find Java class `{typeSig.SimpleReference}`.",
							nameof (targetType),
							e);
				}

				using (var handleClassOperation = TrimmableTypeMapTelemetry.StartOperation ("typemap.llvm.peer.get_handle_class")) {
					handleClass = JniEnvironment.Types.GetObjectClass (new JniObjectReference (handle));
				}
				bool isAssignable;
				using (var assignabilityOperation = TrimmableTypeMapTelemetry.StartOperation ("typemap.llvm.peer.assignability_check")) {
					if (assignabilityOperation.IsActive) {
						assignabilityOperation.SetTag ("managed.type", type.FullName);
						assignabilityOperation.SetTag ("jni.simple_reference", typeSig.SimpleReference);
					}
					isAssignable = JniEnvironment.Types.IsAssignableFrom (handleClass, typeClass);
					if (assignabilityOperation.IsActive) {
						assignabilityOperation.SetTag ("assignable", isAssignable);
					}
				}
				if (!isAssignable) {
					if (Logger.LogAssembly) {
						var message = $"Handle 0x{handle:x} is of type '{JNIEnv.GetClassNameFromInstance (handle)}' which is not assignable to '{typeSig.SimpleReference}'";
						Logger.Log (LogLevel.Debug, "monodroid-assembly", message);
					}
					if (RuntimeFeature.IsAssignableFromCheck) {
						return null;
					}
				}
			} finally {
				JniObjectReference.Dispose (ref handleClass);
				JniObjectReference.Dispose (ref typeClass);
			}

			IJavaPeerable? result = null;

			try {
				result = (IJavaPeerable) CreateProxy (type, handle, transfer);
				if (operation.IsActive) {
					operation.SetTag ("created", true);
				}
				if (Runtime.IsGCUserPeer (result.PeerReference.Handle)) {
					result.SetJniManagedPeerState (JniManagedPeerStates.Replaceable | JniManagedPeerStates.Activatable);
				}
			} catch (MissingMethodException e) {
				var key_handle  = JNIEnv.IdentityHash (handle);
				JNIEnv.DeleteRef (handle, transfer);
				throw new NotSupportedException (FormattableString.Invariant (
					$"Unable to activate instance of type {type} from native handle 0x{handle:x} (key_handle 0x{key_handle:x})."), e);
			}
			return result;
 		}

		static  readonly    Type[]  XAConstructorSignature  = new Type [] { typeof (IntPtr), typeof (JniHandleOwnership) };
		static  readonly    Type[]  JIConstructorSignature  = new Type [] { typeof (JniObjectReference).MakeByRefType (), typeof (JniObjectReferenceOptions) };

		internal static object CreateProxy (
				[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
				Type type,
				IntPtr handle,
				JniHandleOwnership transfer)
		{
			using var operation = TrimmableTypeMapTelemetry.StartOperation ("typemap.llvm.proxy.create");
			if (operation.IsActive) {
				operation.SetTag ("managed.type", type.FullName);
			}
			// Skip Activator.CreateInstance() as that requires public constructors,
			// and we want to hide some constructors for sanity reasons.
			var peer = GetUninitializedObject (type);
			BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
			var c = type.GetConstructor (flags, null, XAConstructorSignature, null);
			if (c != null) {
				if (operation.IsActive) {
					operation.SetTag ("constructor.signature", "IntPtr,JniHandleOwnership");
				}
				c.Invoke (peer, new object[] { handle, transfer });
				return peer;
			}
			c = type.GetConstructor (flags, null, JIConstructorSignature, null);
			if (c != null) {
				if (operation.IsActive) {
					operation.SetTag ("constructor.signature", "JniObjectReference,JniObjectReferenceOptions");
				}
				JniObjectReference          r = new JniObjectReference (handle);
				JniObjectReferenceOptions   o = JniObjectReferenceOptions.Copy;
				c.Invoke (peer, new object [] { r, o });
				JNIEnv.DeleteRef (handle, transfer);
				return peer;
			}
			GC.SuppressFinalize (peer);
			throw new MissingMethodException (
					"No constructor found for " + type.FullName + "::.ctor(System.IntPtr, Android.Runtime.JniHandleOwnership)",
					CreateJavaLocationException ());

			static IJavaPeerable GetUninitializedObject (
					[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
					Type type)
			{
				var v   = (IJavaPeerable) System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject (type);
				v.SetJniManagedPeerState (JniManagedPeerStates.Replaceable | JniManagedPeerStates.Activatable);
				return v;
			}
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
			"Java package type registration is no longer supported. Java-to-managed type resolution now goes through the native and trimmable type maps.";

		// The package-based Java-to-managed type registration fallback was removed
		// (https://github.com/dotnet/android/issues/11663); type resolution now goes
		// through the native / trimmable type map, and the generator no longer emits
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

		[Register ("mono/android/TypeManager", DoNotGenerateAcw = true)]
		internal class JavaTypeManager : Java.Lang.Object
		{
			[Register ("activate", "(Ljava/lang/String;Ljava/lang/String;Ljava/lang/Object;[Ljava/lang/Object;)V", "")]
			static void n_Activate (IntPtr jnienv, IntPtr jclass, IntPtr typename_ptr, IntPtr signature_ptr, IntPtr jobject, IntPtr parameters_ptr)
			{
				TypeManager.n_Activate (jnienv, jclass, typename_ptr, signature_ptr, jobject, parameters_ptr);
			}

			[UnmanagedCallersOnly]
			static void n_Activate_mm (IntPtr jnienv, IntPtr jclass, IntPtr typename_ptr, IntPtr signature_ptr, IntPtr jobject, IntPtr parameters_ptr)
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
