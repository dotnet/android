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

			Type resultType = typeof (TResult);
			if (resultType.IsClass) {
				return (TResult) CastClass (instance, resultType);
			}
			else if (resultType.IsInterface) {
				return (TResult?) Java.Lang.Object.GetObject (instance.Handle, JniHandleOwnership.DoNotTransfer, resultType);
			}
			else
				throw new NotSupportedException (FormattableString.Invariant ($"Unable to convert type '{instance.GetType ().FullName}' to '{resultType.FullName}'."));
		}

		static IJavaObject CastClass (
				IJavaObject instance,
				[DynamicallyAccessedMembers (Constructors)]
				Type resultType)
		{
			var klass = JNIEnv.FindClass (resultType);
			try {
				if (klass == IntPtr.Zero)
					throw new ArgumentException ("Unable to determine JNI class for '" + resultType.FullName + "'.", "TResult");
				if (!JNIEnv.IsInstanceOf (instance.Handle, klass))
					throw new InvalidCastException (
							FormattableString.Invariant ($"Unable to convert instance of type '{instance.GetType ().FullName}' to type '{resultType.FullName}'."));
			} finally {
				JNIEnv.DeleteGlobalRef (klass);
			}

			if (resultType.IsAbstract) {
				// TODO: keep in sync with TypeManager.CreateInstance() algorithm
				var invokerType = GetInvokerType (resultType);
				if (invokerType == null)
					throw new ArgumentException ("Unable to get Invoker for abstract type '" + resultType.FullName + "'.", "TResult");
				resultType = invokerType;
			}
			return (IJavaObject) TypeManager.CreateProxy (resultType, instance.Handle, JniHandleOwnership.DoNotTransfer);
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

			if (resultType.IsClass) {
				return CastClass (instance, resultType);
			}
			else if (resultType.IsInterface) {
				return (IJavaObject?) Java.Lang.Object.GetObject (instance.Handle, JniHandleOwnership.DoNotTransfer, resultType);
			}
			else
				throw new NotSupportedException (FormattableString.Invariant ($"Unable to convert type '{instance.GetType ().FullName}' to '{resultType.FullName}'."));
		}

		// typeof(Foo) -> FooInvoker
		// typeof(Foo<>) -> FooInvoker`1
		[return: DynamicallyAccessedMembers (Constructors)]
		internal static Type? GetInvokerType (Type type)
		{
			const string InvokerTypes = "*Invoker types are preserved by the MarkJavaObjects linker step.";

			[UnconditionalSuppressMessage ("Trimming", "IL2026", Justification = InvokerTypes)]
			[UnconditionalSuppressMessage ("Trimming", "IL2055", Justification = InvokerTypes)]
			[UnconditionalSuppressMessage ("Trimming", "IL2073", Justification = InvokerTypes)]
			[return: DynamicallyAccessedMembers (Constructors)]
			static Type? AssemblyGetType (Assembly assembly, string typeName) =>
				assembly.GetType (typeName);

			// FIXME: https://github.com/xamarin/xamarin-android/issues/8724
			// IL3050 disabled in source: if someone uses NativeAOT, they will get the warning.
			[UnconditionalSuppressMessage ("Trimming", "IL2055", Justification = InvokerTypes)]
			[UnconditionalSuppressMessage ("Trimming", "IL2068", Justification = InvokerTypes)]
			[return: DynamicallyAccessedMembers (Constructors)]
			static Type MakeGenericType (Type type, params Type [] typeArguments) =>
				#pragma warning disable IL3050
				type.MakeGenericType (typeArguments);
				#pragma warning restore IL3050

			const string suffix = "Invoker";
			
			Type[] arguments = type.GetGenericArguments ();
			if (arguments.Length == 0)
				return AssemblyGetType (type.Assembly, type + suffix);
			Type definition = type.GetGenericTypeDefinition ();
			int bt = definition.FullName!.IndexOf ("`", StringComparison.Ordinal);
			if (bt == -1)
				throw new NotSupportedException ("Generic type doesn't follow generic type naming convention! " + type.FullName);
			Type? suffixDefinition = AssemblyGetType (
					definition.Assembly,
					definition.FullName.Substring (0, bt) + suffix + definition.FullName.Substring (bt));
			if (suffixDefinition == null)
				return null;
			return MakeGenericType (suffixDefinition, arguments);
		}
	}
}
