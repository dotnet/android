using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Java.Interop;
using Java.Interop.Tools.TypeNameMappings;

namespace Android.Runtime
{
	/// <summary>
	/// Provides type mappings for Java-to-.NET type resolution using compile-time generated attributes.
	/// Used for NativeAOT and CoreCLR runtimes where the Type Mapping API is available.
	/// </summary>
	class TypeMapAttributeTypeMap : ITypeMap
	{
		readonly IReadOnlyDictionary<string, Type> _externalTypeMap;
		readonly IReadOnlyDictionary<Type, Type> _invokerTypeMap;

		// Cache of JavaPeerProxy instances keyed by the target type
		static readonly Dictionary<Type, JavaPeerProxy?> s_proxyInstances = new ();
		static readonly Lock s_proxyInstancesLock = new ();

		public TypeMapAttributeTypeMap ()
		{
			_externalTypeMap = TypeMapping.GetOrCreateExternalTypeMapping<Java.Lang.Object> ();
			_invokerTypeMap = TypeMapping.GetOrCreateProxyTypeMapping<InvokerUniverse> ();
		}

		/// <inheritdoc/>
		public bool TryGetTypesForJniName (string jniSimpleReference, [NotNullWhen (true)] out IEnumerable<Type>? types)
		{
			if (!_externalTypeMap.TryGetValue (jniSimpleReference, out Type? type)) {
				types = null;
				return false;
			}

			// Check if this is an alias type (multiple .NET types map to same Java name)
			var aliasesAttr = type.GetCustomAttribute<JavaInteropAliasesAttribute> ();
			if (aliasesAttr != null) {
				var aliasedTypes = new List<Type> ();
				foreach (var aliasKey in aliasesAttr.AliasKeys) {
					if (_externalTypeMap.TryGetValue (aliasKey, out Type? aliasedType)) {
						aliasedTypes.Add (aliasedType);
					}
				}
				if (aliasedTypes.Count > 0) {
					types = aliasedTypes;
					return true;
				}
			}

			// Not an alias type, just return it directly
			types = [type];
			return true;
		}

		/// <inheritdoc/>
		public bool TryGetInvokerType (Type type, [NotNullWhen (true)] out Type? invokerType)
		{
			return _invokerTypeMap.TryGetValue (type, out invokerType);
		}

		/// <inheritdoc/>
		public bool TryGetJniNameForType (Type type, [NotNullWhen (true)] out string? jniName)
		{
			// 1. Try to get explicit JNI name from [Register] attribute (or any IJniNameProviderAttribute)
			//    Use inherit: false because each type must have its own JNI name!
			var attrs = type.GetCustomAttributes (typeof (IJniNameProviderAttribute), inherit: false);
			if (attrs.Length > 0 && attrs[0] is IJniNameProviderAttribute jniNameProvider && !string.IsNullOrEmpty (jniNameProvider.Name)) {
				jniName = jniNameProvider.Name.Replace ('.', '/');
				return true;
			}

			// 2. Fallback: derive JNI name using naming conventions for types without explicit [Register]
			jniName = JavaNativeTypeManager.ToJniName (type);
			return !string.IsNullOrEmpty (jniName);
		}

		/// <inheritdoc/>
		public IEnumerable<string> GetJniNamesForType (Type type)
		{
			if (TryGetJniNameForType (type, out string? jniName)) {
				return [jniName];
			}
			return [];
		}

		/// <summary>
		/// Gets or creates a cached JavaPeerProxy instance for the given type.
		/// Uses GetCustomAttribute which is AOT-safe (the runtime knows how to instantiate attributes).
		/// </summary>
		internal static JavaPeerProxy? GetProxyForType (Type type)
		{
			lock (s_proxyInstancesLock) {
				if (s_proxyInstances.TryGetValue (type, out var cached)) {
					return cached;
				}

				// AOT-safe: GetCustomAttribute uses the runtime's attribute instantiation mechanism
				return s_proxyInstances [type] = type.GetCustomAttribute<JavaPeerProxy> (inherit: false);
			}
		}

		const DynamicallyAccessedMemberTypes Constructors = DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors;
		static readonly Type[] XAConstructorSignature = [typeof (IntPtr), typeof (JniHandleOwnership)];
		static readonly Type[] JIConstructorSignature = [typeof (JniObjectReference).MakeByRefType (), typeof (JniObjectReferenceOptions)];

		/// <inheritdoc/>
		public IJavaPeerable? CreatePeer (
			IntPtr handle,
			JniHandleOwnership transfer,
			[DynamicallyAccessedMembers (Constructors)]
			Type? targetType)
		{
			Type? type = null;
			IntPtr class_ptr = JNIEnv.GetObjectClass (handle);
			string? class_name = GetClassNameFromJavaClassHandle (class_ptr);

			lock (TypeManagerMapDictionaries.AccessLock) {
				while (class_ptr != IntPtr.Zero) {
					if (class_name != null) {
						_externalTypeMap.TryGetValue (class_name, out type);
						if (type != null) {
							break;
						}
					}

					IntPtr super_class_ptr = JNIEnv.GetSuperclass (class_ptr);
					JNIEnv.DeleteLocalRef (class_ptr);
					class_name = null;
					class_ptr = super_class_ptr;
					if (class_ptr != IntPtr.Zero) {
						class_name = GetClassNameFromJavaClassHandle (class_ptr);
					}
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

			if (type == null) {
				class_name = JNIEnv.GetClassNameFromInstance (handle);
				JNIEnv.DeleteRef (handle, transfer);
				throw new NotSupportedException (
						FormattableString.Invariant ($"Internal error finding wrapper class for '{class_name}'. (Where is the Java.Lang.Object wrapper?!)"),
						CreateJavaLocationException ());
			}

			if (type.IsInterface || type.IsAbstract) {
				if (!TryGetInvokerType (type, out var invokerType)) {
					throw new InvalidOperationException (
						FormattableString.Invariant ($"Cannot create instance of interface or abstract type '{type.FullName}'. No invoker type found."),
						CreateJavaLocationException ());
				}
				if (invokerType == null)
					throw new NotSupportedException ("Unable to find Invoker for type '" + type.FullName + "'. Was it linked away?",
						CreateJavaLocationException ());
				type = invokerType;
			}

			var typeSig = JNIEnvInit.androidRuntime?.TypeManager.GetTypeSignature (type) ?? default;
			if (!typeSig.IsValid || typeSig.SimpleReference == null) {
				throw new ArgumentException ($"Could not determine Java type corresponding to `{type.AssemblyQualifiedName}`.", nameof (targetType));
			}

			JniObjectReference typeClass = default;
			JniObjectReference handleClass = default;
			try {
				try {
					typeClass = JniEnvironment.Types.FindClass (typeSig.SimpleReference);
				} catch (Exception e) {
					throw new ArgumentException ($"Could not find Java class `{typeSig.SimpleReference}`.",
							nameof (targetType),
							e);
				}

				handleClass = JniEnvironment.Types.GetObjectClass (new JniObjectReference (handle));
				if (!JniEnvironment.Types.IsAssignableFrom (handleClass, typeClass)) {
					return null;
				}
			} finally {
				JniObjectReference.Dispose (ref handleClass);
				JniObjectReference.Dispose (ref typeClass);
			}

			if (!TryCreateInstance (type, handle, transfer, out var result)) {
				var key_handle = JNIEnv.IdentityHash (handle);
				JNIEnv.DeleteRef (handle, transfer);
				throw new NotSupportedException (FormattableString.Invariant (
					$"Unable to activate instance of type {type} from native handle 0x{handle:x} (key_handle 0x{key_handle:x})."));
			}

			if (Java.Interop.Runtime.IsGCUserPeer (result!.PeerReference.Handle)) {
				result.SetJniManagedPeerState (JniManagedPeerStates.Replaceable | JniManagedPeerStates.Activatable);
			}

			return result;

			static string? GetClassNameFromJavaClassHandle (IntPtr class_ptr)
			{
				// TODO could we move this code elsewhere so we don't need to reference TypeManager at all?
				return Java.Interop.TypeManager.GetClassName (class_ptr);
			}

			static Exception CreateJavaLocationException ()
			{
				// TODO could we move this code elsewhere so we don't need to reference TypeManager at all?
				return Java.Interop.TypeManager.CreateJavaLocationException ();
			}
		}

		/// <summary>
		/// Tries to create an instance of the specified type using AOT-safe reflection.
		/// </summary>
		/// <returns>true if the instance was created successfully; false if no suitable constructor was found.</returns>
		bool TryCreateInstance (
			[DynamicallyAccessedMembers (Constructors)]
			Type type,
			IntPtr handle,
			JniHandleOwnership transfer,
			[NotNullWhen (true)] out IJavaPeerable? result)
		{
			const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

			var peer = (IJavaPeerable) RuntimeHelpers.GetUninitializedObject (type);
			peer.SetJniManagedPeerState (JniManagedPeerStates.Replaceable | JniManagedPeerStates.Activatable);

			if (TryInvokeXAConstructor () || TryInvokeJIConstructor ())
			{
				result = peer;
				return true;
			}

			GC.SuppressFinalize (peer);
			result = null;
			return false;

			bool TryInvokeXAConstructor ()
			{
				var c = type.GetConstructor (flags, null, XAConstructorSignature, null);
				if (c != null) {
					c.Invoke (peer, [handle, transfer]);
					return true;
				}

				return false;
			}

			bool TryInvokeJIConstructor ()
			{
				var c = type.GetConstructor (flags, null, JIConstructorSignature, null);
				if (c != null) {
					c.Invoke (peer, [new JniObjectReference (handle), JniObjectReferenceOptions.Copy]);
					JNIEnv.DeleteRef (handle, transfer);
					return true;
				}

				return false;
			}
		}
	}
}
