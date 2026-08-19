#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;

using Android.Runtime;

namespace Java.Interop;

/// <summary>
/// Produces the Java-handle-to-managed converters used by the trimmable typemap when the marshaling
/// target is a Java collection wrapper (<see cref="JavaList{T}"/>, <see cref="JavaCollection{T}"/>,
/// <see cref="JavaDictionary{K,V}"/>, or the <see cref="IList{T}"/>/<see cref="ICollection{T}"/>/
/// <see cref="IDictionary{TKey,TValue}"/> interfaces they implement).
/// </summary>
/// <remarks>
/// NativeAOT's <see cref="Type.MakeGenericType(Type[])"/> path eventually calls
/// <c>ExecutionEnvironment.TryGetConstructedGenericTypeForComponents()</c>, then
/// <c>TypeBuilder.TryBuildGenericType()</c>. The builder looks for a template by canonical form:
/// reference type arguments canonicalize to <c>__Canon</c>, while value-type arguments stay
/// value-specific. Consequently, <c>JavaList&lt;string&gt;</c> can share the <c>JavaList&lt;__Canon&gt;</c>
/// template, but <c>JavaList&lt;int&gt;</c> and <c>JavaList&lt;int?&gt;</c> need exact rooted instantiations.
/// <para>
/// The per-container construction therefore splits into explicit, non-overlapping paths:
/// </para>
/// <list type="bullet">
/// <item><description>reference element arguments ride the <c>__Canon</c> template: the wrapper definition is
/// reflectively closed and its activation constructor invoked, kept alive by a concrete-literal
/// <c>IJavaPeerable</c> rooting branch in the same method;</description></item>
/// <item><description>primitive/nullable value-type arguments go through <see cref="ValueTypeFactory"/>,
/// which roots the exact instantiation with a direct <c>new</c>;</description></item>
/// <item><description>other value types use the corresponding untyped collection wrapper because
/// their exact generic instantiations are not rooted.</description></item>
/// </list>
/// </remarks>
static class SafeJavaCollectionFactory
{
	internal const DynamicallyAccessedMemberTypes Constructors =
		DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors;

	/// <summary>Binding flags used to find the activation constructor of the Java collection wrappers.</summary>
	internal const BindingFlags ActivationConstructorBinding = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

	internal static bool TryGetFromJniHandleConverter (
		Type targetType,
		[NotNullWhen (true)] out Func<IntPtr, JniHandleOwnership, object?>? converter)
	{
		ArgumentNullException.ThrowIfNull (targetType);

		// Reject open and partially-open constructed types (e.g. IList<>, IDictionary<T,int>). These have
		// ContainsGenericParameters == true and would produce an open wrapper from MakeGenericType (), whose
		// activation would throw ArgumentException. Fail cleanly here rather than crash during construction.
		if (targetType.ContainsGenericParameters) {
			converter = null;
			return false;
		}

		if (targetType.IsGenericType && !targetType.IsGenericTypeDefinition) {
			var genericDefinition = targetType.GetGenericTypeDefinition ();
			if (IsKnownContainerDefinition (genericDefinition)) {
				// Capture the parsed arguments so GetGenericArguments () runs once when the converter is
				// selected, rather than again for every conversion.
				var arguments = targetType.GetGenericArguments ();
				if (genericDefinition == typeof (IList<>) || genericDefinition == typeof (JavaList<>)) {
					var elementType = arguments [0];
					if (elementType.IsValueType) {
						if (!ValueTypeFactory.PrimitiveTypeFactories.TryGetValue (elementType, out var listFactory)) {
							converter = GetUntypedFromJniHandleConverter (genericDefinition);
							return true;
						}
						converter = (handle, transfer) => handle == IntPtr.Zero ? null : listFactory.CreateList (handle, transfer);
						return true;
					}
					converter = (handle, transfer) => CreateReferenceListFromJniHandle (elementType, handle, transfer);
					return true;
				}

				if (genericDefinition == typeof (ICollection<>) || genericDefinition == typeof (JavaCollection<>)) {
					var elementType = arguments [0];
					if (elementType.IsValueType) {
						if (!ValueTypeFactory.PrimitiveTypeFactories.TryGetValue (elementType, out var collectionFactory)) {
							converter = GetUntypedFromJniHandleConverter (genericDefinition);
							return true;
						}
						converter = (handle, transfer) => handle == IntPtr.Zero ? null : collectionFactory.CreateCollection (handle, transfer);
						return true;
					}
					converter = (handle, transfer) => CreateReferenceCollectionFromJniHandle (elementType, handle, transfer);
					return true;
				}

				var keyType = arguments [0];
				var valueType = arguments [1];
				ValueTypeFactory? keyFactory = null;
				ValueTypeFactory? valueFactory = null;
				if ((keyType.IsValueType && !ValueTypeFactory.PrimitiveTypeFactories.TryGetValue (keyType, out keyFactory))
						|| (valueType.IsValueType && !ValueTypeFactory.PrimitiveTypeFactories.TryGetValue (valueType, out valueFactory))) {
					converter = GetUntypedFromJniHandleConverter (genericDefinition);
					return true;
				}
				if (keyFactory != null) {
					converter = valueFactory != null
						? (handle, transfer) => handle == IntPtr.Zero ? null : keyFactory.CreateDictionary (valueFactory, handle, transfer)
						: (handle, transfer) => handle == IntPtr.Zero ? null : keyFactory.CreateDictionaryWithReferenceValue (valueType, handle, transfer);
					return true;
				}
				if (valueFactory != null) {
					converter = (handle, transfer) => handle == IntPtr.Zero ? null : valueFactory.CreateDictionaryWithReferenceKey (keyType, handle, transfer);
					return true;
				}
				converter = (handle, transfer) => CreateReferenceDictionaryFromJniHandle (keyType, valueType, handle, transfer);
				return true;
			}
		}

		converter = null;
		return false;
	}

	static bool IsKnownContainerDefinition (Type genericDefinition)
		=> genericDefinition == typeof (IList<>) || genericDefinition == typeof (JavaList<>)
			|| genericDefinition == typeof (ICollection<>) || genericDefinition == typeof (JavaCollection<>)
			|| genericDefinition == typeof (IDictionary<,>) || genericDefinition == typeof (JavaDictionary<,>);

	static Func<IntPtr, JniHandleOwnership, object?> GetUntypedFromJniHandleConverter (Type genericDefinition)
	{
		if (genericDefinition == typeof (IList<>) || genericDefinition == typeof (JavaList<>))
			return (handle, transfer) => JavaList.FromJniHandle (handle, transfer);
		if (genericDefinition == typeof (ICollection<>) || genericDefinition == typeof (JavaCollection<>))
			return (handle, transfer) => JavaCollection.FromJniHandle (handle, transfer);
		return (handle, transfer) => JavaDictionary.FromJniHandle (handle, transfer);
	}

	[UnconditionalSuppressMessage ("AOT", "IL3050:RequiresDynamicCode",
		Justification = "MakeGenericType () and Activator.CreateInstance () are annotated because arbitrary constructed generics can lack a runtime template. " +
			"elementType is always a reference type here (value types are diverted to ValueTypeFactory above), so JavaList<elementType> canonicalizes to the " +
			"JavaList<__Canon> template whose activation constructor is rooted by the direct JavaList<IJavaPeerable> construction in the other branch.")]
	[UnconditionalSuppressMessage ("Trimming", "IL2071:MakeGenericType",
		Justification = "IL2071 fires because MakeGenericType () cannot statically prove the runtime elementType satisfies the DynamicallyAccessedMembers(Constructors) " +
			"requirement that JavaList<[DAM(Constructors)] TElement> places on its element parameter. That requirement exists only for the dynamic-code path, where the wrapper " +
			"reflectively activates element peers from their constructors. On the trimmable typemap path the wrapper never activates its elements — element peer creation goes " +
			"through JavaConvert and the typemap's registered activation constructors — so the unsatisfied element requirement is never exercised.")]
	[UnconditionalSuppressMessage ("Trimming", "IL2072:UnrecognizedReflectionPattern",
		Justification = "The dynamically constructed JavaList<elementType> rides the JavaList<IJavaPeerable> canonical template whose activation constructor is rooted by the " +
			"concrete-literal branch. Only the known JavaList<T> activation constructor is invoked here.")]
	static object? CreateReferenceListFromJniHandle (Type elementType, IntPtr handle, JniHandleOwnership transfer)
	{
		if (handle == IntPtr.Zero) {
			return null;
		}

		if (elementType == typeof (IJavaPeerable)) {
			// Concrete-literal rooting branch. Taken only when marshaling the JavaList<IJavaPeerable> shape itself
			// (uncommon), its main purpose is to statically root the (IntPtr, JniHandleOwnership) constructor on the
			// JavaList<__Canon> template for the trimmer/ILC. The else branch reuses that same canonical constructor
			// for every other JavaList<referenceType>.
			return new JavaList<IJavaPeerable> (handle, transfer);
		}

		var listType = typeof (JavaList<>).MakeGenericType (elementType);
		var result = Activator.CreateInstance (listType, ActivationConstructorBinding, binder: null, args: [handle, transfer], culture: CultureInfo.InvariantCulture);
		if (result == null) {
			throw new InvalidOperationException ($"Unable to create a JavaList instance for element type '{elementType}'.");
		}
		return result;
	}

	[UnconditionalSuppressMessage ("AOT", "IL3050:RequiresDynamicCode",
		Justification = "MakeGenericType () and Activator.CreateInstance () are annotated because arbitrary constructed generics can lack a runtime template. " +
			"elementType is always a reference type here (value types are diverted to ValueTypeFactory above), so JavaCollection<elementType> canonicalizes to the " +
			"JavaCollection<__Canon> template whose activation constructor is rooted by the direct JavaCollection<IJavaPeerable> construction in the other branch.")]
	[UnconditionalSuppressMessage ("Trimming", "IL2071:MakeGenericType",
		Justification = "IL2071 fires because MakeGenericType () cannot statically prove the runtime elementType satisfies the DynamicallyAccessedMembers(Constructors) " +
			"requirement that JavaCollection<[DAM(Constructors)] TElement> places on its element parameter. That requirement exists only for the dynamic-code path, where the " +
			"wrapper reflectively activates element peers from their constructors. On the trimmable typemap path the wrapper never activates its elements — element peer creation " +
			"goes through JavaConvert and the typemap's registered activation constructors — so the unsatisfied element requirement is never exercised.")]
	[UnconditionalSuppressMessage ("Trimming", "IL2072:UnrecognizedReflectionPattern",
		Justification = "The dynamically constructed JavaCollection<elementType> rides the JavaCollection<IJavaPeerable> canonical template whose activation constructor is rooted " +
			"by the concrete-literal branch. Only the known JavaCollection<T> activation constructor is invoked here.")]
	static object? CreateReferenceCollectionFromJniHandle (Type elementType, IntPtr handle, JniHandleOwnership transfer)
	{
		if (handle == IntPtr.Zero) {
			return null;
		}

		if (elementType == typeof (IJavaPeerable)) {
			// Concrete-literal rooting branch. Taken only when marshaling the JavaCollection<IJavaPeerable> shape
			// itself (uncommon), its main purpose is to statically root the (IntPtr, JniHandleOwnership) constructor
			// on the JavaCollection<__Canon> template for the trimmer/ILC. The else branch reuses that same canonical
			// constructor for every other JavaCollection<referenceType>.
			return new JavaCollection<IJavaPeerable> (handle, transfer);
		}

		var collectionType = typeof (JavaCollection<>).MakeGenericType (elementType);
		var result = Activator.CreateInstance (collectionType, ActivationConstructorBinding, binder: null, args: [handle, transfer], culture: CultureInfo.InvariantCulture);
		if (result == null) {
			throw new InvalidOperationException ($"Unable to create a JavaCollection instance for element type '{elementType}'.");
		}
		return result;
	}

	[UnconditionalSuppressMessage ("AOT", "IL3050:RequiresDynamicCode",
		Justification = "MakeGenericType () and Activator.CreateInstance () are annotated because arbitrary constructed generics can lack a runtime template. " +
			"The reflective branch is reached only when both arguments are reference types, so JavaDictionary<keyType,valueType> canonicalizes to the " +
			"JavaDictionary<__Canon,__Canon> template whose activation constructor is rooted by the direct JavaDictionary<IJavaPeerable,IJavaPeerable> construction in the other branch.")]
	[UnconditionalSuppressMessage ("Trimming", "IL2071:MakeGenericType",
		Justification = "IL2071 fires because MakeGenericType () cannot statically prove the runtime keyType/valueType satisfy the DynamicallyAccessedMembers(Constructors) " +
			"requirement that JavaDictionary<[DAM(Constructors)] TKey, [DAM(Constructors)] TValue> places on its element parameters. That requirement exists only for the " +
			"dynamic-code path, where the wrapper reflectively activates key/value peers from their constructors. On the trimmable typemap path the wrapper never activates its " +
			"elements — element peer creation goes through JavaConvert and the typemap's registered activation constructors — so the unsatisfied element requirement is never exercised.")]
	[UnconditionalSuppressMessage ("Trimming", "IL2072:UnrecognizedReflectionPattern",
		Justification = "The dynamically constructed JavaDictionary<keyType,valueType> rides the JavaDictionary<IJavaPeerable,IJavaPeerable> canonical template whose activation " +
			"constructor is rooted by the concrete-literal branch. Only the known JavaDictionary<TKey,TValue> activation constructor is invoked here.")]
	static object? CreateReferenceDictionaryFromJniHandle (Type keyType, Type valueType, IntPtr handle, JniHandleOwnership transfer)
	{
		if (handle == IntPtr.Zero) {
			return null;
		}

		// Both arguments are reference types: JavaDictionary<TKey,TValue> rides the __Canon template.
		if (keyType == typeof (IJavaPeerable) && valueType == typeof (IJavaPeerable)) {
			// Concrete-literal rooting branch. Taken only when marshaling the JavaDictionary<IJavaPeerable,
			// IJavaPeerable> shape itself (uncommon), its main purpose is to statically root the (IntPtr,
			// JniHandleOwnership) constructor on the JavaDictionary<__Canon,__Canon> template for the trimmer/ILC.
			// The else branch reuses that same canonical constructor for every other reference pair.
			return new JavaDictionary<IJavaPeerable, IJavaPeerable> (handle, transfer);
		}

		var dictionaryType = typeof (JavaDictionary<,>).MakeGenericType (keyType, valueType);
		var result = Activator.CreateInstance (dictionaryType, ActivationConstructorBinding, binder: null, args: [handle, transfer], culture: CultureInfo.InvariantCulture);
		if (result == null) {
			throw new InvalidOperationException ($"Unable to create a JavaDictionary instance for key type '{keyType}' and value type '{valueType}'.");
		}
		return result;
	}
}
