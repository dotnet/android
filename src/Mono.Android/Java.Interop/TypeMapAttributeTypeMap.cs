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

		static bool LogTypemapTrace => Logger.LogTypemapTrace;

		static void Log (string message)
		{
			if (LogTypemapTrace)
				Logger.Log (LogLevel.Info, "monodroid-typemap", message);
		}

		public TypeMapAttributeTypeMap ()
		{
			Log ("TypeMapAttributeTypeMap: Initializing...");
			_externalTypeMap = TypeMapping.GetOrCreateExternalTypeMapping<Java.Lang.Object> ();
			_invokerTypeMap = TypeMapping.GetOrCreateProxyTypeMapping<InvokerUniverse> ();
			Log ("TypeMapAttributeTypeMap: Initialized external and invoker type mappings");
		}

		/// <inheritdoc/>
		public bool TryGetTypesForJniName (string jniSimpleReference, [NotNullWhen (true)] out IEnumerable<Type>? types)
		{
			if (!_externalTypeMap.TryGetValue (jniSimpleReference, out Type? type)) {
				Log ($"TryGetTypesForJniName: '{jniSimpleReference}' -> NOT FOUND");
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
					Log ($"TryGetTypesForJniName: '{jniSimpleReference}' -> {aliasedTypes.Count} aliased types");
					types = aliasedTypes;
					return true;
				}
			}

			// Not an alias type, just return it directly
			Log ($"TryGetTypesForJniName: '{jniSimpleReference}' -> {type.FullName}");
			types = [type];
			return true;
		}

		/// <inheritdoc/>
		public bool TryGetInvokerType (Type type, [NotNullWhen (true)] out Type? invokerType)
		{
			var result = _invokerTypeMap.TryGetValue (type, out invokerType);
			Log ($"TryGetInvokerType: {type.FullName} -> {(result ? invokerType!.FullName : "NOT FOUND")}");
			return result;
		}

		/// <inheritdoc/>
		public bool TryGetJniNameForType (Type type, [NotNullWhen (true)] out string? jniName)
		{
			// 1. Try to get explicit JNI name from [Register] attribute (or any IJniNameProviderAttribute)
			//    Use inherit: false because each type must have its own JNI name!
			var attrs = type.GetCustomAttributes (typeof (IJniNameProviderAttribute), inherit: false);
			if (attrs.Length > 0 && attrs[0] is IJniNameProviderAttribute jniNameProvider && !string.IsNullOrEmpty (jniNameProvider.Name)) {
				jniName = jniNameProvider.Name.Replace ('.', '/');
				Log ($"TryGetJniNameForType: {type.FullName} -> '{jniName}' (from [Register])");
				return true;
			}

			// 2. Fallback: use [JniTypeSignature] if present
			var sigAttr = type.GetCustomAttribute<JniTypeSignatureAttribute> (inherit: false);
			if (sigAttr != null && !string.IsNullOrEmpty (sigAttr.SimpleReference)) {
				jniName = sigAttr.SimpleReference;
				Log ($"TryGetJniNameForType: {type.FullName} -> '{jniName}' (from [JniTypeSignature])");
				return true;
			}

			// 3. Fallback: derive JNI name using naming conventions for types without explicit [Register]
			jniName = JavaNativeTypeManager.ToJniName (type);
			Log ($"TryGetJniNameForType: {type.FullName} -> '{jniName}' (derived)");
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
			Log ($"CreatePeer: handle=0x{handle:x}, targetType={targetType?.FullName ?? "null"}");
			
			Type? type = null;
			IntPtr class_ptr = JNIEnv.GetObjectClass (handle);
			string? class_name = GetClassNameFromJavaClassHandle (class_ptr);
			string? original_class_name = class_name;

			lock (TypeManagerMapDictionaries.AccessLock) {
				while (class_ptr != IntPtr.Zero) {
					if (class_name != null) {
						_externalTypeMap.TryGetValue (class_name, out type);
						if (type != null) {
							Log ($"CreatePeer: Found type {type.FullName} for Java class '{class_name}'");
							break;
						}
						Log ($"CreatePeer: No mapping for '{class_name}', checking superclass...");
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
				Log ($"CreatePeer: Using targetType {targetType.FullName} instead of {type?.FullName ?? "null"}");
				type = targetType;
			}

			if (type == null) {
				class_name = JNIEnv.GetClassNameFromInstance (handle);
				Log ($"CreatePeer: FAILED - No wrapper class for '{class_name}'");
				JNIEnv.DeleteRef (handle, transfer);
				throw new NotSupportedException (
						FormattableString.Invariant ($"Internal error finding wrapper class for '{class_name}'. (Where is the Java.Lang.Object wrapper?!)"),
						CreateJavaLocationException ());
			}

			if (type.IsInterface || type.IsAbstract) {
				Log ($"CreatePeer: Type {type.FullName} is interface/abstract, looking for invoker...");
				if (!TryGetInvokerType (type, out var invokerType)) {
					throw new InvalidOperationException (
						FormattableString.Invariant ($"Cannot create instance of interface or abstract type '{type.FullName}'. No invoker type found."),
						CreateJavaLocationException ());
				}
				if (invokerType == null)
					throw new NotSupportedException ("Unable to find Invoker for type '" + type.FullName + "'. Was it linked away?",
						CreateJavaLocationException ());
				Log ($"CreatePeer: Using invoker type {invokerType.FullName}");
				type = invokerType;
			}

			if (!TryGetJniNameForType (type, out string? jniName) || string.IsNullOrEmpty (jniName)) {
				throw new ArgumentException ($"Could not determine Java type corresponding to `{type.AssemblyQualifiedName}`.", nameof (targetType));
			}

			JniObjectReference typeClass = default;
			JniObjectReference handleClass = default;
			try {
				try {
					typeClass = JniEnvironment.Types.FindClass (jniName);
				} catch (Exception e) {
					throw new ArgumentException ($"Could not find Java class `{jniName}`.",
							nameof (targetType),
							e);
				}

				handleClass = JniEnvironment.Types.GetObjectClass (new JniObjectReference (handle));
				if (!JniEnvironment.Types.IsAssignableFrom (handleClass, typeClass)) {
					Log ($"CreatePeer: Handle class is not assignable to {jniName}, returning null");
					return null;
				}
			} finally {
				JniObjectReference.Dispose (ref handleClass);
				JniObjectReference.Dispose (ref typeClass);
			}

			Log ($"CreatePeer: Activating instance of {type.FullName}...");
			if (!TryCreateInstance (type, handle, transfer, out var result)) {
				var key_handle = JNIEnv.IdentityHash (handle);
				JNIEnv.DeleteRef (handle, transfer);
				throw new NotSupportedException (FormattableString.Invariant (
					$"Unable to activate instance of type {type} from native handle 0x{handle:x} (key_handle 0x{key_handle:x})."));
			}

			Log ($"CreatePeer: SUCCESS - Created {result!.GetType ().FullName} for Java class '{original_class_name}'");
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

		#region Marshal Method Function Pointer Resolution

		/// <summary>
		/// Resolves a marshal method function pointer by JNI class name and method index.
		/// Called from native code (LLVM IR stubs) via the get_function_pointer callback.
		/// </summary>
		[UnmanagedCallersOnly]
		internal static unsafe void GetFunctionPointer (byte* classNamePtr, int classNameLength, int methodIndex, IntPtr* targetPtr)
		{
			try {
				string className = System.Text.Encoding.UTF8.GetString (classNamePtr, classNameLength);
				Log ($"GetFunctionPointer: class='{className}', methodIndex={methodIndex}");

				// Look up type directly from the external type map
				var typeMap = JNIEnvInit.TypeMap as TypeMapAttributeTypeMap;
				if (typeMap == null || !typeMap._externalTypeMap.TryGetValue (className, out Type? type)) {
					Log ($"GetFunctionPointer: No type found for '{className}'");
					*targetPtr = IntPtr.Zero;
					return;
				}

				Log ($"GetFunctionPointer: Found type {type.FullName}");

				// Get the JavaPeerProxy attribute for this type
				JavaPeerProxy? proxy = GetProxyForType (type);
				if (proxy == null) {
					Log ($"GetFunctionPointer: No JavaPeerProxy attribute on {type.FullName}, attempting reflection fallback...");
					*targetPtr = ResolveUserTypeMethod (type, methodIndex);
					return;
				}

				// Get the function pointer from the proxy
				IntPtr fnPtr = proxy.GetFunctionPointer (methodIndex);
				Log ($"GetFunctionPointer: Got function pointer 0x{fnPtr:x} for method index {methodIndex}");

				*targetPtr = fnPtr;
			} catch (Exception ex) {
				Log ($"GetFunctionPointer: Exception - {ex}");
				*targetPtr = IntPtr.Zero;
			}
		}

		static IntPtr ResolveUserTypeMethod (Type type, int index)
		{
			var candidates = new List<RegisterAttribute> ();
			// Assumption: Reflection returns methods in definition order (matching Cecil)
			foreach (var m in type.GetMethods (BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)) {
				var attr = m.GetCustomAttribute<RegisterAttribute> ();
				if (attr != null && !string.IsNullOrEmpty (attr.Name) && !string.IsNullOrEmpty (attr.Signature)) {
					candidates.Add (attr);
				}
			}

			if (index < 0 || index >= candidates.Count) {
				Log ($"ResolveUserTypeMethod: Index {index} out of range (count {candidates.Count})");
				return IntPtr.Zero;
			}

			var regAttr = candidates [index];
			string callbackName = regAttr.Connector;
			if (string.IsNullOrEmpty (callbackName)) {
				callbackName = "n_" + regAttr.Name;
			}

			var callbackMethod = type.GetMethod (callbackName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			if (callbackMethod == null) {
				Log ($"ResolveUserTypeMethod: Callback method '{callbackName}' not found on {type.FullName}");
				return IntPtr.Zero;
			}

			Log ($"ResolveUserTypeMethod: Resolved index {index} to {callbackName}");
			return callbackMethod.MethodHandle.GetFunctionPointer ();
		}

		#endregion
	}
}
