using System;
using Android.Runtime;
using Java.Interop;
using NUnit.Framework;

namespace Java.InteropTests
{
	[Category("JavaList")]
	[TestFixture(typeof(JavaList))]
	[TestFixture(typeof(JavaList<string>))]
	public class JavaListTest<T> where T : JavaList, new()
	{
		JavaList list;

		[SetUp]
		public void Setup ()
		{
			// Note: originally this was just `list = new T ();` but this doesn't work with NativeAOT due to how
			// NUnit creates the `JavaListTest<T>` instance via reflection. The `new()` constraint cannot be respected
			// under NativeAOT and so this would fail. Given we have just 2 cases, we can simply switch on them and
			// call the ctors directly.
			list = typeof (T) switch
			{
				Type t when t == typeof (JavaList) => new JavaList (),
				Type t when t == typeof (JavaList<string>) => new JavaList<string> (),
				_ => throw new NotSupportedException ($"Unexpected fixture type '{typeof (T)}'."),
			};
		}

		[Test]
		public void Add ()
		{
			list.Add ("foo");
			Assert.AreEqual ("foo", list [0]);

			// Ensure duplicates are allowed.
			list.Add ("foo");
			Assert.AreEqual (2, list.Count);
			Assert.AreEqual ("foo", list [1]);
		}

		[Test]
		public void AddWithIndex ()
		{
			list.Add ("Apple");
			list.Add ("Banana");
			list.Add ("Cherry");

			// Ensure index is respected.
			list.Add (3, "Grape");
			list.Add (2, "Blueberry");
			list.Add (4, "Fig");

			Assert.AreEqual ("Apple", list [0]);
			Assert.AreEqual ("Banana", list [1]);
			Assert.AreEqual ("Blueberry", list [2]);
			Assert.AreEqual ("Cherry", list [3]);
			Assert.AreEqual ("Fig", list [4]);
			Assert.AreEqual ("Grape", list [5]);
		}

		[Test]
		public void Count ()
		{
			list.Add ("foo");
			Assert.AreEqual (1, list.Count);
		}

		[Test]
		public void Indexer ()
		{
			list.Add ("foo");
			list [0] = "bar";
			Assert.AreEqual ("bar", list [0]);
		}

		[Test]
		public void ForEach ()
		{
			list.Add ("foo");
			foreach (var item in list) {
				Assert.AreEqual ("foo", item);
			}
		}

		[Test]
		public void Clear ()
		{
			list.Add ("foo");
			list.Clear ();
			Assert.AreEqual (0, list.Count);
		}

		[Test]
		public void Contains ()
		{
			list.Add ("foo");
			Assert.IsTrue (list.Contains ("foo"));
			Assert.IsFalse (list.Contains ("bar"));
		}

		[Test]
		public void CopyTo ()
		{
			var array = new object [1];
			list.Add ("foo");
			list.CopyTo (array, 0);
			Assert.AreEqual ("foo", array [0]);
		}

		[Test]
		public void IndexOf ()
		{
			list.Add ("foo");
			Assert.AreEqual (0, list.IndexOf ("foo"));
			Assert.AreEqual (-1, list.IndexOf ("bar"));
		}

		[Test]
		public void LastIndexOf ()
		{
			list.Add ("foo");
			Assert.AreEqual (0, list.LastIndexOf ("foo"));
			Assert.AreEqual (-1, list.LastIndexOf ("bar"));
		}

		[Test]
		public void Insert ()
		{
			list.Add ("foo");
			list.Insert (0, "bar");
			Assert.AreEqual ("bar", list [0]);
			Assert.AreEqual ("foo", list [1]);
		}

		[Test]
		public void RemoveAt ()
		{
			list.Add ("foo");
			list.Insert (0, "bar");
			Assert.AreEqual ("bar", list [0]);
			Assert.AreEqual ("foo", list [1]);
		}

		[Test]
		public void RemoveNonExistentItemDoesNotThrow ()
		{
			list.Add ("foo");
			Assert.DoesNotThrow (() => list.Remove ("bar"));
			Assert.AreEqual (1, list.Count);
		}

		[Test]
		public void Set ()
		{
			list.Add ("foo");
			list.Set (0, "bar");
			Assert.AreEqual ("bar", list [0]);
		}

		[Test]
		public void SubList ()
		{
			list.Add ("foo");
			list.Add ("bar");
			var sub = list.SubList (0, 1);
			Assert.AreEqual (1, sub.Count);
			Assert.AreEqual ("foo", sub [0]);
		}
	}
}