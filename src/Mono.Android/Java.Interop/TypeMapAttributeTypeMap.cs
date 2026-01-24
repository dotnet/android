#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
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

		// Cache of JavaPeerProxy instances keyed by the target type
		static readonly Dictionary<Type, JavaPeerProxy?> s_proxyInstances = new ();
		static readonly Lock s_proxyInstancesLock = new ();

		static bool LogTypemapTrace => Logger.LogTypemapTrace;

		static void Log (string message)
		{
			if (LogTypemapTrace)
				Logger.Log (LogLevel.Info, "monodroid-typemap", message);
		}

		const string TypeMapsAssemblyName = "_Microsoft.Android.TypeMaps";

		public TypeMapAttributeTypeMap ()
		{
			Log ("TypeMapAttributeTypeMap: Initializing...");

			// Load the TypeMaps assembly and set it as the entry assembly so the TypeMapping API
			// knows where to look for TypeMap attributes. Android apps don't have a traditional
			// entry point assembly, so we need to set this explicitly.
			var typeMapAssembly = Assembly.Load (TypeMapsAssemblyName);
			Assembly.SetEntryAssembly (typeMapAssembly);
			Log ($"TypeMapAttributeTypeMap: Set entry assembly to {typeMapAssembly.FullName}");

			try {
				_externalTypeMap = TypeMapping.GetOrCreateExternalTypeMapping<Java.Lang.Object> ();
				Log ($"TypeMapAttributeTypeMap: External type map created, testing TryGetValue...");
				// Test lookup to verify map is populated
				bool found = _externalTypeMap.TryGetValue ("example/MainActivity", out var testType);
				Log ($"TypeMapAttributeTypeMap: Direct test lookup 'example/MainActivity' -> found={found}, type={testType?.FullName ?? "null"}");
			} catch (Exception ex) {
				Log ($"TypeMapAttributeTypeMap: EXCEPTION creating external type map: {ex.GetType ().Name}: {ex.Message}");
				throw;
			}
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

				// Use GetCustomAttribute to get the proxy instance - this is AOT and trimming safe
				// The proxy type extends JavaPeerProxy (which extends Attribute) and is applied to the original type
				var proxy = type.GetCustomAttribute<JavaPeerProxy> (inherit: false);
				Log ($"GetProxyForType: {type.FullName} -> {(proxy != null ? proxy.GetType ().FullName : "null")}");
				return s_proxyInstances [type] = proxy;
			}
		}

		/// <inheritdoc/>
		public IJavaPeerable? CreatePeer (IntPtr handle, JniHandleOwnership transfer, Type? targetType)
		{
			Log ($"CreatePeer: handle=0x{handle:x}, targetType={targetType?.FullName ?? "null"}");
			
			Type? type = null;
			Type? proxyType = null;
			IntPtr class_ptr = JNIEnv.GetObjectClass (handle);
			string? class_name = GetClassNameFromJavaClassHandle (class_ptr);
			string? original_class_name = class_name;

			lock (TypeManagerMapDictionaries.AccessLock) {
				while (class_ptr != IntPtr.Zero) {
					if (class_name != null) {
						_externalTypeMap.TryGetValue (class_name, out type);
						if (type != null) {
							Log ($"CreatePeer: Found type {type.FullName} for Java class '{class_name}'");
							// If the found type is a JavaPeerProxy, it's our generated proxy
							if (typeof (JavaPeerProxy).IsAssignableFrom (type)) {
								proxyType = type;
								Log ($"CreatePeer: {type.FullName} is a JavaPeerProxy");
							}
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

			// If we found a proxy type, get the target type from it
			// If we have a targetType hint and it's assignable, prefer it
			if (proxyType != null) {
				if (targetType != null) {
					Log ($"CreatePeer: Using targetType {targetType.FullName} with proxy {proxyType.FullName}");
					type = targetType;
				} else {
					// Use the proxy's target type (we'll create instance via proxy)
					type = proxyType;
				}
			} else if (targetType != null &&
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

			if (!TryGetJniNameForType (type, out string? jniName) || string.IsNullOrEmpty (jniName)) {
				throw new ArgumentException ($"Could not determine Java type corresponding to `{type.AssemblyQualifiedName}`.", nameof (targetType));
			}

			if (!IsJavaTypeAssignableFrom (handle, jniName)) {
				Log ($"CreatePeer: Handle class is not assignable to {jniName}, returning null");
				return null;
			}

			Log ($"CreatePeer: Activating instance of {type.FullName}...");
			if (!TryCreateInstance (type, proxyType, handle, transfer, out var result)) {
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

			static bool IsJavaTypeAssignableFrom (IntPtr handle, string jniName)
			{
				JniObjectReference typeClass = default;
				try {
					typeClass = JniEnvironment.Types.FindClass (jniName);
				} catch (Exception e) {
					throw new ArgumentException ($"Could not find Java class `{jniName}`.", nameof (targetType), e);
				}

				JniObjectReference handleClass = default;
				try {
					handleClass = JniEnvironment.Types.GetObjectClass (new JniObjectReference (handle));
					return JniEnvironment.Types.IsAssignableFrom (handleClass, typeClass);
				} finally {
					JniObjectReference.Dispose (ref handleClass);
					JniObjectReference.Dispose (ref typeClass);
				}
			}

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
		/// Tries to create an instance of the specified type using AOT-safe factory method.
		/// Uses the generated proxy's CreateInstance method to avoid reflection.
		/// </summary>
		/// <param name="type">The target type to create (e.g., HelloWorld.MainActivity)</param>
		/// <param name="proxyType">The proxy type from TypeMap lookup, or null to look it up via attributes</param>
		/// <returns>true if the instance was created successfully; false if no proxy or factory was found.</returns>
		bool TryCreateInstance (Type type, Type? proxyType, IntPtr handle, JniHandleOwnership transfer, [NotNullWhen (true)] out IJavaPeerable? result)
		{
			JavaPeerProxy? proxy = null;

			// If we have a proxy type from the TypeMap, instantiate it
			if (proxyType != null && typeof (JavaPeerProxy).IsAssignableFrom (proxyType)) {
				try {
					proxy = (JavaPeerProxy?) Activator.CreateInstance (proxyType);
					Log ($"TryCreateInstance: Instantiated proxy {proxyType.FullName} for {type.FullName}");
				} catch (Exception ex) {
					Log ($"TryCreateInstance: Failed to instantiate proxy {proxyType.FullName}: {ex.Message}");
				}
			}

			// Fallback: try to get proxy from type's attributes
			if (proxy == null) {
				proxy = GetProxyForType (type);
			}

			if (proxy == null) {
				Log ($"TryCreateInstance: No JavaPeerProxy found for {type.FullName}");
				result = null;
				return false;
			}

			// Use the generated factory method - no reflection needed!
			result = proxy.CreateInstance (handle, transfer);
			return result != null;
		}

		/// <inheritdoc/>
		public unsafe IntPtr GetFunctionPointer (ReadOnlySpan<char> className, int methodIndex)
		{
			// Note: No try-catch here. If this crashes, the app should crash.
			// It's a critical infrastructure failure.

			// Convert to string for dictionary lookup
			// TODO: Use Dictionary.GetAlternateLookup<ReadOnlySpan<char>>() to avoid this allocation
			string classNameStr = className.ToString();
			Log ($"GetFunctionPointer: class='{classNameStr}', methodIndex={methodIndex}");

			// Special case: TypeManager.n_Activate is called by framework JCWs in mono.android.jar
			// These JCWs don't have generated proxies, so we handle them directly here.
			// methodIndex 0 = n_Activate
			if (classNameStr == "mono/android/TypeManager" && methodIndex == 0) {
				IntPtr activatePtr = Java.Interop.TypeManager.GetActivateFunctionPointer ();
				Log ($"GetFunctionPointer: Special case TypeManager.n_Activate -> 0x{activatePtr:x}");
				return activatePtr;
			}

			// Look up type directly from the external type map
			// The typemap now returns the proxy type directly (not the original type)
			if (!_externalTypeMap.TryGetValue (classNameStr, out Type? type)) {
				Log ($"GetFunctionPointer: No type found for '{classNameStr}'");
				return IntPtr.Zero;
			}

			Log ($"GetFunctionPointer: Found type {type.FullName}");

			// Get or create a JavaPeerProxy instance
			// The type from typemap is now the proxy type itself, so we instantiate it directly
			JavaPeerProxy? proxy = GetProxyForType (type);
			if (proxy == null) {
				Log ($"GetFunctionPointer: Failed to get proxy for {type.FullName}");
				return IntPtr.Zero;
			}

			// Get the function pointer from the proxy
			IntPtr fnPtr = proxy.GetFunctionPointer (methodIndex);
			Log ($"GetFunctionPointer: Got function pointer 0x{fnPtr:x} for method index {methodIndex}");

			return fnPtr;
		}
	}
}
