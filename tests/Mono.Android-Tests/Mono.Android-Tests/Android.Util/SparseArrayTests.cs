using System;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Android.Runtime;
using Android.Util;
using Java.Interop;

namespace Android.UtilTests {

	[TestClass]
	[TestCategory ("HandwrittenJniBindings")]
	public class SparseArrayTests {

		[TestMethod]
		public void GenericSparseArrayUsesSparseArrayClass ()
		{
			using var values = new SparseArray<string> ();

			values.Put (1, "one");
			values.Append (2, "two");

			Assert.AreEqual ("one", values.Get (1));
			Assert.AreEqual ("two", values.Get (2));
		}

		[TestMethod]
		public void AppendResolvesAppendMethod ()
		{
			using var values = new JavaSparseArray ();
			values.Append (1, "one");

			Assert.AreEqual ("append:1:one", values.LastCall);
		}

		[TestMethod]
		public void GenericMethodsDispatchToJavaOverrides ()
		{
			using var values = new JavaSparseArray ();
			using var argument = new Java.Lang.String ("direct");
			var type = values.Class;
			var append = JNIEnv.GetMethodID (type.Handle, "append", "(ILjava/lang/Object;)V");
			JNIEnv.CallVoidMethod (values.Handle, append, new JValue (9), new JValue (argument));
			GC.KeepAlive (type);
			GC.KeepAlive (argument);
			Assert.AreEqual ("append:9:direct", values.LastCall);

			Assert.AreEqual ("get:2", values.Get (2));
			Assert.AreEqual ("getDefault:3:fallback", values.Get (3, "fallback"));
			Assert.AreEqual (123, values.IndexOfValue ("needle"));
			Assert.AreEqual ("indexOfValue:needle", values.LastCall);
			values.Put (4, "put");
			Assert.AreEqual ("put:4:put", values.LastCall);
			values.SetValueAt (5, "set");
			Assert.AreEqual ("setValueAt:5:set", values.LastCall);
			Assert.AreEqual ("valueAt:6", values.ValueAt (6));
		}

		[TestMethod]
		public void GenericManagedBaseCallUsesJavaBaseImplementation ()
		{
			using var values = new ManagedDerivedSparseArray ();
			using var argument = new Java.Lang.String ("direct");
			var type = values.Class;
			var put = JNIEnv.GetMethodID (type.Handle, "put", "(ILjava/lang/Object;)V");
			JNIEnv.CallVoidMethod (values.Handle, put, new JValue (1), new JValue (argument));
			GC.KeepAlive (type);
			GC.KeepAlive (argument);
			Assert.AreEqual ("derivedPut:1:direct", values.LastCall);

			values.Put (2, "base");
			Assert.AreEqual ("put:2:base", values.LastCall);
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

		[Register ("net/dot/android/test/VirtualSparseArray", DoNotGenerateAcw = true)]
		class JavaSparseArray : SparseArray<string> {

			static readonly JniPeerMembers members = new JniPeerMembers (
				"net/dot/android/test/VirtualSparseArray", typeof (JavaSparseArray));

			public override JniPeerMembers JniPeerMembers => members;

			public string LastCall {
				get {
					var type = Class;
					var method = JNIEnv.GetMethodID (type.Handle, "getLastCall", "()Ljava/lang/String;");
					var result = JNIEnv.GetString (JNIEnv.CallObjectMethod (Handle, method), JniHandleOwnership.TransferLocalRef);
					GC.KeepAlive (type);
					GC.KeepAlive (this);
					return result ?? throw new InvalidOperationException ("The Java fixture returned a null call record.");
				}
			}
		}

		[Register ("net/dot/android/test/VirtualSparseArray$Derived", DoNotGenerateAcw = true)]
		sealed class ManagedDerivedSparseArray : JavaSparseArray {

			public override void Put (int key, string value)
			{
				base.Put (key, value);
			}
		}
	}
}
