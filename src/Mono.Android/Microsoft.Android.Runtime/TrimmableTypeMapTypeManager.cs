#nullable enable

using System;
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
class TrimmableTypeMapTypeManager : JniRuntime.JniTypeManager
{
	protected override IEnumerable<Type> GetTypesForSimpleReference (string jniSimpleReference)
	{
		foreach (var t in base.GetTypesForSimpleReference (jniSimpleReference)) {
			yield return t;
		}

		if (TrimmableTypeMap.Instance.TryGetType (jniSimpleReference, out var type)) {
			yield return type;
		}
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
