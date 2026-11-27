using System;
using System.Reflection;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Android.Runtime;
using Android.Util;

namespace Android.UtilTests {

	[TestClass]
	[TestCategory ("HandwrittenJniBindings")]
	public class SparseArrayTests {

		[TestMethod]
		public void GenericSparseArrayUsesSparseArrayClass ()
		{
			using var values = new SparseArray<string> ();

			values.Put (1, "one");

			Assert.AreEqual ("one", values.Get (1));
		}

		[TestMethod]
		public void AppendResolvesAppendMethod ()
		{
			using var values = new SparseArray<string> ();
			values.Append (1, "one");

			var sparseArrayClass = JNIEnv.FindClass ("android/util/SparseArray");
			try {
				var append = JNIEnv.GetMethodID (sparseArrayClass, "append", "(ILjava/lang/Object;)V");
				var cachedMethod = typeof (SparseArray<string>).GetField (
					"id_append_ILjava_lang_Object_", BindingFlags.Static | BindingFlags.NonPublic);
				Assert.IsNotNull (cachedMethod);
				Assert.AreEqual (append, cachedMethod?.GetValue (null));
			} finally {
				JNIEnv.DeleteGlobalRef (sparseArrayClass);
			}

			Assert.AreEqual ("one", values.Get (1));
		}

		// https://github.com/dotnet/android/issues/13674
		[TestMethod]
		public void JavaCreatedSparseArrayUsesNonGenericPeer ()
		{
			// Keep SparseArray<T> in trimmed builds, so both types map to android/util/SparseArray
			GC.KeepAlive (typeof (SparseArray<Java.Lang.Object>));

			var sparseArrayClass = JNIEnv.FindClass ("android/util/SparseArray");
			try {
				var constructor = JNIEnv.GetMethodID (sparseArrayClass, "<init>", "()V");

				var handle = JNIEnv.NewObject (sparseArrayClass, constructor);
				using (var value = Java.Lang.Object.GetObject<Java.Lang.Object> (handle, JniHandleOwnership.TransferLocalRef)) {
					Assert.AreEqual (typeof (SparseArray), value?.GetType ());
				}

				handle = JNIEnv.NewObject (sparseArrayClass, constructor);
				using (var value = Java.Lang.Object.GetObject<SparseArray> (handle, JniHandleOwnership.TransferLocalRef)) {
					Assert.AreEqual (typeof (SparseArray), value?.GetType ());
				}
			} finally {
				JNIEnv.DeleteGlobalRef (sparseArrayClass);
			}
		}
	}
}
