using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Android.App;
using Android.OS;
using Android.Runtime;
using Android.Util;

using Java.Interop;

using Net.Dot.Android.Test;

namespace ${ROOT_NAMESPACE}
{
	[Register ("${JAVA_PACKAGENAME}.MainActivity"), Activity (Label = "${PROJECT_NAME}", MainLauncher = true)]
	public class MainActivity : Activity
	{
		const string ResultPrefix = "INTERFACE_COLLECTION_RESULT";
		const string Tag = "InterfaceCollections";

		protected override void OnCreate (Bundle savedInstanceState)
		{
			base.OnCreate (savedInstanceState);

			int passed = 0;
			try {
				JavaList_InterfaceElementsPreserveIdentityAndRoundTrip ();
				passed++;
				JavaList_InheritedInterfaceUsesExplicitInvoker ();
				passed++;
				JavaCollection_InterfaceElementsSupportOperationsAndRoundTrip ();
				passed++;
				JavaDictionary_InterfaceKeysSupportOperationsAndRoundTrip ();
				passed++;
				JavaDictionary_InterfaceValuesPreserveDuplicatesAndRoundTrip ();
				passed++;
				JavaDictionary_InterfaceKeysAndValuesPreserveIdentityAndRoundTrip ();
				passed++;
				Log.Info (Tag, $"{ResultPrefix} PASS {passed}/6");
			} catch (Exception e) {
				Log.Error (Tag, $"{ResultPrefix} FAIL {passed}/6: {e}");
			} finally {
				Finish ();
			}
		}

		static void JavaList_InterfaceElementsPreserveIdentityAndRoundTrip ()
		{
			using var holder = new RawInterfaceCollectionHolder ();
			var list = holder.CreateList ();
			try {
				AssertWrapperType (list, typeof (JavaList<>), typeof (IValueProvider));
				AssertEqual (4, list.Count, "list count");

				var first = list [0];
				var duplicate = list [1];
				var second = list [2];

				AssertBaseInterfacePeer (first, 11);
				AssertBaseInterfacePeer (second, 22);
				AssertSame (first, duplicate, "duplicate list reference");
				AssertSame (first, list [0], "repeated list lookup");
				AssertSameJavaObject (first, duplicate);
				AssertDistinctJavaObjects (first, second);
				AssertNull (list [3], "null list element");
				AssertTrue (list.Contains (first), "list contains first");
				AssertTrue (list.Contains (null), "list contains null");

				list.Add (second);
				AssertEqual (5, list.Count, "list count after add");
				AssertSame (second, list [4], "added list reference");
				AssertSequence ([11, 11, 22, 22], GetValues (list), "list enumeration");

				var roundTrip = holder.RoundTripList (list);
				try {
					AssertWrapperType (roundTrip, typeof (JavaList<>), typeof (IValueProvider));
					AssertSameJavaObject (list, roundTrip);
					AssertSame (first, roundTrip [0], "round-tripped list element");
				} finally {
					DisposeIfDistinct (list, roundTrip);
				}

				AssertTrue (list.Remove (first), "remove first list reference");
				AssertTrue (list.Contains (first), "list retains duplicate");
				AssertTrue (list.Remove (first), "remove duplicate list reference");
				AssertFalse (list.Contains (first), "list no longer contains first");
			} finally {
				DisposeJavaObject (list);
			}
		}

		static void JavaList_InheritedInterfaceUsesExplicitInvoker ()
		{
			using var holder = new RawInterfaceCollectionHolder ();
			var list = holder.CreateInheritedList ();
			try {
				AssertWrapperType (list, typeof (JavaList<>), typeof (IExtendedValueProvider));
				AssertEqual (2, list.Count, "inherited list count");
				AssertExtendedInterfacePeer (list [0], 33, 333);
				AssertExtendedInterfacePeer (list [1], 44, 444);
			} finally {
				DisposeJavaObject (list);
			}
		}

		static void JavaCollection_InterfaceElementsSupportOperationsAndRoundTrip ()
		{
			using var holder = new RawInterfaceCollectionHolder ();
			var collection = holder.CreateCollection ();
			try {
				AssertWrapperType (collection, typeof (JavaCollection<>), typeof (IValueProvider));
				AssertEqual (3, collection.Count, "collection count");

				var values = new IValueProvider [3];
				collection.CopyTo (values, 0);
				var first = values [0];
				var second = values [1];
				AssertBaseInterfacePeer (first, 11);
				AssertBaseInterfacePeer (second, 22);
				AssertDistinctJavaObjects (first, second);
				AssertNull (values [2], "null collection element");

				collection.Add (first);
				AssertEqual (4, collection.Count, "collection count after add");
				AssertTrue (collection.Contains (first), "collection contains first");
				AssertTrue (collection.Contains (null), "collection contains null");
				AssertSame (first, GetElement (collection, 3), "collection enumeration");

				var roundTrip = holder.RoundTripCollection (collection);
				try {
					AssertWrapperType (roundTrip, typeof (JavaCollection<>), typeof (IValueProvider));
					AssertSameJavaObject (collection, roundTrip);
					AssertSame (first, GetElement (roundTrip, 0), "round-tripped collection element");
				} finally {
					DisposeIfDistinct (collection, roundTrip);
				}

				collection.Clear ();
				AssertEqual (0, collection.Count, "collection count after clear");
			} finally {
				DisposeJavaObject (collection);
			}
		}

		static void JavaDictionary_InterfaceKeysSupportOperationsAndRoundTrip ()
		{
			using var holder = new RawInterfaceCollectionHolder ();
			var dictionary = holder.CreateKeyDictionary ();
			try {
				AssertWrapperType (dictionary, typeof (JavaDictionary<,>), typeof (IValueProvider), typeof (string));
				AssertEqual (3, dictionary.Count, "key dictionary count");

				var first = holder.GetFirst ();
				var second = holder.GetSecond ();
				AssertBaseInterfacePeer (first, 11);
				AssertBaseInterfacePeer (second, 22);
				AssertDistinctJavaObjects (first, second);
				AssertTrue (dictionary.ContainsKey (first), "dictionary contains first key");
				AssertTrue (dictionary.ContainsKey (null), "dictionary contains null key");
				AssertEqual ("first", dictionary [first], "first key value");
				AssertEqual ("null", dictionary [null], "null key value");
				AssertSame (first, holder.GetFirst (), "repeated key lookup");
				AssertKeyDictionaryEnumeration (dictionary, first, second);

				var roundTrip = holder.RoundTripKeyDictionary (dictionary);
				try {
					AssertWrapperType (roundTrip, typeof (JavaDictionary<,>), typeof (IValueProvider), typeof (string));
					AssertSameJavaObject (dictionary, roundTrip);
					AssertEqual ("second", roundTrip [second], "round-tripped key value");
				} finally {
					DisposeIfDistinct (dictionary, roundTrip);
				}

				AssertTrue (dictionary.Remove (first), "remove interface key");
				AssertFalse (dictionary.ContainsKey (first), "removed interface key");
			} finally {
				DisposeJavaObject (dictionary);
			}
		}

		static void JavaDictionary_InterfaceValuesPreserveDuplicatesAndRoundTrip ()
		{
			using var holder = new RawInterfaceCollectionHolder ();
			var dictionary = holder.CreateValueDictionary ();
			try {
				AssertWrapperType (dictionary, typeof (JavaDictionary<,>), typeof (string), typeof (IValueProvider));
				AssertEqual (4, dictionary.Count, "value dictionary count");

				var first = dictionary ["first"];
				var duplicate = dictionary ["duplicate"];
				var second = dictionary ["second"];
				AssertBaseInterfacePeer (first, 11);
				AssertBaseInterfacePeer (second, 22);
				AssertSame (first, duplicate, "duplicate dictionary value");
				AssertSame (first, dictionary ["first"], "repeated value lookup");
				AssertDistinctJavaObjects (first, second);
				AssertNull (dictionary ["null"], "null dictionary value");
				AssertSequence ([11, 11, 22], GetRawDictionaryValues (dictionary), "dictionary value enumeration");

				dictionary.Add ("added", second);
				AssertSame (second, dictionary ["added"], "added dictionary value");

				var roundTrip = holder.RoundTripValueDictionary (dictionary);
				try {
					AssertWrapperType (roundTrip, typeof (JavaDictionary<,>), typeof (string), typeof (IValueProvider));
					AssertSameJavaObject (dictionary, roundTrip);
					AssertSame (first, roundTrip ["duplicate"], "round-tripped dictionary value");
				} finally {
					DisposeIfDistinct (dictionary, roundTrip);
				}

				AssertTrue (dictionary.Remove ("first"), "remove string key");
				AssertFalse (dictionary.ContainsKey ("first"), "removed string key");
			} finally {
				DisposeJavaObject (dictionary);
			}
		}

		static void JavaDictionary_InterfaceKeysAndValuesPreserveIdentityAndRoundTrip ()
		{
			using var holder = new RawInterfaceCollectionHolder ();
			var dictionary = holder.CreateInterfaceDictionary ();
			try {
				AssertWrapperType (dictionary, typeof (JavaDictionary<,>), typeof (IValueProvider), typeof (IValueProvider));
				AssertEqual (3, dictionary.Count, "interface dictionary count");

				var first = holder.GetFirst ();
				var second = holder.GetSecond ();
				AssertBaseInterfacePeer (first, 11);
				AssertBaseInterfacePeer (second, 22);
				AssertSame (second, dictionary [first], "interface dictionary first value");
				AssertSame (first, dictionary [second], "interface dictionary second value");
				AssertNull (dictionary [null], "interface dictionary null value");
				AssertTrue (dictionary.ContainsKey (first), "interface dictionary contains first");
				AssertTrue (ContainsRawPair (dictionary, first, second), "interface dictionary enumeration");

				var roundTrip = holder.RoundTripInterfaceDictionary (dictionary);
				try {
					AssertWrapperType (roundTrip, typeof (JavaDictionary<,>), typeof (IValueProvider), typeof (IValueProvider));
					AssertSameJavaObject (dictionary, roundTrip);
					AssertSame (second, roundTrip [first], "round-tripped interface dictionary value");
				} finally {
					DisposeIfDistinct (dictionary, roundTrip);
				}

				AssertTrue (dictionary.Remove (second), "remove interface dictionary key");
				AssertFalse (dictionary.ContainsKey (second), "removed interface dictionary key");
			} finally {
				DisposeJavaObject (dictionary);
			}
		}

		static void AssertBaseInterfacePeer (IValueProvider peer, int expectedValue)
		{
			AssertNotNull (peer, "base interface peer");
			AssertEqual (expectedValue, peer.Value, "base interface value");
			AssertEqual (typeof (IValueProviderInvoker), peer.GetType (), "base interface invoker");
			AssertFalse (peer is IExtendedValueProvider, "base peer must not implement the derived interface");
		}

		static void AssertExtendedInterfacePeer (IExtendedValueProvider peer, int expectedValue, int expectedOtherValue)
		{
			AssertNotNull (peer, "extended interface peer");
			AssertEqual (expectedValue, peer.Value, "extended interface value");
			AssertEqual (expectedOtherValue, peer.OtherValue, "extended interface other value");
			AssertEqual (typeof (IExtendedValueProviderInvoker), peer.GetType (), "extended interface invoker");
		}

		static void AssertWrapperType (object wrapper, Type expectedGenericDefinition, params Type [] expectedArguments)
		{
			var wrapperType = wrapper.GetType ();
			AssertTrue (wrapperType.IsGenericType, "wrapper must be generic");
			AssertEqual (expectedGenericDefinition, wrapperType.GetGenericTypeDefinition (), "wrapper generic definition");
			AssertEqual (expectedArguments.Length, wrapperType.GenericTypeArguments.Length, "wrapper generic argument count");
			for (int i = 0; i < expectedArguments.Length; i++) {
				AssertEqual (expectedArguments [i], wrapperType.GenericTypeArguments [i], $"wrapper generic argument {i}");
			}
		}

		static T ConvertJavaValue<T> (IntPtr handle, JniHandleOwnership transfer = JniHandleOwnership.TransferLocalRef)
		{
			return (T) InvokeJavaConvertFromJniHandle (typeof (T), handle, transfer);
		}

		[DynamicDependency ("FromJniHandle", "Java.Interop.JavaConvert", "Mono.Android")]
		static object InvokeJavaConvertFromJniHandle (Type targetType, IntPtr handle, JniHandleOwnership transfer)
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

			var value = method.Invoke (null, [handle, transfer, targetType]);
			if (value == null) {
				throw new InvalidOperationException ($"JavaConvert returned null for target type '{targetType}'.");
			}
			return value;
		}

		static void AssertKeyDictionaryEnumeration (
			IDictionary<IValueProvider, string> dictionary,
			IValueProvider first,
			IValueProvider second)
		{
			int count = 0;
			bool foundFirst = false;
			bool foundSecond = false;
			bool foundNull = false;
			VisitDictionaryEntries (dictionary, (keyHandle, valueHandle) => {
				var key = keyHandle == IntPtr.Zero
					? null
					: ConvertJavaValue<IValueProvider> (keyHandle, JniHandleOwnership.DoNotTransfer);
				var value = valueHandle == IntPtr.Zero
					? null
					: JNIEnv.GetString (valueHandle, JniHandleOwnership.DoNotTransfer);
				count++;
				if (key == null) {
					AssertEqual ("null", value, "raw null key value");
					foundNull = true;
				} else if (ReferenceEquals (key, first)) {
					AssertEqual ("first", value, "raw first key value");
					foundFirst = true;
				} else if (ReferenceEquals (key, second)) {
					AssertEqual ("second", value, "raw second key value");
					foundSecond = true;
				} else {
					throw new InvalidOperationException ($"Unexpected raw dictionary key value '{key.Value}'.");
				}
			});
			AssertEqual (3, count, "raw key dictionary entry count");
			AssertTrue (foundFirst, "raw first key entry");
			AssertTrue (foundSecond, "raw second key entry");
			AssertTrue (foundNull, "raw null key entry");
		}

		static IValueProvider GetElement (ICollection<IValueProvider> values, int index)
		{
			int current = 0;
			foreach (var value in values) {
				if (current == index) {
					return value;
				}
				current++;
			}
			throw new InvalidOperationException ($"Collection element {index} was not found.");
		}

		static int [] GetValues (IEnumerable<IValueProvider> peers)
		{
			var values = new List<int> ();
			foreach (var peer in peers) {
				if (peer != null) {
					values.Add (peer.Value);
				}
			}
			return values.ToArray ();
		}

		static int [] GetRawDictionaryValues (IDictionary<string, IValueProvider> dictionary)
		{
			var values = new List<int> ();
			bool foundNull = false;
			VisitDictionaryEntries (dictionary, (keyHandle, valueHandle) => {
				var value = valueHandle == IntPtr.Zero
					? null
					: ConvertJavaValue<IValueProvider> (valueHandle, JniHandleOwnership.DoNotTransfer);
				if (value == null) {
					foundNull = true;
				} else {
					values.Add (value.Value);
				}
			});
			AssertTrue (foundNull, "raw null dictionary value");
			return values.ToArray ();
		}

		static bool ContainsRawPair (
			IDictionary<IValueProvider, IValueProvider> dictionary,
			IValueProvider expectedKey,
			IValueProvider expectedValue)
		{
			int count = 0;
			bool found = false;
			VisitDictionaryEntries (dictionary, (keyHandle, valueHandle) => {
				var key = keyHandle == IntPtr.Zero
					? null
					: ConvertJavaValue<IValueProvider> (keyHandle, JniHandleOwnership.DoNotTransfer);
				var value = valueHandle == IntPtr.Zero
					? null
					: ConvertJavaValue<IValueProvider> (valueHandle, JniHandleOwnership.DoNotTransfer);
				count++;
				if (ReferenceEquals (key, expectedKey) && ReferenceEquals (value, expectedValue)) {
					found = true;
				}
			});
			AssertEqual (3, count, "raw interface dictionary entry count");
			return found;
		}

		static void VisitDictionaryEntries (object dictionary, Action<IntPtr, IntPtr> visitor)
		{
			JniObjectReference mapClass = default;
			JniObjectReference setClass = default;
			JniObjectReference iteratorClass = default;
			JniObjectReference entryClass = default;
			IntPtr entrySet = IntPtr.Zero;
			IntPtr iterator = IntPtr.Zero;
			try {
				mapClass = JniEnvironment.Types.FindClass ("java/util/Map");
				setClass = JniEnvironment.Types.FindClass ("java/util/Set");
				iteratorClass = JniEnvironment.Types.FindClass ("java/util/Iterator");
				entryClass = JniEnvironment.Types.FindClass ("java/util/Map$Entry");
				var entrySetMethod = JNIEnv.GetMethodID (mapClass.Handle, "entrySet", "()Ljava/util/Set;");
				var iteratorMethod = JNIEnv.GetMethodID (setClass.Handle, "iterator", "()Ljava/util/Iterator;");
				var hasNextMethod = JNIEnv.GetMethodID (iteratorClass.Handle, "hasNext", "()Z");
				var nextMethod = JNIEnv.GetMethodID (iteratorClass.Handle, "next", "()Ljava/lang/Object;");
				var getKeyMethod = JNIEnv.GetMethodID (entryClass.Handle, "getKey", "()Ljava/lang/Object;");
				var getValueMethod = JNIEnv.GetMethodID (entryClass.Handle, "getValue", "()Ljava/lang/Object;");
				var dictionaryPeer = (IJavaObject) dictionary;

				entrySet = JNIEnv.CallObjectMethod (dictionaryPeer.Handle, entrySetMethod);
				iterator = JNIEnv.CallObjectMethod (entrySet, iteratorMethod);
				while (JNIEnv.CallBooleanMethod (iterator, hasNextMethod)) {
					var entry = JNIEnv.CallObjectMethod (iterator, nextMethod);
					var key = IntPtr.Zero;
					var value = IntPtr.Zero;
					try {
						key = JNIEnv.CallObjectMethod (entry, getKeyMethod);
						value = JNIEnv.CallObjectMethod (entry, getValueMethod);
						visitor (key, value);
					} finally {
						DeleteLocalRef (ref value);
						DeleteLocalRef (ref key);
						DeleteLocalRef (ref entry);
					}
				}
				GC.KeepAlive (dictionary);
			} finally {
				DeleteLocalRef (ref iterator);
				DeleteLocalRef (ref entrySet);
				JniObjectReference.Dispose (ref entryClass);
				JniObjectReference.Dispose (ref iteratorClass);
				JniObjectReference.Dispose (ref setClass);
				JniObjectReference.Dispose (ref mapClass);
			}
		}

		static void DeleteLocalRef (ref IntPtr handle)
		{
			if (handle != IntPtr.Zero) {
				JNIEnv.DeleteLocalRef (handle);
				handle = IntPtr.Zero;
			}
		}

		static void AssertSequence (int [] expected, int [] actual, string message)
		{
			AssertEqual (expected.Length, actual.Length, $"{message} length");
			for (int i = 0; i < expected.Length; i++) {
				AssertEqual (expected [i], actual [i], $"{message} element {i}");
			}
		}

		static void AssertSameJavaObject (object expected, object actual)
		{
			var expectedPeer = (IJavaObject) expected;
			var actualPeer = (IJavaObject) actual;
			AssertTrue (
				JNIEnv.IsSameObject (expectedPeer.Handle, actualPeer.Handle),
				$"expected identical Java peers; expected '{expected.GetType ()}' at '{expectedPeer.Handle}', " +
					$"actual '{actual.GetType ()}' at '{actualPeer.Handle}'");
		}

		static void AssertDistinctJavaObjects (object first, object second)
		{
			var firstPeer = (IJavaObject) first;
			var secondPeer = (IJavaObject) second;
			AssertFalse (
				JNIEnv.IsSameObject (firstPeer.Handle, secondPeer.Handle),
				$"expected distinct Java peers; first '{first.GetType ()}' at '{firstPeer.Handle}', " +
					$"second '{second.GetType ()}' at '{secondPeer.Handle}'");
		}

		static void AssertTrue (bool value, string message)
		{
			if (!value) {
				throw new InvalidOperationException ($"Assertion failed: {message}.");
			}
		}

		static void AssertFalse (bool value, string message)
		{
			AssertTrue (!value, message);
		}

		static void AssertNull (object value, string message)
		{
			if (value != null) {
				throw new InvalidOperationException ($"Assertion failed: {message}; expected null, found '{value}'.");
			}
		}

		static void AssertNotNull (object value, string message)
		{
			if (value == null) {
				throw new InvalidOperationException ($"Assertion failed: {message}; value was null.");
			}
		}

		static void AssertSame (object expected, object actual, string message)
		{
			if (!ReferenceEquals (expected, actual)) {
				throw new InvalidOperationException ($"Assertion failed: {message}; managed references differ.");
			}
		}

		static void AssertEqual<T> (T expected, T actual, string message)
		{
			if (!EqualityComparer<T>.Default.Equals (expected, actual)) {
				throw new InvalidOperationException ($"Assertion failed: {message}; expected '{expected}', found '{actual}'.");
			}
		}

		static void DisposeIfDistinct (object owner, object value)
		{
			if (!ReferenceEquals (owner, value)) {
				DisposeJavaObject (value);
			}
		}

		static void DisposeJavaObject (object value)
		{
			if (value is IDisposable disposable) {
				disposable.Dispose ();
			}
		}

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

			public IDictionary<IValueProvider, IValueProvider> RoundTripInterfaceDictionary (
				IDictionary<IValueProvider, IValueProvider> value)
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
		}
	}
}
