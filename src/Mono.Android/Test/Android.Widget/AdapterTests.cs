using System;

using NUnit.Framework;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Widget;
using Android.Runtime;

namespace Android.WidgetTests {

	[TestFixture]
	public class AdapterTests {

		[Test]
		public void InvokeOverriddenAbsListView_AdapterProperty ()
		{
			IntPtr grefAbsListView_class  = JNIEnv.FindClass ("android/widget/AbsListView");
			// AbsListView doesn't override getAdapter(), and thus it inherits the
			// AdapterView method; no need to check its behavior.
			IntPtr AbsListView_setAdapter = IntPtr.Zero;
			if ((int) Build.VERSION.SdkInt >= 11) {
				AbsListView_setAdapter  = JNIEnv.GetMethodID (grefAbsListView_class, "setAdapter", "(Landroid/widget/ListAdapter;)V");
			}

			IntPtr grefAdapterView_class  = JNIEnv.FindClass ("android/widget/AdapterView");
			IntPtr AdapterView_getAdapter = JNIEnv.GetMethodID (grefAdapterView_class, "getAdapter", "()Landroid/widget/Adapter;");
			IntPtr AdapterView_setAdapter = JNIEnv.GetMethodID (grefAdapterView_class, "setAdapter", "(Landroid/widget/Adapter;)V");

			JNIEnv.DeleteGlobalRef (grefAbsListView_class);
			JNIEnv.DeleteGlobalRef (grefAdapterView_class);

			using (var adapter = new CanOverrideAbsListView_Adapter (Application.Context)) {
				var a = Java.Lang.Object.GetObject<IListAdapter>(
						JNIEnv.CallObjectMethod (adapter.Handle, AdapterView_getAdapter), JniHandleOwnership.TransferLocalRef);
				Assert.AreSame (adapter.AdapterValue, a);

				if (AbsListView_setAdapter != IntPtr.Zero) {
					adapter.AdapterSetterInvoked = false;
					JNIEnv.CallVoidMethod (adapter.Handle, AbsListView_setAdapter, new JValue (IntPtr.Zero));
					Assert.IsTrue (adapter.AdapterSetterInvoked);
				}

				adapter.AdapterSetterInvoked = false;
				JNIEnv.CallVoidMethod (adapter.Handle, AdapterView_setAdapter, new JValue (IntPtr.Zero));
				Assert.IsTrue (adapter.AdapterSetterInvoked);
			}
		}

		[Test]
		public void GridView_Adapter ()
		{
			var view = new GridView (Application.Context);
			var adapter = view.Adapter;
			view.Adapter = adapter;
		}
	}

	public class CanOverrideAbsListView_Adapter : AbsListView {

		public CanOverrideAbsListView_Adapter (Context context)
			: base (context)
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


		/* 
		 * On Pre-Honeycomb targets, the 
		 * (IntPtr, JniHandleOwnership) ctor is reqiured because AbsListView
		 * constructor virtually invokes getAdapter() and the normal
		 * JNIEnv.AllocObject()-fu doesn't work.
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
		public CanOverrideAbsListView_Adapter (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}
	}
}
