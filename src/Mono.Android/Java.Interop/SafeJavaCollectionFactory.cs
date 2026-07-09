#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;

using Android.Runtime;

namespace Java.Interop {
	static class SafeJavaCollectionFactory
	{
		const DynamicallyAccessedMemberTypes Constructors =
			DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors;

		// NativeAOT's MakeGenericType() path eventually calls
		// ExecutionEnvironment.TryGetConstructedGenericTypeForComponents(), then TypeBuilder.TryBuildGenericType().
		// The builder looks for a template by canonical form: reference type arguments canonicalize to __Canon,
		// while value-type arguments stay value-specific. Consequently, JavaList<string> can share the
		// JavaList<__Canon> template, but JavaList<int> and JavaList<int?> need exact rooted instantiations.
		//
		// These factories intentionally root the exact primitive/nullable Java collection instantiations through
		// direct generic type references and constructors instead of asking MakeGenericType() to invent them.
		static readonly Dictionary<Type, CollectionArgumentFactory> ValueTypeArgumentFactories = new Dictionary<Type, CollectionArgumentFactory> {
			{ typeof (bool),    CollectionArgumentFactory<bool>.Instance },
			{ typeof (sbyte),   CollectionArgumentFactory<sbyte>.Instance },
			{ typeof (char),    CollectionArgumentFactory<char>.Instance },
			{ typeof (short),   CollectionArgumentFactory<short>.Instance },
			{ typeof (int),     CollectionArgumentFactory<int>.Instance },
			{ typeof (long),    CollectionArgumentFactory<long>.Instance },
			{ typeof (float),   CollectionArgumentFactory<float>.Instance },
			{ typeof (double),  CollectionArgumentFactory<double>.Instance },
			{ typeof (bool?),   CollectionArgumentFactory<bool?>.Instance },
			{ typeof (sbyte?),  CollectionArgumentFactory<sbyte?>.Instance },
			{ typeof (char?),   CollectionArgumentFactory<char?>.Instance },
			{ typeof (short?),  CollectionArgumentFactory<short?>.Instance },
			{ typeof (int?),    CollectionArgumentFactory<int?>.Instance },
			{ typeof (long?),   CollectionArgumentFactory<long?>.Instance },
			{ typeof (float?),  CollectionArgumentFactory<float?>.Instance },
			{ typeof (double?), CollectionArgumentFactory<double?>.Instance },
		};

		internal static bool TryGetCollectionType (Type targetType, [NotNullWhen (true)] out Type? collectionType)
		{
			if (targetType == null)
				throw new ArgumentNullException (nameof (targetType));

			if (!TryGetCollectionShape (targetType, out var shape)) {
				collectionType = null;
				return false;
			}

			collectionType = shape.Kind switch {
				JavaCollectionKind.List => GetClosedCollectionType (typeof (JavaList<>), shape.Arguments),
				JavaCollectionKind.Collection => GetClosedCollectionType (typeof (JavaCollection<>), shape.Arguments),
				JavaCollectionKind.Dictionary => GetClosedCollectionType (typeof (JavaDictionary<,>), shape.Arguments),
				_ => null,
			};
			return collectionType != null;
		}

		internal static bool TryCreateFromJniHandle (
			Type targetType,
			IntPtr handle,
			JniHandleOwnership transfer,
			out object? collection)
		{
			if (targetType == null)
				throw new ArgumentNullException (nameof (targetType));

			if (handle == IntPtr.Zero) {
				collection = null;
				return true;
			}

			if (!TryGetCollectionShape (targetType, out var shape)) {
				collection = null;
				return false;
			}

			if (TryCreateFromMappedValueTypeFactories (shape, handle, transfer, out collection)) {
				return true;
			}

			if (!TryGetCollectionType (targetType, out var collectionType)) {
				collection = null;
				return false;
			}

			collection = CreateInstance (collectionType, handle, transfer);
			return true;
		}

		internal static bool TryCreateInstance (Type targetType, object? items, out IJavaObject? collection)
		{
			if (targetType == null)
				throw new ArgumentNullException (nameof (targetType));

			if (items == null) {
				collection = null;
				return true;
			}

			if (!TryGetCollectionType (targetType, out var collectionType)) {
				collection = null;
				return false;
			}

			var instance = CreateInstance (collectionType, items);
			if (instance is not IJavaObject javaObject) {
				throw new InvalidOperationException ($"The collection type '{collectionType}' did not create an IJavaObject instance.");
			}

			collection = javaObject;
			return true;
		}

		static bool TryGetCollectionShape (Type targetType, out CollectionShape shape)
		{
			if (!targetType.IsGenericType || targetType.IsGenericTypeDefinition) {
				shape = default;
				return false;
			}

			var genericDefinition = targetType.GetGenericTypeDefinition ();
			if (genericDefinition == typeof (IList<>) || genericDefinition == typeof (JavaList<>)) {
				shape = new CollectionShape (JavaCollectionKind.List, targetType.GetGenericArguments ());
				return true;
			}

			if (genericDefinition == typeof (ICollection<>) || genericDefinition == typeof (JavaCollection<>)) {
				shape = new CollectionShape (JavaCollectionKind.Collection, targetType.GetGenericArguments ());
				return true;
			}

			if (genericDefinition == typeof (IDictionary<,>) || genericDefinition == typeof (JavaDictionary<,>)) {
				shape = new CollectionShape (JavaCollectionKind.Dictionary, targetType.GetGenericArguments ());
				return true;
			}

			shape = default;
			return false;
		}

		static bool TryCreateFromMappedValueTypeFactories (
			CollectionShape shape,
			IntPtr handle,
			JniHandleOwnership transfer,
			[NotNullWhen (true)] out object? collection)
		{
			if (shape.Kind == JavaCollectionKind.Dictionary) {
				var hasKeyFactory = TryGetValueTypeFactory (shape.Arguments [0], out var keyFactory);
				var hasValueFactory = TryGetValueTypeFactory (shape.Arguments [1], out var valueFactory);

				if (hasKeyFactory && hasValueFactory) {
					collection = keyFactory.CreateDictionary (valueFactory, handle, transfer);
					return true;
				}

				// Mixed dictionaries are safe only when the other side is a reference type. If it is an
				// unsupported value type, MakeGenericType() would need an exact unrooted instantiation.
				if (hasKeyFactory && !shape.Arguments [1].IsValueType) {
					collection = keyFactory.CreateDictionaryWithReferenceValue (shape.Arguments [1], handle, transfer);
					return true;
				}

				if (hasValueFactory && !shape.Arguments [0].IsValueType) {
					collection = valueFactory.CreateDictionaryWithReferenceKey (shape.Arguments [0], handle, transfer);
					return true;
				}

				collection = null;
				return false;
			}

			if (TryGetValueTypeFactory (shape.Arguments [0], out var factory)) {
				collection = shape.Kind == JavaCollectionKind.List
					? factory.CreateList (handle, transfer)
					: factory.CreateCollection (handle, transfer);
				return true;
			}

			collection = null;
			return false;
		}

		static bool TryGetValueTypeFactory (Type type, [NotNullWhen (true)] out CollectionArgumentFactory? factory)
		{
			if (type.IsValueType) {
				return ValueTypeArgumentFactories.TryGetValue (type, out factory);
			}

			factory = null;
			return false;
		}

		[return: DynamicallyAccessedMembers (Constructors)]
		static Type? GetClosedCollectionType (
			[DynamicallyAccessedMembers (Constructors)]
			Type genericTypeDefinition,
			Type[] arguments)
		{
			foreach (var argument in arguments) {
				if (argument.IsValueType && !ValueTypeArgumentFactories.ContainsKey (argument)) {
					return null;
				}
			}

			return MakeGenericType (genericTypeDefinition, arguments);
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
		static Type MakeGenericType (
			[DynamicallyAccessedMembers (Constructors)]
			Type genericTypeDefinition,
			Type[] arguments)
		{
			return genericTypeDefinition.MakeGenericType (arguments);
		}

		[UnconditionalSuppressMessage ("AOT", "IL3050:RequiresDynamicCode",
			Justification = "Activator.CreateInstance() targets only collection wrapper types produced by SafeJavaCollectionFactory. " +
				"Reference-only wrappers use NativeAOT's canonical generic construction, exact value-type wrappers are rooted by CollectionArgumentFactory<T>, " +
				"and mixed dictionaries root their reference/value canonical shapes with dedicated type tokens.")]
		[UnconditionalSuppressMessage ("Trimming", "IL2072:UnrecognizedReflectionPattern",
			Justification = "The collection type is annotated with DynamicallyAccessedMembers(Constructors) by GetClosedCollectionType/MakeGenericType. " +
				"Only the known JavaList<T>, JavaCollection<T>, and JavaDictionary<TKey,TValue> constructors are invoked here.")]
		static object CreateInstance ([DynamicallyAccessedMembers (Constructors)] Type collectionType, params object?[] arguments)
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

		readonly struct CollectionShape
		{
			public CollectionShape (JavaCollectionKind kind, Type[] arguments)
			{
				Kind = kind;
				Arguments = arguments;
			}

			public JavaCollectionKind Kind { get; }

			public Type[] Arguments { get; }
		}

		enum JavaCollectionKind {
			List,
			Collection,
			Dictionary,
		}

		abstract class CollectionArgumentFactory
		{
			internal abstract IList CreateList (IntPtr handle, JniHandleOwnership transfer);

			internal abstract ICollection CreateCollection (IntPtr handle, JniHandleOwnership transfer);

			internal abstract IDictionary CreateDictionary (CollectionArgumentFactory valueFactory, IntPtr handle, JniHandleOwnership transfer);

			internal abstract IDictionary CreateDictionaryWithReferenceKey (Type keyType, IntPtr handle, JniHandleOwnership transfer);

			internal abstract IDictionary CreateDictionaryWithReferenceValue (Type valueType, IntPtr handle, JniHandleOwnership transfer);

			internal abstract IDictionary CreateDictionaryWithKey<[DynamicallyAccessedMembers (Constructors)] TKey> (
				CollectionArgumentFactory<TKey> keyFactory,
				IntPtr handle,
				JniHandleOwnership transfer);
		}

		sealed class CollectionArgumentFactory<[DynamicallyAccessedMembers (Constructors)] T> : CollectionArgumentFactory
		{
			internal static readonly CollectionArgumentFactory<T> Instance = new CollectionArgumentFactory<T> ();

			// These tokens root the mixed reference/value dictionary canonical shapes. For example,
			// JavaDictionary<string,int> canonicalizes like JavaDictionary<__Canon,int>, so
			// JavaDictionary<object,T> supplies the NativeAOT template when T is a mapped value type.
			static readonly Type ReferenceKeyDictionaryType = typeof (JavaDictionary<object, T>);
			static readonly Type ReferenceValueDictionaryType = typeof (JavaDictionary<T, object>);

			CollectionArgumentFactory ()
			{
			}

			internal override IList CreateList (IntPtr handle, JniHandleOwnership transfer)
			{
				return new JavaList<T> (handle, transfer);
			}

			internal override ICollection CreateCollection (IntPtr handle, JniHandleOwnership transfer)
			{
				return new JavaCollection<T> (handle, transfer);
			}

			internal override IDictionary CreateDictionary (CollectionArgumentFactory valueFactory, IntPtr handle, JniHandleOwnership transfer)
			{
				return valueFactory.CreateDictionaryWithKey (this, handle, transfer);
			}

			internal override IDictionary CreateDictionaryWithReferenceKey (Type keyType, IntPtr handle, JniHandleOwnership transfer)
			{
				_ = ReferenceKeyDictionaryType;
				var dictionaryType = MakeGenericType (typeof (JavaDictionary<,>), [keyType, typeof (T)]);
				return (IDictionary) CreateInstance (dictionaryType, handle, transfer);
			}

			internal override IDictionary CreateDictionaryWithReferenceValue (Type valueType, IntPtr handle, JniHandleOwnership transfer)
			{
				_ = ReferenceValueDictionaryType;
				var dictionaryType = MakeGenericType (typeof (JavaDictionary<,>), [typeof (T), valueType]);
				return (IDictionary) CreateInstance (dictionaryType, handle, transfer);
			}

			internal override IDictionary CreateDictionaryWithKey<[DynamicallyAccessedMembers (Constructors)] TKey> (
				CollectionArgumentFactory<TKey> keyFactory,
				IntPtr handle,
				JniHandleOwnership transfer)
			{
				return new JavaDictionary<TKey, T> (handle, transfer);
			}
		}
	}
}
