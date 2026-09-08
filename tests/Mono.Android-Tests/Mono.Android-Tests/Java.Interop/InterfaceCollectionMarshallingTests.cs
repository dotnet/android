using System;
using System.Collections.Generic;
using System.Linq;

using Android.Runtime;

using Net.Dot.Android.Test;
using NUnit.Framework;

namespace Java.InteropTests;

[TestFixture]
[Category ("InterfaceCollections")]
public class InterfaceCollectionMarshallingTests
{
	[Test]
	public void JavaList_InterfaceElementsPreserveIdentityAndRoundTrip ()
	{
		using var holder = new RawInterfaceCollectionHolder ();
		var list = holder.CreateList ();
		try {
			AssertWrapperType (list, typeof (JavaList<>), typeof (IValueProvider));
			Assert.AreEqual (4, list.Count, "list count");

			var first = list [0];
			var duplicate = list [1];
			var second = list [2];

			AssertBaseInterfacePeer (first, 11);
			AssertBaseInterfacePeer (second, 22);
			Assert.AreSame (first, duplicate, "duplicate list reference");
			Assert.AreSame (first, list [0], "repeated list lookup");
			AssertSameJavaObject (first, duplicate);
			AssertDistinctJavaObjects (first, second);
			Assert.IsNull (list [3], "null list element");
			Assert.IsTrue (list.Contains (first), "list contains first");
			Assert.IsTrue (list.Contains (null), "list contains null");

			list.Add (second);
			Assert.AreEqual (5, list.Count, "list count after add");
			Assert.AreSame (second, list [4], "added list reference");
			CollectionAssert.AreEqual (
				new [] { 11, 11, 22, 22 },
				list.Where (value => value != null).Select (value => value.Value),
				"list enumeration");

			var roundTrip = holder.RoundTripList (list);
			try {
				AssertWrapperType (roundTrip, typeof (JavaList<>), typeof (IValueProvider));
				AssertSameJavaObject (list, roundTrip);
				Assert.AreSame (first, roundTrip [0], "round-tripped list element");
			} finally {
				DisposeIfDistinct (list, roundTrip);
			}

			Assert.IsTrue (list.Remove (first), "remove first list reference");
			Assert.IsTrue (list.Contains (first), "list retains duplicate");
			Assert.IsTrue (list.Remove (first), "remove duplicate list reference");
			Assert.IsFalse (list.Contains (first), "list no longer contains first");
		} finally {
			DisposeJavaObject (list);
		}
	}

	[Test]
	public void JavaList_InheritedInterfaceUsesExplicitInvoker ()
	{
		using var holder = new RawInterfaceCollectionHolder ();
		var list = holder.CreateInheritedList ();
		try {
			AssertWrapperType (list, typeof (JavaList<>), typeof (IExtendedValueProvider));
			Assert.AreEqual (2, list.Count, "inherited list count");
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
		var collection = holder.CreateCollection ();
		try {
			AssertWrapperType (collection, typeof (JavaCollection<>), typeof (IValueProvider));
			Assert.AreEqual (3, collection.Count, "collection count");

			var values = new IValueProvider [3];
			collection.CopyTo (values, 0);
			var first = values [0];
			var second = values [1];
			AssertBaseInterfacePeer (first, 11);
			AssertBaseInterfacePeer (second, 22);
			AssertDistinctJavaObjects (first, second);
			Assert.IsNull (values [2], "null collection element");

			collection.Add (first);
			Assert.AreEqual (4, collection.Count, "collection count after add");
			Assert.IsTrue (collection.Contains (first), "collection contains first");
			Assert.IsTrue (collection.Contains (null), "collection contains null");
			Assert.AreSame (first, collection.ElementAt (3), "collection enumeration");

			var roundTrip = holder.RoundTripCollection (collection);
			try {
				AssertWrapperType (roundTrip, typeof (JavaCollection<>), typeof (IValueProvider));
				AssertSameJavaObject (collection, roundTrip);
				Assert.AreSame (first, roundTrip.ElementAt (0), "round-tripped collection element");
			} finally {
				DisposeIfDistinct (collection, roundTrip);
			}

			collection.Clear ();
			Assert.AreEqual (0, collection.Count, "collection count after clear");
		} finally {
			DisposeJavaObject (collection);
		}
	}

	[Test]
	public void JavaDictionary_InterfaceKeysSupportOperationsAndRoundTrip ()
	{
		using var holder = new RawInterfaceCollectionHolder ();
		var dictionary = holder.CreateKeyDictionary ();
		try {
			AssertWrapperType (dictionary, typeof (JavaDictionary<,>), typeof (IValueProvider), typeof (string));
			Assert.AreEqual (3, dictionary.Count, "key dictionary count");

			var first = holder.GetFirst ();
			var second = holder.GetSecond ();
			AssertBaseInterfacePeer (first, 11);
			AssertBaseInterfacePeer (second, 22);
			AssertDistinctJavaObjects (first, second);
			Assert.IsTrue (dictionary.ContainsKey (first), "dictionary contains first key");
			Assert.IsTrue (dictionary.ContainsKey (null), "dictionary contains null key");
			Assert.AreEqual ("first", dictionary [first], "first key value");
			Assert.AreEqual ("null", dictionary [null], "null key value");
			Assert.AreSame (first, holder.GetFirst (), "repeated key lookup");
			AssertKeyDictionaryEnumeration (dictionary, first, second);

			var roundTrip = holder.RoundTripKeyDictionary (dictionary);
			try {
				AssertWrapperType (roundTrip, typeof (JavaDictionary<,>), typeof (IValueProvider), typeof (string));
				AssertSameJavaObject (dictionary, roundTrip);
				Assert.AreEqual ("second", roundTrip [second], "round-tripped key value");
			} finally {
				DisposeIfDistinct (dictionary, roundTrip);
			}

			Assert.IsTrue (dictionary.Remove (first), "remove interface key");
			Assert.IsFalse (dictionary.ContainsKey (first), "removed interface key");
		} finally {
			DisposeJavaObject (dictionary);
		}
	}

	[Test]
	public void JavaDictionary_InterfaceValuesPreserveDuplicatesAndRoundTrip ()
	{
		using var holder = new RawInterfaceCollectionHolder ();
		var dictionary = holder.CreateValueDictionary ();
		try {
			AssertWrapperType (dictionary, typeof (JavaDictionary<,>), typeof (string), typeof (IValueProvider));
			Assert.AreEqual (4, dictionary.Count, "value dictionary count");

			var first = dictionary ["first"];
			var duplicate = dictionary ["duplicate"];
			var second = dictionary ["second"];
			AssertBaseInterfacePeer (first, 11);
			AssertBaseInterfacePeer (second, 22);
			Assert.AreSame (first, duplicate, "duplicate dictionary value");
			Assert.AreSame (first, dictionary ["first"], "repeated value lookup");
			AssertDistinctJavaObjects (first, second);
			Assert.IsNull (dictionary ["null"], "null dictionary value");
			CollectionAssert.AreEqual (
				new IValueProvider [] { first, duplicate, second, null },
				dictionary.Select (entry => entry.Value),
				"dictionary value enumeration");

			dictionary.Add ("added", second);
			Assert.AreSame (second, dictionary ["added"], "added dictionary value");

			var roundTrip = holder.RoundTripValueDictionary (dictionary);
			try {
				AssertWrapperType (roundTrip, typeof (JavaDictionary<,>), typeof (string), typeof (IValueProvider));
				AssertSameJavaObject (dictionary, roundTrip);
				Assert.AreSame (first, roundTrip ["duplicate"], "round-tripped dictionary value");
			} finally {
				DisposeIfDistinct (dictionary, roundTrip);
			}

			Assert.IsTrue (dictionary.Remove ("first"), "remove string key");
			Assert.IsFalse (dictionary.ContainsKey ("first"), "removed string key");
		} finally {
			DisposeJavaObject (dictionary);
		}
	}

	[Test]
	public void JavaDictionary_InterfaceKeysAndValuesPreserveIdentityAndRoundTrip ()
	{
		using var holder = new RawInterfaceCollectionHolder ();
		var dictionary = holder.CreateInterfaceDictionary ();
		try {
			AssertWrapperType (dictionary, typeof (JavaDictionary<,>), typeof (IValueProvider), typeof (IValueProvider));
			Assert.AreEqual (3, dictionary.Count, "interface dictionary count");

			var first = holder.GetFirst ();
			var second = holder.GetSecond ();
			AssertBaseInterfacePeer (first, 11);
			AssertBaseInterfacePeer (second, 22);
			Assert.AreSame (second, dictionary [first], "interface dictionary first value");
			Assert.AreSame (first, dictionary [second], "interface dictionary second value");
			Assert.IsNull (dictionary [null], "interface dictionary null value");
			Assert.IsTrue (dictionary.ContainsKey (first), "interface dictionary contains first");
			var entries = dictionary.ToArray ();
			Assert.AreEqual (3, entries.Length, "interface dictionary enumeration count");
			Assert.IsTrue (
				entries.Any (entry => ReferenceEquals (entry.Key, first) && ReferenceEquals (entry.Value, second)),
				"interface dictionary enumeration");
			Assert.IsTrue (entries.Any (entry => entry.Key == null && entry.Value == null), "null dictionary entry");

			var roundTrip = holder.RoundTripInterfaceDictionary (dictionary);
			try {
				AssertWrapperType (roundTrip, typeof (JavaDictionary<,>), typeof (IValueProvider), typeof (IValueProvider));
				AssertSameJavaObject (dictionary, roundTrip);
				Assert.AreSame (second, roundTrip [first], "round-tripped interface dictionary value");
			} finally {
				DisposeIfDistinct (dictionary, roundTrip);
			}

			Assert.IsTrue (dictionary.Remove (second), "remove interface dictionary key");
			Assert.IsFalse (dictionary.ContainsKey (second), "removed interface dictionary key");
		} finally {
			DisposeJavaObject (dictionary);
		}
	}

	static void AssertBaseInterfacePeer (IValueProvider peer, int expectedValue)
	{
		Assert.IsNotNull (peer, "base interface peer");
		Assert.AreEqual (expectedValue, peer.Value, "base interface value");
		Assert.AreEqual (typeof (IValueProviderInvoker), peer.GetType (), "base interface invoker");
		Assert.IsFalse (peer is IExtendedValueProvider, "base peer must not implement the derived interface");
	}

	static void AssertExtendedInterfacePeer (IExtendedValueProvider peer, int expectedValue, int expectedOtherValue)
	{
		Assert.IsNotNull (peer, "extended interface peer");
		Assert.AreEqual (expectedValue, peer.Value, "extended interface value");
		Assert.AreEqual (expectedOtherValue, peer.OtherValue, "extended interface other value");
		Assert.AreEqual (typeof (IExtendedValueProviderInvoker), peer.GetType (), "extended interface invoker");
	}

	static void AssertWrapperType (object wrapper, Type expectedGenericDefinition, params Type [] expectedArguments)
	{
		var wrapperType = wrapper.GetType ();
		Assert.IsTrue (wrapperType.IsGenericType, "wrapper must be generic");
		Assert.AreEqual (expectedGenericDefinition, wrapperType.GetGenericTypeDefinition (), "wrapper generic definition");
		CollectionAssert.AreEqual (expectedArguments, wrapperType.GenericTypeArguments, "wrapper generic arguments");
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
		foreach (var entry in dictionary) {
			count++;
			if (entry.Key == null) {
				Assert.AreEqual ("null", entry.Value, "null key value");
				foundNull = true;
			} else if (ReferenceEquals (entry.Key, first)) {
				Assert.AreEqual ("first", entry.Value, "first key value");
				foundFirst = true;
			} else if (ReferenceEquals (entry.Key, second)) {
				Assert.AreEqual ("second", entry.Value, "second key value");
				foundSecond = true;
			} else {
				Assert.Fail ($"Unexpected dictionary key value '{entry.Key.Value}'.");
			}
		}
		Assert.AreEqual (3, count, "key dictionary entry count");
		Assert.IsTrue (foundFirst, "first key entry");
		Assert.IsTrue (foundSecond, "second key entry");
		Assert.IsTrue (foundNull, "null key entry");
	}

	static void AssertSameJavaObject (object expected, object actual)
	{
		var expectedPeer = (IJavaObject) expected;
		var actualPeer = (IJavaObject) actual;
		Assert.IsTrue (
			JNIEnv.IsSameObject (expectedPeer.Handle, actualPeer.Handle),
			$"expected identical Java peers; expected '{expected.GetType ()}' at '{expectedPeer.Handle}', " +
				$"actual '{actual.GetType ()}' at '{actualPeer.Handle}'");
	}

	static void AssertDistinctJavaObjects (object first, object second)
	{
		var firstPeer = (IJavaObject) first;
		var secondPeer = (IJavaObject) second;
		Assert.IsFalse (
			JNIEnv.IsSameObject (firstPeer.Handle, secondPeer.Handle),
			$"expected distinct Java peers; first '{first.GetType ()}' at '{firstPeer.Handle}', " +
				$"second '{second.GetType ()}' at '{secondPeer.Handle}'");
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
