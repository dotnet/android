using System;
using Android.Runtime;
using Java.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Java.InteropTests
{
	[TestCategory("JavaList")]
	public abstract class JavaListTest<T> where T : JavaList, new()
	{
		JavaList list;

		[TestInitialize]
		public void Setup ()
		{
			// Keep both Java peers' constructors statically reachable under NativeAOT.
			list = typeof (T) switch
			{
				Type t when t == typeof (JavaList) => new JavaList (),
				Type t when t == typeof (JavaList<string>) => new JavaList<string> (),
				_ => throw new NotSupportedException ($"Unexpected fixture type '{typeof (T)}'."),
			};
		}

		[TestMethod]
		public void Add ()
		{
			list.Add ("foo");
			Assert.AreEqual (1, list.Count);
			Assert.AreEqual ("foo", list [0]?.ToString ());

			// Ensure duplicates are allowed.
			list.Add ("foo");
			Assert.AreEqual (2, list.Count);
			Assert.AreEqual ("foo", list [1]?.ToString ());
		}

		[TestMethod]
		public void AddWithIndex ()
		{
			list.Add ("Apple");
			list.Add ("Banana");
			list.Add ("Cherry");

			// Ensure index is respected.
			list.Add (3, "Grape");
			list.Add (2, "Blueberry");
			list.Add (4, "Fig");

			Assert.AreEqual ("Apple", list [0]?.ToString ());
			Assert.AreEqual ("Banana", list [1]?.ToString ());
			Assert.AreEqual ("Blueberry", list [2]?.ToString ());
			Assert.AreEqual ("Cherry", list [3]?.ToString ());
			Assert.AreEqual ("Fig", list [4]?.ToString ());
			Assert.AreEqual ("Grape", list [5]?.ToString ());
		}

		[TestMethod]
		public void Indexer ()
		{
			list.Add ("foo");
			list [0] = "bar";
			Assert.AreEqual ("bar", list [0]);
		}

		[TestMethod]
		public void ForEach ()
		{
			list.Add ("foo");
			foreach (var item in list) {
				Assert.AreEqual ("foo", item?.ToString ());
			}
		}

		[TestMethod]
		public void Clear ()
		{
			list.Add ("foo");
			list.Clear ();
			Assert.AreEqual (0, list.Count);
		}

		[TestMethod]
		public void Contains ()
		{
			list.Add ("foo");
			Assert.IsTrue (list.Contains ("foo"));
			Assert.IsFalse (list.Contains ("bar"));
		}

		[TestMethod]
		public void CopyTo ()
		{
			var array = new object [1];
			list.Add ("foo");
			list.CopyTo (array, 0);
			Assert.AreEqual ("foo", array [0]);
		}

		[TestMethod]
		public void IndexOf ()
		{
			list.Add ("foo");
			Assert.AreEqual (0, list.IndexOf ("foo"));
			Assert.AreEqual (-1, list.IndexOf ("bar"));
		}

		[TestMethod]
		public void LastIndexOf ()
		{
			list.Add ("foo");
			Assert.AreEqual (0, list.LastIndexOf ("foo"));
			Assert.AreEqual (-1, list.LastIndexOf ("bar"));
		}

		[TestMethod]
		public void Insert ()
		{
			list.Add ("foo");
			list.Insert (0, "bar");
			Assert.AreEqual ("bar", list [0]?.ToString ());
			Assert.AreEqual ("foo", list [1]?.ToString ());
		}

		[TestMethod]
		public void RemoveAt ()
		{
			list.Add ("foo");
			list.Add ("bar");
			list.RemoveAt (0);
			Assert.AreEqual (1, list.Count);
			Assert.AreEqual ("bar", list [0]?.ToString ());
		}

		[TestMethod]
		public void RemoveNonExistentItemDoesNotThrow ()
		{
			list.Add ("foo");
			global::Xamarin.Android.UnitTests.AndroidTestAssert.DoesNotThrow (() => list.Remove ("bar"));
			Assert.AreEqual (1, list.Count);
		}

		[TestMethod]
		public void Set ()
		{
			list.Add ("foo");
			list.Set (0, "bar");
			Assert.AreEqual ("bar", list [0]?.ToString ());
		}

		[TestMethod]
		public void SubList ()
		{
			list.Add ("foo");
			list.Add ("bar");
			var sub = list.SubList (0, 1);
			Assert.AreEqual (1, sub.Count);
			Assert.AreEqual ("foo", sub [0]?.ToString ());
		}
	}

	[TestClass]
	public class JavaListNonGenericTest : JavaListTest<JavaList>
	{
	}

	[TestClass]
	public class JavaListStringTest : JavaListTest<JavaList<string>>
	{
	}
}