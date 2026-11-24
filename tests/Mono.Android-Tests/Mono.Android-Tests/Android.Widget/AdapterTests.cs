using System;
using System.Collections.Generic;
using System.Threading;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Util;
using Android.Views;
using Android.Widget;
using Android.Runtime;

namespace Android.WidgetTests {

	[TestClass]
	public class AdapterTests {

		[TestMethod]
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
			IntPtr AdapterView_setSelection = JNIEnv.GetMethodID (grefAdapterView_class, "setSelection", "(I)V");

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

				JNIEnv.CallVoidMethod (adapter.Handle, AdapterView_setSelection, new JValue (23));
				Assert.AreEqual (23, adapter.SelectionPosition);
			}
		}

		[TestMethod]
		public void GridView_Adapter ()
		{
			var view = new GridView (Application.Context);
			var adapter = view.Adapter;
			view.Adapter = adapter;
		}

		[TestMethod]
		public void GenericArrayAdapterUsesArrayAdapterClass ()
		{
			using var adapter = new ArrayAdapter<string> (Application.Context, Android.Resource.Layout.SimpleListItem1);

			adapter.Add ("first");

			Assert.AreEqual ("first", adapter.GetItem (0));
		}

		[TestMethod]
		public void ArrayAdapterConstructorsPopulateItems ()
		{
			var context = Application.Context;
			const int layout = Android.Resource.Layout.SimpleListItem1;
			const int textViewId = Android.Resource.Id.Text1;
			var array = new [] { "one", "two" };
			var list = new List<string> { "alpha", "beta" };

			using (var a = new ArrayAdapter<string> (context, layout, textViewId)) {
				a.Add ("zero");
				Assert.AreEqual ("zero", a.GetItem (0));
			}
			using (var a = new ArrayAdapter<string> (context, layout, array)) {
				Assert.AreEqual (2, a.Count);
				Assert.AreEqual ("one", a.GetItem (0));
			}
			using (var a = new ArrayAdapter<string> (context, layout, textViewId, array)) {
				Assert.AreEqual (2, a.Count);
				Assert.AreEqual ("two", a.GetItem (1));
			}
			using (var a = new ArrayAdapter<string> (context, layout, list)) {
				Assert.AreEqual (2, a.Count);
				Assert.AreEqual ("alpha", a.GetItem (0));
			}
			using (var a = new ArrayAdapter<string> (context, layout, textViewId, list)) {
				Assert.AreEqual (2, a.Count);
				Assert.AreEqual ("beta", a.GetItem (1));
			}
		}

		[TestMethod]
		public void ArrayAdapterConstructorsReleaseLocalRefsOnFailure ()
		{
			const int layout = Android.Resource.Layout.SimpleListItem1;
			const int textViewId = Android.Resource.Id.Text1;
			var array = new [] { "one" };
			var list = new List<string> { "alpha" };
			Context nullContext = null;

			Assert.Throws<Java.Lang.NullPointerException> (() => new ArrayAdapter<string> (nullContext, layout, array));
			Assert.Throws<Java.Lang.NullPointerException> (() => new ArrayAdapter<string> (nullContext, layout, textViewId, array));
			Assert.Throws<Java.Lang.NullPointerException> (() => new ArrayAdapter<string> (nullContext, layout, list));
			Assert.Throws<Java.Lang.NullPointerException> (() => new ArrayAdapter<string> (nullContext, layout, textViewId, list));
		}

		[TestMethod]
		[TestCategory ("GCBridge")]
		public void FinishCreateInstanceConstructorsSurviveConcurrentCollection ()
		{
			const int iterations = 200;
			var context = Application.Context;
			const int layout = Android.Resource.Layout.SimpleListItem1;
			const int textViewId = Android.Resource.Id.Text1;
			var array = new [] { "one" };
			var list = new List<string> { "alpha" };

			using var done = new CancellationTokenSource ();
			var collector = new Thread (() => {
				while (!done.IsCancellationRequested) {
					GC.Collect ();
					GC.WaitForPendingFinalizers ();
					Thread.Yield ();
				}
			});
			collector.Start ();

			try {
				for (int i = 0; i < iterations; i++) {
					_ = new JavaDictionary ();
					_ = new JavaDictionary<string, string> ();
					_ = new JavaSet ();
					_ = new JavaSet<string> ();
					_ = new ArrayAdapter<string> (context, layout);
					_ = new ArrayAdapter<string> (context, layout, textViewId);
					_ = new ArrayAdapter<string> (context, layout, array);
					_ = new ArrayAdapter<string> (context, layout, textViewId, array);
					_ = new ArrayAdapter<string> (context, layout, list);
					_ = new ArrayAdapter<string> (context, layout, textViewId, list);
					_ = new TestBaseAdapter ();
					_ = new TestAdapterView (context);
					_ = new TestAdapterView (context, null);
					_ = new TestAdapterView (context, null, 0);
					_ = new TestAsyncTask ();
				}
			} finally {
				done.Cancel ();
				collector.Join ();
			}
		}

		[TestMethod]
		[TestCategory ("ThresholdDispatch")]
		public void DerivedArrayAdapterInListView ()
		{
			using var adapter = new DerivedStringArrayAdapter (Application.Context);
			using var view = new ListView (Application.Context);

			adapter.Add ("first");
			view.Adapter = adapter;

			Assert.AreSame (adapter, view.Adapter);
			Assert.AreEqual ("first", view.GetItemAtPosition (0)?.ToString ());
		}

		[TestMethod]
		[TestCategory ("ThresholdDispatch")]
		public void AbsListView_SetAdapter ()
		{
			using (var view = new ListView (Application.Context)) {
				AssertSetAdapterRoundTrips (view);
			}
			using (var view = new ManagedListView (Application.Context))
			using (var adapter = new ArrayAdapter (Application.Context, 0)) {
				view.CallBaseSetAdapter (adapter);
				Assert.IsFalse (view.SetAdapterInvoked);
				Assert.AreSame (adapter, view.Adapter);
			}
		}

		static void AssertSetAdapterRoundTrips (ListView view)
		{
			using (var adapter = new ArrayAdapter (Application.Context, 0)) {
#pragma warning disable 618
				view.SetAdapter (adapter);
#pragma warning restore 618
				Assert.AreSame (adapter, view.Adapter);
			}
		}

		[TestMethod]
		[TestCategory ("ThresholdDispatch")]
		public void AdapterViewAnimator_Adapter ()
		{
			using (var view = new AdapterViewFlipper (Application.Context)) {
				AssertAdapterRoundTrips (view);
			}
			using (var view = new ManagedAdapterViewFlipper (Application.Context)) {
				AssertAdapterRoundTrips (view);
			}
		}

		static void AssertAdapterRoundTrips (AdapterViewAnimator view)
		{
			using (var adapter = new ArrayAdapter (Application.Context, 0)) {
				view.Adapter = adapter;
				Assert.AreSame (adapter, view.Adapter);
			}
		}
	}

	public class DerivedStringArrayAdapter : ArrayAdapter<string> {

		public DerivedStringArrayAdapter (Context context)
			: base (context, Android.Resource.Layout.SimpleListItem1)
		{
		}

		protected DerivedStringArrayAdapter (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}
	}

	public class TestBaseAdapter : BaseAdapter<string> {

		public TestBaseAdapter ()
		{
		}

		protected TestBaseAdapter (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}

		public override int Count => 0;

		public override string this [int position] => "";

		public override long GetItemId (int position) => position;

		public override View GetView (int position, View convertView, ViewGroup parent) => null;
	}

	public class TestAdapterView : AdapterView<IListAdapter> {

		public TestAdapterView (Context context)
			: base (context)
		{
		}

		public TestAdapterView (Context context, IAttributeSet attrs)
			: base (context, attrs)
		{
		}

		public TestAdapterView (Context context, IAttributeSet attrs, int defStyle)
			: base (context, attrs, defStyle)
		{
		}

		protected TestAdapterView (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}

		public override IListAdapter Adapter { get; set; }

		public override View SelectedView => null;

		public override void SetSelection (int position)
		{
		}
	}

#pragma warning disable CA1422 // AsyncTask is obsoleted on android30.0.
	public class TestAsyncTask : AsyncTask<string, string, string> {

		public TestAsyncTask ()
		{
		}

		protected TestAsyncTask (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}

		protected override string RunInBackground (params string [] @params) => "";
	}
#pragma warning restore CA1422

	public class ManagedListView : ListView {

		public ManagedListView (Context context)
			: base (context)
		{
		}

		protected ManagedListView (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}

		public bool SetAdapterInvoked { get; private set; }

#pragma warning disable 618
		public void CallBaseSetAdapter (IListAdapter adapter)
		{
			base.SetAdapter (adapter);
		}

		[Obsolete ("Please use the Adapter property setter")]
		public override void SetAdapter (IListAdapter adapter)
		{
			SetAdapterInvoked = true;
		}
#pragma warning restore 618
	}

	public class ManagedAdapterViewFlipper : AdapterViewFlipper {

		public ManagedAdapterViewFlipper (Context context)
			: base (context)
		{
		}

		protected ManagedAdapterViewFlipper (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
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

		public int SelectionPosition;

		public override IListAdapter Adapter {
			get {return AdapterValue;}
			set {
				AdapterSetterInvoked = true;
			}
		}

		public override void SetSelection (int position)
		{
			SelectionPosition = position;
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
