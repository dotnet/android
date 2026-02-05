using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;

using Android.Runtime;

namespace Java.Interop {

	static class JavaConvert {
		const DynamicallyAccessedMemberTypes Constructors = DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors;

		static Dictionary<Type, Func<IntPtr, JniHandleOwnership, object>> JniHandleConverters = new Dictionary<Type, Func<IntPtr, JniHandleOwnership, object>>() {
			{ typeof (bool), (handle, transfer) => {
				using (var value = new Java.Lang.Boolean (handle, transfer | JniHandleOwnership.DoNotRegister))
					return value.BooleanValue ();
			} },
			{ typeof (byte), (handle, transfer) => {
				using (var value = new Java.Lang.Byte (handle, transfer | JniHandleOwnership.DoNotRegister))
					return (byte) value.ByteValue ();
			} },
			{ typeof (sbyte), (handle, transfer) => {
				using (var value = new Java.Lang.Byte (handle, transfer | JniHandleOwnership.DoNotRegister))
					return value.ByteValue ();
			} },
			{ typeof (char), (handle, transfer) => {
				using (var value = new Java.Lang.Character (handle, transfer | JniHandleOwnership.DoNotRegister))
					return value.CharValue ();
			} },
			{ typeof (short), (handle, transfer) => {
				using (var value = new Java.Lang.Short (handle, transfer | JniHandleOwnership.DoNotRegister))
					return value.ShortValue ();
			} },
			{ typeof (int), (handle, transfer) => {
				using (var value = new Java.Lang.Integer (handle, transfer | JniHandleOwnership.DoNotRegister))
					return value.IntValue ();
			} },
			{ typeof (long), (handle, transfer) => {
				using (var value = new Java.Lang.Long (handle, transfer | JniHandleOwnership.DoNotRegister))
					return value.LongValue ();
			} },
			{ typeof (float), (handle, transfer) => {
				using (var value = new Java.Lang.Float (handle, transfer | JniHandleOwnership.DoNotRegister))
					return value.FloatValue ();
			} },
			{ typeof (double), (handle, transfer) => {
				using (var value = new Java.Lang.Double (handle, transfer | JniHandleOwnership.DoNotRegister))
					return value.DoubleValue ();
			} },
			{ typeof (string), (handle, transfer) => {
				using (var value = new Java.Lang.String (handle, transfer | JniHandleOwnership.DoNotRegister))
					return value.ToString ();
			} },
		};

		static Func<IntPtr, JniHandleOwnership, object?>? GetJniHandleConverter (Type? target)
		{
			if (target == null)
				return null;

			if (JniHandleConverters.TryGetValue (target, out var converter))
				return converter;
			if (target.IsArray)
				return (h, t) => JNIEnv.GetArray (h, t, target.GetElementType ());

			// Handle generic IList<T> using JavaPeerContainerFactory for AOT-safe conversion
			if (target.IsGenericType && target.GetGenericTypeDefinition () == typeof (IList<>)) {
				return TryCreateGenericListConverter (target);
			}

			// Handle generic IDictionary<K,V> using JavaPeerContainerFactory for AOT-safe conversion
			if (target.IsGenericType && target.GetGenericTypeDefinition () == typeof (IDictionary<,>)) {
				return TryCreateGenericDictionaryConverter (target);
			}

			// Handle generic ICollection<T> using JavaPeerContainerFactory for AOT-safe conversion
			if (target.IsGenericType && target.GetGenericTypeDefinition () == typeof (ICollection<>)) {
				return TryCreateGenericCollectionConverter (target);
			}

			// Non-generic collection fallbacks
			if (typeof (IDictionary).IsAssignableFrom (target))
				return (h, t) => JavaDictionary.FromJniHandle (h, t);
			if (typeof (IList).IsAssignableFrom (target))
				return (h, t) => JavaList.FromJniHandle (h, t);
			if (typeof (ICollection).IsAssignableFrom (target))
				return (h, t) => JavaCollection.FromJniHandle (h, t);

			return null;
		}

		/// <summary>
		/// Creates a converter for IList&lt;T&gt; using JavaPeerContainerFactory.
		/// Uses the TypeMap to look up the proxy for T and get its factory.
		/// </summary>
		static Func<IntPtr, JniHandleOwnership, object?>? TryCreateGenericListConverter (Type listType)
		{
			var elementType = listType.GetGenericArguments ()[0];

			// For primitive types and string, we need specific converters since they don't have proxies
			// These return JavaList<T> which implements IList<T>
			if (elementType == typeof (string)) {
				return (h, t) => JavaList<string>.FromJniHandle (h, t);
			}
			if (elementType == typeof (int)) {
				return (h, t) => JavaList<int>.FromJniHandle (h, t);
			}
			if (elementType == typeof (long)) {
				return (h, t) => JavaList<long>.FromJniHandle (h, t);
			}
			if (elementType == typeof (bool)) {
				return (h, t) => JavaList<bool>.FromJniHandle (h, t);
			}
			if (elementType == typeof (float)) {
				return (h, t) => JavaList<float>.FromJniHandle (h, t);
			}
			if (elementType == typeof (double)) {
				return (h, t) => JavaList<double>.FromJniHandle (h, t);
			}
			if (elementType == typeof (object)) {
				return (h, t) => JavaList<object>.FromJniHandle (h, t);
			}

			// For Java peer types, use the TypeMap to get the proxy and its JavaPeerContainerFactory
			var typeMap = JNIEnvInit.TypeMap;
			if (typeMap == null) {
				return (h, t) => JavaList.FromJniHandle (h, t);
			}

			var proxy = typeMap.GetProxyForManagedType (elementType);
			if (proxy == null) {
				// No proxy found - fall back to non-generic JavaList
				return (h, t) => JavaList.FromJniHandle (h, t);
			}

			// Get the JavaPeerContainerFactory and use it to create lists
			var factory = proxy.GetJavaPeerContainerFactory ();
			return (h, t) => factory.CreateListFromHandle (h, t);
		}

		/// <summary>
		/// Creates a converter for IDictionary&lt;K,V&gt; using JavaPeerContainerFactory.
		/// Note: Most dictionary access goes through JavaDictionary&lt;K,V&gt;.FromJniHandle directly.
		/// This converter is used when explicitly requesting FromJniHandle&lt;IDictionary&lt;K,V&gt;&gt;().
		/// </summary>
		static Func<IntPtr, JniHandleOwnership, object?>? TryCreateGenericDictionaryConverter (Type dictType)
		{
			var typeArgs = dictType.GetGenericArguments ();
			var keyType = typeArgs[0];
			var valueType = typeArgs[1];

			// Handle common key/value type combinations directly
			// These are the most frequently used dictionary types
			if (keyType == typeof (string)) {
				if (valueType == typeof (string)) {
					return (h, t) => JavaDictionary<string, string>.FromJniHandle (h, t);
				}
				if (valueType == typeof (object)) {
					return (h, t) => JavaDictionary<string, object>.FromJniHandle (h, t);
				}
				// For IList<string> values (e.g., HeaderFields), the dictionary itself is typed
				// and value access goes through JavaConvert.FromJniHandle<IList<string>>
				// which is handled by TryCreateGenericListConverter
			}

			// For Java peer types, use JavaPeerContainerFactory
			var typeMap = JNIEnvInit.TypeMap;
			if (typeMap == null) {
				return (h, t) => JavaDictionary.FromJniHandle (h, t);
			}

			var keyProxy = typeMap.GetProxyForManagedType (keyType);
			var valueProxy = typeMap.GetProxyForManagedType (valueType);
			if (keyProxy == null || valueProxy == null) {
				// Fall back to non-generic - caller may need to cast values manually
				return (h, t) => JavaDictionary.FromJniHandle (h, t);
			}

			var keyFactory = keyProxy.GetJavaPeerContainerFactory ();
			var valueFactory = valueProxy.GetJavaPeerContainerFactory ();
			return (h, t) => valueFactory.CreateDictionaryFromHandle (keyFactory, h, t);
		}

		/// <summary>
		/// Creates a converter for ICollection&lt;T&gt; using JavaPeerContainerFactory.
		/// </summary>
		static Func<IntPtr, JniHandleOwnership, object?>? TryCreateGenericCollectionConverter (Type collectionType)
		{
			var elementType = collectionType.GetGenericArguments ()[0];

			// Handle primitive and string element types directly
			if (elementType == typeof (string)) {
				return (h, t) => JavaCollection<string>.FromJniHandle (h, t);
			}
			if (elementType == typeof (int)) {
				return (h, t) => JavaCollection<int>.FromJniHandle (h, t);
			}
			if (elementType == typeof (long)) {
				return (h, t) => JavaCollection<long>.FromJniHandle (h, t);
			}
			if (elementType == typeof (bool)) {
				return (h, t) => JavaCollection<bool>.FromJniHandle (h, t);
			}
			if (elementType == typeof (float)) {
				return (h, t) => JavaCollection<float>.FromJniHandle (h, t);
			}
			if (elementType == typeof (double)) {
				return (h, t) => JavaCollection<double>.FromJniHandle (h, t);
			}
			if (elementType == typeof (object)) {
				return (h, t) => JavaCollection<object>.FromJniHandle (h, t);
			}

			// For Java peer types, use the TypeMap to get the proxy's factory
			var typeMap = JNIEnvInit.TypeMap;
			if (typeMap == null) {
				return (h, t) => JavaCollection.FromJniHandle (h, t);
			}

			var proxy = typeMap.GetProxyForManagedType (elementType);
			if (proxy == null) {
				return (h, t) => JavaCollection.FromJniHandle (h, t);
			}

			var factory = proxy.GetJavaPeerContainerFactory ();
			return (h, t) => factory.CreateCollectionFromHandle (h, t);
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

