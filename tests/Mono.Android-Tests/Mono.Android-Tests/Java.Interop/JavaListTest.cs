using System;
using System.Linq;
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
		[DataRow (1)]
		[DataRow (2)]
		public void RemoveAllRemovesEveryMatchingElement (int removalCount)
		{
			list.Add ("foo");
			list.Add ("foo");
			list.Add ("bar");
			list.Add ("foo");
			list.Add ("baz");
			using var collection = new JavaList ();
			for (int i = 0; i < removalCount; i++)
				collection.Add ("foo");

			Assert.IsTrue (list.RemoveAll (collection));
			Assert.AreSequenceEqual (new [] { "bar", "baz" }, list.Cast<object> ().Select (value => value?.ToString ()));
			Assert.AreEqual (removalCount, collection.Count);
			Assert.IsFalse (list.RemoveAll (collection));
		}

		[TestMethod]
		public void RemoveAllWithoutMatchesReturnsFalse ()
		{
			list.Add ("foo");
			using var collection = new JavaList (new [] { "bar" });

			Assert.IsFalse (list.RemoveAll (collection));
			Assert.AreSequenceEqual (new [] { "foo" }, list.Cast<object> ().Select (value => value?.ToString ()));
		}

		[TestMethod]
		public void RemoveAllRemovesBoxedValues ()
		{
			list.Add ((object) 42);
			list.Add ((object) 42);
			list.Add ((object) 7);
			using var collection = new JavaList (new object [] { 42 });

			Assert.IsTrue (list.RemoveAll (collection));
			Assert.AreEqual (1, list.Count);
			Assert.AreEqual (7, list [0]);
		}

		[TestMethod]
		public void RemoveAllDoesNotDisposeExistingPeers ()
		{
			using var item = new Java.Lang.String ("foo");
			list.Add ((object) item);
			list.Add ((object) item);
			using var collection = new JavaList ();
			collection.Add ((object) item);

			Assert.IsTrue (list.RemoveAll (collection));
			Assert.AreEqual (0, list.Count);
			Assert.IsTrue (item.PeerReference.IsValid);
			Assert.AreEqual ("foo", item.ToString ());
		}

		[TestMethod]
		[DataRow (false)]
		[DataRow (true)]
		[DoNotParallelize]
		public void RemoveAllDoesNotDisposePeerFromReentrantLookup (bool reentrantActivation)
		{
			var handle = JNIEnv.CreateInstance (ReentrantLookupPeer.JniName, "()V");
			ReentrantLookupPeer registered = null;
			try {
				using (var item = new Java.Lang.Object (handle, JniHandleOwnership.DoNotTransfer | JniHandleOwnership.DoNotRegister))
					list.Add ((object) item);
				if (reentrantActivation) {
					ReentrantLookupPeer.BeforeRegistration = value => {
						registered = Java.Lang.Object.GetObject<ReentrantLookupPeer> (value, JniHandleOwnership.DoNotTransfer);
					};
				}
				using var collection = new BulkRemovalJavaList {
					BeforeContains = item => {
						registered = Java.Lang.Object.GetObject<ReentrantLookupPeer> (handle, JniHandleOwnership.DoNotTransfer);
					},
				};

				Assert.IsNull (JniRuntime.CurrentRuntime.ValueManager.PeekPeer (new JniObjectReference (handle)));
				Assert.IsFalse (list.RemoveAll (collection));
				Assert.AreEqual (1, collection.ContainsCalls);
				Assert.IsNotNull (registered);
				if (registered == null)
					throw new InvalidOperationException ("The Contains callback did not acquire a peer.");
				Assert.IsTrue (registered.PeerReference.IsValid, "RemoveAll must not dispose a peer acquired by another lookup.");
				Assert.AreSame (registered, JniRuntime.CurrentRuntime.ValueManager.PeekPeer (new JniObjectReference (handle)));
				Assert.IsTrue (JniEnvironment.Types.IsSameObject (new JniObjectReference (handle), registered.PeerReference));
			} finally {
				ReentrantLookupPeer.BeforeRegistration = null;
				registered?.Dispose ();
				JNIEnv.DeleteLocalRef (handle);
			}
		}

		[TestMethod]
		public void RemoveAllWithEmptyCollectionsReturnsFalse ()
		{
			using var collection = new JavaList ();
			Assert.IsFalse (list.RemoveAll (collection));

			list.Add ("foo");
			Assert.IsFalse (list.RemoveAll (collection));
			Assert.AreSequenceEqual (new [] { "foo" }, list.Cast<object> ().Select (value => value?.ToString ()));

			list.Clear ();
			collection.Add ("foo");
			Assert.IsFalse (list.RemoveAll (collection));
			Assert.AreEqual (0, list.Count);
		}

		[TestMethod]
		public void RemoveAllRemovesNullItems ()
		{
			list.Add ((object) null);
			list.Add ("foo");
			list.Add ((object) null);
			using var collection = new JavaList ();
			collection.Add ((object) null);

			Assert.IsTrue (list.RemoveAll (collection));
			Assert.AreSequenceEqual (new [] { "foo" }, list.Cast<object> ().Select (value => value?.ToString ()));
			Assert.IsFalse (list.RemoveAll (collection));
		}

		[TestMethod]
		[DataRow (true)]
		[DataRow (false)]
		public void RemoveAllRejectsNullCollection (bool populated)
		{
			if (populated)
				list.Add ("foo");

			var exception = Assert.ThrowsExactly<ArgumentNullException> (() => list.RemoveAll (null));
			Assert.AreEqual ("collection", exception.ParamName);
			Assert.AreEqual (populated ? 1 : 0, list.Count);
		}

		[TestMethod]
		public void RemoveAllWithSelfRemovesEveryElement ()
		{
			list.Add ("foo");
			list.Add ("foo");
			list.Add ("bar");
			list.Add ((object) null);

			Assert.IsTrue (list.RemoveAll (list));
			Assert.AreEqual (0, list.Count);
			Assert.IsFalse (list.RemoveAll (list));
		}

		[TestMethod]
		[DataRow (true)]
		[DataRow (false)]
		public void RemoveAllUsesVirtualContainsAndRemove (bool allowRemoval)
		{
			using var destination = new BulkRemovalJavaList { AllowRemoval = allowRemoval };
			using var collection = new BulkRemovalJavaList ();
			destination.Add ("foo");
			destination.Add ("foo");
			destination.Add ("bar");
			collection.Add ("foo");

			Assert.AreEqual (allowRemoval, destination.RemoveAll (collection));
			Assert.AreEqual (3, collection.ContainsCalls);
			Assert.AreEqual (2, destination.RemoveCalls);
			Assert.AreSequenceEqual (allowRemoval ? new [] { "bar" } : new [] { "foo", "foo", "bar" }, destination.Cast<object> ().Select (value => value?.ToString ()));
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

	public class BulkRemovalJavaList : JavaList
	{
		public bool AllowRemoval { get; set; } = true;
		public int ContainsCalls { get; private set; }
		public int RemoveCalls { get; private set; }
		public Action<Java.Lang.Object> BeforeContains { get; set; }

		public override bool Contains (Java.Lang.Object item)
		{
			ContainsCalls++;
			BeforeContains?.Invoke (item);
			return base.Contains (item);
		}

		public override bool Remove (Java.Lang.Object item)
		{
			RemoveCalls++;
			return AllowRemoval && base.Remove (item);
		}
	}
}