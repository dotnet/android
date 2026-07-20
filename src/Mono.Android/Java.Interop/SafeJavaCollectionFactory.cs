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
/// <item><description>other value types are not handled here, preserving <see cref="JavaConvert"/>'s
/// existing untyped collection fallback.</description></item>
/// </list>
/// </remarks>
static class SafeJavaCollectionFactory
{
	internal const DynamicallyAccessedMemberTypes Constructors =
		DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors;

	/// <summary>Binding flags used to find the activation constructor of the Java collection wrappers.</summary>
	const BindingFlags ActivationConstructorBinding = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

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
				if (!AreSupportedCollectionArguments (arguments)) {
					converter = null;
					return false;
				}
				converter = (handle, transfer) => CreateFromJniHandle (genericDefinition, arguments, handle, transfer);
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

	static bool AreSupportedCollectionArguments (Type[] arguments)
	{
		foreach (var argument in arguments) {
			if (argument.IsValueType && !ValueTypeFactory.PrimitiveTypeFactories.ContainsKey (argument)) {
				return false;
			}
		}

		return true;
	}

	static object? CreateFromJniHandle (Type genericDefinition, Type[] arguments, IntPtr handle, JniHandleOwnership transfer)
	{
		if (handle == IntPtr.Zero) {
			return null;
		}

		object? result;
		if (TryCreateListFromJniHandle (genericDefinition, arguments, handle, transfer, out result)
			|| TryCreateCollectionFromJniHandle (genericDefinition, arguments, handle, transfer, out result)
			|| TryCreateDictionaryFromJniHandle (genericDefinition, arguments, handle, transfer, out result)) {
			return result;
		}

		throw new NotSupportedException ($"Unsupported Java container type with generic definition '{genericDefinition}'.");
	}

	[UnconditionalSuppressMessage ("AOT", "IL3050:RequiresDynamicCode",
		Justification = "MakeGenericType () and Activator.CreateInstance () are annotated because arbitrary constructed generics can lack a runtime template. " +
			"elementType is always a reference type here (value types are diverted to ValueTypeFactory above), so JavaList<elementType> canonicalizes to the " +
			"JavaList<__Canon> template whose activation constructor is rooted by the reflective JavaList<IJavaPeerable> anchor in the other branch.")]
	[UnconditionalSuppressMessage ("Trimming", "IL2055:MakeGenericType",
		Justification = "IL2055 fires because MakeGenericType () cannot statically prove the runtime elementType satisfies the DynamicallyAccessedMembers(Constructors) " +
			"requirement that JavaList<[DAM(Constructors)] TElement> places on its element parameter. That requirement exists only for the dynamic-code path, where the wrapper " +
			"reflectively activates element peers from their constructors. On the trimmable typemap path the wrapper never activates its elements — element peer creation goes " +
			"through JavaConvert and the typemap's registered activation constructors — so the unsatisfied element requirement is never exercised.")]
	[UnconditionalSuppressMessage ("Trimming", "IL2072:UnrecognizedReflectionPattern",
		Justification = "The dynamically constructed JavaList<elementType> rides the JavaList<IJavaPeerable> canonical template whose activation constructor is rooted by the " +
			"concrete-literal branch. Only the known JavaList<T> activation constructor is invoked here.")]
	static bool TryCreateListFromJniHandle (Type genericDefinition, Type[] arguments, IntPtr handle, JniHandleOwnership transfer, [NotNullWhen (true)] out object? result)
	{
		if (genericDefinition != typeof (IList<>) && genericDefinition != typeof (JavaList<>)) {
			result = null;
			return false;
		}

		var elementType = arguments [0];
		if (elementType.IsValueType) {
			var valueFactory = ValueTypeFactory.PrimitiveTypeFactories [elementType];
			result = valueFactory.CreateList (handle, transfer);
			return true;
		}

		if (elementType == typeof (IJavaPeerable)) {
			// Concrete-literal rooting branch. Taken only when marshaling the JavaList<IJavaPeerable> shape itself
			// (uncommon), its main purpose is to keep the reflection metadata + invoke stub of the (IntPtr,
			// JniHandleOwnership) constructor on the JavaList<__Canon> template alive for the trimmer/ILC. The else
			// branch reuses that same canonical constructor for every other JavaList<referenceType>.
			result = Activator.CreateInstance (typeof (JavaList<IJavaPeerable>), ActivationConstructorBinding, binder: null, args: [handle, transfer], culture: CultureInfo.InvariantCulture);
		} else {
			var listType = typeof (JavaList<>).MakeGenericType (elementType);
			result = Activator.CreateInstance (listType, ActivationConstructorBinding, binder: null, args: [handle, transfer], culture: CultureInfo.InvariantCulture);
		}
		if (result == null) {
			throw new InvalidOperationException ($"Unable to create a JavaList instance for element type '{elementType}'.");
		}
		return true;
	}

	[UnconditionalSuppressMessage ("AOT", "IL3050:RequiresDynamicCode",
		Justification = "MakeGenericType () and Activator.CreateInstance () are annotated because arbitrary constructed generics can lack a runtime template. " +
			"elementType is always a reference type here (value types are diverted to ValueTypeFactory above), so JavaCollection<elementType> canonicalizes to the " +
			"JavaCollection<__Canon> template whose activation constructor is rooted by the reflective JavaCollection<IJavaPeerable> anchor in the other branch.")]
	[UnconditionalSuppressMessage ("Trimming", "IL2055:MakeGenericType",
		Justification = "IL2055 fires because MakeGenericType () cannot statically prove the runtime elementType satisfies the DynamicallyAccessedMembers(Constructors) " +
			"requirement that JavaCollection<[DAM(Constructors)] TElement> places on its element parameter. That requirement exists only for the dynamic-code path, where the " +
			"wrapper reflectively activates element peers from their constructors. On the trimmable typemap path the wrapper never activates its elements — element peer creation " +
			"goes through JavaConvert and the typemap's registered activation constructors — so the unsatisfied element requirement is never exercised.")]
	[UnconditionalSuppressMessage ("Trimming", "IL2072:UnrecognizedReflectionPattern",
		Justification = "The dynamically constructed JavaCollection<elementType> rides the JavaCollection<IJavaPeerable> canonical template whose activation constructor is rooted " +
			"by the concrete-literal branch. Only the known JavaCollection<T> activation constructor is invoked here.")]
	static bool TryCreateCollectionFromJniHandle (Type genericDefinition, Type[] arguments, IntPtr handle, JniHandleOwnership transfer, [NotNullWhen (true)] out object? result)
	{
		if (genericDefinition != typeof (ICollection<>) && genericDefinition != typeof (JavaCollection<>)) {
			result = null;
			return false;
		}

		var elementType = arguments [0];
		if (elementType.IsValueType) {
			var valueFactory = ValueTypeFactory.PrimitiveTypeFactories [elementType];
			result = valueFactory.CreateCollection (handle, transfer);
			return true;
		}

		if (elementType == typeof (IJavaPeerable)) {
			// Concrete-literal rooting branch. Taken only when marshaling the JavaCollection<IJavaPeerable> shape
			// itself (uncommon), its main purpose is to keep the reflection metadata + invoke stub of the (IntPtr,
			// JniHandleOwnership) constructor on the JavaCollection<__Canon> template alive for the trimmer/ILC. The
			// else branch reuses that same canonical constructor for every other JavaCollection<referenceType>.
			result = Activator.CreateInstance (typeof (JavaCollection<IJavaPeerable>), ActivationConstructorBinding, binder: null, args: [handle, transfer], culture: CultureInfo.InvariantCulture);
		} else {
			var collectionType = typeof (JavaCollection<>).MakeGenericType (elementType);
			result = Activator.CreateInstance (collectionType, ActivationConstructorBinding, binder: null, args: [handle, transfer], culture: CultureInfo.InvariantCulture);
		}
		if (result == null) {
			throw new InvalidOperationException ($"Unable to create a JavaCollection instance for element type '{elementType}'.");
		}
		return true;
	}

	[UnconditionalSuppressMessage ("AOT", "IL3050:RequiresDynamicCode",
		Justification = "MakeGenericType () and Activator.CreateInstance () are annotated because arbitrary constructed generics can lack a runtime template. " +
			"The reflective branch is reached only when both arguments are reference types, so JavaDictionary<keyType,valueType> canonicalizes to the " +
			"JavaDictionary<__Canon,__Canon> template whose activation constructor is rooted by the reflective JavaDictionary<IJavaPeerable,IJavaPeerable> anchor in the other branch.")]
	[UnconditionalSuppressMessage ("Trimming", "IL2055:MakeGenericType",
		Justification = "IL2055 fires because MakeGenericType () cannot statically prove the runtime keyType/valueType satisfy the DynamicallyAccessedMembers(Constructors) " +
			"requirement that JavaDictionary<[DAM(Constructors)] TKey, [DAM(Constructors)] TValue> places on its element parameters. That requirement exists only for the " +
			"dynamic-code path, where the wrapper reflectively activates key/value peers from their constructors. On the trimmable typemap path the wrapper never activates its " +
			"elements — element peer creation goes through JavaConvert and the typemap's registered activation constructors — so the unsatisfied element requirement is never exercised.")]
	[UnconditionalSuppressMessage ("Trimming", "IL2072:UnrecognizedReflectionPattern",
		Justification = "The dynamically constructed JavaDictionary<keyType,valueType> rides the JavaDictionary<IJavaPeerable,IJavaPeerable> canonical template whose activation " +
			"constructor is rooted by the concrete-literal branch. Only the known JavaDictionary<TKey,TValue> activation constructor is invoked here.")]
	static bool TryCreateDictionaryFromJniHandle (Type genericDefinition, Type[] arguments, IntPtr handle, JniHandleOwnership transfer, [NotNullWhen (true)] out object? result)
	{
		if (genericDefinition != typeof (IDictionary<,>) && genericDefinition != typeof (JavaDictionary<,>)) {
			result = null;
			return false;
		}

		var keyType = arguments [0];
		var valueType = arguments [1];

		if (keyType.IsValueType) {
			var keyFactory = ValueTypeFactory.PrimitiveTypeFactories [keyType];
			// The key is a primitive/nullable value type. A value/value dictionary uses the full rooted cross-product;
			// a value/reference dictionary roots JavaDictionary<value,__Canon> via the value factory's token.
			result = valueType.IsValueType
				? keyFactory.CreateDictionary (ValueTypeFactory.PrimitiveTypeFactories [valueType], handle, transfer)
				: keyFactory.CreateDictionaryWithReferenceValue (valueType, handle, transfer);
			return true;
		}

		if (valueType.IsValueType) {
			// The key is a reference type and the value is primitive/nullable: root JavaDictionary<__Canon,value>.
			result = ValueTypeFactory.PrimitiveTypeFactories [valueType].CreateDictionaryWithReferenceKey (keyType, handle, transfer);
			return true;
		}

		// Both arguments are reference types: JavaDictionary<TKey,TValue> rides the __Canon template.
		if (keyType == typeof (IJavaPeerable) && valueType == typeof (IJavaPeerable)) {
			// Concrete-literal rooting branch. Taken only when marshaling the JavaDictionary<IJavaPeerable,
			// IJavaPeerable> shape itself (uncommon), its main purpose is to keep the reflection metadata + invoke stub
			// of the (IntPtr, JniHandleOwnership) constructor on the JavaDictionary<__Canon,__Canon> template alive for
			// the trimmer/ILC. The else branch reuses that same canonical constructor for every other reference pair.
			result = Activator.CreateInstance (typeof (JavaDictionary<IJavaPeerable, IJavaPeerable>), ActivationConstructorBinding, binder: null, args: [handle, transfer], culture: CultureInfo.InvariantCulture);
		} else {
			var dictionaryType = typeof (JavaDictionary<,>).MakeGenericType (keyType, valueType);
			result = Activator.CreateInstance (dictionaryType, ActivationConstructorBinding, binder: null, args: [handle, transfer], culture: CultureInfo.InvariantCulture);
		}
		if (result == null) {
			throw new InvalidOperationException ($"Unable to create a JavaDictionary instance for key type '{keyType}' and value type '{valueType}'.");
		}
		return true;
	}
}
