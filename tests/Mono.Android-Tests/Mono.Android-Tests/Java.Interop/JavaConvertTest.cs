using System;
using System.Collections.Generic;
using System.Linq;

using Android.App;
using Android.Content;
using Android.Runtime;

using NUnit.Framework;

using Xamarin.Android.RuntimeTests;

namespace Java.InteropTests
{
	[TestFixture]
	public class JavaConvertTest
	{
		[Test]
		public void Conversions ()
		{
			var entries = new []{
				new {Key = "b",   Value = (object) (byte) 0x01},
				new {Key = "c",   Value = (object) 'c'},
				new {Key = "d",   Value = (object) 1.0 },
				new {Key = "f",   Value = (object) 2.0f },
				new {Key = "i",   Value = (object) 3},
				new {Key = "j",   Value = (object) 4L},
				new {Key = "s",   Value = (object) (short) 5},
				new {Key = "z",   Value = (object) false },
				new {Key = "_",   Value = (object) "string" },
				new {Key = "nil", Value = (object) null },
				new {Key = "jlb", Value = (object) new Java.Lang.Byte (10)},
				new {Key = "jlc", Value = (object) new Java.Lang.Character ('d')},
				new {Key = "jld", Value = (object) new Java.Lang.Double (12.01)},
				new {Key = "jlf", Value = (object) new Java.Lang.Float (13.02f)},
				new {Key = "jli", Value = (object) new Java.Lang.Integer (14)},
				new {Key = "jlj", Value = (object) new Java.Lang.Long (15L)},
				new {Key = "jls", Value = (object) new Java.Lang.Short (16)},
				new {Key = "jlz", Value = (object) new Java.Lang.Boolean (true)},
				new {Key = "jl_", Value = (object) new Java.Lang.String ("JavaString")},
				new {Key = "njo", Value = (object) new NonJavaObject ()},
				new {Key = "jo",  Value = (object) new MyIntent ()},
			};
			Action<object, object, string> compare = (e, a, m) => {
					if (a != null)
						Assert.AreEqual (e.GetType (), a.GetType (), m);
					Assert.IsTrue (object.Equals (e, a), m);
			};

			using (var d = new JavaDictionary<string, object>()) {
				foreach (var e in entries)
					d.Add (e.Key, e.Value);
				foreach (var e in entries) {
					object v;
					Assert.IsTrue (d.TryGetValue (e.Key, out v), "JavaDictionary<string, object>.TryGetValue: " + e.Key);
					compare (e.Value, v, "JavaDictionary<string, object>: " + e.Key);
				}
			}

			using (var d = new JavaDictionary ()) {
				foreach (var e in entries)
					d.Add (e.Key, e.Value);
				foreach (var e in entries) {
					object v = d [e.Key];
					if (v == null && e.Value != null)
						Assert.Fail ("JavaDictionary.this[] returned unexpected value.");
					compare (e.Value, v, "JavaDictionary: " + e.Key);
				}
			}

			using (var l = new JavaList<object> (entries.Select (e => e.Value))) {
				for (int i = 0; i < entries.Length; ++i) {
					compare (entries [i].Value, l [i], "JavaList<object>: " + entries [i].Key);
				}
			}

			using (var l = new JavaList (entries.Select (e => e.Value))) {
				for (int i = 0; i < entries.Length; ++i) {
					compare (entries [i].Value, l [i], "JavaList: " + entries [i].Key);
				}
			}

			do {
				var c = JavaCollection<object>.FromJniHandle (
							JavaCollection<object>.ToLocalJniHandle (entries.Select (e => e.Value).ToArray ()),
							JniHandleOwnership.TransferLocalRef);
				int i = 0;
				foreach (object v in c) {
					compare (entries [i].Value, v, "JavaCollection<object> through lref: " + entries [i].Key);
					i++;
				}
				((IDisposable) c).Dispose ();
			} while (false);

			do {
				var c = JavaCollection.FromJniHandle (
							JavaCollection.ToLocalJniHandle (entries.Select (e => e.Value).ToArray ()),
							JniHandleOwnership.TransferLocalRef);
				int i = 0;
				foreach (object v in c) {
					compare (entries [i].Value, v, "JavaCollection through lref: " + entries [i].Key);
					i++;
				}
				((IDisposable) c).Dispose ();
			} while (false);
		}

		[Test]
		public void NullStringMarshalsAsIntPtrZero ()
		{
			var list = new JavaList<string> ();
			list.Add (null);
			Assert.AreEqual (null, list [0]);
		}

		[Test]
		public void MarshalInt23Array ()
		{
			using (var values = new JavaList<int[]>(
						CreateList (new[]{1,2,3}, new[]{4,5,6}, new[]{7,8,9}).Handle,
						JniHandleOwnership.DoNotTransfer)) {
				Assert.AreEqual (3, values.Count);

				Assert.IsTrue (values [0].SequenceEqual (new[]{1, 2, 3}));
				Assert.IsTrue (values [1].SequenceEqual (new[]{4, 5, 6}));
				Assert.IsTrue (values [2].SequenceEqual (new[]{7, 8, 9}));
			}
		}

		static Java.Util.ArrayList CreateList (params int[][] items)
		{
			var list = new Java.Util.ArrayList ();
			foreach (int[] values in items) {
				using (var v = new Java.Lang.Object (
							JNIEnv.NewArray (values),
							JniHandleOwnership.TransferLocalRef))
					list.Add (v);
			}
			return list;
		}
	}
}

