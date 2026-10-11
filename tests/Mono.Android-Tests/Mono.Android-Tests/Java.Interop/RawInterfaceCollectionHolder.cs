using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Android.Runtime;

using Java.Interop;
using Net.Dot.Android.Test;

namespace Java.InteropTests;

// Shared with the isolated NativeAOT probe: do not reference closed Java collection wrapper types here.
sealed class RawInterfaceCollectionHolder : IDisposable
{
	const string CollectionSignature = "()Ljava/util/Collection;";
	const string DictionarySignature = "()Ljava/util/Map;";
	const string JniName = "net/dot/android/test/InterfaceCollectionHolder";
	const string ListSignature = "()Ljava/util/List;";
	const string RoundTripCollectionSignature = "(Ljava/util/Collection;)Ljava/util/Collection;";
	const string RoundTripDictionarySignature = "(Ljava/util/Map;)Ljava/util/Map;";
	const string RoundTripListSignature = "(Ljava/util/List;)Ljava/util/List;";

	readonly Java.Lang.Object holder;

	public RawInterfaceCollectionHolder ()
	{
		var holderClass = JniEnvironment.Types.FindClass (JniName);
		try {
			var constructor = JNIEnv.GetMethodID (holderClass.Handle, "<init>", "()V");
			var handle = JNIEnv.NewObject (holderClass.Handle, constructor);
			holder = new Java.Lang.Object (handle, JniHandleOwnership.TransferLocalRef);
		} finally {
			JniObjectReference.Dispose (ref holderClass);
		}
	}

	public IList<IValueProvider> CreateList ()
	{
		return ConvertJavaValue<IList<IValueProvider>> (Call ("createList", ListSignature));
	}

	public IList<IExtendedValueProvider> CreateInheritedList ()
	{
		return ConvertJavaValue<IList<IExtendedValueProvider>> (Call ("createInheritedList", ListSignature));
	}

	public ICollection<IValueProvider> CreateCollection ()
	{
		return ConvertJavaValue<ICollection<IValueProvider>> (Call ("createCollection", CollectionSignature));
	}

	public IValueProvider GetFirst ()
	{
		return ConvertJavaValue<IValueProvider> (Call ("getFirst", "()Lnet/dot/android/test/ValueProvider;"));
	}

	public IValueProvider GetSecond ()
	{
		return ConvertJavaValue<IValueProvider> (Call ("getSecond", "()Lnet/dot/android/test/ValueProvider;"));
	}

	public IDictionary<IValueProvider, string> CreateKeyDictionary ()
	{
		return ConvertJavaValue<IDictionary<IValueProvider, string>> (Call ("createKeyDictionary", DictionarySignature));
	}

	public IDictionary<string, IValueProvider> CreateValueDictionary ()
	{
		return ConvertJavaValue<IDictionary<string, IValueProvider>> (Call ("createValueDictionary", DictionarySignature));
	}

	public IDictionary<IValueProvider, IValueProvider> CreateInterfaceDictionary ()
	{
		return ConvertJavaValue<IDictionary<IValueProvider, IValueProvider>> (Call ("createInterfaceDictionary", DictionarySignature));
	}

	public IList<IValueProvider> RoundTripList (IList<IValueProvider> value)
	{
		return ConvertJavaValue<IList<IValueProvider>> (Call ("roundTripList", RoundTripListSignature, value));
	}

	public ICollection<IValueProvider> RoundTripCollection (ICollection<IValueProvider> value)
	{
		return ConvertJavaValue<ICollection<IValueProvider>> (Call ("roundTripCollection", RoundTripCollectionSignature, value));
	}

	public IDictionary<IValueProvider, string> RoundTripKeyDictionary (IDictionary<IValueProvider, string> value)
	{
		return ConvertJavaValue<IDictionary<IValueProvider, string>> (
			Call ("roundTripKeyDictionary", RoundTripDictionarySignature, value));
	}

	public IDictionary<string, IValueProvider> RoundTripValueDictionary (IDictionary<string, IValueProvider> value)
	{
		return ConvertJavaValue<IDictionary<string, IValueProvider>> (
			Call ("roundTripValueDictionary", RoundTripDictionarySignature, value));
	}

	public IDictionary<IValueProvider, IValueProvider> RoundTripInterfaceDictionary (IDictionary<IValueProvider, IValueProvider> value)
	{
		return ConvertJavaValue<IDictionary<IValueProvider, IValueProvider>> (
			Call ("roundTripInterfaceDictionary", RoundTripDictionarySignature, value));
	}

	public void Dispose ()
	{
		holder.Dispose ();
	}

	IntPtr Call (string methodName, string signature, object value = null)
	{
		var holderClass = JniEnvironment.Types.GetObjectClass (holder.PeerReference);
		try {
			var method = JNIEnv.GetMethodID (holderClass.Handle, methodName, signature);
			IntPtr handle;
			if (value == null) {
				handle = JNIEnv.CallObjectMethod (holder.Handle, method);
			} else {
				var peer = (IJavaObject) value;
				handle = JNIEnv.CallObjectMethod (holder.Handle, method, new JValue (peer.Handle));
				GC.KeepAlive (value);
			}
			return handle;
		} finally {
			JniObjectReference.Dispose (ref holderClass);
		}
	}

	[DynamicDependency ("FromJniHandle", "Java.Interop.JavaConvert", "Mono.Android")]
	[UnconditionalSuppressMessage ("Trimming", "IL2111",
		Justification = "T preserves the required constructors; reflection is needed by the shared NativeAOT probe without JavaConvert access.")]
	static T ConvertJavaValue<
		[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
		T> (IntPtr handle)
	{
		var javaConvert = typeof (Java.Lang.Object).Assembly.GetType ("Java.Interop.JavaConvert");
		if (javaConvert == null) {
			throw new InvalidOperationException ("JavaConvert type was not found.");
		}

		var method = javaConvert.GetMethod (
			"FromJniHandle",
			BindingFlags.Public | BindingFlags.Static,
			binder: null,
			types: [typeof (IntPtr), typeof (JniHandleOwnership), typeof (Type)],
			modifiers: null);
		if (method == null) {
			throw new InvalidOperationException ("JavaConvert.FromJniHandle method was not found.");
		}

		var value = method.Invoke (null, [handle, JniHandleOwnership.TransferLocalRef, typeof (T)]);
		if (value == null) {
			throw new InvalidOperationException ($"JavaConvert returned null for target type '{typeof (T)}'.");
		}
		return (T) value;
	}
}
