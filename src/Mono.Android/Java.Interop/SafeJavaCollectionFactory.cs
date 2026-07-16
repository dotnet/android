#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
/// The per-container factories therefore split construction into explicit, non-overlapping paths:
/// </para>
/// <list type="bullet">
/// <item><description>reference element arguments ride the <c>__Canon</c> template via
/// <see cref="SafeContainerTypeFactory.MakeGenericType{T}(Type[])"/> plus
/// <see cref="SafeContainerTypeFactory.CreateInstance(Type, object?[])"/>;</description></item>
/// <item><description>mapped primitive/nullable arguments go through <see cref="ValueTypeFactory"/>,
/// which roots the exact instantiation with a direct <c>new</c>;</description></item>
/// <item><description>any other value type throws <see cref="NotSupportedException"/> (no reflection
/// fallback is AOT-safe).</description></item>
/// </list>
/// </remarks>
static class SafeJavaCollectionFactory
{
	internal const DynamicallyAccessedMemberTypes Constructors =
		DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors;

	static readonly SafeContainerTypeFactory ListFactory = new JavaListTypeFactory ();
	static readonly SafeContainerTypeFactory CollectionFactory = new JavaCollectionTypeFactory ();
	static readonly SafeContainerTypeFactory DictionaryFactory = new JavaDictionaryTypeFactory ();

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
		}

		converter = null;
		return false;
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
}

/// <summary>
/// Base class for the per-container factories. One factory owns a single Java collection container
/// so that the reference-vs-value construction paths stay explicit and statically analyzable
/// (correctness over reuse). The reflection-based construction helpers live here (rather than being
/// shared through <see cref="SafeJavaCollectionFactory"/>) so their trimming/AOT suppressions are
/// scoped to the factories that actually build the wrapper types.
/// </summary>
abstract class SafeContainerTypeFactory
{
	/// <summary>
	/// Constructs the managed wrapper for a Java collection handle.
	/// </summary>
	/// <returns>
	/// <see langword="false"/> when <paramref name="genericDefinition"/> is not this factory's container,
	/// so callers can chain the factories with <c>||</c>. <see langword="true"/> (and <paramref name="result"/>
	/// set) once the matching container is constructed; an unsupported (non-primitive) value-type argument
	/// throws instead of falling through.
	/// </returns>
	public abstract bool TryCreateFromJniHandle (
		Type genericDefinition,
		Type[] arguments,
		IntPtr handle,
		JniHandleOwnership transfer,
		out object? result);

	/// <summary>
	/// Closes the generic definition of the reference-type collection wrapper <typeparamref name="T"/>
	/// over <paramref name="arguments"/>.
	/// </summary>
	/// <typeparam name="T">
	/// A concrete reference-argument instantiation of the wrapper (for example
	/// <c>JavaList&lt;IJavaPeerable&gt;</c>). Its <see cref="DynamicallyAccessedMembersAttribute"/> roots the
	/// wrapper's constructors on the <c>__Canon</c> template, which every reference-argument instantiation
	/// (such as <c>JavaList&lt;string&gt;</c>) shares. This makes the AOT safety of the construction obvious:
	/// the exact template the runtime resolves for <paramref name="arguments"/> is the one already rooted here.
	/// </typeparam>
	/// <param name="arguments">The reference-type generic arguments to close the definition over.</param>
	[UnconditionalSuppressMessage ("AOT", "IL3050:RequiresDynamicCode",
		Justification = "NativeAOT's Type.MakeGenericType() is annotated because arbitrary constructed generics can lack a runtime template. " +
			"This helper only closes the generic definition of the reference-argument wrapper T (e.g. JavaList<IJavaPeerable>) over reference-type arguments, " +
			"which all canonicalize to the same __Canon template that T already roots. Value-type arguments never reach this helper; " +
			"they are rejected or built by ValueTypeFactory, which roots the exact instantiation.")]
	[UnconditionalSuppressMessage ("Trimming", "IL2055:MakeGenericType",
		Justification = "IL2055 is raised because the runtime arguments cannot be statically proven to satisfy the DynamicallyAccessedMembers(Constructors) " +
			"requirement that the wrapper (e.g. JavaList<[DAM(Constructors)] TElement>) places on its element type parameter. That requirement exists for the " +
			"dynamic-code path, where the wrapper reflectively activates element peers from their constructors. On the trimmable typemap path taken here the wrapper " +
			"never activates its elements: element peer creation goes through JavaConvert and the typemap's registered activation constructors, so the unmet element " +
			"requirement is never exercised. The wrapper's own constructor is separately rooted by the DynamicallyAccessedMembers(Constructors) annotation on T.")]
	protected static Type MakeGenericType<[DynamicallyAccessedMembers (SafeJavaCollectionFactory.Constructors)] T> (Type[] arguments)
	{
		Debug.Assert (typeof (T).IsGenericType && typeof (T) != typeof (T).GetGenericTypeDefinition ());
		foreach (var argument in arguments) {
			Debug.Assert (!argument.IsValueType);
		}

		return typeof (T).GetGenericTypeDefinition ().MakeGenericType (arguments);
	}

	/// <summary>
	/// Activates <paramref name="collectionType"/> using its <c>(IntPtr, JniHandleOwnership)</c> constructor.
	/// </summary>
	/// <remarks>
	/// <paramref name="collectionType"/> is intentionally not annotated with
	/// <see cref="DynamicallyAccessedMembersAttribute"/>: the only supported callers construct it with
	/// <see cref="MakeGenericType{T}(Type[])"/>, whose <c>T</c> already roots the wrapper constructors on the
	/// <c>__Canon</c> template. The suppressions below capture that guarantee rather than a static annotation.
	/// </remarks>
	[UnconditionalSuppressMessage ("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Activator.CreateInstance() targets only collection wrapper types produced by the per-container factories. " +
			"Reference-argument wrappers use NativeAOT's canonical generic construction rooted by MakeGenericType<T>, " +
			"and exact value-type wrappers are rooted by ValueTypeFactory<T>.")]
	[UnconditionalSuppressMessage ("Trimming", "IL2067:UnrecognizedReflectionPattern",
		Justification = "collectionType is a JavaList<T>, JavaCollection<T>, or JavaDictionary<TKey,TValue> produced by MakeGenericType<T>, " +
			"whose T (a reference-argument wrapper instantiation) roots the wrapper constructors on the __Canon template. " +
			"Only that activation constructor is invoked here.")]
	protected static object CreateInstance (Type collectionType, params object?[] arguments)
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

sealed class JavaListTypeFactory : SafeContainerTypeFactory
{
	public override bool TryCreateFromJniHandle (Type genericDefinition, Type[] arguments, IntPtr handle, JniHandleOwnership transfer, out object? result)
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

		result = CreateInstance (MakeGenericType<JavaList<IJavaPeerable>> ([elementType]), handle, transfer);
		return true;
	}
}

sealed class JavaCollectionTypeFactory : SafeContainerTypeFactory
{
	public override bool TryCreateFromJniHandle (Type genericDefinition, Type[] arguments, IntPtr handle, JniHandleOwnership transfer, out object? result)
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

		result = CreateInstance (MakeGenericType<JavaCollection<IJavaPeerable>> ([elementType]), handle, transfer);
		return true;
	}
}

sealed class JavaDictionaryTypeFactory : SafeContainerTypeFactory
{
	public override bool TryCreateFromJniHandle (Type genericDefinition, Type[] arguments, IntPtr handle, JniHandleOwnership transfer, out object? result)
	{
		if (genericDefinition != typeof (IDictionary<,>) && genericDefinition != typeof (JavaDictionary<,>)) {
			result = null;
			return false;
		}

		var keyType = arguments [0];
		var valueType = arguments [1];

		// A value-type argument is only AOT-safe when it is a mapped primitive/nullable; anything else
		// (a custom struct) has no rooted instantiation and must not fall back to reflection. Validate both
		// arguments up front so the construction paths below only deal with reference or mapped-primitive types.
		EnsureReferenceOrPrimitive (keyType);
		EnsureReferenceOrPrimitive (valueType);

		if (TryGetPrimitiveValueTypeFactory (keyType, out var keyFactory)) {
			// The key is a mapped primitive. A value/value dictionary uses the full rooted cross-product;
			// a value/reference dictionary roots JavaDictionary<value,__Canon> via the value factory's token.
			result = TryGetPrimitiveValueTypeFactory (valueType, out var valueFactory)
				? keyFactory.CreateDictionary (valueFactory, handle, transfer)
				: keyFactory.CreateDictionaryWithReferenceValue (valueType, handle, transfer);
			return true;
		}

		if (TryGetPrimitiveValueTypeFactory (valueType, out var referenceKeyValueFactory)) {
			// The key is a reference type and the value is a mapped primitive: root JavaDictionary<__Canon,value>.
			result = referenceKeyValueFactory.CreateDictionaryWithReferenceKey (keyType, handle, transfer);
			return true;
		}

		// Both arguments are reference types: JavaDictionary<TKey,TValue> rides the __Canon template.
		result = CreateInstance (MakeGenericType<JavaDictionary<IJavaPeerable, IJavaPeerable>> ([keyType, valueType]), handle, transfer);
		return true;
	}

	static void EnsureReferenceOrPrimitive (Type argument)
	{
		if (argument.IsValueType && !ValueTypeFactory.PrimitiveTypeFactories.ContainsKey (argument)) {
			throw new NotSupportedException ($"'JavaDictionary' with value-type argument '{argument}' is not available on the trimmable typemap: only reference and mapped primitive arguments are supported.");
		}
	}

	static bool TryGetPrimitiveValueTypeFactory (Type argument, [NotNullWhen (true)] out ValueTypeFactory? factory)
		=> ValueTypeFactory.PrimitiveTypeFactories.TryGetValue (argument, out factory);
}
