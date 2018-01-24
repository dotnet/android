using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

using Android.Runtime;

namespace Java.Interop {

	static class JavaConvert {

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

		static Func<IntPtr, JniHandleOwnership, object> GetJniHandleConverter (Type target)
		{
			if (target == null)
				return null;

			Func<IntPtr, JniHandleOwnership, object> converter;
			if (JniHandleConverters.TryGetValue (target, out converter))
				return converter;
			if (target.IsArray)
				return (h, t) => JNIEnv.GetArray (h, t, target.GetElementType ());
			if (target.IsGenericType && target.GetGenericTypeDefinition() == typeof (IDictionary<,>)) {
				Type t = typeof (JavaDictionary<,>).MakeGenericType (target.GetGenericArguments ());
				return GetJniHandleConverterForType (t);
			}
			if (typeof (IDictionary).IsAssignableFrom (target))
				return (h, t) => JavaDictionary.FromJniHandle (h, t);
			if (target.IsGenericType && target.GetGenericTypeDefinition() == typeof (IList<>)) {
				Type t = typeof (JavaList<>).MakeGenericType (target.GetGenericArguments ());
				return GetJniHandleConverterForType (t);
			}
			if (typeof (IList).IsAssignableFrom (target))
				return (h, t) => JavaList.FromJniHandle (h, t);
			if (target.IsGenericType && target.GetGenericTypeDefinition() == typeof (ICollection<>)) {
				Type t = typeof (JavaCollection<>).MakeGenericType (target.GetGenericArguments ());
				return GetJniHandleConverterForType (t);
			}
			if (typeof (ICollection).IsAssignableFrom (target))
				return (h, t) => JavaCollection.FromJniHandle (h, t);

			return null;
		}

		static Func<IntPtr, JniHandleOwnership, object> GetJniHandleConverterForType (Type t)
		{
			MethodInfo m = t.GetMethod ("FromJniHandle", BindingFlags.Static | BindingFlags.Public);
			return (Func<IntPtr, JniHandleOwnership, object>) Delegate.CreateDelegate (
					typeof (Func<IntPtr, JniHandleOwnership, object>), m);
		}

		public static T FromJniHandle<T>(IntPtr handle, JniHandleOwnership transfer)
		{
			bool set;
			return FromJniHandle<T>(handle, transfer, out set);
		}

		public static T FromJniHandle<T>(IntPtr handle, JniHandleOwnership transfer, out bool set)
		{
			if (handle == IntPtr.Zero) {
				set = false;
				return default (T);
			}

			IJavaObject interned = Java.Lang.Object.PeekObject (handle);
			if (interned != null) {
				T r = FromJavaObject<T>(interned, out set);
				if (set) {
					JNIEnv.DeleteRef (handle, transfer);
					return r;
				}
			}

			set = true;

			if (typeof (IJavaObject).IsAssignableFrom (typeof (T)))
				return (T) Java.Lang.Object._GetObject<T> (handle, transfer);

			Func<IntPtr, JniHandleOwnership, object> converter = GetJniHandleConverter (typeof (T)) ??
				GetJniHandleConverter (GetTypeMapping (handle));
			if (converter != null)
				return (T) converter (handle, transfer);

			var v = Java.Lang.Object.GetObject (handle, transfer);
			if (v is T)
				return (T) v;

			// hail mary pass; perhaps there's a MCW which participates in normal
			// .NET type conversion?
			return (T) Convert.ChangeType (v, typeof (T));
		}

		public static object FromJniHandle (IntPtr handle, JniHandleOwnership transfer, Type targetType = null)
		{
			if (handle == IntPtr.Zero) {
				return null;
			}

			IJavaObject interned = Java.Lang.Object.PeekObject (handle);
			if (interned != null) {
				var unwrapped = FromJavaObject (interned, targetType);
				if (unwrapped != null) {
					JNIEnv.DeleteRef (handle, transfer);
					return unwrapped;
				}
			}

			if (targetType != null && typeof (IJavaObject).IsAssignableFrom (targetType))
				return Java.Lang.Object.GetObject (handle, transfer, targetType);

			Func<IntPtr, JniHandleOwnership, object> converter = targetType != null
				? (GetJniHandleConverter (targetType) ?? GetJniHandleConverter (GetTypeMapping (handle)))
				: GetJniHandleConverter (GetTypeMapping (handle));
			if (converter != null)
				return converter (handle, transfer);

			var v = Java.Lang.Object.GetObject (handle, transfer);
			if (v != null && (targetType == null || targetType.IsAssignableFrom (v.GetType ())))
				return v;

			// hail mary pass; perhaps there's a MCW which participates in normal
			// .NET type conversion?
			return Convert.ChangeType (v, targetType);
		}

		static Dictionary<string, Type> TypeMappings = new Dictionary<string, Type> {
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

		static Type GetTypeMapping (IntPtr handle)
		{
			string className = JNIEnv.GetClassNameFromInstance (handle);
			Type match;
			if (TypeMappings.TryGetValue (className, out match))
				return match;
			IntPtr lrefClass = JNIEnv.GetObjectClass (handle);
			try {
				if (JNIEnv.IsAssignableFrom (lrefClass, JavaDictionary.map_class))
					return typeof (JavaDictionary);
				if (JNIEnv.IsAssignableFrom (lrefClass, JavaList.arraylist_class))
					return typeof (JavaList);
				if (JNIEnv.IsAssignableFrom (lrefClass, JavaCollection.collection_class))
					return typeof (JavaCollection);
				return null;
			} finally {
				JNIEnv.DeleteLocalRef (lrefClass);
			}
		}

		internal static string GetJniClassForType (Type type)
		{
			foreach (var e in TypeMappings) {
				if (e.Value == type)
					return e.Key;
			}
			return null;
		}

		public static T FromJavaObject<T>(IJavaObject value)
		{
			bool set;
			return FromJavaObject<T>(value, out set);
		}

		public static T FromJavaObject<T>(IJavaObject value, out bool set)
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
				return (T) Convert.ChangeType (o.Instance, typeof (T));
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
			Func<IntPtr, JniHandleOwnership, object> converter = GetJniHandleConverter (typeof (T));
			if (converter != null)
				return (T) converter (lrefValue, JniHandleOwnership.TransferLocalRef);
			JNIEnv.DeleteLocalRef (lrefValue);
			return (T) Convert.ChangeType (value, typeof (T));
		}

		public static object FromJavaObject (IJavaObject value, Type targetType = null)
		{
			if (value == null)
				return null;

			if (targetType != null && typeof (IJavaObject).IsAssignableFrom (targetType)) {
				return JavaObjectExtensions.JavaCast (value, targetType);
			}

			if (value is Android.Runtime.JavaObject o) {
				if (targetType == null)
					return o.Instance;
				return Convert.ChangeType (o.Instance, targetType);
			}

			if (targetType == null || targetType.IsAssignableFrom (value.GetType ()))
				return value;

			IntPtr lrefValue  = JNIEnv.ToLocalJniHandle (value);
			if (lrefValue == IntPtr.Zero) {
				return null;
			}
			Func<IntPtr, JniHandleOwnership, object> converter = GetJniHandleConverter (targetType);
			if (converter != null)
				return converter (lrefValue, JniHandleOwnership.TransferLocalRef);
			JNIEnv.DeleteLocalRef (lrefValue);
			return Convert.ChangeType (value, targetType);
		}

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
			{ typeof (string), value => new Java.Lang.String (value.ToString ()) },
		};

		static Func<object, IJavaObject> GetJavaObjectConverter (Type source)
		{
			Func<object, IJavaObject> converter;
			if (JavaObjectConverters.TryGetValue (source, out converter))
				return converter;
			return null;
		}

		public static IJavaObject ToJavaObject<T>(T value)
		{
			if (value is IJavaObject)
				return (IJavaObject) value;
			if (value == null)
				return null;
			Func<object, IJavaObject> converter = GetJavaObjectConverter (typeof (T));
			if (converter != null)
				return converter (value);
			return new Android.Runtime.JavaObject (value);
		}

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
				using (var v = new Java.Lang.String (value.ToString ()))
					return JNIEnv.ToLocalJniHandle (v);
			} },
			{ typeof (Android.Runtime.JavaObject), value => {
				return value == null ? IntPtr.Zero : JNIEnv.ToLocalJniHandle (new Android.Runtime.JavaObject (value));
			} },
		};

		static Func<object, IntPtr> GetLocalJniHandleConverter<T> (T value, Type sourceType)
		{
			Func<object, IntPtr> converter;
			if (LocalJniHandleConverters.TryGetValue (sourceType, out converter))
				return converter;
			if (value != null && LocalJniHandleConverters.TryGetValue (value.GetType (), out converter))
				return converter;
			return LocalJniHandleConverters [typeof (Android.Runtime.JavaObject)];
		}

		public static TReturn WithLocalJniHandle<TValue, TReturn>(TValue value, Func<IntPtr, TReturn> action)
		{
			IntPtr lref = IntPtr.Zero;
			try {
				IJavaObject v = value as IJavaObject;
				if (v != null) {
					lref = JNIEnv.ToLocalJniHandle (v);
					return action (lref);
				}
				Func<object, IntPtr> converter = GetLocalJniHandleConverter (value, typeof (TValue));
				lref = converter (value);
				return action (lref);
			}
			finally {
				JNIEnv.DeleteLocalRef (lref);
			}
		}

		public static TReturn WithLocalJniHandle<TReturn>(object value, Func<IntPtr, TReturn> action)
		{
			IntPtr lref = IntPtr.Zero;
			try {
				if (value == null) {
					return action (lref);
				}
				IJavaObject v = value as IJavaObject;
				if (v != null) {
					lref = JNIEnv.ToLocalJniHandle (v);
					return action (lref);
				}
				Func<object, IntPtr> converter = GetLocalJniHandleConverter (value, value.GetType ());
				lref = converter (value);
				return action (lref);
			}
			finally {
				JNIEnv.DeleteLocalRef (lref);
			}
		}
	}
}

