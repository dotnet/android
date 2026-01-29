using System;
using System.Collections.Generic;

using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Runtime;
using Android.Views;

using NUnit.Framework;

using Java.LangTests;

namespace Android.RuntimeTests {

	[TestFixture]
	public class JnienvArrayMarshaling {

		[Test]
		public void MarshalInt32ArrayArray ()
		{
			var states = new []{
				new[]{1, 2, 3},
				new[]{4, 5, 6},
			};
			var colors = new[]{7, 8};
			var list = new global::Android.Content.Res.ColorStateList (states, colors);
			Assert.AreEqual (7, list.GetColorForState (states [0], Color.Transparent));
			Assert.AreEqual (8, list.GetColorForState (states [1], Color.Transparent));
		}

		[Test]
		public void CopyArray_JavaToSystemByteArray ()
		{
			using (var byteArray = new Java.Lang.Object (JNIEnv.NewArray (new byte[]{1,2,3}), JniHandleOwnership.TransferLocalRef)) {
				var copy = new byte [3];
				JNIEnv.CopyArray (byteArray.Handle, copy, typeof (byte));
				AssertArrays ("CopyArray(Handle, byte[])", copy, (byte) 1, (byte) 2, (byte) 3);
			}
		}

		[Test]
		public void CopyArray_Byte_JavaToGenericArrayT ()
		{
			using (var byteArray = new Java.Lang.Object (JNIEnv.NewArray (new byte[]{1,2,3}), JniHandleOwnership.TransferLocalRef)) {
				var copy = new byte [3];
				JNIEnv.CopyArray<byte> (byteArray.Handle, copy);
				AssertArrays ("CopyArray<byte>(Handle, byte[])", copy, (byte) 1, (byte) 2, (byte) 3);
			}
		}

		[Test]
		public void CopyArray_JavaToSystemArray ()
		{
			using (var byteArray = new Java.Lang.Object (JNIEnv.NewArray (new byte[]{1,2,3}), JniHandleOwnership.TransferLocalRef)) {
				var copy = new byte [3];
				JNIEnv.CopyArray (byteArray.Handle, (Array) copy);
				AssertArrays ("CopyArray(Handle, Array)", copy, (byte) 1, (byte) 2, (byte) 3);
			}
		}

		[Test]
		public void CopyArray_SystemByteArrayToJava ()
		{
			using (var byteArray = new Java.Lang.Object (JNIEnv.NewArray (new byte[]{1,2,3}), JniHandleOwnership.TransferLocalRef)) {
				var orig = new byte[]{ 4, 5, 6 };
				JNIEnv.CopyArray (orig, byteArray.Handle);
				var copy = JNIEnv.GetArray<byte> (byteArray.Handle);
				AssertArrays ("CopyArray(byte[], Handle)", copy, orig);
			}
		}
		
		[Test]
		public void CopyArray_GenericByteArrayToJava ()
		{
			using (var byteArray = new Java.Lang.Object (JNIEnv.NewArray (new byte[]{1,2,3}), JniHandleOwnership.TransferLocalRef)) {
				var orig = new byte[]{ 4, 5, 6 };
				JNIEnv.CopyArray<byte> (orig, byteArray.Handle);
				var copy = JNIEnv.GetArray<byte> (byteArray.Handle);
				AssertArrays ("CopyArray<byte>(byte[], Handle)", copy, orig);
			}
		}

		[Test]
		public void CopyArray_JavaLangStringArrayArrayToSystemStringArrayArray ()
		{
			using (var stringArray = new Java.Lang.Object (JNIEnv.NewArray (new[]{new[]{"a", "b"}, new[]{"c", "d"}}), JniHandleOwnership.TransferLocalRef)) {
				var values = new[]{new string [2], new string [2]};
				JNIEnv.CopyArray (stringArray.Handle, values);
				AssertArrays ("GetArray<string[]>", values, new string[]{"a", "b"}, new string[]{"c", "d"});
			}
		}

		[Test]
		public void CopyArray_JavaLangObjectArrayToJavaLangStringArray ()
		{
			using (var stringArray = new Java.Lang.Object (JNIEnv.NewArray (new[]{"a", "b"}), JniHandleOwnership.TransferLocalRef)) {
				Java.Lang.Object[] values = (Java.Lang.Object[]) JNIEnv.GetArray (stringArray.Handle, JniHandleOwnership.DoNotTransfer, typeof(Java.Lang.Object));
				values [0] = new Java.Lang.String ("c");
				JNIEnv.CopyArray (values, stringArray.Handle);
				Assert.AreEqual ("c", JNIEnv.GetArrayItem<string> (stringArray.Handle, 0));
				Assert.AreEqual ("c", JNIEnv.GetArrayItem<Java.Lang.String> (stringArray.Handle, 0));
			}
		}

		[Test]
		public void ByteArrayArray_IsConvertibleTo_JavaLangObjectArray ()
		{
			/*
			 * Yay, Java array covariance allows this:
			 *   byte[][] a = new byte[][]{new byte[]{1,2}};
			 *   Object[] o = a;
			 *   byte[]   c = (byte[]) o [0];
			 */
			IntPtr x = JNIEnv.NewArray<byte[]>(new byte[][]{new byte[]{11, 12}, new byte[]{21, 22}});
			Assert.AreEqual ("[[B", JNIEnv.GetClassNameFromInstance (x));
			var items = JNIEnv.GetArray<Java.Lang.Object>(x);
			JNIEnv.DeleteLocalRef (x);

			Assert.AreEqual (2, items.Length);
			Assert.AreEqual (typeof (Java.Lang.Object), items [0].GetType ());

			var bytes = new byte[2];
			JNIEnv.CopyArray (items [0].Handle, bytes);
			AssertArrays ("CopyArray<byte>", bytes, (byte) 11, (byte) 12);
		}

		[Test]
		public void NewArray_JavaLangString()
		{
			using (var stringArray = new Java.Lang.Object (JNIEnv.NewArray (new[] { new Java.Lang.String ("a"), new Java.Lang.String ("b") }), JniHandleOwnership.TransferLocalRef)) {
				Assert.AreEqual ("[Ljava/lang/String;", JNIEnv.GetClassNameFromInstance (stringArray.Handle));
			}
		}

		[Test]
		public void CopyObjectArray ()
		{
			IntPtr p = JNIEnv.NewObjectArray (new byte[]{1, 2, 3});
			byte[] dest = new byte [3];
			JNIEnv.CopyObjectArray (p, dest);
			AssertArrays ("CopyObjectArray: java->C#", dest, (byte)1, (byte)2, (byte)3);
			dest = new byte[] { 42 };
			JNIEnv.CopyObjectArray (dest, p);
			byte written;
			using (var b = JNIEnv.GetArrayItem<Java.Lang.Byte>(p, 0))
				written = (byte) b.ByteValue ();
			Assert.AreEqual (42, written);
			JNIEnv.DeleteLocalRef (p);
		}

		[Test]
		public void GetArray_Byte ()
		{
			using (var byteArray = new Java.Lang.Object (JNIEnv.NewArray (new byte[]{1,2,3}), JniHandleOwnership.TransferLocalRef)) {
				var copy = JNIEnv.GetArray<byte> (byteArray.Handle);
				AssertArrays ("GetArray<byte>", copy, (byte) 1, (byte) 2, (byte) 3);
			}
		}

		[Test]
		public void GetArray_ByteArrayArray ()
		{
			byte[][] data = new byte[][]{
				new byte[]{11, 12, 13},
				new byte[]{21, 22, 23},
				new byte[]{31, 32, 33},
			};
			using (var byteArrayArray = new Java.Lang.Object (JNIEnv.NewArray (data), JniHandleOwnership.TransferLocalRef)) {
				Assert.AreEqual ("[[B", JNIEnv.GetClassNameFromInstance (byteArrayArray.Handle));
				byte[][] data2 = JNIEnv.GetArray<byte[]> (byteArrayArray.Handle);
				Assert.AreEqual (data, data2);
				byte[][] data3 = (byte[][]) JNIEnv.GetArray (byteArrayArray.Handle, JniHandleOwnership.DoNotTransfer, typeof (byte[]));
				Assert.AreEqual (data, data3);
				JNIEnv.CopyArray (data3, byteArrayArray.Handle);
			}
		}

		[Test]
		public void GetArray_JavaLangByteArrayToSystemByteArray ()
		{
			var byteObjectArray = new Java.Lang.Byte[]{
				new Java.Lang.Byte (1),
				new Java.Lang.Byte (2),
				new Java.Lang.Byte (3),
			};
			byte[] byteArray = JNIEnv.GetArray<byte>(byteObjectArray);
			AssertArrays ("GetArray: Java.Lang.Byte[]->byte[]", byteArray, (byte) 1, (byte) 2, (byte) 3);
		}

		[Test]
		public void GetArray_JavaLangStringArrayArrayToSystemStringArrayArray ()
		{
			using (var stringArray = new Java.Lang.Object (JNIEnv.NewArray (new[]{new[]{"a", "b"}, new[]{"c", "d"}}), JniHandleOwnership.TransferLocalRef)) {
				string[][] values = JNIEnv.GetArray<string[]>(stringArray.Handle);
				AssertArrays ("GetArray<string[]>", values, new string[]{"a", "b"}, new string[]{"c", "d"});
			}
		}

		[Test]
		public void GetArray_KeycodeEnum ()
		{
			using (var enumArray = new Java.Lang.Object (JNIEnv.NewArray (new[]{Keycode.A}), JniHandleOwnership.TransferLocalRef)) {
				var copy = JNIEnv.GetArray<Keycode>(enumArray.Handle);
				AssertArrays ("GetArray<Keycode>", copy, Keycode.A);
			}
		}

		[Test]
		public void GetArray_JavaLangStringArrayToJavaLangObjectArray ()
		{
			using (var stringArray = new Java.Lang.Object (JNIEnv.NewArray (new[]{"a", "b"}), JniHandleOwnership.TransferLocalRef)) {
				Java.Lang.Object[] values = (Java.Lang.Object[]) JNIEnv.GetArray (stringArray.Handle, JniHandleOwnership.DoNotTransfer, typeof(Java.Lang.Object));
				Assert.AreEqual (2, values.Length);
				Assert.AreEqual (typeof(Java.Lang.String), values [0].GetType ());
				Assert.AreEqual ("a", values [0].ToString ());
				Assert.AreEqual (typeof(Java.Lang.String), values [1].GetType ());
				Assert.AreEqual ("b", values [1].ToString ());
			}
		}

		[Test]
		public void GetArrayItem ()
		{
			using (var byteArray = new Java.Lang.Object (JNIEnv.NewArray (new byte[]{1,2,3}), JniHandleOwnership.TransferLocalRef)) {
				Assert.AreEqual (2, JNIEnv.GetArrayItem<byte> (byteArray.Handle, 1));
				JNIEnv.SetArrayItem (byteArray.Handle, 1, (byte) 42);
				Assert.AreEqual (42, JNIEnv.GetArrayItem<byte> (byteArray.Handle, 1));
			}
		}

		[Test]
		public void GetArrayItem_Int32ArrayArray ()
		{
			IntPtr array = JNIEnv.NewObjectArray (1, Java.Lang.Class.Object);
			Assert.AreEqual ("[Ljava/lang/Object;", JNIEnv.GetClassNameFromInstance (array));
			int[] seq = new int[]{1, 2, 3};
			JNIEnv.SetArrayItem (array, 0, seq);
			int[] oArray = JNIEnv.GetArrayItem<int[]> (array, 0);
			AssertArrays ("GetArrayItem_Int32ArrayArray", seq, oArray);
			JNIEnv.DeleteLocalRef (array);
		}

		[Test]
		public void SetArrayItem ()
		{
			using (var byteArray = new Java.Lang.Object (JNIEnv.NewArray (new byte[]{1,2,3}), JniHandleOwnership.TransferLocalRef)) {
				JNIEnv.SetArrayItem (byteArray.Handle, 1, (byte) 42);

				var copy = new byte [3];
				JNIEnv.CopyArray (byteArray.Handle, copy);
				AssertArrays ("CopyArray<byte>", copy, (byte) 1, (byte) 42, (byte) 3);
			}
		}

		[Test]
		public void SetArrayItem_JavaLangString ()
		{
			using (var stringArray = new Java.Lang.Object (JNIEnv.NewArray (new[]{"a", "b"}), JniHandleOwnership.TransferLocalRef)) {
				using (var v = new Java.Lang.String ("d"))
					JNIEnv.SetArrayItem (stringArray.Handle, 1, v);
				Assert.AreEqual ("d", JNIEnv.GetArrayItem<string> (stringArray.Handle, 1));
			}
		}

		[Test]
		public void GetObjectArray ()
		{
			using (var byteArray = new Java.Lang.Object (JNIEnv.NewArray (new byte[]{1,2,3}), JniHandleOwnership.TransferLocalRef)) {
				object[] data = JNIEnv.GetObjectArray (byteArray.Handle, new[]{typeof (byte), typeof (byte), typeof (byte)});
				AssertArrays ("GetObjectArray", data, (object) 1, (object) 2, (object) 3);
			}
			using (var objectArray =
					new Java.Lang.Object (
							JNIEnv.NewArray (
								new Java.Lang.Object[]{Application.Context, 42L, "string"},
								typeof (Java.Lang.Object)),
						JniHandleOwnership.TransferLocalRef)) {
				object[] values = JNIEnv.GetObjectArray (objectArray.Handle, new[]{typeof(Context), typeof (int)});
				Assert.AreEqual (3, values.Length);
				Assert.IsTrue (object.ReferenceEquals (values [0], Application.Context));
				Assert.IsTrue (values [1] is int);
				Assert.AreEqual (42, (int)values [1]);
				Assert.AreEqual ("string", values [2].ToString ());
			}
		}

		[Test]
		public void NewArray_Int32ArrayArray ()
		{
			IntPtr x = JNIEnv.NewArray<int[]>(new int[][]{new[]{11, 12}, new []{21, 22}});
			string t = JNIEnv.GetClassNameFromInstance (x);
			JNIEnv.DeleteLocalRef (x);
			Assert.AreEqual ("[[I", t);
		}

		[Test]
		public void NewArray_Int32ArrayArray_ArrayOverload ()
		{
			Array array = new int[][]{new[]{11, 12}, new []{21, 22}};
			IntPtr x = JNIEnv.NewArray(array);
			string t = JNIEnv.GetClassNameFromInstance (x);
			JNIEnv.DeleteLocalRef (x);
			Assert.AreEqual ("[[I", t);
		}

		// http://bugzilla.xamarin.com/show_bug.cgi?id=12479
		[Test]
		public void NewArray_Int32ArrayArray_ShouldNotLeak ()
		{
			int[][] array = new int[][]{
				new int[]{1,2,3,4},
				new int[]{5,6,7,8},
			};

			// 600 chosen as LREF table is 512 entries, so if this leaks it should overflow
			for (int i = 0; i < 600; ++i) {
				IntPtr l = JNIEnv.NewArray (array);
				JNIEnv.DeleteLocalRef (l);
			}
		}

		[Test]
		public void NewArray_UseJcwTypeWhenRenamed ()
		{
			IntPtr lref = JNIEnv.NewArray<CreateInstance_OverrideAbsListView_Adapter>(new CreateInstance_OverrideAbsListView_Adapter[0]);
			Assert.AreEqual (
					"[Lcom/xamarin/android/runtimetests/CreateInstance_OverrideAbsListView_Adapter;",
					JNIEnv.GetClassNameFromInstance (lref));
			JNIEnv.DeleteLocalRef (lref);
		}

		[Test]
		public void NewObjectArray_SystemByteArrayToJavaLangByteArray ()
		{
			IntPtr p = JNIEnv.NewObjectArray (new byte[]{1, 2, 3});
			string t = JNIEnv.GetClassNameFromInstance (p);
			JNIEnv.DeleteLocalRef (p);
			Assert.AreEqual ("[Ljava/lang/Byte;", t);
		}

		// http://bugzilla.xamarin.com/show_bug.cgi?id=360
		[Test]
		public void BoundArrayPropertiesHaveSetters ()
		{
			using (var opt = new BitmapFactory.Options ()) {
				opt.InTempStorage = new byte [] {1, 3, 5};
				var inTempStorage = opt.InTempStorage;
				Assert.AreEqual (3, inTempStorage.Count);
				AssertArrays ("BoundArrayPropertiesHaveSetters", inTempStorage, (byte) 1, (byte) 3, (byte) 5);
				Assert.DoesNotThrow (() => ((IDisposable)inTempStorage).Dispose ());
			}
		}

		static void AssertArrays<T> (string message, IList<T> actual, params T[] expected)
		{
			Assert.AreEqual (expected.Length, actual.Count, message);
			for (int i = 0; i < expected.Length; ++i)
				Assert.AreEqual (expected [i], actual [i], message);
		}
	}
}
