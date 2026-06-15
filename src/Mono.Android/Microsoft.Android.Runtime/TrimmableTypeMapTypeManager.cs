#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Java.Interop;

namespace Microsoft.Android.Runtime;

/// <summary>
/// Type manager for the trimmable typemap path. Delegates type lookups
/// to <see cref="TrimmableTypeMap"/>.
/// </summary>
class TrimmableTypeMapTypeManager : JniRuntime.JniTypeManager
{
	const string NoSimpleReference = "\0";
	readonly ConcurrentDictionary<Type, string> _simpleReferenceCache = new ();

	protected override IEnumerable<Type> GetTypesForSimpleReference (string jniSimpleReference)
	{
		foreach (var t in base.GetTypesForSimpleReference (jniSimpleReference)) {
			yield return t;
		}

		if (TrimmableTypeMap.Instance.TryGetTargetTypes (jniSimpleReference, out var types)) {
			foreach (var type in types) {
				yield return type;
			}
		}
	}

	protected override string? GetSimpleReference (Type type)
	{
		var simpleReference = _simpleReferenceCache.GetOrAdd (type, GetSimpleReferenceUncached);
		return simpleReference == NoSimpleReference ? null : simpleReference;
	}

	string GetSimpleReferenceUncached (Type type)
	{
		if (TrimmableTypeMap.Instance.TryGetJniNameForManagedType (type, out var jniName)) {
			return jniName;
		}

		foreach (var r in base.GetSimpleReferences (type)) {
			return r;
		}

		// Walk the base type chain for managed-only subclasses (e.g., JavaProxyThrowable
		// extends Java.Lang.Error but has no [Register] attribute itself).
		for (var baseType = type.BaseType; baseType is not null; baseType = baseType.BaseType) {
			if (TrimmableTypeMap.Instance.TryGetJniNameForManagedType (baseType, out var baseJniName)) {
				return baseJniName;
			}
		}

		return NoSimpleReference;
	}

	protected override IEnumerable<string> GetSimpleReferences (Type type)
	{
		if (TrimmableTypeMap.Instance.TryGetJniNameForManagedType (type, out var jniName)) {
			yield return jniName;
			yield break;
		}

		foreach (var r in base.GetSimpleReferences (type)) {
			yield return r;
		}

		// Walk the base type chain for managed-only subclasses (e.g., JavaProxyThrowable
		// extends Java.Lang.Error but has no [Register] attribute itself).
		for (var baseType = type.BaseType; baseType is not null; baseType = baseType.BaseType) {
			if (TrimmableTypeMap.Instance.TryGetJniNameForManagedType (baseType, out var baseJniName)) {
				yield return baseJniName;
				yield break;
			}
		}
	}

	[return: DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
	protected override Type? GetInvokerTypeCore (
			[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
			Type type)
	{
		var invokerType = TrimmableTypeMap.Instance.GetInvokerType (type);
		if (invokerType != null) {
			return invokerType;
		}

		return base.GetInvokerTypeCore (type);
	}

	protected override IReadOnlyList<string>? GetStaticMethodFallbackTypesCore (string jniSimpleReference)
	{
		return JniRemappingLookup.GetStaticMethodFallbackTypes (jniSimpleReference, useReplacementTypes: true);
	}

	protected override string? GetReplacementTypeCore (string jniSimpleReference)
	{
		return JniRemappingLookup.GetReplacementType (jniSimpleReference);
	}

	protected override JniRuntime.ReplacementMethodInfo? GetReplacementMethodInfoCore (string jniSourceType, string jniMethodName, string jniMethodSignature)
	{
		return JniRemappingLookup.GetReplacementMethodInfo (jniSourceType, jniMethodName, jniMethodSignature);
	}

	public override void RegisterNativeMembers (
		JniType nativeClass,
		[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods | DynamicallyAccessedMemberTypes.NonPublicNestedTypes)]
		Type type,
		ReadOnlySpan<char> methods)
	{
		// In the trimmable type map, native methods are registered by Java Callable Wrapper static
		// initializers via the fast path (mono.android.Runtime.registerNatives). The string-based
		// entry points that reach this overload (a JCW calling mono.android.Runtime.register(...),
		// or Java.Interop.ManagedPeer) are disabled by default and only honored when
		// RuntimeFeature.StringBasedJniRegistration is enabled.
		if (RuntimeFeature.StringBasedJniRegistration) {
			if (!NativeMethodRegistration.TryRegisterNativeMembers (nativeClass, type, methods)) {
				throw new InvalidOperationException ($"Unable to register native methods for '{type.FullName}'.");
			}
		} else {
			throw new NotSupportedException (
				$"""
				Java called back to register native methods for '{type.FullName}' using the string-based JNI registration path, which is disabled for the trimmable type map.

				This is either:
				- A bug in .NET for Android - the trimmable type map should have registered these natives via 'mono.android.Runtime.registerNatives'. Please report it at https://github.com/dotnet/android/issues, quoting the type name above.
				- Caused by an outdated/precompiled Java library whose Java Callable Wrappers call 'mono.android.Runtime.register(...)'. To keep using it, re-enable string-based JNI registration by adding this to your .csproj:
						<PropertyGroup>
						<_AndroidEnableStringBasedJniRegistration>true</_AndroidEnableStringBasedJniRegistration>
						</PropertyGroup>
					Please also report the library at https://github.com/dotnet/android/issues so we can investigate further.
				""");
		}
	}
}
