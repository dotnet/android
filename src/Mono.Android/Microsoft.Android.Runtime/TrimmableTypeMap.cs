#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using Android.Runtime;
using Java.Interop;

namespace Microsoft.Android.Runtime;

/// <summary>
/// Central type map for the trimmable typemap path. Owns the TypeMapping dictionary
/// and provides peer creation, invoker resolution, container factories, and native
/// method registration. All proxy attribute access is encapsulated here.
/// </summary>
class TrimmableTypeMap
{
	static TrimmableTypeMap? s_instance;

	internal static TrimmableTypeMap? Instance => s_instance;

	readonly IReadOnlyDictionary<string, Type> _typeMap;

	internal TrimmableTypeMap ()
	{
		_typeMap = TypeMapping.GetOrCreateExternalTypeMapping<Java.Lang.Object> ();

		var previous = Interlocked.CompareExchange (ref s_instance, this, null);
		Debug.Assert (previous is null, "TrimmableTypeMap must only be created once.");
	}

	internal bool TryGetType (string jniSimpleReference, [NotNullWhen (true)] out Type? type)
		=> _typeMap.TryGetValue (jniSimpleReference, out type);

	/// <summary>
	/// Creates a peer instance using the proxy's CreateInstance method.
	/// </summary>
	internal bool TryCreatePeer (Type type, IntPtr handle, JniHandleOwnership transfer)
	{
		var proxy = type.GetCustomAttribute<JavaPeerProxy> (inherit: false);
		if (proxy is null) {
			return false;
		}

		return proxy.CreateInstance (handle, transfer) != null;
	}

	const DynamicallyAccessedMemberTypes Constructors = DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors;

	/// <summary>
	/// Gets the invoker type for an interface or abstract class from the proxy attribute.
	/// </summary>
	[return: DynamicallyAccessedMembers (Constructors)]
	internal Type? GetInvokerType (Type type)
	{
		var proxy = type.GetCustomAttribute<JavaPeerProxy> (inherit: false);
		return proxy?.InvokerType;
	}

	/// <summary>
	/// Gets the container factory for a type from its proxy attribute.
	/// Used for AOT-safe array/collection/dictionary creation.
	/// </summary>
	internal JavaPeerContainerFactory? GetContainerFactory (Type type)
	{
		var proxy = type.GetCustomAttribute<JavaPeerProxy> (inherit: false);
		return proxy?.GetContainerFactory ();
	}

	/// <summary>
	/// Creates a managed peer instance for a Java object being constructed.
	/// Called from generated UCO constructor wrappers (nctor_*_uco).
	/// </summary>
	internal static void ActivateInstance (IntPtr self, Type targetType)
	{
		var instance = s_instance;
		if (instance is null) {
			throw new InvalidOperationException ("TrimmableTypeMap has not been initialized.");
		}

		if (!instance.TryCreatePeer (targetType, self, JniHandleOwnership.DoNotTransfer)) {
			throw new TypeMapException (
				$"Failed to create peer for type '{targetType.FullName}'. " +
				"Ensure the type has a generated proxy in the TypeMap assembly.");
		}
	}

	// TODO (https://github.com/dotnet/android/issues/10794): The generator currently emits per-method RegisterMethod() calls.
	// This should be changed to emit a single JNI RegisterNatives call with
	// all methods at once, eliminating this helper. Follow-up generator change.

	/// <summary>
	/// Registers a single JNI native method. Called from generated
	/// <see cref="IAndroidCallableWrapper"/> implementations.
	/// </summary>
	public static void RegisterMethod (JniType nativeClass, string name, string signature, IntPtr functionPointer)
	{
		// The java-interop JniNativeMethodRegistration API requires a Delegate, but we have
		// a raw function pointer from an [UnmanagedCallersOnly] method. JNI only uses the
		// function pointer extracted via Marshal.GetFunctionPointerForDelegate(), so the
		// delegate type doesn't matter — Action is used as a lightweight wrapper.
		// TODO (https://github.com/dotnet/java-interop/pull/1391): Use JniNativeMethod overload to avoid delegate allocation.
		var registration = new JniNativeMethodRegistration (name, signature,
			Marshal.GetDelegateForFunctionPointer<Action> (functionPointer));
		JniEnvironment.Types.RegisterNatives (
			nativeClass.PeerReference,
			new [] { registration },
			1);
	}

	static readonly RegisterNativesHandler s_onRegisterNatives = OnRegisterNatives;

	delegate void RegisterNativesHandler (IntPtr jnienv, IntPtr klass, IntPtr nativeClassHandle);

	/// <summary>
	/// Registers the <c>mono.android.Runtime.registerNatives</c> JNI native method.
	/// Must be called after the JNI runtime is initialized and before any JCW class is loaded.
	/// </summary>
	internal void RegisterBootstrapNativeMethod ()
	{
		using var runtimeClass = new JniType ("mono/android/Runtime");
		JniEnvironment.Types.RegisterNatives (
			runtimeClass.PeerReference,
			new [] { new JniNativeMethodRegistration ("registerNatives", "(Ljava/lang/Class;)V",
				s_onRegisterNatives) },
			1);
	}

	static void OnRegisterNatives (IntPtr jnienv, IntPtr klass, IntPtr nativeClassHandle)
	{
		string? className = null;
		try {
			if (s_instance is null) {
				return;
			}

			var classRef = new JniObjectReference (nativeClassHandle);
			className = JniEnvironment.Types.GetJniTypeNameFromInstance (classRef);
			if (className is null) {
				return;
			}

			if (!s_instance._typeMap.TryGetValue (className, out var type)) {
				return;
			}

			var proxy = type.GetCustomAttribute<JavaPeerProxy> (inherit: false);
			if (proxy is IAndroidCallableWrapper acw) {
				using var jniType = new JniType (className);
				acw.RegisterNatives (jniType);
			}
		} catch (Exception ex) {
			Environment.FailFast ($"TrimmableTypeMap: Failed to register natives for class '{className}'.", ex);
		}
	}
}
