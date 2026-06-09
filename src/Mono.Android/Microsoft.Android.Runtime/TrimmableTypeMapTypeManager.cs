#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Java.Interop;

namespace Microsoft.Android.Runtime;

/// <summary>
/// Type manager for the trimmable typemap path. Delegates type lookups
/// to <see cref="TrimmableTypeMap"/>.
/// </summary>
class TrimmableTypeMapTypeManager : JniRuntime.ReflectionJniTypeManager
{
	const string NoSimpleReference = "\0";
	readonly ConcurrentDictionary<Type, string> _simpleReferenceCache = new ();

	[UnconditionalSuppressMessage ("Trimming", "IL2026", Justification = "This manager reuses Java.Interop built-in type mappings while trimmable typemap lookups avoid reflection fallback.")]
	[UnconditionalSuppressMessage ("AOT", "IL3050", Justification = "This manager reuses Java.Interop built-in type mappings while trimmable typemap lookups avoid reflection fallback.")]
	public TrimmableTypeMapTypeManager ()
	{
	}

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
		throw new UnreachableException (
			$"RegisterNativeMembers should not be called in the trimmable typemap path. " +
			$"Native methods for '{type.FullName}' should be registered by JCW static initializer blocks.");
	}
}
