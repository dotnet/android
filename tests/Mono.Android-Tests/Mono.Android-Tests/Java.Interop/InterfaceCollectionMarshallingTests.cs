using System;
using System.Collections.Generic;
using System.Linq;

using Android.Runtime;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests
{
	[TestFixture]
	[Category ("InterfaceCollections")]
	public class InterfaceCollectionMarshallingTests
	{
		[Test]
		public void JavaList_InterfaceElementsPreserveIdentityAndRoundTrip ()
		{
			using var holder = new global::Net.Dot.Android.Test.InterfaceCollectionHolder ();
			var list = holder.CreateList ();
			try {
				Assert.AreEqual (typeof (JavaList<global::Net.Dot.Android.Test.IValueProvider>), list.GetType ());
				Assert.AreEqual (4, list.Count);

				var first = list [0];
				var duplicate = list [1];
				var second = list [2];

				AssertInterfacePeer (first, 11);
				AssertInterfacePeer (second, 22);
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

				var roundTrip = holder.RoundTripList (list);
				try {
					Assert.AreEqual (typeof (JavaList<global::Net.Dot.Android.Test.IValueProvider>), roundTrip.GetType ());
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
			using var holder = new global::Net.Dot.Android.Test.InterfaceCollectionHolder ();
			var list = holder.CreateInheritedList ();
			try {
				Assert.AreEqual (typeof (JavaList<global::Net.Dot.Android.Test.IExtendedValueProvider>), list.GetType ());
				Assert.AreEqual (2, list.Count);
				AssertInterfacePeer (list [0], 11);
				AssertInterfacePeer (list [1], 22);
				Assert.AreEqual (111, list [0].OtherValue);
				Assert.AreEqual (222, list [1].OtherValue);
				StringAssert.EndsWith ("IExtendedValueProviderInvoker", list [0].GetType ().FullName);
			} finally {
				DisposeJavaObject (list);
			}
		}

		[Test]
		public void JavaCollection_InterfaceElementsSupportOperationsAndRoundTrip ()
		{
			using var holder = new global::Net.Dot.Android.Test.InterfaceCollectionHolder ();
			var collection = holder.CreateCollection ();
			try {
				Assert.AreEqual (typeof (JavaCollection<global::Net.Dot.Android.Test.IValueProvider>), collection.GetType ());
				Assert.AreEqual (3, collection.Count);

				var values = collection.ToArray ();
				var first = values [0];
				var second = values [1];
				AssertInterfacePeer (first, 11);
				AssertInterfacePeer (second, 22);
				AssertDistinctJavaObjects (first, second);
				Assert.IsNull (values [2]);

				collection.Add (first);
				Assert.AreEqual (4, collection.Count);
				Assert.IsTrue (collection.Contains (first));
				Assert.IsTrue (collection.Contains (null));
				Assert.AreSame (first, collection.ToArray () [3]);

				var roundTrip = holder.RoundTripCollection (collection);
				try {
					Assert.AreEqual (typeof (JavaCollection<global::Net.Dot.Android.Test.IValueProvider>), roundTrip.GetType ());
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
			using var holder = new global::Net.Dot.Android.Test.InterfaceCollectionHolder ();
			var dictionary = holder.CreateKeyDictionary ();
			try {
				Assert.AreEqual (
					typeof (JavaDictionary<global::Net.Dot.Android.Test.IValueProvider, string>),
					dictionary.GetType ());
				Assert.AreEqual (3, dictionary.Count);

				var first = dictionary.Keys.Single (key => key != null && key.Value == 11);
				var second = dictionary.Keys.Single (key => key != null && key.Value == 22);
				AssertInterfacePeer (first, 11);
				AssertInterfacePeer (second, 22);
				AssertDistinctJavaObjects (first, second);
				Assert.IsTrue (dictionary.ContainsKey (first));
				Assert.IsTrue (dictionary.ContainsKey (null));
				Assert.AreEqual ("first", dictionary [first]);
				Assert.AreEqual ("null", dictionary [null]);
				Assert.AreSame (first, dictionary.Keys.Single (key => key != null && key.Value == 11));

				var roundTrip = holder.RoundTripKeyDictionary (dictionary);
				try {
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
			using var holder = new global::Net.Dot.Android.Test.InterfaceCollectionHolder ();
			var dictionary = holder.CreateValueDictionary ();
			try {
				Assert.AreEqual (
					typeof (JavaDictionary<string, global::Net.Dot.Android.Test.IValueProvider>),
					dictionary.GetType ());
				Assert.AreEqual (4, dictionary.Count);

				var first = dictionary ["first"];
				var duplicate = dictionary ["duplicate"];
				var second = dictionary ["second"];
				AssertInterfacePeer (first, 11);
				AssertInterfacePeer (second, 22);
				Assert.AreSame (first, duplicate);
				Assert.AreSame (first, dictionary ["first"]);
				AssertDistinctJavaObjects (first, second);
				Assert.IsNull (dictionary ["null"]);
				CollectionAssert.AreEquivalent (new [] { 11, 11, 22 }, dictionary.Values.Where (value => value != null).Select (value => value.Value));

				dictionary.Add ("added", second);
				Assert.AreSame (second, dictionary ["added"]);

				var roundTrip = holder.RoundTripValueDictionary (dictionary);
				try {
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
			using var holder = new global::Net.Dot.Android.Test.InterfaceCollectionHolder ();
			var dictionary = holder.CreateInterfaceDictionary ();
			try {
				Assert.AreEqual (
					typeof (JavaDictionary<
						global::Net.Dot.Android.Test.IValueProvider,
						global::Net.Dot.Android.Test.IValueProvider>),
					dictionary.GetType ());
				Assert.AreEqual (3, dictionary.Count);

				var first = dictionary.Keys.Single (key => key != null && key.Value == 11);
				var second = dictionary.Keys.Single (key => key != null && key.Value == 22);
				Assert.AreSame (second, dictionary [first]);
				Assert.AreSame (first, dictionary [second]);
				Assert.IsNull (dictionary [null]);
				Assert.IsTrue (dictionary.ContainsKey (first));
				Assert.IsTrue (dictionary.Any (pair => ReferenceEquals (pair.Key, first) && ReferenceEquals (pair.Value, second)));

				var roundTrip = holder.RoundTripInterfaceDictionary (dictionary);
				try {
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

		static void AssertInterfacePeer (global::Net.Dot.Android.Test.IValueProvider peer, int expectedValue)
		{
			Assert.IsNotNull (peer);
			Assert.AreEqual (expectedValue, peer.Value);
			StringAssert.EndsWith ("Invoker", peer.GetType ().Name);
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
	}
}
