using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;

using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Widget;

using Java.Interop;

using NUnit.Framework;

namespace Java.LangTests
{
	[TestFixture]
	public class ObjectTest
	{
		[Test]
		public void GetObject_ReturnsMostDerivedType ()
		{
			IntPtr lref = JNIEnv.NewString ("Hello, world!");
			using (Java.Lang.Object s = Java.Lang.Object.GetObject<Java.Lang.Object>(lref, JniHandleOwnership.TransferLocalRef)) {
				Assert.AreEqual (typeof (Java.Lang.String), s.GetType ());
			}

			lref = JNIEnv.CreateInstance ("android/gesture/Gesture", "()V");
			using (Java.Lang.Object g = Java.Lang.Object.GetObject<Java.Lang.Object>(lref, JniHandleOwnership.TransferLocalRef)) {
				Assert.AreEqual (typeof (global::Android.Gestures.Gesture), g.GetType ());
			}
		}

		[Test]
		public void JavaConvert_FromJavaObject_ShouldNotBreakExistingReferences ()
		{
			Func<IJavaObject, int> toInt = GetIJavaObjectToInt32 ();

			using (var instance  = new Java.Lang.Integer (42)) {
				Assert.AreSame (instance, Java.Lang.Object.GetObject<Java.Lang.Integer>(instance.Handle, JniHandleOwnership.DoNotTransfer));
				Assert.IsTrue (Java.Interop.Runtime.GetSurfacedObjects ()
						.Any (o => object.ReferenceEquals (o.Target , instance)));
				int e = toInt (instance);
				Assert.AreEqual (42, e);
				Assert.AreSame (instance, Java.Lang.Object.GetObject<Java.Lang.Integer>(instance.Handle, JniHandleOwnership.DoNotTransfer));
			}
		}

		static Func<IJavaObject, int> GetIJavaObjectToInt32 ()
		{
			[UnconditionalSuppressMessage ("Trimming", "IL2060", Justification = "")]
			static MethodInfo MakeGenericMethod (MethodInfo method, Type type) =>
				// FIXME: https://github.com/xamarin/xamarin-android/issues/8724
				#pragma warning disable IL3050
				method.MakeGenericMethod (type);
				#pragma warning restore IL3050

			var JavaConvert       = Type.GetType ("Java.Interop.JavaConvert, Mono.Android");
			var FromJavaObject_T  = JavaConvert.GetMethods (BindingFlags.Public | BindingFlags.Static)
				.First (m => m.Name == "FromJavaObject" && m.IsGenericMethod);
			return (Func<IJavaObject, int>) Delegate.CreateDelegate (
					typeof(Func<IJavaObject, int>),
					MakeGenericMethod (FromJavaObject_T, typeof (int)));
		}

		[Test]
		public void JnienvCreateInstance_RegistersMultipleInstances ()
		{
			using (var adapter = new CreateInstance_OverrideAbsListView_Adapter (Application.Context)) {

				var intermediate  = CreateInstance_OverrideAbsListView_Adapter.Intermediate;
				var registered    = Java.Lang.Object.GetObject<CreateInstance_OverrideAbsListView_Adapter>(adapter.Handle, JniHandleOwnership.DoNotTransfer);

				Assert.AreNotSame (adapter, intermediate);
				Assert.AreSame (adapter, registered);
			}
		}

		[Test]
		public void NestedDisposeInvocations ()
		{
			var value = new MyDisposableObject ();
			value.Dispose ();
			value.Dispose ();
		}

		[Test]
		public void java_lang_Object_Is_Java_Lang_Object ()
		{
			var jloType = global::Java.Interop.JniEnvironment.Runtime.TypeManager.GetType (new JniTypeSignature ("java/lang/Object"));
			Assert.AreSame (typeof (Java.Lang.Object), jloType,
					$"`java/lang/Object` is typemap'd to `{jloType}`, not `Java.Lang.Object, Mono.Android`!");
		}
	}

	/*
	 * Using JNIEnv.NewObject()/JNIEnv.CreateInstance() is "bad, mkay?", because
	 * using them _may_ result in a Java-side activation & registration of a
	 * "temporary" instance; dragons be here.
	 *
	 * Alas, this is the pre-4.10 behavior!
	 */
	[Register (CreateInstance_OverrideAbsListView_Adapter.JcwType)]
	public class CreateInstance_OverrideAbsListView_Adapter : AbsListView {

		/* (IntPtr, JniHandleOwnership) ctor is reqiured because AbsListView
		 * constructor virtually invokes getAdapter():
		 *
		 *	Executing: "/opt/android/sdk/platform-tools/adb"   shell am instrument -w Xamarin.Android.RuntimeTests/xamarin.android.runtimetests.TestInstrumentation
		 *	INSTRUMENTATION_RESULT: failure: Xamarin.Android.RuntimeTests.AdapterTests.InvokeOverriddenAbsListView_AdapterProperty=System.NotSupportedException : Unable to activate instance of type Xamarin.Android.RuntimeTests.CanOverrideAbsListView_Adapter from native handle 41e44de8
		 *		----> System.MissingMethodException : No constructor found for Xamarin.Android.RuntimeTests.CanOverrideAbsListView_Adapter::.ctor(System.IntPtr, Android.Runtime.JniHandleOwnership)
		 *		----> Java.Interop.JavaLocationException : Exception of type 'Java.Interop.JavaLocationException' was thrown.
		 *		at Java.Interop.TypeManager.CreateInstance (IntPtr handle, JniHandleOwnership transfer, System.Type targetType) [0x00000] in <filename unknown>:0 
		 *		at Java.Lang.Object.GetObject (IntPtr handle, JniHandleOwnership transfer, System.Type type) [0x00000] in <filename unknown>:0 
		 *		at Java.Lang.Object._GetObject[AbsListView] (IntPtr handle, JniHandleOwnership transfer) [0x00000] in <filename unknown>:0 
		 *		at Java.Lang.Object.GetObject[AbsListView] (IntPtr handle, JniHandleOwnership transfer) [0x00000] in <filename unknown>:0 
		 *		at Java.Lang.Object.GetObject[AbsListView] (IntPtr jnienv, IntPtr handle, JniHandleOwnership transfer) [0x00000] in <filename unknown>:0 
		 *		at Android.Widget.AbsListView.n_GetAdapter (IntPtr jnienv, IntPtr native__this) [0x00000] in <filename unknown>:0 
		 *		at (wrapper dynamic-method) object:2173e40b-99e1-484f-8c82-1f45de2f5a3a (intptr,intptr)
		 *	--MissingMethodException
		 *		at Java.Interop.TypeManager.CreateProxy (System.Type type, IntPtr handle, JniHandleOwnership transfer) [0x00000] in <filename unknown>:0 
		 *		at Java.Interop.TypeManager.CreateInstance (IntPtr handle, JniHandleOwnership transfer, System.Type targetType) [0x00000] in <filename unknown>:0 
		 *	--JavaLocationException
		 *	Java.Lang.Error: Exception of type 'Java.Lang.Error' was thrown.
		 *	
		 *		--- End of managed exception stack trace ---
		 *	java.lang.Error: Java callstack:
		 *		at xamarin.android.runtimetests.CanOverrideAbsListView_Adapter.n_getAdapter(Native Method)
		 *		at xamarin.android.runtimetests.CanOverrideAbsListView_Adapter.getAdapter(CanOverrideAbsListView_Adapter.java:46)
		 *		at xamarin.android.runtimetests.CanOverrideAbsListView_Adapter.getAdapter(CanOverrideAbsListView_Adapter.java:4)
		 *		at android.widget.AdapterView.setFocusableInTouchMode(AdapterView.java:699)
		 *		at android.widget.AbsListView.initAbsListView(AbsListView.java:812)
		 *		at android.widget.AbsListView.<init>(AbsListView.java:753)
		 *		at xamarin.android.runtimetests.CanOverrideAbsListView_Adapter.<init>(CanOverrideAbsListView_Adapter.java:22)
		 *		at xamarin.android.nunitlite.TestSuiteInstrumentation.n_onStart(Native Method)
		 *		at xamarin.android.nunitlite.TestSuiteInstrumentation.onStart(TestSuiteInstrumentation.java:52)
		 *		at android.app.Instrumentation$InstrumentationThread.run(Instrumentation.java:1701)
		 */
		public CreateInstance_OverrideAbsListView_Adapter (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
			Intermediate = this;
		}

		internal const string JcwType = "com/xamarin/android/runtimetests/CreateInstance_OverrideAbsListView_Adapter";

		public static CreateInstance_OverrideAbsListView_Adapter Intermediate;

		public CreateInstance_OverrideAbsListView_Adapter (Context context)
			: base (
					JNIEnv.CreateInstance (
						JcwType,
						"(Landroid/content/Context;)V",
						new JValue (context)),
					JniHandleOwnership.TransferLocalRef)
		{
			AdapterValue = new ArrayAdapter (context, 0);
		}

		protected override void Dispose (bool disposing)
		{
			if (!disposing)
				return;
			AdapterValue.Dispose ();
			AdapterValue = null;
		}

		public ArrayAdapter AdapterValue;

		public bool         AdapterSetterInvoked;

		public override IListAdapter Adapter {
			get {return AdapterValue;}
			set {
				AdapterSetterInvoked = true;
			}
		}

		public override void SetSelection (int position)
		{
			throw new NotImplementedException();
		}
	}

	public class MyDisposableObject : Java.Lang.Object
	{
		bool _isDisposed;
		public MyDisposableObject ()
		{
		}

		protected override void Dispose (bool disposing)
		{
			if (_isDisposed) {
				return;
			}
			_isDisposed = true;
			if (this.Handle != IntPtr.Zero)
				this.Dispose ();
			base.Dispose (disposing);
		}
	}
}
