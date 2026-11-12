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
	}
}
