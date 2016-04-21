using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Android.Runtime;

namespace Java.Interop {

	public static class JavaObjectExtensions {

		[Obsolete ("Use Android.Runtime.JavaCollection.ToLocalJniHandle()")]
		public static JavaCollection ToInteroperableCollection (this ICollection instance)
		{
			return instance is JavaCollection ? (JavaCollection) instance : new JavaCollection (instance);
		}

		[Obsolete ("Use Android.Runtime.JavaCollection<T>.ToLocalJniHandle()")]
		public static JavaCollection<T> ToInteroperableCollection<T> (this ICollection<T> instance)
		{
			return instance is JavaCollection<T> ? (JavaCollection<T>) instance : new JavaCollection<T> (instance);
		}

		[Obsolete ("Use Android.Runtime.JavaList.ToLocalJniHandle()")]
		public static JavaList ToInteroperableCollection (this IList instance)
		{
			return instance is JavaList ? (JavaList) instance : new JavaList (instance);
		}

		[Obsolete ("Use Android.Runtime.JavaList<T>.ToLocalJniHandle()")]
		public static JavaList<T> ToInteroperableCollection<T> (this IList<T> instance)
		{
			return instance is JavaList<T> ? (JavaList<T>) instance : new JavaList<T> (instance);
		}

		[Obsolete ("Use Android.Runtime.JavaDictionary.ToLocalJniHandle()")]
		public static JavaDictionary ToInteroperableCollection (this IDictionary instance)
		{
			return instance is JavaDictionary ? (JavaDictionary) instance : new JavaDictionary (instance);
		}

		[Obsolete ("Use Android.Runtime.JavaDictionary<K, V>.ToLocalJniHandle()")]
		public static JavaDictionary<K,V> ToInteroperableCollection<K,V> (this IDictionary<K,V> instance)
		{
			return instance is JavaDictionary<K,V> ? (JavaDictionary<K,V>) instance : new JavaDictionary<K,V> (instance);
		}

		public static TResult JavaCast<TResult> (this IJavaObject instance)
			where TResult : class, IJavaObject
		{
			return _JavaCast<TResult> (instance);
		}

		internal static TResult _JavaCast<TResult> (this IJavaObject instance)
		{
			if (instance == null)
				return default (TResult);

			if (instance is TResult)
				return (TResult) instance;

			Type resultType = typeof (TResult);
			if (resultType.IsClass) {
				return (TResult) CastClass (instance, resultType);
			}
			else if (resultType.IsInterface) {
				Type invokerType = GetHelperType (resultType, "Invoker");
				if (invokerType == null)
					throw new ArgumentException ("Unable to get Invoker for interface '" + resultType.FullName + "'.", "TResult");
				Func<IntPtr, JniHandleOwnership, TResult> getObject = (Func<IntPtr, JniHandleOwnership, TResult>) Delegate.CreateDelegate (typeof (Func<IntPtr, JniHandleOwnership, TResult>), invokerType, "GetObject");
				return getObject (instance.Handle, JniHandleOwnership.DoNotTransfer);
			}
			else
				throw new NotSupportedException (string.Format ("Unable to convert type '{0}' to '{1}'.",
							instance.GetType ().FullName, resultType.FullName));
		}

		static IJavaObject CastClass (IJavaObject instance, Type resultType)
		{
			var klass = JNIEnv.FindClass (resultType);
			try {
				if (klass == IntPtr.Zero)
					throw new ArgumentException ("Unable to determine JNI class for '" + resultType.FullName + "'.", "TResult");
				if (!JNIEnv.IsInstanceOf (instance.Handle, klass))
					throw new InvalidCastException (
							string.Format ("Unable to convert instance of type '{0}' to type '{1}'.",
								instance.GetType ().FullName, resultType.FullName));
			} finally {
				JNIEnv.DeleteGlobalRef (klass);
			}

			if (resultType.IsAbstract) {
				// TODO: keep in sync with TypeManager.CreateInstance() algorithm
				Type invokerType = GetHelperType (resultType, "Invoker");
				if (invokerType == null)
					throw new ArgumentException ("Unable to get Invoker for abstract type '" + resultType.FullName + "'.", "TResult");
				resultType = invokerType;
			}
			return (IJavaObject) TypeManager.CreateProxy (resultType, instance.Handle, JniHandleOwnership.DoNotTransfer);
		}

		internal static IJavaObject JavaCast (IJavaObject instance, Type resultType)
		{
			if (resultType == null)
				throw new ArgumentNullException ("resultType");

			if (instance == null)
				return null;

			if (resultType.IsAssignableFrom (instance.GetType ()))
				return instance;

			if (resultType.IsClass) {
				return CastClass (instance, resultType);
			}
			else if (resultType.IsInterface) {
				Type invokerType = GetHelperType (resultType, "Invoker");
				if (invokerType == null)
					throw new ArgumentException ("Unable to get Invoker for interface '" + resultType.FullName + "'.", "resultType");
				var getObject = invokerType.GetMethod ("GetObject", new[]{typeof (IntPtr), typeof (JniHandleOwnership)});
				return (IJavaObject) getObject.Invoke (null, new object[]{instance.Handle, JniHandleOwnership.DoNotTransfer});
			}
			else
				throw new NotSupportedException (string.Format ("Unable to convert type '{0}' to '{1}'.",
							instance.GetType ().FullName, resultType.FullName));
		}

		// typeof(Foo) -> FooSuffix
		// typeof(Foo<>) -> FooSuffix`1
		internal static Type GetHelperType (Type type, string suffix)
		{
			Type[] arguments = type.GetGenericArguments ();
			if (arguments.Length == 0)
				return type.Assembly.GetType (type + suffix);
			Type definition = type.GetGenericTypeDefinition ();
			int bt = definition.FullName.IndexOf ("`");
			if (bt == -1)
				throw new NotSupportedException ("Generic type doesn't follow generic type naming convention! " + type.FullName);
			Type suffixDefinition = definition.Assembly.GetType (
					definition.FullName.Substring (0, bt) + suffix + definition.FullName.Substring (bt));
			if (suffixDefinition == null)
				return null;
			return suffixDefinition.MakeGenericType (arguments);
		}
	}
}
