using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Android.Runtime;

namespace Java.Interop {

	public static class JavaObjectExtensions {
		const DynamicallyAccessedMemberTypes Constructors = DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors;

		[Obsolete ("Use Android.Runtime.JavaCollection.ToLocalJniHandle()")]
		public static JavaCollection ToInteroperableCollection (this ICollection instance)
		{
			return instance is JavaCollection ? (JavaCollection) instance : new JavaCollection (instance);
		}

		[Obsolete ("Use Android.Runtime.JavaCollection<T>.ToLocalJniHandle()")]
		public static JavaCollection<T> ToInteroperableCollection<
				[DynamicallyAccessedMembers (Constructors)]
				T
		> (this ICollection<T> instance)
		{
			return instance is JavaCollection<T> ? (JavaCollection<T>) instance : new JavaCollection<T> (instance);
		}

		[Obsolete ("Use Android.Runtime.JavaList.ToLocalJniHandle()")]
		public static JavaList ToInteroperableCollection (this IList instance)
		{
			return instance is JavaList ? (JavaList) instance : new JavaList (instance);
		}

		[Obsolete ("Use Android.Runtime.JavaList<T>.ToLocalJniHandle()")]
		public static JavaList<T> ToInteroperableCollection<
				[DynamicallyAccessedMembers (Constructors)]
				T
		> (this IList<T> instance)
		{
			return instance is JavaList<T> ? (JavaList<T>) instance : new JavaList<T> (instance);
		}

		[Obsolete ("Use Android.Runtime.JavaDictionary.ToLocalJniHandle()")]
		public static JavaDictionary ToInteroperableCollection (this IDictionary instance)
		{
			return instance is JavaDictionary ? (JavaDictionary) instance : new JavaDictionary (instance);
		}

		[Obsolete ("Use Android.Runtime.JavaDictionary<K, V>.ToLocalJniHandle()")]
		public static JavaDictionary<K,V> ToInteroperableCollection<
				[DynamicallyAccessedMembers (Constructors)]
				K,
				[DynamicallyAccessedMembers (Constructors)]
				V
		> (this IDictionary<K,V> instance)
		{
			return instance is JavaDictionary<K,V> ? (JavaDictionary<K,V>) instance : new JavaDictionary<K,V> (instance);
		}

		[return: NotNullIfNotNull ("instance")]
		public static TResult? JavaCast<
				[DynamicallyAccessedMembers (Constructors)]
				TResult
		> (this IJavaObject? instance)
			where TResult : class, IJavaObject
		{
			return _JavaCast<TResult> (instance);
		}

		internal static TResult? _JavaCast<
				[DynamicallyAccessedMembers (Constructors)]
				TResult
		> (this IJavaObject? instance)
		{
			if (instance == null)
				return default (TResult);

			if (instance is TResult)
				return (TResult) instance;

			if (instance.Handle == IntPtr.Zero)
				throw new ObjectDisposedException (instance.GetType ().FullName);

			return (TResult) Java.Lang.Object.GetObject (instance.Handle, JniHandleOwnership.DoNotTransfer, typeof (TResult)) ??
				throw new InvalidCastException (
					FormattableString.Invariant ($"Unable to convert instance of type '{instance.GetType ().FullName}' to type '{typeof (TResult).FullName}'."));
		}

		internal static IJavaObject? JavaCast (
				IJavaObject? instance,
				[DynamicallyAccessedMembers (Constructors)]
				Type resultType)
		{
			if (resultType == null)
				throw new ArgumentNullException ("resultType");

			if (instance == null)
				return null;

			if (resultType.IsAssignableFrom (instance.GetType ()))
				return instance;

			return (IJavaObject?) Java.Lang.Object.GetObject (instance.Handle, JniHandleOwnership.DoNotTransfer, resultType) ??
				throw new InvalidCastException (
					FormattableString.Invariant ($"Unable to convert instance of type '{instance.GetType ().FullName}' to type '{resultType.FullName}'."));
		}

		// typeof(Foo) -> FooInvoker
		// typeof(Foo<>) -> FooInvoker`1
		[RequiresUnreferencedCode ("Invoker type lookup uses Assembly.GetType() which cannot be statically analyzed.")]
		[RequiresDynamicCode ("Generic invoker types require MakeGenericType which is not compatible with NativeAOT.")]
		[return: DynamicallyAccessedMembers (Constructors)]
		internal static Type? GetInvokerType (Type type)
		{
			const string suffix = "Invoker";

			Type[] arguments = type.GetGenericArguments ();
			if (arguments.Length == 0)
				return type.Assembly.GetType (type + suffix);

			Type definition = type.GetGenericTypeDefinition ();
			int bt = definition.FullName!.IndexOf ("`", StringComparison.Ordinal);
			if (bt == -1)
				throw new NotSupportedException ("Generic type doesn't follow generic type naming convention! " + type.FullName);

			Type? suffixDefinition = definition.Assembly.GetType (
					definition.FullName.Substring (0, bt) + suffix + definition.FullName.Substring (bt));
			if (suffixDefinition == null)
				return null;

			return suffixDefinition.MakeGenericType (arguments);
		}
	}
}
