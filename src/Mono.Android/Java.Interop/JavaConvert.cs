using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;

using Android.Runtime;
using Microsoft.Android.Runtime;

namespace Java.Interop {

	static class JavaConvert {
		const DynamicallyAccessedMemberTypes Constructors = DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors;
		// Mirrors JniObjectReference.DisposeSource; JniObjectReferenceOptions only exposes it through CopyAndDispose.
		const JniObjectReferenceOptions DisposeSource = (JniObjectReferenceOptions)(1 << 1);

		static Dictionary<Type, Func<IntPtr, JniHandleOwnership, object?>> JniHandleConverters = new Dictionary<Type, Func<IntPtr, JniHandleOwnership, object?>>() {
			{ typeof (bool), (handle, transfer) => {
				using (var value = new Java.Lang.Boolean (handle, transfer | JniHandleOwnership.DoNotRegister))
					return value.BooleanValue ();
			} },
			{ typeof (bool?), (handle, transfer) => {
				if (handle == IntPtr.Zero)
					return null;
				using (var value = new Java.Lang.Boolean (handle, transfer | JniHandleOwnership.DoNotRegister))
					return value.BooleanValue ();
			} },
			{ typeof (byte), (handle, transfer) => {
				using (var value = new Java.Lang.Byte (handle, transfer | JniHandleOwnership.DoNotRegister))
					return (byte) value.ByteValue ();
			} },
			{ typeof (byte?), (handle, transfer) => {
				if (handle == IntPtr.Zero)
					return null;
				using (var value = new Java.Lang.Byte (handle, transfer | JniHandleOwnership.DoNotRegister))
					return (byte) value.ByteValue ();
			} },
			{ typeof (sbyte), (handle, transfer) => {
				using (var value = new Java.Lang.Byte (handle, transfer | JniHandleOwnership.DoNotRegister))
					return value.ByteValue ();
			} },
			{ typeof (sbyte?), (handle, transfer) => {
				if (handle == IntPtr.Zero)
					return null;
				using (var value = new Java.Lang.Byte (handle, transfer | JniHandleOwnership.DoNotRegister))
					return value.ByteValue ();
			} },
			{ typeof (char), (handle, transfer) => {
				using (var value = new Java.Lang.Character (handle, transfer | JniHandleOwnership.DoNotRegister))
					return value.CharValue ();
			} },
			{ typeof (char?), (handle, transfer) => {
				if (handle == IntPtr.Zero)
					return null;
				using (var value = new Java.Lang.Character (handle, transfer | JniHandleOwnership.DoNotRegister))
					return value.CharValue ();
			} },
			{ typeof (short), (handle, transfer) => {
				using (var value = new Java.Lang.Short (handle, transfer | JniHandleOwnership.DoNotRegister))
					return value.ShortValue ();
			} },
			{ typeof (short?), (handle, transfer) => {
				if (handle == IntPtr.Zero)
					return null;
				using (var value = new Java.Lang.Short (handle, transfer | JniHandleOwnership.DoNotRegister))
					return value.ShortValue ();
			} },
			{ typeof (int), (handle, transfer) => {
				using (var value = new Java.Lang.Integer (handle, transfer | JniHandleOwnership.DoNotRegister))
					return value.IntValue ();
			} },
			{ typeof (int?), (handle, transfer) => {
				if (handle == IntPtr.Zero)
					return null;
				using (var value = new Java.Lang.Integer (handle, transfer | JniHandleOwnership.DoNotRegister))
					return value.IntValue ();
			} },
			{ typeof (long), (handle, transfer) => {
				using (var value = new Java.Lang.Long (handle, transfer | JniHandleOwnership.DoNotRegister))
					return value.LongValue ();
			} },
			{ typeof (long?), (handle, transfer) => {
				if (handle == IntPtr.Zero)
					return null;
				using (var value = new Java.Lang.Long (handle, transfer | JniHandleOwnership.DoNotRegister))
					return value.LongValue ();
			} },
			{ typeof (float), (handle, transfer) => {
				using (var value = new Java.Lang.Float (handle, transfer | JniHandleOwnership.DoNotRegister))
					return value.FloatValue ();
			} },
			{ typeof (float?), (handle, transfer) => {
				if (handle == IntPtr.Zero)
					return null;
				using (var value = new Java.Lang.Float (handle, transfer | JniHandleOwnership.DoNotRegister))
					return value.FloatValue ();
			} },
			{ typeof (double), (handle, transfer) => {
				using (var value = new Java.Lang.Double (handle, transfer | JniHandleOwnership.DoNotRegister))
					return value.DoubleValue ();
			} },
			{ typeof (double?), (handle, transfer) => {
				if (handle == IntPtr.Zero)
					return null;
				using (var value = new Java.Lang.Double (handle, transfer | JniHandleOwnership.DoNotRegister))
					return value.DoubleValue ();
			} },
			{ typeof (string), (handle, transfer) => {
				using (var value = new Java.Lang.String (handle, transfer | JniHandleOwnership.DoNotRegister))
					return value.ToString ();
			} },
		};

		static readonly Dictionary<Type, JavaPeerContainerFactory> ScalarContainerFactories = new Dictionary<Type, JavaPeerContainerFactory> {
			{ typeof (bool), new JavaPeerContainerFactory<bool> () },
			{ typeof (byte), new JavaPeerContainerFactory<byte> () },
			{ typeof (sbyte), new JavaPeerContainerFactory<sbyte> () },
			{ typeof (char), new JavaPeerContainerFactory<char> () },
			{ typeof (short), new JavaPeerContainerFactory<short> () },
			{ typeof (int), new JavaPeerContainerFactory<int> () },
			{ typeof (long), new JavaPeerContainerFactory<long> () },
			{ typeof (float), new JavaPeerContainerFactory<float> () },
			{ typeof (double), new JavaPeerContainerFactory<double> () },
			{ typeof (string), new JavaPeerContainerFactory<string> () },
		};

		static Func<IntPtr, JniHandleOwnership, object?>? GetJniHandleConverter (Type? target)
		{
			if (target == null)
				return null;

			if (JniHandleConverters.TryGetValue (target, out var converter))
				return converter;
			if (target.IsArray)
				return (h, t) => JNIEnv.GetArray (h, t, target.GetElementType ());

			if (target.IsGenericType && !target.IsGenericTypeDefinition) {
				if (RuntimeFeature.TrimmableTypeMap) {
					var factoryConverter = TryGetFactoryBasedConverter (target);
					if (factoryConverter != null)
						return factoryConverter;
				} else if (RuntimeFeature.IsMonoRuntime || RuntimeFeature.IsCoreClrRuntime) {
					if (target.GetGenericTypeDefinition() == typeof (IDictionary<,>)) {
						Type t = typeof (JavaDictionary<,>).MakeGenericType (target.GetGenericArguments ());
						return GetJniHandleConverterForType (t);
					}
					if (target.GetGenericTypeDefinition() == typeof (IList<>)) {
						Type t = typeof (JavaList<>).MakeGenericType (target.GetGenericArguments ());
						return GetJniHandleConverterForType (t);
					}
					if (target.GetGenericTypeDefinition() == typeof (ICollection<>)) {
						Type t = typeof (JavaCollection<>).MakeGenericType (target.GetGenericArguments ());
						return GetJniHandleConverterForType (t);
					}
				}
			}

			if (typeof (IDictionary).IsAssignableFrom (target))
				return (h, t) => JavaDictionary.FromJniHandle (h, t);
			if (typeof (IList).IsAssignableFrom (target))
				return (h, t) => JavaList.FromJniHandle (h, t);
			if (typeof (ICollection).IsAssignableFrom (target))
				return (h, t) => JavaCollection.FromJniHandle (h, t);

			return null;
		}

		/// <summary>
		/// AOT-safe converter using <see cref="JavaPeerContainerFactory"/> from the generated proxy.
		/// Avoids <c>MakeGenericType()</c> by using the pre-typed factory from the proxy attribute.
		/// </summary>
		static Func<IntPtr, JniHandleOwnership, object?>? TryGetFactoryBasedConverter (Type target)
		{
			if (TryGetSingleGenericArgument (target, typeof (IList<>), typeof (JavaList<>), out var listElementType)) {
				var factory = TryGetContainerFactory (listElementType);
				if (factory != null)
					return (h, t) => factory.CreateList (h, t);
			}

			if (TryGetSingleGenericArgument (target, typeof (ICollection<>), typeof (JavaCollection<>), out var collectionElementType)) {
				var factory = TryGetContainerFactory (collectionElementType);
				if (factory != null)
					return (h, t) => factory.CreateCollection (h, t);
			}

			if (TryGetDictionaryArguments (target, out var typeArgs)) {
				var keyFactory = TryGetContainerFactory (typeArgs [0]);
				var valueFactory = TryGetContainerFactory (typeArgs [1]);
				if (keyFactory != null && valueFactory != null)
					return (h, t) => valueFactory.CreateDictionary (keyFactory, h, t);
			}

			return null;

			static bool TryGetSingleGenericArgument (Type target, Type interfaceType, Type wrapperType, [NotNullWhen (true)] out Type? argument)
			{
				if (target.IsGenericType && !target.IsGenericTypeDefinition) {
					var genericDef = target.GetGenericTypeDefinition ();
					if (genericDef == interfaceType || genericDef == wrapperType) {
						argument = target.GetGenericArguments () [0];
						return true;
					}
				}

				argument = null;
				return false;
			}

			static bool TryGetDictionaryArguments (Type target, [NotNullWhen (true)] out Type []? arguments)
			{
				if (target.IsGenericType && !target.IsGenericTypeDefinition) {
					var genericDef = target.GetGenericTypeDefinition ();
					if (genericDef == typeof (IDictionary<,>) || genericDef == typeof (JavaDictionary<,>)) {
						arguments = target.GetGenericArguments ();
						return true;
					}
				}

				arguments = null;
				return false;
			}

			static JavaPeerContainerFactory? TryGetContainerFactory (Type elementType)
			{
				if (ScalarContainerFactories.TryGetValue (elementType, out var scalarFactory))
					return scalarFactory;

				if (typeof (IJavaPeerable).IsAssignableFrom (elementType))
					return TrimmableTypeMap.Instance?.GetContainerFactory (elementType);

				return null;
			}
		}

		static Func<IntPtr, JniHandleOwnership, object> GetJniHandleConverterForType ([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] Type t)
		{
			MethodInfo m = t.GetMethod ("FromJniHandle", BindingFlags.Static | BindingFlags.Public)!;
			return (Func<IntPtr, JniHandleOwnership, object>) Delegate.CreateDelegate (
					typeof (Func<IntPtr, JniHandleOwnership, object>), m);
		}

		internal readonly struct ArrayElementConverter
		{
			readonly Type? elementType;
			readonly Func<IntPtr, JniHandleOwnership, object>? converter;
			readonly bool useRuntimeTypeMapping;

			public ArrayElementConverter (Array array)
			{
				elementType = array.GetType ().GetElementType ();
				converter = elementType != null ? GetJniHandleConverter (elementType) : null;
				useRuntimeTypeMapping = elementType is null || elementType == typeof (object);
			}

			public object? FromJniHandle (IntPtr handle, JniHandleOwnership transfer)
			{
				if (handle == IntPtr.Zero)
					return null;

				if (useRuntimeTypeMapping)
					return FromJniHandleWithRuntimeTypeMapping (handle, transfer);

				if (elementType != null) {
					var peeked = Java.Lang.Object.PeekObject (handle, elementType);
					if (peeked != null) {
						JNIEnv.DeleteRef (handle, transfer);
						return peeked;
					}
				}

				if (converter != null)
					return converter (handle, transfer);

				if (elementType != null && elementType.IsArray)
					return JNIEnv.GetArray (handle, transfer, elementType.GetElementType ());

				if (elementType != null && typeof (IJavaPeerable).IsAssignableFrom (elementType)) {
					if (RuntimeFeature.TrimmableTypeMap)
						return FromJniHandleWithTrimmableTypeMapping (handle, transfer, elementType);
					return Java.Lang.Object.GetObject (handle, transfer, elementType);
				}

				var value = FromJniHandleWithRuntimeTypeMapping (handle, transfer);
				if (value == null || elementType == null || elementType.IsAssignableFrom (value.GetType ()))
					return value;
				return Convert.ChangeType (value, elementType, CultureInfo.InvariantCulture);
			}
		}

		static object? FromJniHandleWithRuntimeTypeMapping (IntPtr handle, JniHandleOwnership transfer)
		{
			var converter = GetJniHandleConverter (GetTypeMapping (handle));
			if (converter != null)
				return converter (handle, transfer);
			return FromJniHandle (handle, transfer);
		}

		static object? FromJniHandleWithTrimmableTypeMapping (IntPtr handle, JniHandleOwnership transfer, Type elementType)
		{
			bool consumed = false;
			try {
				if (elementType.IsGenericType) {
					throw new NotSupportedException (
						FormattableString.Invariant ($"Cannot convert Java collection elements to closed generic array element type '{elementType}'."));
				}

				// This path intentionally avoids the reflection fallback used by TrimmableTypeMap.CreateInstance ()
				// because passing array element types there would require DAM annotations.  Closed generic element
				// types cannot be supported without that fallback: creating a non-generic base peer would not be
				// assignable to the requested closed generic array element type.  If the requested element type is
				// already a non-generic base type, the typemap lookup can still select that base mapping.
				var peer = TrimmableTypeMap.Instance.CreateInstanceWithoutReflectionFallback (handle, elementType);
				if (peer != null) {
					consumed = true;
					JNIEnv.DeleteRef (handle, transfer);
					return peer;
				}

				throw new NotSupportedException (
					FormattableString.Invariant ($"Cannot convert Java collection element to array element type '{elementType}' using the trimmable type map."));
			} finally {
				if (!consumed) {
					JNIEnv.DeleteRef (handle, transfer);
				}
			}
		}

		public static T? FromJniHandle<
				[DynamicallyAccessedMembers (Constructors)]
				T
		>(IntPtr handle, JniHandleOwnership transfer)
		{
			bool set;
			return FromJniHandle<T>(handle, transfer, out set);
		}

		public static T? FromJniHandle<
				[DynamicallyAccessedMembers (Constructors)]
				T
		>(IntPtr handle, JniHandleOwnership transfer, out bool set)
		{
			if (handle == IntPtr.Zero) {
				set = false;
				return default (T);
			}

			var interned = (IJavaObject?) Java.Lang.Object.PeekObject (handle);
			if (interned != null) {
				T? r = FromJavaObject<T>(interned, out set);
				if (set) {
					JNIEnv.DeleteRef (handle, transfer);
					return r;
				}
			}

			set = true;

			if (typeof (IJavaObject).IsAssignableFrom (typeof (T)))
				return (T?) Java.Lang.Object._GetObject<T> (handle, transfer);

			var converter = GetJniHandleConverter (typeof (T)) ??
				GetJniHandleConverter (GetTypeMapping (handle));
			if (converter != null)
				return (T?) converter (handle, transfer);

			var v = Java.Lang.Object.GetObject (handle, transfer);
			if (v is T)
				return (T) v;

			// hail mary pass; perhaps there's a MCW which participates in normal
			// .NET type conversion?
			return (T?) Convert.ChangeType (v, typeof (T), CultureInfo.InvariantCulture);
		}

		internal static object? FromObjectReference (
			ref JniObjectReference reference,
			JniObjectReferenceOptions options,
			[DynamicallyAccessedMembers (Constructors)]
			Type? targetType = null)
		{
			JniHandleOwnership transfer;
			if ((options & DisposeSource) != DisposeSource) {
				transfer = JniHandleOwnership.DoNotTransfer;
			} else {
				transfer = reference.Type switch {
					JniObjectReferenceType.Local => JniHandleOwnership.TransferLocalRef,
					JniObjectReferenceType.Global => JniHandleOwnership.TransferGlobalRef,
					_ => JniHandleOwnership.DoNotTransfer,
				};
			}

			var value = FromJniHandle (reference.Handle, transfer, targetType);
			if (transfer != JniHandleOwnership.DoNotTransfer) {
				reference = default;
			}

			return value;
		}

		public static object? FromJniHandle (
				IntPtr handle,
				JniHandleOwnership transfer,
				[DynamicallyAccessedMembers (Constructors)]
				Type? targetType = null)
		{
			if (handle == IntPtr.Zero) {
				return null;
			}

			var interned = (IJavaObject?) Java.Lang.Object.PeekObject (handle);
			if (interned != null) {
				var unwrapped = FromJavaObject (interned, targetType);
				if (unwrapped != null) {
					JNIEnv.DeleteRef (handle, transfer);
					return unwrapped;
				}
			}

			if (targetType != null && typeof (IJavaObject).IsAssignableFrom (targetType))
				return Java.Lang.Object.GetObject (handle, transfer, targetType);

			var converter = targetType != null
				? (GetJniHandleConverter (targetType) ?? GetJniHandleConverter (GetTypeMapping (handle)))
				: GetJniHandleConverter (GetTypeMapping (handle));
			if (converter != null)
				return converter (handle, transfer);

			var v = Java.Lang.Object.GetObject (handle, transfer);
			if (v != null && (targetType == null || targetType.IsAssignableFrom (v.GetType ())))
				return v;

			// hail mary pass; perhaps there's a MCW which participates in normal
			// .NET type conversion?
			return Convert.ChangeType (v, targetType!, CultureInfo.InvariantCulture);
		}

		static Dictionary<string, Type> TypeMappings = new Dictionary<string, Type> (9, StringComparer.Ordinal) {
			{ "java/lang/Boolean",    typeof (bool) },
			{ "java/lang/Byte",       typeof (byte) },
			{ "java/lang/Character",  typeof (char) },
			{ "java/lang/Short",      typeof (short) },
			{ "java/lang/Integer",    typeof (int) },
			{ "java/lang/Long",       typeof (long) },
			{ "java/lang/Float",      typeof (float) },
			{ "java/lang/Double",     typeof (double) },
			{ "java/lang/String",     typeof (string) },
		};

		static Type? GetTypeMapping (IntPtr handle)
		{
			var lref = JniEnvironment.Types.GetObjectClass (new JniObjectReference (handle));
			try {
				string className = JniEnvironment.Types.GetJniTypeNameFromClass (lref);
				if (TypeMappings.TryGetValue (className, out var match))
					return match;
				if (JniEnvironment.Types.IsAssignableFrom (lref, new JniObjectReference (JavaDictionary.map_class)))
					return typeof (JavaDictionary);
				if (JniEnvironment.Types.IsAssignableFrom (lref, JavaList.list_members.JniPeerType.PeerReference))
					return typeof (JavaList);
				if (JniEnvironment.Types.IsAssignableFrom (lref, new JniObjectReference (JavaCollection.collection_class)))
					return typeof (JavaCollection);
				return null;
			} finally {
				JniObjectReference.Dispose (ref lref);
			}
		}

		internal static string? GetJniClassForType (Type type)
		{
			foreach (var e in TypeMappings) {
				if (e.Value == type)
					return e.Key;
			}
			return null;
		}

		public static T? FromJavaObject<
				[DynamicallyAccessedMembers (Constructors)]
				T
		>(IJavaObject? value)
		{
			bool set;
			return FromJavaObject<T>(value, out set);
		}

		public static T? FromJavaObject<
				[DynamicallyAccessedMembers (Constructors)]
				T
		>(IJavaObject? value, out bool set)
		{
			if (value == null) {
				set = false;
				return default (T);
			}
			if (typeof (IJavaObject).IsAssignableFrom (typeof (T))) {
				set = true;
				return value._JavaCast<T>();
			}
			if (value is Android.Runtime.JavaObject o) {
				set = true;
				if (o.Instance is T)
					return (T) o.Instance;
				return (T) Convert.ChangeType (o.Instance, typeof (T), CultureInfo.InvariantCulture);
			}
			if (value is T) {
				set = true;
				return (T) value;
			}
			IntPtr lrefValue  = JNIEnv.ToLocalJniHandle (value);
			if (lrefValue == IntPtr.Zero) {
				set = false;
				return default (T);
			}
			set = true;
			var converter = GetJniHandleConverter (typeof (T));
			if (converter != null)
				return (T?) converter (lrefValue, JniHandleOwnership.TransferLocalRef);
			JNIEnv.DeleteLocalRef (lrefValue);
			return (T) Convert.ChangeType (value, typeof (T), CultureInfo.InvariantCulture);
		}

		public static object? FromJavaObject (
				IJavaObject value,
				[DynamicallyAccessedMembers (Constructors)]
				Type? targetType = null)
		{
			if (value == null)
				return null;

			if (targetType != null && typeof (IJavaObject).IsAssignableFrom (targetType)) {
				return JavaObjectExtensions.JavaCast (value, targetType);
			}

			if (value is Android.Runtime.JavaObject o) {
				if (targetType == null)
					return o.Instance;
				return Convert.ChangeType (o.Instance, targetType, CultureInfo.InvariantCulture);
			}

			if (targetType == null || targetType.IsAssignableFrom (value.GetType ()))
				return value;

			IntPtr lrefValue  = JNIEnv.ToLocalJniHandle (value);
			if (lrefValue == IntPtr.Zero) {
				return null;
			}
			var converter = GetJniHandleConverter (targetType);
			if (converter != null)
				return converter (lrefValue, JniHandleOwnership.TransferLocalRef);
			JNIEnv.DeleteLocalRef (lrefValue);
			return Convert.ChangeType (value, targetType, CultureInfo.InvariantCulture);
		}

		[System.Diagnostics.CodeAnalysis.SuppressMessage ("Interoperability", "CA1422:Validate platform compatibility", Justification = "Suggested replacement uses instance sharing")]
		static Dictionary<Type, Func<object, IJavaObject>> JavaObjectConverters = new Dictionary<Type, Func<object, IJavaObject>>() {
			{ typeof (bool),   value => new Java.Lang.Boolean ((bool) value) },
			{ typeof (byte),   value => new Java.Lang.Byte ((sbyte) (byte) value) },
			{ typeof (sbyte),  value => new Java.Lang.Byte ((sbyte) value) },
			{ typeof (char),   value => new Java.Lang.Character ((char) value) },
			{ typeof (short),  value => new Java.Lang.Short ((short) value) },
			{ typeof (int),    value => new Java.Lang.Integer ((int) value) },
			{ typeof (long),   value => new Java.Lang.Long ((long) value) },
			{ typeof (float),  value => new Java.Lang.Float ((float) value) },
			{ typeof (double), value => new Java.Lang.Double ((double) value) },
			{ typeof (string), value => new Java.Lang.String (value.ToString ()!) },
		};

		static Func<object, IJavaObject>? GetJavaObjectConverter (Type source)
		{
			if (JavaObjectConverters.TryGetValue (source, out var converter))
				return converter;
			return null;
		}

		public static IJavaObject? ToJavaObject<T>([AllowNull]T value)
		{
			if (value is IJavaObject)
				return (IJavaObject) value;
			if (value == null)
				return null;
			var converter = GetJavaObjectConverter (typeof (T));
			if (converter != null)
				return converter (value);
			return new Android.Runtime.JavaObject (value);
		}

		[System.Diagnostics.CodeAnalysis.SuppressMessage ("Interoperability", "CA1422:Validate platform compatibility", Justification = "Suggested replacement uses instance sharing")]
		static Dictionary<Type, Func<object, IntPtr>> LocalJniHandleConverters = new Dictionary<Type, Func<object, IntPtr>> {
			{ typeof (bool),   value => {
				using (var v = new Java.Lang.Boolean ((bool) value))
					return JNIEnv.ToLocalJniHandle (v);
			} },
			{ typeof (byte),   value => {
				using (var v = new Java.Lang.Byte ((sbyte) (byte) value))
					return JNIEnv.ToLocalJniHandle (v);
			} },
			{ typeof (sbyte),  value => {
				using (var v = new Java.Lang.Byte ((sbyte) value))
					return JNIEnv.ToLocalJniHandle (v);
			} },
			{ typeof (char),   value => {
				using (var v = new Java.Lang.Character ((char) value))
					return JNIEnv.ToLocalJniHandle (v);
			} },
			{ typeof (short),  value => {
				using (var v = new Java.Lang.Short ((short) value))
					return JNIEnv.ToLocalJniHandle (v);
			} },
			{ typeof (int),    value => {
				using (var v = new Java.Lang.Integer ((int) value))
					return JNIEnv.ToLocalJniHandle (v);
			} },
			{ typeof (long),   value => {
				using (var v = new Java.Lang.Long ((long) value))
					return JNIEnv.ToLocalJniHandle (v);
			} },
			{ typeof (float),  value => {
				using (var v = new Java.Lang.Float ((float) value))
					return JNIEnv.ToLocalJniHandle (v);
			} },
			{ typeof (double), value => {
				using (var v = new Java.Lang.Double ((double) value))
					return JNIEnv.ToLocalJniHandle (v);
			} },
			{ typeof (string), value => {
				if (value == null)
					return IntPtr.Zero;
				using (var v = new Java.Lang.String (value.ToString ()!))
					return JNIEnv.ToLocalJniHandle (v);
			} },
			{ typeof (Array), value => {
				return JNIEnv.NewArray ((Array) value);
			} },
			{ typeof (Android.Runtime.JavaObject), value => {
				return value == null ? IntPtr.Zero : JNIEnv.ToLocalJniHandle (new Android.Runtime.JavaObject (value));
			} },
		};

		static Func<object, IntPtr> GetLocalJniHandleConverter (object value)
		{
			Type sourceType = value.GetType ();
			Func<object, IntPtr>? converter;
			if (LocalJniHandleConverters.TryGetValue (sourceType, out converter))
				return converter;
			if (value != null && LocalJniHandleConverters.TryGetValue (value.GetType (), out converter))
				return converter;
			if (sourceType.IsArray)
				return LocalJniHandleConverters [typeof (Array)];
			return LocalJniHandleConverters [typeof (Android.Runtime.JavaObject)];
		}

		internal static IntPtr ToLocalJniHandle (object? value)
		{
			if (value == null) {
				return IntPtr.Zero;
			}
			if (value is IJavaObject v) {
				return JNIEnv.ToLocalJniHandle (v);
			}
			Func<object, IntPtr> converter = GetLocalJniHandleConverter (value);
			return converter (value);
		}

		internal static bool TryConvertKnownValueToLocalJniHandle (object? value, out IntPtr handle)
		{
			if (value == null) {
				handle = IntPtr.Zero;
				return true;
			}
			if (value is IJavaObject v) {
				handle = JNIEnv.ToLocalJniHandle (v);
				return true;
			}

			Type sourceType = value.GetType ();
			Func<object, IntPtr>? converter;
			if (LocalJniHandleConverters.TryGetValue (sourceType, out converter)) {
				handle = converter (value);
				return true;
			}
			if (sourceType.IsArray) {
				handle = LocalJniHandleConverters [typeof (Array)] (value);
				return true;
			}

			handle = IntPtr.Zero;
			return false;
		}

		public static TReturn WithLocalJniHandle<TValue, TReturn>(TValue value, Func<IntPtr, TReturn> action)
		{
			IntPtr lref = ToLocalJniHandle (value);
			try {
				return action (lref);
			}
			finally {
				JNIEnv.DeleteLocalRef (lref);
				GC.KeepAlive (value);
			}
		}

		public static TReturn WithLocalJniHandle<TReturn>(object? value, Func<IntPtr, TReturn> action)
		{
			IntPtr lref = ToLocalJniHandle (value);
			try {
				return action (lref);
			}
			finally {
				JNIEnv.DeleteLocalRef (lref);
				GC.KeepAlive (value);
			}
		}
	}
}
