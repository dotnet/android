using System;

using Android.Text;
using Android.Runtime;
using Android.Views;

using Java.Interop;

using Microsoft.Android.Runtime;

using NUnit.Framework;

namespace Java.InteropTests
{
	[TestFixture]
	[Category ("TrimmableTypeMapRuntimeCoverage")]
	public class TrimmableTypeMapRuntimeCoverageTests
	{
		[Test]
		public void JavaToManagedTextWatcherCallback_MarshalsStringAndPrimitiveParameters ()
		{
			AssumeTrimmableTypeMapEnabled ();
			TrimmableRuntimeTextWatcher.Reset ();

			using var watcher = new TrimmableRuntimeTextWatcher ();
			using var text = new Java.Lang.String ("managed");

			var method = JNIEnv.GetMethodID (watcher.Class.Handle, "onTextChanged", "(Ljava/lang/CharSequence;III)V");
			JNIEnv.CallVoidMethod (
				watcher.Handle,
				method,
				new JValue (text.Handle),
				new JValue (2),
				new JValue (3),
				new JValue (4));

			Assert.AreEqual (1, TrimmableRuntimeTextWatcher.OnTextChangedInvocations);
			Assert.AreEqual ("managed", watcher.TextValue);
			Assert.AreEqual (2, watcher.StartValue);
			Assert.AreEqual (3, watcher.BeforeValue);
			Assert.AreEqual (4, watcher.CountValue);
		}

		[Test]
		public void JavaToManagedClickCallback_MarshalsObjectParameter ()
		{
			AssumeTrimmableTypeMapEnabled ();
			TrimmableRuntimeClickListener.Reset ();

			using var listener = new TrimmableRuntimeClickListener ();
			using var view = new View (Android.App.Application.Context);

			var method = JNIEnv.GetMethodID (listener.Class.Handle, "onClick", "(Landroid/view/View;)V");
			JNIEnv.CallVoidMethod (listener.Handle, method, new JValue (view.Handle));

			Assert.AreEqual (1, TrimmableRuntimeClickListener.OnClickInvocations);
			Assert.AreEqual (view.Handle, listener.ViewHandle);
		}

		[Test]
		public void JavaToManagedInvocationHandlerCallback_MarshalsObjectArrayParameter ()
		{
			AssumeTrimmableTypeMapEnabled ();
			TrimmableRuntimeInvocationHandler.Reset ();

			using var handler = new TrimmableRuntimeInvocationHandler ();
			using var first = new Java.Lang.String ("first");
			using var second = new Java.Lang.String ("second");
			var args = JNIEnv.NewArray (new Java.Lang.Object [] { first, second });

			try {
				var method = JNIEnv.GetMethodID (handler.Class.Handle, "invoke", "(Ljava/lang/Object;Ljava/lang/reflect/Method;[Ljava/lang/Object;)Ljava/lang/Object;");
				var result = JNIEnv.CallObjectMethod (handler.Handle, method, JValue.Zero, JValue.Zero, new JValue (args));
				JNIEnv.DeleteLocalRef (result);

				Assert.AreEqual (1, TrimmableRuntimeInvocationHandler.InvokeInvocations);
				Assert.AreEqual (2, handler.ArgumentCount);
				Assert.AreEqual ("first", handler.FirstArgument);
				Assert.AreEqual ("second", handler.SecondArgument);
			} finally {
				JNIEnv.DeleteLocalRef (args);
			}
		}

		[Test]
		public void JavaActivatedPeer_DisposeCanAccessThisAndInvokeVirtualMember ()
		{
			AssumeTrimmableTypeMapEnabled ();
			TrimmableRuntimeDisposePeer.Reset ();

			using (var peer = CreateFromJava<TrimmableRuntimeDisposePeer> ()) {
				Assert.AreEqual (1, TrimmableRuntimeDisposePeer.ConstructorInvocations);
				peer.Dispose ();
			}

			Assert.AreEqual (1, TrimmableRuntimeDisposePeer.DisposeInvocations);
			Assert.AreEqual (1, TrimmableRuntimeDisposePeer.VirtualInvocationsDuringDispose);
			Assert.AreNotEqual (0, TrimmableRuntimeDisposePeer.DisposeIdentityHashCode);
		}

		[Test]
		public void ClosedGenericJavaList_CanWrapJavaCreatedArrayListHandle ()
		{
			AssumeTrimmableTypeMapEnabled ();

			var arrayListClass = JniEnvironment.Types.FindClass ("java/util/ArrayList");
			try {
				var constructor = JNIEnv.GetMethodID (arrayListClass.Handle, "<init>", "()V");
				var handle = JNIEnv.NewObject (arrayListClass.Handle, constructor);
				using (var list = Java.Lang.Object.GetObject<JavaList<string>> (handle, JniHandleOwnership.TransferLocalRef)) {
					Assert.IsNotNull (list);
					Assert.AreEqual (typeof (JavaList<string>), list.GetType ());

					list.Add ("alpha");
					list.Add ("beta");

					Assert.AreEqual (2, list.Count);
					Assert.AreEqual ("alpha", list [0]);
					Assert.AreEqual ("beta", list [1]);
				}
			} finally {
				JniObjectReference.Dispose (ref arrayListClass);
			}
		}

		[Test]
		public void NonGenericCollection_CopyTo_ViewArray_UsesTrimmableTypeMapForArrayElementConversion ()
		{
			AssumeTrimmableTypeMapEnabled ();

			using (var arrayList = new Java.Util.ArrayList ()) {
				var viewClass = JniEnvironment.Types.FindClass ("android/view/View");
				var viewHandle = IntPtr.Zero;
				try {
					var constructor = JNIEnv.GetMethodID (viewClass.Handle, "<init>", "(Landroid/content/Context;)V");
					viewHandle = JNIEnv.NewObject (viewClass.Handle, constructor, new JValue (Android.App.Application.Context.Handle));
					AddToJavaCollection (arrayList, viewHandle);

					var values = new View [1];
					CopyToJavaCollection (arrayList, values);
					Assert.IsTrue (JNIEnv.IsSameObject (viewHandle, values [0].Handle));

					values = new View [1];
					CopyToJavaList (arrayList, values);
					Assert.IsTrue (JNIEnv.IsSameObject (viewHandle, values [0].Handle));
				} finally {
					JNIEnv.DeleteLocalRef (viewHandle);
					JniObjectReference.Dispose (ref viewClass);
				}
			}
		}

		[Test]
		public void NonGenericCollection_CopyTo_ObjectArray_PreservesNullElement ()
		{
			AssumeTrimmableTypeMapEnabled ();

			using (var arrayList = new Java.Util.ArrayList ()) {
				arrayList.Add (42);
				arrayList.Add (null);

				var values = new object [2];
				CopyToJavaCollection (arrayList, values);
				Assert.AreEqual (42, values [0]);
				Assert.IsNull (values [1]);

				values = new object [2];
				CopyToJavaList (arrayList, values);
				Assert.AreEqual (42, values [0]);
				Assert.IsNull (values [1]);
			}
		}

		[Test]
		public void NonGenericCollection_CopyTo_StringArray_ConvertsJavaString ()
		{
			AssumeTrimmableTypeMapEnabled ();

			using (var arrayList = new Java.Util.ArrayList ()) {
				arrayList.Add ("alpha");

				var values = new string [1];
				CopyToJavaCollection (arrayList, values);
				Assert.AreEqual ("alpha", values [0]);

				values = new string [1];
				CopyToJavaList (arrayList, values);
				Assert.AreEqual ("alpha", values [0]);
			}
		}

		static void CopyToJavaCollection (Java.Util.ArrayList arrayList, Array values)
		{
			using (var collection = new JavaCollection (arrayList.Handle, JniHandleOwnership.DoNotTransfer)) {
				collection.CopyTo (values, 0);
			}
		}

		static void CopyToJavaList (Java.Util.ArrayList arrayList, Array values)
		{
			using (var list = new JavaList (arrayList.Handle, JniHandleOwnership.DoNotTransfer)) {
				list.CopyTo (values, 0);
			}
		}

		static void AddToJavaCollection (Java.Util.ArrayList arrayList, IntPtr handle)
		{
			var add = JNIEnv.GetMethodID (arrayList.Class.Handle, "add", "(Ljava/lang/Object;)Z");
			JNIEnv.CallBooleanMethod (arrayList.Handle, add, new JValue (handle));
		}

		static T CreateFromJava<T> ()
			where T : Java.Lang.Object
		{
			var instance = JNIEnv.StartCreateInstance (typeof (T), "()V");
			JNIEnv.FinishCreateInstance (instance, "()V");
			var result = Java.Lang.Object.GetObject<T> (instance, JniHandleOwnership.TransferLocalRef);
			Assert.IsNotNull (result);
			return result;
		}

		static void AssumeTrimmableTypeMapEnabled ()
		{
			if (!RuntimeFeature.TrimmableTypeMap) {
				Assert.Ignore ("TrimmableTypeMap feature switch is off; test only relevant for the trimmable typemap path.");
			}
		}
	}

	class TrimmableRuntimeTextWatcher : Java.Lang.Object, ITextWatcher
	{
		public static int OnTextChangedInvocations;

		public string TextValue;
		public int StartValue;
		public int BeforeValue;
		public int CountValue;

		public void AfterTextChanged (IEditable s)
		{
		}

		public void BeforeTextChanged (Java.Lang.ICharSequence s, int start, int count, int after)
		{
		}

		public void OnTextChanged (Java.Lang.ICharSequence s, int start, int before, int count)
		{
			OnTextChangedInvocations++;
			TextValue = s?.ToString ();
			StartValue = start;
			BeforeValue = before;
			CountValue = count;
		}

		public static void Reset ()
		{
			OnTextChangedInvocations = 0;
		}
	}

	class TrimmableRuntimeClickListener : Java.Lang.Object, View.IOnClickListener
	{
		public static int OnClickInvocations;

		public IntPtr ViewHandle;

		public void OnClick (View v)
		{
			OnClickInvocations++;
			ViewHandle = v.Handle;
		}

		public static void Reset ()
		{
			OnClickInvocations = 0;
		}
	}

	class TrimmableRuntimeInvocationHandler : Java.Lang.Object, Java.Lang.Reflect.IInvocationHandler
	{
		public static int InvokeInvocations;

		public int ArgumentCount;
		public string FirstArgument;
		public string SecondArgument;

		public Java.Lang.Object Invoke (Java.Lang.Object proxy, Java.Lang.Reflect.Method method, Java.Lang.Object [] args)
		{
			InvokeInvocations++;
			ArgumentCount = args.Length;
			FirstArgument = args [0].ToString ();
			SecondArgument = args [1].ToString ();
			return null;
		}

		public static void Reset ()
		{
			InvokeInvocations = 0;
		}
	}

	[Register ("net/dot/android/test/TrimmableRuntimeDisposePeer")]
	class TrimmableRuntimeDisposePeer : Java.Lang.Object
	{
		public static int ConstructorInvocations;
		public static int DisposeInvocations;
		public static int VirtualInvocationsDuringDispose;
		public static int DisposeIdentityHashCode;

		public TrimmableRuntimeDisposePeer ()
		{
			ConstructorInvocations++;
		}

		protected virtual int GetDisposeIdentityHashCodeCore ()
		{
			VirtualInvocationsDuringDispose++;
			return Java.Lang.JavaSystem.IdentityHashCode (this);
		}

		protected override void Dispose (bool disposing)
		{
			if (disposing && Handle != IntPtr.Zero) {
				DisposeInvocations++;
				DisposeIdentityHashCode = GetDisposeIdentityHashCodeCore ();
			}

			base.Dispose (disposing);
		}

		public static void Reset ()
		{
			ConstructorInvocations = 0;
			DisposeInvocations = 0;
			VirtualInvocationsDuringDispose = 0;
			DisposeIdentityHashCode = 0;
		}
	}
}
