using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;

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

		[Test]
		public void FromJniHandle_IListNullableInt32 ()
		{
			using (var source = new JavaList<int?> ()) {
				source.Add (1);
				source.Add (null);
				source.Add (3);

				var converted = InvokeJavaConvertFromJniHandle (typeof (IList<int?>), source.Handle, JniHandleOwnership.DoNotTransfer);
				try {
					Assert.AreEqual (typeof (JavaList<int?>), converted.GetType ());

					var list = (IList<int?>) converted;
					Assert.AreEqual (3, list.Count);
					Assert.AreEqual ((int?) 1, list [0]);
					Assert.IsNull (list [1]);
					Assert.AreEqual ((int?) 3, list [2]);
				} finally {
					(converted as IDisposable)?.Dispose ();
				}
			}
		}

		[Test]
		public void FromJniHandle_IDictionaryNullableInt32String ()
		{
			using (var source = new JavaDictionary<int?, string> ()) {
				source.Add (1, "one");
				source.Add (null, "null");

				var converted = InvokeJavaConvertFromJniHandle (typeof (IDictionary<int?, string>), source.Handle, JniHandleOwnership.DoNotTransfer);
				try {
					Assert.AreEqual (typeof (JavaDictionary<int?, string>), converted.GetType ());

					var dictionary = (IDictionary<int?, string>) converted;
					Assert.AreEqual ("one", dictionary [1]);
					Assert.AreEqual ("null", dictionary [null]);
				} finally {
					(converted as IDisposable)?.Dispose ();
				}
			}
		}

		// The non-generic source and assertions intentionally avoid referencing
		// JavaDictionary<int, long>, so NativeAOT must root that exact wrapper through
		// ValueTypeFactory<T>.CreateDictionaryWithKey<TKey>'s generic-virtual dispatch.
		[Test]
		[Category ("NativeAOTTrimmable")]
		public void FromJniHandle_IDictionaryInt32Int64 ()
		{
			if (!Microsoft.Android.Runtime.RuntimeFeature.TrimmableTypeMap) {
				Assert.Ignore ("This test validates value/value dictionary rooting on the trimmable typemap path.");
			}

			using (var source = new JavaDictionary ()) {
				source.Add (1, 100L);
				source.Add (2, 200L);

				var converted = InvokeJavaConvertFromJniHandle (typeof (IDictionary<int, long>), source.Handle, JniHandleOwnership.DoNotTransfer);
				try {
					var dictionary = (IDictionary<int, long>) converted;
					Assert.AreEqual (100L, dictionary [1]);
					Assert.AreEqual (200L, dictionary [2]);
				} finally {
					(converted as IDisposable)?.Dispose ();
				}
			}
		}

		// Regression: byte-element collections must stay supported on the trimmable typemap path
		// (ValueTypeFactory maps byte alongside sbyte). byte marshals to java.lang.Byte bitwise, so
		// values above 127 round-trip through the signed Java byte.
		[Test]
		public void FromJniHandle_IListByte ()
		{
			using (var source = new JavaList<byte> ()) {
				source.Add ((byte) 1);
				source.Add ((byte) 200);

				var converted = InvokeJavaConvertFromJniHandle (typeof (IList<byte>), source.Handle, JniHandleOwnership.DoNotTransfer);
				try {
					Assert.AreEqual (typeof (JavaList<byte>), converted.GetType ());

					var list = (IList<byte>) converted;
					Assert.AreEqual (2, list.Count);
					Assert.AreEqual ((byte) 1, list [0]);
					Assert.AreEqual ((byte) 200, list [1]);
				} finally {
					(converted as IDisposable)?.Dispose ();
				}
			}
		}

		[TestCase (typeof (IList<DateTime>))]
		[TestCase (typeof (JavaList<DateTime>))]
		[TestCase (typeof (ICollection<DateTime>))]
		[TestCase (typeof (JavaCollection<DateTime>))]
		[TestCase (typeof (IDictionary<DateTime, string>))]
		[TestCase (typeof (JavaDictionary<DateTime, string>))]
		[TestCase (typeof (IDictionary<string, DateTime>))]
		[TestCase (typeof (JavaDictionary<string, DateTime>))]
		[Category ("NativeAOTTrimmable")]
		public void FromJniHandle_UnsupportedValueTypeThrows (Type targetType)
		{
			if (!Microsoft.Android.Runtime.RuntimeFeature.TrimmableTypeMap) {
				Assert.Ignore ("This test validates unsupported value-type container arguments on the trimmable typemap path.");
			}

			using (var source = new JavaList ()) {
				Assert.Throws<NotSupportedException> (() =>
					InvokeJavaConvertFromJniHandle (targetType, source.Handle, JniHandleOwnership.DoNotTransfer));
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

		static object InvokeJavaConvertFromJniHandle (Type targetType, IntPtr handle, JniHandleOwnership transfer)
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

			object value;
			try {
				value = method.Invoke (null, new object [] { handle, transfer, targetType });
			} catch (TargetInvocationException e) when (e.InnerException != null) {
				ExceptionDispatchInfo.Capture (e.InnerException).Throw ();
				throw;
			}
			Assert.IsNotNull (value);
			return value;
		}
	}
}
