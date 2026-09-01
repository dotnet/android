using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Android.Runtime;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests
{
	[TestFixture]
	[Category ("InterfaceCollections")]
	public class InterfaceCollectionMarshallingTests
	{
		const string CollectionSignature = "()Ljava/util/Collection;";
		const string DictionarySignature = "()Ljava/util/Map;";
		const string ListSignature = "()Ljava/util/List;";
		const string RoundTripCollectionSignature = "(Ljava/util/Collection;)Ljava/util/Collection;";
		const string RoundTripDictionarySignature = "(Ljava/util/Map;)Ljava/util/Map;";
		const string RoundTripListSignature = "(Ljava/util/List;)Ljava/util/List;";

		[Test]
		public void JavaList_InterfaceElementsPreserveIdentityAndRoundTrip ()
		{
			using var holder = new RawInterfaceCollectionHolder ();
			var targetType = typeof (IList<global::Net.Dot.Android.Test.IValueProvider>);
			var list = holder.Create<IList<global::Net.Dot.Android.Test.IValueProvider>> ("createList", ListSignature, targetType);
			try {
				AssertWrapperType (list, typeof (JavaList<>), typeof (global::Net.Dot.Android.Test.IValueProvider));
				Assert.AreEqual (4, list.Count);

				var first = list [0];
				var duplicate = list [1];
				var second = list [2];

				AssertBaseInterfacePeer (first, 11);
				AssertBaseInterfacePeer (second, 22);
				Assert.AreSame (first, duplicate);
				Assert.AreSame (first, list [0]);
				AssertSameJavaObject (first, duplicate);
				AssertDistinctJavaObjects (first, second);
				Assert.IsNull (list [3]);
				Assert.IsTrue (list.Contains (first));
				Assert.IsTrue (list.Contains (null));

				list.Add (second);
				Assert.AreEqual (5, list.Count);
				Assert.AreSame (second, list [4]);
				CollectionAssert.AreEqual (new [] { 11, 11, 22, 22 }, list.Where (value => value != null).Select (value => value.Value));

				var roundTrip = holder.RoundTrip<IList<global::Net.Dot.Android.Test.IValueProvider>> (
					"roundTripList",
					RoundTripListSignature,
					list,
					targetType);
				try {
					AssertWrapperType (roundTrip, typeof (JavaList<>), typeof (global::Net.Dot.Android.Test.IValueProvider));
					AssertSameJavaObject (list, roundTrip);
					Assert.AreSame (first, roundTrip [0]);
				} finally {
					DisposeIfDistinct (list, roundTrip);
				}

				Assert.IsTrue (list.Remove (first));
				Assert.IsTrue (list.Contains (first));
				Assert.IsTrue (list.Remove (first));
				Assert.IsFalse (list.Contains (first));
			} finally {
				DisposeJavaObject (list);
			}
		}

		[Test]
		public void JavaList_InheritedInterfaceUsesExplicitInvoker ()
		{
			using var holder = new RawInterfaceCollectionHolder ();
			var targetType = typeof (IList<global::Net.Dot.Android.Test.IExtendedValueProvider>);
			var list = holder.Create<IList<global::Net.Dot.Android.Test.IExtendedValueProvider>> (
				"createInheritedList",
				ListSignature,
				targetType);
			try {
				AssertWrapperType (list, typeof (JavaList<>), typeof (global::Net.Dot.Android.Test.IExtendedValueProvider));
				Assert.AreEqual (2, list.Count);
				AssertExtendedInterfacePeer (list [0], 33, 333);
				AssertExtendedInterfacePeer (list [1], 44, 444);
			} finally {
				DisposeJavaObject (list);
			}
		}

		[Test]
		public void JavaCollection_InterfaceElementsSupportOperationsAndRoundTrip ()
		{
			using var holder = new RawInterfaceCollectionHolder ();
			var targetType = typeof (ICollection<global::Net.Dot.Android.Test.IValueProvider>);
			var collection = holder.Create<ICollection<global::Net.Dot.Android.Test.IValueProvider>> (
				"createCollection",
				CollectionSignature,
				targetType);
			try {
				AssertWrapperType (collection, typeof (JavaCollection<>), typeof (global::Net.Dot.Android.Test.IValueProvider));
				Assert.AreEqual (3, collection.Count);

				var values = collection.ToArray ();
				var first = values [0];
				var second = values [1];
				AssertBaseInterfacePeer (first, 11);
				AssertBaseInterfacePeer (second, 22);
				AssertDistinctJavaObjects (first, second);
				Assert.IsNull (values [2]);

				collection.Add (first);
				Assert.AreEqual (4, collection.Count);
				Assert.IsTrue (collection.Contains (first));
				Assert.IsTrue (collection.Contains (null));
				Assert.AreSame (first, collection.ToArray () [3]);

				var roundTrip = holder.RoundTrip<ICollection<global::Net.Dot.Android.Test.IValueProvider>> (
					"roundTripCollection",
					RoundTripCollectionSignature,
					collection,
					targetType);
				try {
					AssertWrapperType (roundTrip, typeof (JavaCollection<>), typeof (global::Net.Dot.Android.Test.IValueProvider));
					AssertSameJavaObject (collection, roundTrip);
					Assert.AreSame (first, roundTrip.First ());
				} finally {
					DisposeIfDistinct (collection, roundTrip);
				}

				collection.Clear ();
				Assert.AreEqual (0, collection.Count);
			} finally {
				DisposeJavaObject (collection);
			}
		}

		[Test]
		public void JavaDictionary_InterfaceKeysSupportOperationsAndRoundTrip ()
		{
			using var holder = new RawInterfaceCollectionHolder ();
			var targetType = typeof (IDictionary<global::Net.Dot.Android.Test.IValueProvider, string>);
			var dictionary = holder.Create<IDictionary<global::Net.Dot.Android.Test.IValueProvider, string>> (
				"createKeyDictionary",
				DictionarySignature,
				targetType);
			try {
				AssertWrapperType (
					dictionary,
					typeof (JavaDictionary<,>),
					typeof (global::Net.Dot.Android.Test.IValueProvider),
					typeof (string));
				Assert.AreEqual (3, dictionary.Count);

				var first = dictionary.Keys.Single (key => key != null && key.Value == 11);
				var second = dictionary.Keys.Single (key => key != null && key.Value == 22);
				AssertBaseInterfacePeer (first, 11);
				AssertBaseInterfacePeer (second, 22);
				AssertDistinctJavaObjects (first, second);
				Assert.IsTrue (dictionary.ContainsKey (first));
				Assert.IsTrue (dictionary.ContainsKey (null));
				Assert.AreEqual ("first", dictionary [first]);
				Assert.AreEqual ("null", dictionary [null]);
				Assert.AreSame (first, dictionary.Keys.Single (key => key != null && key.Value == 11));

				var roundTrip = holder.RoundTrip<IDictionary<global::Net.Dot.Android.Test.IValueProvider, string>> (
					"roundTripKeyDictionary",
					RoundTripDictionarySignature,
					dictionary,
					targetType);
				try {
					AssertWrapperType (
						roundTrip,
						typeof (JavaDictionary<,>),
						typeof (global::Net.Dot.Android.Test.IValueProvider),
						typeof (string));
					AssertSameJavaObject (dictionary, roundTrip);
					Assert.AreEqual ("second", roundTrip [second]);
				} finally {
					DisposeIfDistinct (dictionary, roundTrip);
				}

				Assert.IsTrue (dictionary.Remove (first));
				Assert.IsFalse (dictionary.ContainsKey (first));
			} finally {
				DisposeJavaObject (dictionary);
			}
		}

		[Test]
		public void JavaDictionary_InterfaceValuesPreserveDuplicatesAndRoundTrip ()
		{
			using var holder = new RawInterfaceCollectionHolder ();
			var targetType = typeof (IDictionary<string, global::Net.Dot.Android.Test.IValueProvider>);
			var dictionary = holder.Create<IDictionary<string, global::Net.Dot.Android.Test.IValueProvider>> (
				"createValueDictionary",
				DictionarySignature,
				targetType);
			try {
				AssertWrapperType (
					dictionary,
					typeof (JavaDictionary<,>),
					typeof (string),
					typeof (global::Net.Dot.Android.Test.IValueProvider));
				Assert.AreEqual (4, dictionary.Count);

				var first = dictionary ["first"];
				var duplicate = dictionary ["duplicate"];
				var second = dictionary ["second"];
				AssertBaseInterfacePeer (first, 11);
				AssertBaseInterfacePeer (second, 22);
				Assert.AreSame (first, duplicate);
				Assert.AreSame (first, dictionary ["first"]);
				AssertDistinctJavaObjects (first, second);
				Assert.IsNull (dictionary ["null"]);
				CollectionAssert.AreEquivalent (new [] { 11, 11, 22 }, dictionary.Values.Where (value => value != null).Select (value => value.Value));

				dictionary.Add ("added", second);
				Assert.AreSame (second, dictionary ["added"]);

				var roundTrip = holder.RoundTrip<IDictionary<string, global::Net.Dot.Android.Test.IValueProvider>> (
					"roundTripValueDictionary",
					RoundTripDictionarySignature,
					dictionary,
					targetType);
				try {
					AssertWrapperType (
						roundTrip,
						typeof (JavaDictionary<,>),
						typeof (string),
						typeof (global::Net.Dot.Android.Test.IValueProvider));
					AssertSameJavaObject (dictionary, roundTrip);
					Assert.AreSame (first, roundTrip ["duplicate"]);
				} finally {
					DisposeIfDistinct (dictionary, roundTrip);
				}

				Assert.IsTrue (dictionary.Remove ("first"));
				Assert.IsFalse (dictionary.ContainsKey ("first"));
			} finally {
				DisposeJavaObject (dictionary);
			}
		}

		[Test]
		public void JavaDictionary_InterfaceKeysAndValuesPreserveIdentityAndRoundTrip ()
		{
			using var holder = new RawInterfaceCollectionHolder ();
			var targetType = typeof (IDictionary<
				global::Net.Dot.Android.Test.IValueProvider,
				global::Net.Dot.Android.Test.IValueProvider>);
			var dictionary = holder.Create<IDictionary<
				global::Net.Dot.Android.Test.IValueProvider,
				global::Net.Dot.Android.Test.IValueProvider>> (
					"createInterfaceDictionary",
					DictionarySignature,
					targetType);
			try {
				AssertWrapperType (
					dictionary,
					typeof (JavaDictionary<,>),
					typeof (global::Net.Dot.Android.Test.IValueProvider),
					typeof (global::Net.Dot.Android.Test.IValueProvider));
				Assert.AreEqual (3, dictionary.Count);

				var first = dictionary.Keys.Single (key => key != null && key.Value == 11);
				var second = dictionary.Keys.Single (key => key != null && key.Value == 22);
				AssertBaseInterfacePeer (first, 11);
				AssertBaseInterfacePeer (second, 22);
				Assert.AreSame (second, dictionary [first]);
				Assert.AreSame (first, dictionary [second]);
				Assert.IsNull (dictionary [null]);
				Assert.IsTrue (dictionary.ContainsKey (first));
				Assert.IsTrue (dictionary.Any (pair => ReferenceEquals (pair.Key, first) && ReferenceEquals (pair.Value, second)));

				var roundTrip = holder.RoundTrip<IDictionary<
					global::Net.Dot.Android.Test.IValueProvider,
					global::Net.Dot.Android.Test.IValueProvider>> (
						"roundTripInterfaceDictionary",
						RoundTripDictionarySignature,
						dictionary,
						targetType);
				try {
					AssertWrapperType (
						roundTrip,
						typeof (JavaDictionary<,>),
						typeof (global::Net.Dot.Android.Test.IValueProvider),
						typeof (global::Net.Dot.Android.Test.IValueProvider));
					AssertSameJavaObject (dictionary, roundTrip);
					Assert.AreSame (second, roundTrip [first]);
				} finally {
					DisposeIfDistinct (dictionary, roundTrip);
				}

				Assert.IsTrue (dictionary.Remove (second));
				Assert.IsFalse (dictionary.ContainsKey (second));
			} finally {
				DisposeJavaObject (dictionary);
			}
		}

		static void AssertBaseInterfacePeer (global::Net.Dot.Android.Test.IValueProvider peer, int expectedValue)
		{
			Assert.IsNotNull (peer);
			Assert.AreEqual (expectedValue, peer.Value);
			Assert.AreEqual (typeof (global::Net.Dot.Android.Test.IValueProviderInvoker), peer.GetType ());
			Assert.IsFalse (peer is global::Net.Dot.Android.Test.IExtendedValueProvider);
		}

		static void AssertExtendedInterfacePeer (
			global::Net.Dot.Android.Test.IExtendedValueProvider peer,
			int expectedValue,
			int expectedOtherValue)
		{
			Assert.IsNotNull (peer);
			Assert.AreEqual (expectedValue, peer.Value);
			Assert.AreEqual (expectedOtherValue, peer.OtherValue);
			Assert.AreEqual (typeof (global::Net.Dot.Android.Test.IExtendedValueProviderInvoker), peer.GetType ());
		}

		static void AssertWrapperType (object wrapper, Type expectedGenericDefinition, params Type [] expectedArguments)
		{
			var wrapperType = wrapper.GetType ();
			Assert.IsTrue (wrapperType.IsGenericType);
			Assert.AreEqual (expectedGenericDefinition, wrapperType.GetGenericTypeDefinition ());
			CollectionAssert.AreEqual (expectedArguments, wrapperType.GenericTypeArguments);
		}

		static object InvokeJavaConvertFromJniHandle (Type targetType, IntPtr handle)
		{
			var javaConvert = typeof (Java.Lang.Object).Assembly.GetType ("Java.Interop.JavaConvert");
			Assert.IsNotNull (javaConvert);

			var method = javaConvert.GetMethod (
				"FromJniHandle",
				BindingFlags.Public | BindingFlags.Static,
				binder: null,
				types: new [] { typeof (IntPtr), typeof (JniHandleOwnership), typeof (Type) },
				modifiers: null);
			Assert.IsNotNull (method);

			var value = method.Invoke (null, new object [] { handle, JniHandleOwnership.TransferLocalRef, targetType });
			Assert.IsNotNull (value);
			return value;
		}

		static void AssertSameJavaObject (object expected, object actual)
		{
			var expectedPeer = (IJavaObject) expected;
			var actualPeer = (IJavaObject) actual;
			Assert.IsTrue (JNIEnv.IsSameObject (expectedPeer.Handle, actualPeer.Handle));
		}

		static void AssertDistinctJavaObjects (object first, object second)
		{
			var firstPeer = (IJavaObject) first;
			var secondPeer = (IJavaObject) second;
			Assert.IsFalse (JNIEnv.IsSameObject (firstPeer.Handle, secondPeer.Handle));
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
			const string JniName = "net/dot/android/test/InterfaceCollectionHolder";

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

			public T Create<T> (string methodName, string signature, Type targetType)
			{
				var method = GetMethod (methodName, signature);
				var handle = JNIEnv.CallObjectMethod (holder.Handle, method);
				return (T) InvokeJavaConvertFromJniHandle (targetType, handle);
			}

			public T RoundTrip<T> (string methodName, string signature, object value, Type targetType)
			{
				var method = GetMethod (methodName, signature);
				var peer = (IJavaObject) value;
				var handle = JNIEnv.CallObjectMethod (holder.Handle, method, new JValue (peer.Handle));
				GC.KeepAlive (value);
				return (T) InvokeJavaConvertFromJniHandle (targetType, handle);
			}

			public void Dispose ()
			{
				holder.Dispose ();
			}

			IntPtr GetMethod (string methodName, string signature)
			{
				var holderClass = JNIEnv.GetObjectClass (holder.Handle);
				try {
					return JNIEnv.GetMethodID (holderClass, methodName, signature);
				} finally {
					JNIEnv.DeleteLocalRef (holderClass);
				}
			}
		}
	}
}
