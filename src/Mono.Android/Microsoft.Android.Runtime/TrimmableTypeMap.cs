#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
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

	internal bool TryGetType (string jniSimpleReference, out Type type)
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

	/// <summary>
	/// Gets the invoker type for an interface or abstract class from the proxy attribute.
	/// </summary>
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
}
