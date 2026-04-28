#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Android.Runtime;
using Java.Interop;

namespace Microsoft.Android.Runtime;

/// <summary>
/// AOT-safe activation for closed generic container types.
/// Uses <see cref="System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject"/> to
/// allocate the closed type and reflection to call the activation constructor. This is safe
/// because the target type carries <c>[DynamicallyAccessedMembers(Constructors)]</c> which
/// guarantees constructor metadata is preserved by the trimmer.
/// </summary>
static class KnownContainerActivator
{
	const DynamicallyAccessedMemberTypes Constructors = DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors;
	const BindingFlags CtorBindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
	static readonly Type[] ActivationCtorSignature = new Type [] { typeof (IntPtr), typeof (JniHandleOwnership) };

	/// <summary>
	/// Tries to create an instance of a known closed generic container type
	/// (e.g. <c>JavaList&lt;int&gt;</c>) from a JNI handle. Returns null if
	/// <paramref name="closedType"/> is not a recognized container type.
	/// </summary>
	internal static IJavaPeerable? TryCreateKnownContainerType (
			[DynamicallyAccessedMembers (Constructors)]
			Type closedType,
			IntPtr handle,
			JniHandleOwnership transfer)
	{
		if (!closedType.IsGenericType || closedType.IsGenericTypeDefinition) {
			return null;
		}

		var genericDef = closedType.GetGenericTypeDefinition ();

		if (genericDef != typeof (global::Android.Runtime.JavaList<>)
			&& genericDef != typeof (global::Android.Runtime.JavaCollection<>)
			&& genericDef != typeof (global::Android.Runtime.JavaDictionary<,>)
			&& genericDef != typeof (global::Android.Runtime.JavaSet<>)
			&& genericDef != typeof (global::Android.Runtime.JavaArray<>)) {
			return null;
		}

		var ctor = closedType.GetConstructor (CtorBindingFlags, null, ActivationCtorSignature, null);
		if (ctor is null) {
			return null;
		}

		return (IJavaPeerable) ctor.Invoke (new object [] { handle, transfer });
	}
}
