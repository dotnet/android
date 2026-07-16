#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;

using Android.Runtime;

namespace Java.Interop;

// Produces the Java-handle-to-managed converters used by the trimmable typemap when the marshaling
// target is a Java collection wrapper (JavaList/JavaCollection/JavaDictionary or the IList/ICollection/
// IDictionary interfaces they implement).
//
// NativeAOT's MakeGenericType() path eventually calls
// ExecutionEnvironment.TryGetConstructedGenericTypeForComponents(), then TypeBuilder.TryBuildGenericType().
// The builder looks for a template by canonical form: reference type arguments canonicalize to __Canon,
// while value-type arguments stay value-specific. Consequently, JavaList<string> can share the
// JavaList<__Canon> template, but JavaList<int> and JavaList<int?> need exact rooted instantiations.
//
// The per-container factories below therefore split construction into explicit, non-overlapping paths:
//   * reference element arguments  -> typeof (JavaList<>).MakeGenericType (...) + Activator, riding the __Canon template;
//   * mapped primitive/nullable    -> ValueTypeFactory, which roots the exact instantiation with a direct `new`;
//   * any other value type         -> NotSupportedException (no reflection fallback is AOT-safe).
static class SafeContainerConverterFactory
{
	internal const DynamicallyAccessedMemberTypes Constructors =
		DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors;

	static readonly ISafeContainerTypeFactory ListFactory = new JavaListTypeFactory ();
	static readonly ISafeContainerTypeFactory CollectionFactory = new JavaCollectionTypeFactory ();
	static readonly ISafeContainerTypeFactory DictionaryFactory = new JavaDictionaryTypeFactory ();

	internal static bool TryCreateConverter (
		Type targetType,
		[NotNullWhen (true)] out Func<IntPtr, JniHandleOwnership, object?>? converter)
	{
		ArgumentNullException.ThrowIfNull (targetType);

		if (targetType.IsGenericType && !targetType.IsGenericTypeDefinition) {
			var genericDefinition = targetType.GetGenericTypeDefinition ();
			if (IsKnownContainerDefinition (genericDefinition)) {
				// Capture the parsed arguments so GetGenericArguments () runs once when the converter is
				// selected, rather than again for every conversion.
				var arguments = targetType.GetGenericArguments ();
				converter = (handle, transfer) => CreateFromJniHandle (genericDefinition, arguments, handle, transfer);
				return true;
			}

			converter = null;
			return false;
		}

		// Non-generic wrappers and raw collection interfaces construct their peer directly (no reflection)
		// through the existing FromJniHandle helpers, which also reuse an already-registered managed peer.
		return TryCreateNonGenericConverter (targetType, out converter);
	}

	static bool IsKnownContainerDefinition (Type genericDefinition)
		=> genericDefinition == typeof (IList<>) || genericDefinition == typeof (JavaList<>)
			|| genericDefinition == typeof (ICollection<>) || genericDefinition == typeof (JavaCollection<>)
			|| genericDefinition == typeof (IDictionary<,>) || genericDefinition == typeof (JavaDictionary<,>);

	static object? CreateFromJniHandle (Type genericDefinition, Type[] arguments, IntPtr handle, JniHandleOwnership transfer)
	{
		if (handle == IntPtr.Zero) {
			return null;
		}

		object? result;
		if (ListFactory.TryCreateFromJniHandle (genericDefinition, arguments, handle, transfer, out result)
			|| CollectionFactory.TryCreateFromJniHandle (genericDefinition, arguments, handle, transfer, out result)
			|| DictionaryFactory.TryCreateFromJniHandle (genericDefinition, arguments, handle, transfer, out result)) {
			return result;
		}

		throw new NotSupportedException ($"Unsupported Java container type with generic definition '{genericDefinition}'.");
	}

	static bool TryCreateNonGenericConverter (
		Type targetType,
		[NotNullWhen (true)] out Func<IntPtr, JniHandleOwnership, object?>? converter)
	{
		// Order mirrors the historical JavaConvert fallback: dictionary, then list, then collection.
		if (typeof (IDictionary).IsAssignableFrom (targetType)) {
			converter = static (handle, transfer) => JavaDictionary.FromJniHandle (handle, transfer);
			return true;
		}

		if (typeof (IList).IsAssignableFrom (targetType)) {
			converter = static (handle, transfer) => JavaList.FromJniHandle (handle, transfer);
			return true;
		}

		if (typeof (ICollection).IsAssignableFrom (targetType)) {
			converter = static (handle, transfer) => JavaCollection.FromJniHandle (handle, transfer);
			return true;
		}

		converter = null;
		return false;
	}

	[UnconditionalSuppressMessage ("AOT", "IL3050:RequiresDynamicCode",
		Justification = "NativeAOT's Type.MakeGenericType() is annotated because arbitrary constructed generics can lack a runtime template. " +
			"Callers of this helper restrict the shape to Android.Runtime Java collection wrappers. Reference arguments use NativeAOT's __Canon generic templates. " +
			"Value-type arguments are either rejected or handled by explicit primitive/nullable factories that root the exact instantiation. " +
			"Mixed reference/value dictionaries additionally root JavaDictionary<__Canon,T> or JavaDictionary<T,__Canon> through dedicated type tokens.")]
	[UnconditionalSuppressMessage ("Trimming", "IL2055:MakeGenericType",
		Justification = "The generic type definitions are known Java collection wrappers, not arbitrary user types. " +
			"The constructed wrapper constructors are preserved by the return annotation and by the explicit value-type factory references. " +
			"The generic element arguments are not activated by this helper; element peer creation still goes through the normal JavaConvert/trimmable typemap path.")]
	[return: DynamicallyAccessedMembers (Constructors)]
	internal static Type MakeGenericType (
		[DynamicallyAccessedMembers (Constructors)]
		Type genericTypeDefinition,
		Type[] arguments)
	{
		return genericTypeDefinition.MakeGenericType (arguments);
	}

	[UnconditionalSuppressMessage ("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Activator.CreateInstance() targets only collection wrapper types produced by SafeContainerConverterFactory. " +
			"Reference-only wrappers use NativeAOT's canonical generic construction, exact value-type wrappers are rooted by ValueTypeFactory<T>, " +
			"and mixed dictionaries root their reference/value canonical shapes with dedicated type tokens.")]
	[UnconditionalSuppressMessage ("Trimming", "IL2072:UnrecognizedReflectionPattern",
		Justification = "The collection type is annotated with DynamicallyAccessedMembers(Constructors) by the per-container factories and MakeGenericType. " +
			"Only the known JavaList<T>, JavaCollection<T>, and JavaDictionary<TKey,TValue> constructors are invoked here.")]
	internal static object CreateInstance ([DynamicallyAccessedMembers (Constructors)] Type collectionType, params object?[] arguments)
	{
		var instance = Activator.CreateInstance (
			collectionType,
			BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
			binder: null,
			args: arguments,
			culture: CultureInfo.InvariantCulture);
		if (instance == null) {
			throw new InvalidOperationException ($"Unable to create an instance of collection type '{collectionType}'.");
		}
		return instance;
	}
}

// One factory per Java collection container. Each factory owns a single container type so that the
// reference-vs-value construction paths stay explicit and statically analyzable (correctness over reuse).
interface ISafeContainerTypeFactory
{
	// Returns false when genericDefinition is not this factory's container, so callers can chain the
	// factories with `||`. Returns true (and sets result) once the matching container is constructed;
	// an unsupported (non-primitive) value-type argument throws instead of falling through.
	bool TryCreateFromJniHandle (
		Type genericDefinition,
		Type[] arguments,
		IntPtr handle,
		JniHandleOwnership transfer,
		out object? result);
}

sealed class JavaListTypeFactory : ISafeContainerTypeFactory
{
	public bool TryCreateFromJniHandle (Type genericDefinition, Type[] arguments, IntPtr handle, JniHandleOwnership transfer, out object? result)
	{
		if (genericDefinition != typeof (IList<>) && genericDefinition != typeof (JavaList<>)) {
			result = null;
			return false;
		}

		var elementType = arguments [0];
		if (elementType.IsValueType) {
			if (!ValueTypeFactory.PrimitiveTypeFactories.TryGetValue (elementType, out var valueFactory)) {
				throw new NotSupportedException ($"'JavaList<{elementType}>' is not available on the trimmable typemap: only reference and mapped primitive element types are supported.");
			}
			result = valueFactory.CreateList (handle, transfer);
			return true;
		}

		result = SafeContainerConverterFactory.CreateInstance (
			SafeContainerConverterFactory.MakeGenericType (typeof (JavaList<>), [elementType]),
			handle, transfer);
		return true;
	}
}

sealed class JavaCollectionTypeFactory : ISafeContainerTypeFactory
{
	public bool TryCreateFromJniHandle (Type genericDefinition, Type[] arguments, IntPtr handle, JniHandleOwnership transfer, out object? result)
	{
		if (genericDefinition != typeof (ICollection<>) && genericDefinition != typeof (JavaCollection<>)) {
			result = null;
			return false;
		}

		var elementType = arguments [0];
		if (elementType.IsValueType) {
			if (!ValueTypeFactory.PrimitiveTypeFactories.TryGetValue (elementType, out var valueFactory)) {
				throw new NotSupportedException ($"'JavaCollection<{elementType}>' is not available on the trimmable typemap: only reference and mapped primitive element types are supported.");
			}
			result = valueFactory.CreateCollection (handle, transfer);
			return true;
		}

		result = SafeContainerConverterFactory.CreateInstance (
			SafeContainerConverterFactory.MakeGenericType (typeof (JavaCollection<>), [elementType]),
			handle, transfer);
		return true;
	}
}

sealed class JavaDictionaryTypeFactory : ISafeContainerTypeFactory
{
	public bool TryCreateFromJniHandle (Type genericDefinition, Type[] arguments, IntPtr handle, JniHandleOwnership transfer, out object? result)
	{
		if (genericDefinition != typeof (IDictionary<,>) && genericDefinition != typeof (JavaDictionary<,>)) {
			result = null;
			return false;
		}

		var keyType = arguments [0];
		var valueType = arguments [1];

		// A value-type argument is only AOT-safe when it is a mapped primitive/nullable; anything else
		// (a custom struct) has no rooted instantiation and must not fall back to reflection.
		var keyFactory = GetValueTypeFactoryOrThrow (keyType);
		var valueFactory = GetValueTypeFactoryOrThrow (valueType);

		if (keyFactory != null && valueFactory != null) {
			result = keyFactory.CreateDictionary (valueFactory, handle, transfer);
			return true;
		}

		// Mixed value/reference dictionaries root JavaDictionary<value,__Canon> / JavaDictionary<__Canon,value>
		// through the value factory's dedicated type tokens.
		if (keyFactory != null) {
			result = keyFactory.CreateDictionaryWithReferenceValue (valueType, handle, transfer);
			return true;
		}

		if (valueFactory != null) {
			result = valueFactory.CreateDictionaryWithReferenceKey (keyType, handle, transfer);
			return true;
		}

		// Both arguments are reference types: JavaDictionary<TKey,TValue> rides the __Canon template.
		result = SafeContainerConverterFactory.CreateInstance (
			SafeContainerConverterFactory.MakeGenericType (typeof (JavaDictionary<,>), [keyType, valueType]),
			handle, transfer);
		return true;
	}

	static ValueTypeFactory? GetValueTypeFactoryOrThrow (Type argument)
	{
		if (!argument.IsValueType) {
			return null;
		}

		if (!ValueTypeFactory.PrimitiveTypeFactories.TryGetValue (argument, out var factory)) {
			throw new NotSupportedException ($"'JavaDictionary' with value-type argument '{argument}' is not available on the trimmable typemap: only reference and mapped primitive arguments are supported.");
		}

		return factory;
	}
}
