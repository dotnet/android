using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

using Android.Runtime;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Java.InteropTests
{
	[TestClass]
	[TestCategory ("JavaDictionary")]
	public class JavaDictionaryTest
	{
		[TestMethod]
		[DataRow ("a", "value", "other")]
		[DataRow ("a", "value", null)]
		[DataRow ("a", null, "other")]
		[DataRow (null, "value", "other")]
		[DataRow (null, null, "other")]
		public void PairOperationsCompareStringValues (string key, string value, string mismatch)
		{
			AssertPairOperations (key, value, value == null ? null : new string (value.ToCharArray ()), mismatch);
		}

		[TestMethod]
		[DataRow (1, 2)]
		[DataRow (0, 1)]
		public void PairOperationsCompareValueTypes (int value, int mismatch)
		{
			AssertPairOperations ("a", value, value, mismatch);
		}

		[TestMethod]
		[DataRow (1, null)]
		[DataRow (null, 1)]
		public void PairOperationsCompareNullableValueTypes (int? value, int? mismatch)
		{
			AssertPairOperations ("a", value, value, mismatch);
		}

		[TestMethod]
		public void PairOperationsCompareManagedValues ()
		{
			var value = new EquatableValue (1);
			var equalValue = new EquatableValue (1);
			var mismatch = new EquatableValue (2);

			Assert.AreNotSame (value, equalValue);
			AssertPairOperations ("a", value, equalValue, mismatch);
		}

		[TestMethod]
		public void PairOperationsUseManagedEqualityForJavaPeersWithoutDisposingThem ()
		{
			using var value = new Java.Lang.String ("value");
			using var equalValue = new Java.Lang.String ("value");

			Assert.AreNotSame (value, equalValue);
			Assert.IsTrue (value.Equals (equalValue), "Java equality compares string contents.");
			Assert.IsFalse (EqualityComparer<Java.Lang.String>.Default.Equals (value, equalValue), "Managed equality compares peer references.");
			AssertPairOperations ("a", value, value, equalValue);
			Assert.AreEqual ("value", value.ToString ());
			Assert.AreEqual ("value", equalValue.ToString ());
		}

		[TestMethod]
		[DataRow ("a")]
		[DataRow ((string) null)]
		public void KeyOnlyRemovalStillIgnoresValue (string key)
		{
			using var dictionary = new JavaDictionary<string, string> ();
			dictionary.Add (key, null);

			Assert.IsTrue (dictionary.Remove (key));
			Assert.AreEqual (0, dictionary.Count);
			Assert.IsFalse (dictionary.Remove (key));
		}

		static void AssertPairOperations<[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)] T> (string key, T value, T equalValue, T mismatch)
		{
			using var dictionary = new JavaDictionary<string, T> ();
			ICollection<KeyValuePair<string, T>> pairs = dictionary;
			dictionary.Add (key, value);

			var mismatchedPair = new KeyValuePair<string, T> (key, mismatch);
			Assert.IsFalse (pairs.Contains (mismatchedPair), "Mismatched values must not match.");
			Assert.IsFalse (pairs.Remove (mismatchedPair), "Mismatched values must not remove the entry.");
			Assert.AreEqual (1, dictionary.Count);
			Assert.IsTrue (dictionary.ContainsKey (key));
			Assert.IsTrue (EqualityComparer<T>.Default.Equals (value, dictionary [key]), "The stored value must be unchanged.");

			var absentPair = new KeyValuePair<string, T> ("absent", value);
			Assert.IsFalse (pairs.Contains (absentPair), "An absent key must not match, even with a null or default value.");
			Assert.IsFalse (pairs.Remove (absentPair));
			Assert.AreEqual (1, dictionary.Count);

			var matchingPair = new KeyValuePair<string, T> (key, equalValue);
			Assert.IsTrue (pairs.Contains (matchingPair), "Equal values must match.");
			Assert.AreEqual (1, dictionary.Count, "Contains must not mutate the dictionary.");
			Assert.IsTrue (pairs.Remove (matchingPair), "Equal values must remove the entry.");
			Assert.AreEqual (0, dictionary.Count);
			Assert.IsFalse (dictionary.ContainsKey (key));
			Assert.IsFalse (pairs.Contains (matchingPair));
			Assert.IsFalse (pairs.Remove (matchingPair));
		}

		sealed class EquatableValue : IEquatable<EquatableValue>
		{
			readonly int value;

			public EquatableValue (int value)
			{
				this.value = value;
			}

			public bool Equals (EquatableValue other)
			{
				return other != null && value == other.value;
			}

			public override bool Equals (object obj)
			{
				return obj is EquatableValue other && Equals (other);
			}

			public override int GetHashCode ()
			{
				return value;
			}
		}
	}
}
